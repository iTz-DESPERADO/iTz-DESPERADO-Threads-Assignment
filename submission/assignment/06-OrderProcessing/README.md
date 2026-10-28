# OrderProcessing

A .NET 10 console application for an order processing assignment covering asynchronous programming, concurrency, and thread safety. Data is stored in local JSON files, with simulated database and external service latency.

The project contains all four phases: sequential validation, concurrent processing, full checkout, and scale-up demonstrations.

## Running the project

Requires the .NET 10 SDK. From the solution directory:

```powershell
dotnet build OrderProcessing.slnx
dotnet run --project OrderProcessing -- phase1
dotnet run --project OrderProcessing -- phase1 --cancel-after 4000
dotnet run --project OrderProcessing -- phase2
dotnet run --project OrderProcessing -- phase2 --cancel-after 18000
dotnet run --project OrderProcessing -- phase3
dotnet run --project OrderProcessing -- phase3 --cancel-after 9000
dotnet run --project OrderProcessing -- phase3-compare
dotnet run --project OrderProcessing -- phase3-fire-and-forget
dotnet run --project OrderProcessing -- phase4
dotnet run --project OrderProcessing -- phase4 --cancel-after 9000
dotnet run --project OrderProcessing -- all
dotnet run --project OrderProcessing
```

The entry point is `static async Task Main(string[] args)`. With no arguments it displays a numbered menu for all four phases, all phases in order, and the two Phase 3 investigations. `all` runs Phase 1 through Phase 4; each phase owns its cancellation source and data reset. Ctrl+C or `--cancel-after <ms>` cancels the current phase and stops `all` before starting another phase.

Data is created under `Path.Combine(AppContext.BaseDirectory, "Data")`. For a Debug build, this is normally `OrderProcessing/bin/Debug/net10.0/Data/`.

Phase 1 deletes and recreates this directory before each comparison run, as required by Task 7.3. Reset finishes before repositories use the files.

## Project structure

```text
OrderProcessing.slnx
OrderProcessing/
|-- OrderProcessing.csproj
|-- Program.cs
|-- Phase1/
|-- Phase2/
|-- Phase3/
|-- Phase4/
+-- Shared/
    |-- Models/
    |   +-- Enums/
    |-- Storage/
    |-- Logging/
    |-- Exceptions/
    |-- Simulation/
    +-- Cancellation/
```

## Design

### Models

`Customer`, `Product`, and `Coupon` hold reference data. `Order` and `OrderItem` describe orders and their items. Money values use `decimal`.

`PaymentTransaction` and `PaymentResult` represent payment records and outcomes. `EmailMessage` stores email details and attempt information. `Report` holds order statistics. `CustomerDashboard` and `ShippingQuote` are defined for later features.

`LoyaltyTier`, `OrderStatus`, and `PaymentStatus` define the corresponding states.

### Storage

`DataFiles` defines file names and paths. `JsonSettings` holds the shared serializer options. `JsonFileStore` handles JSON reads and writes. `DataSeeder` creates the initial collections using the assignment's seed values.

| File | Initial records | Purpose |
|---|---:|---|
| customers.json | 10 | Customer data; read-only after initialization |
| products.json | 8 | Product data and stock quantities |
| coupons.json | 4 | Coupons; read-only after initialization |
| incoming_orders.json | 20 | Input orders; read-only after initialization |
| orders.json | 0 | Processed orders |
| payments.json | 0 | Payment transactions |
| emails.json | 0 | Sent emails |
| email_retry.json | 0 | Emails that exhaust their retry attempts |

The `invoices/` and `reports/` directories start empty. Phase 2 writes invoices and a report into them.

Collections are UTF-8 JSON arrays. Empty collections are written as `[]`. Serialization uses `System.Text.Json` with indentation, camelCase property names, and string enum values.

### JsonFileStore API

- `GetAllRecordsAsync<T>` reads a collection and returns an empty list when the file is missing.
- `GetRecordAsync<T>` validates the JSON document and returns the first matching record, or the default value when no match exists.
- `SeedAsync<T>` creates initial data and rejects an existing destination file.
- `UpdateRecordsAsync<T>` applies a callback to the collection and saves it when the callback returns `true`.
- Synchronous read and update methods support synchronous comparisons.

Invalid JSON raises `DataFileCorruptedException` with the affected file name. Async methods accept a `CancellationToken` as the last parameter.

Async reads and updates share a semaphore per file path for mutable files. Reads of the exact customers.json, coupons.json, and incoming_orders.json paths under Data bypass the gate because these files are read-only after seeding. This assumes reset/seeding finishes before reads begin and does not overlap the run. Updates protect the complete read-modify-write operation. Reads hold the same gate until their stream closes, preventing Windows file-replacement conflicts during concurrent processing. This prevents concurrent updates from overwriting each other's changes. Writes first serialize to a temporary file in the same directory, then replace the destination, so failed serialization does not publish partial JSON.

Seeding runs before other operations, with a separate file for each task. It does not require a per-file semaphore in this usage. Synchronous reads of mutable files and synchronous updates now acquire the same per-file semaphore, supporting the dedicated-thread comparison in Phase 3. SemaphoreSlim.Wait blocks a synchronous caller; it is not a blocking wait on a Task. Read-only seed-file reads still bypass the gate.

### Logging and simulated latency

`Log` prints timestamps and thread IDs. A single lock protects the console color change and line output. Success messages are green and errors are red.

`SimulatedLatency` contains the database, external API, payment, and email delay steps from the assignment. Async operations use `Task.Delay`; synchronous equivalents use `Thread.Sleep`. These delays are separate from JSON storage.

### Cancellation and exceptions

`RunCancellation` owns the cancellation source and handles Ctrl+C. The entry point also supports the `--cancel-after <ms>` timeout.

`Phase1Runner` uses the existing `OrderCancellationCleanup` to save the in-progress order as Pending with FailureReason "Cancelled". Phase 1 does not reserve stock or capture payments. Cleanup uses a separate token so the cancelled run token cannot prevent saving the cancellation result.

The custom exceptions are `CustomerNotFoundException`, `InvalidCouponException`, `ShippingProviderUnavailableException`, `DatabaseWriteException`, `SmtpUnavailableException`, and `DataFileCorruptedException`.

## Verification

The shared infrastructure builds successfully with no warnings or errors. Initialization produced the eight JSON collections with the counts shown above.

Storage checks covered missing files, invalid JSON after an early matching record, failed serialization, cancellation before seeding, concurrent async updates, and rejection of an existing seed file.

Two storage issues were addressed during setup:

- Returning from a streamed lookup at the first match could leave malformed JSON later in the file undetected. The lookup now parses the complete document before searching.
- Writing seed data directly to the destination could leave partial JSON after a failure. Seeding now uses the temporary-file write path.

## Phase 1 design

`Phase1/` contains CustomerRepository, ProductRepository, CouponRepository, OrderRepository, IncomingOrderRepository, OrderValidator, the four synchronous comparison classes, and Phase1Runner.

Repositories use the existing latency helper and JsonFileStore. Coupon matching ignores case. Save replaces an existing order with the same ID or appends a new order. ReserveStockAsync checks and subtracts stock inside the store's protected update callback; validation only checks stock and never calls reservation.

Validation checks customer existence, active status, all product existence, stock, coupon existence, then expiry, stopping at the first failure. Both validators use this same order. The async validator sets Status and FailureReason; the runner calls SaveAsync immediately afterward, matching Task 7.3's explicit validate-then-save flow and saving each order once.

The summary queries orders.json through GetByStatusAsync and GetAllAsync. Final JSON and the actual console log are in `sample-output/phase1/`.

### Timing interpretation

Task 7.3 explicitly excludes saving from the sync run and includes saving in the async run. Each async save adds the required 900 ms of simulated write latency, so 20 saves add approximately 18 seconds. The summary reports the requested full run times, plus async validation-only time to compare equivalent work. Random query delays also cause variation. No delays were removed or adjusted to force similar totals.

### Phase 1 answers

1. Sequential awaits do not overlap orders or queries. Both versions wait for the same simulated query steps, so equivalent validation work takes roughly the same elapsed time. Async improves thread availability during waits, not the speed of this sequential workload. Full async time additionally includes the 20 saves described above.

2. The thread ID can change after await. This console application has no synchronization context requiring continuation on the original thread; a ready continuation can run on another thread-pool thread. Actual log evidence is recorded below.

3. During Task.Delay, the method yields and does not keep a thread blocked for the delay. Thread.Sleep blocks the calling thread until its delay finishes; it cannot do other work during that time.

4. Task<T> represents a result that may arrive later and carries completion, exceptions, or cancellation. Returning T would require the result to be ready before the method returns, preventing callers from awaiting unfinished I/O without blocking.

5. File.ReadAllText blocks its caller until reading finishes. File.ReadAllTextAsync returns a task and lets the caller yield. With asynchronous OS I/O, a managed thread does not need to remain blocked for the disk wait; threads still perform setup, completion, and decoding. This project uses async FileStream and System.Text.Json operations through JsonFileStore.


### Measured run

```text
===== Validation Summary =====
Total Orders: 20
Validated:    15
Rejected:     5
  - Order #4: Customer is inactive
  - Order #9: Insufficient stock for 'Wireless Mouse'
  - Order #12: Coupon 'OLD20' has expired
  - Order #15: Customer #99 not found
  - Order #18: Coupon 'FAKE50' does not exist
Sync Time:  35.8 seconds
Async Time: 53.7 seconds
Async validation only: 34.8 seconds
orders.json now contains 20 records.
```

Thread continuation evidence from the same run:

```text
[17:16:37.280] [Thread 14] OrderRepository -> Inserting/updating...
[17:16:37.689] [Thread 15] OrderRepository -> Committing...
```

Build: 0 warnings, 0 errors. Final JSON checks confirmed 20 distinct order IDs, the exact five rejection reasons, unchanged product stock, and empty payment/email collections. A separate `--cancel-after 1500` run stopped during synchronous validation and saved order #1 as Pending / Cancelled, reporting 0 finished and 1 cancelled.

## Phase 2 design

`Phase2/` contains PricingService, OrderPricer, RaceConditionDemo, OrderProcessorV2, InvoiceGenerator, ReportGenerator, BlockingDemo, and Phase2Runner. It reuses Phase 1 repositories and validation. ProductRepository now includes the required unsafe reservation demonstration and protected stock release. The existing protected reservation checks current stock and subtracts it inside the store's update gate.

The runner resets, demonstrates unsafe and safe keyboard reservations, resets again, and compares sequential/concurrent pricing on order #1 without saving the comparison. It then starts all validation tasks and awaits them individually. It starts processing tasks for the validated orders into a List<Task<bool>>, prints task status immediately and after one second, awaits each in a foreach, handles failures, and prints final task statuses and elapsed time. It confirms 20 distinct orders are saved before starting and awaiting the report. BlockingDemo runs separately after the report to demonstrate question 3.

Pricing fills unit prices and subtotal before starting discount and shipping together. Loyalty and coupon percentages are capped at 30; shipping uses quantity and weight, with free shipping for subtotals of at least 1000. Every money result is rounded to two decimals with AwayFromZero. Shipping failure IDs are constructor input; Phase 2 passes `{ 7 }`. The injected failure occurs immediately after the handshake.

Each processor reserves items in their listed order and tracks only successfully written reservations. On insufficient stock it releases previous reservations and saves Rejected. Otherwise it prices, generates the invoice, and saves Completed. ProcessValidatedOrderAsync contains no catch. The runner catches failed tasks, releases their stock, and saves Failed while continuing the remaining tasks. Each processor has its own reservation list; callbacks record reservation/release immediately after the JSON update, before the cancellable Commit delay, so cleanup cannot miss or double-release a committed update.

Cancellation drains every started processing task. An interrupted order is saved as Pending / Cancelled, using the existing cleanup with an independent token to release its tracked stock. The race demonstration restores its test stock if cancelled. Report JSON is written through a temporary file and published after successful serialization so cancellation cannot leave a partial report.

InvoiceGenerator uses one Task.Run call site for the required 20,000,000-iteration rendering loop, checks cancellation in the loop, and writes the invoice asynchronously. ReportGenerator reads orders and the top product through repositories with delays, counts statuses, sums Completed revenue only, computes the average, and selects the top product by quantity then lowest product ID.

### Phase 2 answers

1. Calling an async method starts executing it until it reaches an incomplete await. Keeping the returned task lets other work start while it is pending. Awaiting observes completion, obtains its result or exception, and suspends the caller if necessary. Starting both discount and shipping before awaiting them lets their waits overlap. [Microsoft: asynchronous programming](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/).

2. A Task represents an operation, not a dedicated thread. Task.Delay uses a timer and does not occupy a worker thread for the waiting period. Continuations need a thread when they execute. [Microsoft: task-based asynchronous model](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/task-asynchronous-programming-model).

3. Result or Wait blocks the calling thread until completion. This can freeze a UI or waste worker threads; a deadlock occurs if the blocked context is also needed by the continuation. BlockingDemo.Run is the only blocking task wait in this project; its measured output is included below. It deliberately shows blocking in a console, not a guaranteed deadlock. [Microsoft: common async bugs](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/common-async-bugs).

4. An exception in a task-returning async method is stored in its faulted task. If nobody awaits or otherwise observes it, the caller may never notice the failure; the runtime can later raise UnobservedTaskException during garbage collection. That event is not a reliable error-handling mechanism. Here each processing task is awaited and #7's exception is observed and handled. [Microsoft: UnobservedTaskException](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception?view=net-10.0).

5. Task.Run fits CPU work such as the invoice-rendering loop: it queues computation to the thread pool. Database simulation and asynchronous file operations already yield during waits, so wrapping them in Task.Run adds scheduling without making their I/O faster. [Microsoft: asynchronous scenarios](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios).

6. Task represents completion without a result, for example invoice generation. Task<T> also produces a typed result: processing returns Task<bool>, and report generation returns Task<Report>. Both can complete, fault, or be cancelled.

7. A lock is owned by the entering thread and must be released by that thread. Await can suspend and continue on another thread, so C# forbids await in a lock body. The store uses SemaphoreSlim.WaitAsync and releases it in finally, which supports asynchronous waiting without requiring thread ownership. [Microsoft: lock statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock).

8. Both unsafe calls read stock 1 before waiting and later write 0 based on that stale value, so both report success. Atomic file publication alone cannot fix the stale decision. The safe method reads, checks, subtracts, and publishes while holding one per-file semaphore; the second call sees stock 0 and returns false. Release uses the same protected update path.

### Phase 2 measured run

```text
Unsafe: Order #10 = True, Order #11 = True, stock = 0
Safe: Order #10 = False, Order #11 = True, stock = 0
Sequential pricing #1: 5.021 seconds; total = 980.00
Concurrent pricing #1: 3.099 seconds; total = 980.00
Phase 2 validation + processing time: 12.837 seconds
Total orders: 20
Completed: 13, Rejected: 6, Failed: 1, PaymentFailed: 0
Total revenue: 5970.95
Average order value: 459.30
Top-selling product: USB-C Hub, 5 units
Blocking demo: caller blocked for 200 ms; status = RanToCompletion
```

The safe race-demo winner and the later processing winner need not be the same: the runner resets between those operations. Either keyboard order may win.

Phase 1 took 53.7 seconds for sequential validation and saving. Phase 2's validation plus processing took 12.837 seconds, despite also reserving stock, pricing, and generating invoices. Concurrent I/O waits overlap across orders. These are different workloads, so the numbers demonstrate the benefit of overlap rather than an exact same-workload speedup. The Phase 2 measurement excludes resets, race/pricing demonstrations, report generation, and the blocking demonstration. Pricing comparisons also include random API/query delays.

The complete console log, final JSON collections, 13 invoice files, and report are saved under `sample-output/phase2/`. The cancellation log is a separate run; the saved final collections represent the successful full run.

### Phase 2 verification

- Build succeeded with 0 warnings and 0 errors.
- Full-run checks verified 20 unique orders, the 13/6/1 status split, only #7 Failed with the exact shipping error, exactly one completed keyboard order, and 13 matching invoices.
- Inventory was checked against initial stock minus quantities in Completed orders: failed/rejected orders retained no stock. Report revenue, average, and top product were recomputed from the saved orders.
- Separate pricing checks verified the exact subtotal/discount/shipping/total values for orders #1 and #2.
- Cancellation during the reservation Commit delay and during a stock-release Commit delay was tested: cleanup restored stock exactly once and persisted Pending / Cancelled.
- A run with `--cancel-after 18000` ended with `Run cancelled — 5 orders finished, 15 cancelled.` All runtime JSON parsed successfully; cancelled orders retained no stock and no temporary files remained.
- A storage check ran 50 overlapping updates and reads and verified that all 50 updates were retained without file-access errors.
- No group task-wait APIs are used. The only Task.Run call site is invoice rendering; the only blocking task wait is BlockingDemo.Run.

## Phase 3 design

`phase3` runs the full 20-order checkout with the configured failures. `phase3-compare` runs Task 9.6, resetting before each version; `phase3-fire-and-forget` runs Task 9.7 on freshly seeded data. The demonstrations are separate commands because each changes the runtime data. Run `phase3` to regenerate the expected final checkout files. Each command uses the existing Ctrl+C / --cancel-after cancellation source.

CustomerService uses CustomerRepository and throws CustomerNotFoundException for a missing customer. InventoryService checks all requested products and quantities in one protected update before subtracting anything, so a rejected reservation leaves every item unchanged. Each order has its own InventoryService instance. Reservation and release state are updated immediately after the file write, before the Commit delay, so cancellation at that boundary does not leak or double-release stock.

Phase 2 PricingService is reused with an empty shipping-failure list and now exposes CalculateTotalAsync. It obtains subtotal first, then overlaps discount and shipping and fills the four money fields. Invalid coupons throw InvalidCouponException. No Phase 2 invoice or report step is added to Phase 3 checkout.

PaymentGateway implements IPaymentGateway. It logs Authorize (800 ms), Fraud check (500 ms), and Capture (700 ms), writes Captured/Declined payments through JsonFileStore, and generates captured transaction IDs as TXN- plus 12 uppercase hexadecimal characters. RefundAsync follows the external API delays and changes the existing record to Refunded.

Failures are constructor configuration: payment timeout IDs `{ 5 }`, decline IDs `{ 8 }`, completed-order save failure IDs `{ 13 }`, and email failure counts `{ [3] = 2, [17] = int.MaxValue }`. Order IDs are not hardcoded into the processor's control flow. `Phase3Runner(store, disableFailures: true)` disables these injected failures for the comparison; ordinary inactive-customer and insufficient-stock validation still applies.

OrderProcessor receives its services through the constructor and performs customer lookup, reservation, pricing, payment, order save, and email. Each step logs its timestamp and elapsed duration. Payment uses a linked cancellation source with CancelAfter(3000): the gateway receives its token for every delay and file operation. A gateway timeout becomes PaymentFailed with the required reason; cancellation of the run becomes Pending / Cancelled. Declines release stock. A Completed save failure refunds the captured payment, releases stock, and upserts the exact Failed reason containing the transaction ID. Every stopped order is saved.

EmailRetryService makes one initial attempt plus three retries, waiting 500, 1000, and 2000 ms before the retries. It logs every SMTP failure. The fourth failure appends a message with Attempts = 4 and LastError to email_retry.json and returns normally, preserving the paid order's Completed status. EmailService stores successful messages with their actual attempt count. Cancellation is not retried as an SMTP error.

Cancellation cleanup uses a separate token to finish refunds, stock release, and saving Pending / Cancelled after the run token is cancelled. The runner awaits every task already started and does not start additional orders after observing cancellation. The full-run cancellation test covered already captured payments and email retries.

### Phase 3 expected state verified from files

| File | Observed full-run result |
|---|---|
| orders.json | 20 unique orders: 11 Completed, 6 Rejected, 2 PaymentFailed, 1 Failed |
| payments.json | 13: 11 Captured, 1 Declined (#8), 1 Refunded (#13); no record for #5 |
| emails.json | 10: Completed orders except #17; #3 has Attempts = 3 |
| email_retry.json | One message for #17, Attempts = 4, LastError = SMTP server unavailable |
| products.json | Initial stock minus quantities from Completed orders only; keyboard stock = 0 |

The five fixed rejections are #4, #9, #12, #15, and #18; one of #10/#11 is the sixth rejection. #5 has `Payment gateway timeout (3s)`, #8 has `Card declined by issuer`, and #13 has `Database save failed after payment; payment {TransactionId} refunded`. The full final collections and real console logs are in `sample-output/phase3/`.

### Task 9.6: synchronous versus asynchronous checkout

The synchronous services use Thread.Sleep via SimulatedLatency and JsonFileStore's synchronous methods. SyncOrderProcessor runs on a new Thread per order; all ten threads are started before Join is called on each. The async comparison uses the real OrderProcessor with failures disabled, starts all ten tasks, then awaits them individually. Both runs use orders #1–#10 and reset first.

ThreadUsage records distinct managed thread IDs that emit workflow log lines during each timed run. These are observed executing threads, not a count of threads continuously occupied by the workload. ThreadPool.ThreadCount is printed separately before and after; it is process-wide and does not include the ten manually created threads. Measuring with Stopwatch excludes initialization, loading input, and the final summary queries.

Actual output:

```text
Sync comparison: 10.786 seconds; dedicated threads = 10; observed workflow threads = 10; ThreadPool.ThreadCount = 4 -> 4
Sync comparison: 8 Completed, 2 Rejected
Async comparison: 9.120 seconds; dedicated threads = 0; observed workflow threads = 8; ThreadPool.ThreadCount = 6 -> 8
Async comparison: 8 Completed, 2 Rejected
```

1. Async does not make the database, payment, or email operation itself faster. Here the async pricing step overlaps discount and shipping while synchronous pricing waits for them in sequence, so one workflow can finish sooner because of that overlap. Random delays also affect the measured difference this is not proof that adding async alone accelerates one order.

2. Async scales better for many waiting orders because a pending I/O operation does not require its own blocked thread. Available threads can execute ready continuations from other orders. The comparison created ten dedicated threads for sync and zero for async; the eight observed async thread IDs were shared executing threads, not eight threads blocked throughout the run.

3. With 1,000 orders this particular synchronous design would attempt to create 1,000 threads. Their stacks, creation cost, scheduling, and context switches consume resources even while most are blocked waiting. It may hit resource limits. Async still needs limits for real downstream capacity, but it avoids dedicating a blocked thread to each I/O wait.

4. Database, file, and API operations spend time waiting for external work. Await lets a UI remain responsive and lets servers reuse threads for other requests while that work is pending. Async improves responsiveness and scalability; it does not eliminate I/O latency or make every small console program faster. [Microsoft: asynchronous scenarios](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios).

### Task 9.7: fire-and-forget investigation

BrokenFireAndForgetDemo contains the supplied method unchanged, with CS4014 suppressed only for the unawaited email call. It is isolated from checkout. The missing await means the returned task represents only order saving: email work may still be pending after `Order processing finished`, and an email exception is not propagated to that caller. The exact broken example has no cancellation parameter as specified the fixed method accepts and forwards ct.

The demonstration records the email task in a small tracking wrapper so the observer can inspect it and drain it afterward. That observation belongs to the investigation, not the broken method. It deliberately avoids leaving test work running after the demonstration returns.

Actual before/after evidence:

```text
[09:43:01.462] [Thread 12] Broken method returned; email task completed = False; sent emails = 0
[09:43:05.164] [Thread 12] Fixed method returned; email task completed = True; sent emails = 2
[09:43:06.724] [Thread 12] Broken method returned without an email exception.
[09:43:07.732] [Thread 12] Observer later found email task Faulted: SMTP server unavailable
[09:43:09.684] [Thread 10] Fixed caller caught: SMTP server unavailable
```

The fixed-success count is two because the observer first waited for the earlier broken example's successful email; the fixed example then sent the second email. On the fixed failure path, `Order processing finished` is not printed because awaiting the email propagates the error. The full transcript includes the SMTP steps before and after each completion message.

Await is necessary when completion, a result, ordering, or failure matters to the caller. If the console process exits while the unawaited email is pending, there is no guarantee it will be sent; the observed return-before-send case demonstrates why printing completion is unsafe. This test demonstrates method return and delayed failure rather than terminating the process mid-send.

Fire-and-forget can be acceptable for deliberately independent, noncritical work only when another owner supervises its lifetime, cancellation, and exceptions, and losing the work is acceptable. Reliable order confirmation should be awaited or handed to a durable, supervised queue. Merely discarding the task provides none of those guarantees.

An async void method returns no task, so callers cannot await its completion or observe its later exception through an awaited task. Event handlers are the normal exception because their required signature returns void; those handlers should catch errors at their boundary and delegate substantial work to Task-returning methods. [Microsoft: common async bugs](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/common-async-bugs).

### Phase 3 verification

The project builds without warnings or errors. Full-run checks verified the exact status/payment/email counts, exact failure reasons, transaction-ID format and amounts, no payment for the timed-out order, retry attempt counts, and stock conservation. The comparison produced 8 Completed and 2 Rejected in both versions. The fire-and-forget transcript demonstrates pending work and exception propagation before/after the fix.

A separate `--cancel-after 9000` run reported `Run cancelled — 9 orders finished, 11 cancelled.` Its final files contained 9 Refunded, 3 Captured, and 1 Declined payment. Cancelled orders had no Captured payment or reserved stock left; sent emails belonged only to Completed orders. All JSON parsed successfully and no temporary files remained. Cancellation results vary with random simulated delays.
Additional checks covered a multi-item reservation whose second item has insufficient stock (no partial deduction), cancellation during reservation/release Commit delays (stock restored once), 50 simultaneous synchronous updates (no lost writes), and cancellation before initialization (0 finished, 0 cancelled without counting data from an earlier run).

## Phase 4 design

Phase4Runner resets once, runs Tasks 10.1–10.4 in order, and reuses the real Phase 3 checkout with the same constructor-injected failures. It does not reset between these tasks. Measurements use Stopwatch and print observed distinct managed thread IDs and ThreadPool.ThreadCount.

- **Live progress:** starts all twenty checkout operations and consumes their completion with Task.WhenEach. Each completed task returns its order and elapsed time; the runner prints one numbered progress line. No unfinished-task list is searched or rebuilt after each completion. Counts are read from orders.json after all operations finish. Cancellation does not abandon the completion enumeration, allowing every started order to finish refund/stock cleanup.

- **CustomerDashboardService:** starts four database-query simulations for profile, orders, payments, and emails and awaits Task.WhenAll once. Each load assigns its separate dashboard property. Payments and emails map order IDs to the requested customer using incoming_orders.json inside their load. A missing customer throws CustomerNotFoundException. The configured payments outage for customer 99 throws TimeoutException. The aggregate from the group task is propagated so the runner prints both errors.

- **FastShip, CairoExpress, NileCourier:** implement IShippingCarrier with their specified delay ranges and price factors. CarrierQuote shares only the handshake, formula, file lookup, and cancellation logging. CairoExpress rejects even order IDs after its response delay. Explicit failForOrderIds represents a handshake outage and fails immediately after 150 ms, making the separate all-carriers-failed example deterministic within the 1.2-second budget. Normal responses retain the required random ranges. Quotes derive subtotal and weight from product prices and order items, including when incoming order money fields are still zero.

- **ShippingQuoteService:** starts all three requests, uses Task.WhenAny to examine finished requests, skips failures, and returns the first successful quote. A linked token enforces the 1.2-second budget and propagates caller cancellation. Remaining carriers are cancelled and all tasks are observed before returning. No success before the deadline yields Flat rate / 25.00; three failures within the budget yield the exact All carriers failed exception. ElapsedMs measures arrival/timeout; the surrounding measurement also includes brief cancellation cleanup.

- **FraudScoring:** creates 10,000 in-memory Order objects and uses the supplied Score calculation unchanged. The first pass uses a plain foreach; the second uses Parallel.ForEach with MaxDegreeOfParallelism = Environment.ProcessorCount. A ConcurrentBag collects flagged IDs and the sorted results are compared with the sequential list. Cancellation is checked before each 100,000-iteration score, bounding cancellation latency to the short current score.

- **EmailResend:** uses Parallel.ForEachAsync with MaxDegreeOfParallelism = 4 to send test messages for order IDs 1001–1030 through IEmailService, with failures disabled. The library schedules and limits the operations. Interlocked counters only measure active/peak sends; they do not implement a limit. Tokens reach every send and delay.

### Phase 4 observed results

Live checkout printed all twenty progress lines in completion order, ending at [20/20], then reported 11 Completed, 6 Rejected, 2 PaymentFailed and 1 Failed. The keyboard winner in this run was #11. Customer #3 returned Omar Hassan with #3 Completed / #13 Failed, #3 Captured / #13 Refunded, and one email for #3. Customer #99 printed both `Customer #99 not found` and `Payments store timeout`.

| Order | Winning quote | Price | Arrival / timeout (ms) | Cancelled carriers |
|---|---|---:|---:|---|
| 1 | NileCourier | 0.00 | 1021 | CairoExpress, FastShip |
| 2 | Flat rate | 25.00 | 1201 | CairoExpress, FastShip, NileCourier |
| 3 | FastShip | 10.70 | 930 | CairoExpress, NileCourier |
| 4 | NileCourier | 7.81 | 1166 | CairoExpress, FastShip |
| 5 | CairoExpress | 15.68 | 912 | FastShip, NileCourier |

The separate all-failed run for #6 printed `All carriers failed`. A deadline measured at 1201 ms reflects timer/scheduler overhead around the requested 1200 ms, not an additional wait for a full carrier response.

Both fraud passes produced exactly 1005 flagged IDs, with the same complete ID list and first five `4, 8, 12, 29, 33`. The measured CPU speed-up was 14.59x on Environment.ProcessorCount = 16. All thirty test emails were written exactly once; peak simultaneous sends was exactly 4 and duration was 8.110 seconds. Seven full waves of four and a final wave of two explain the approximately eight seconds of SMTP delays.

At the end of Task 10.1 the email outbox contains the ten checkout messages. Task 10.4 B adds thirty test messages, so the **final Phase 4 emails.json contains 40 records**. Orders, payments, retry queue, and product stock retain the checkout result. The eight final JSON collections are in `sample-output/phase4/`; test orders for fraud scoring and email re-send are never added to orders.json.

### Consolidated performance table

Times are measured seconds, not expected values. Phases 1–2 were measured again by an external verification program using the existing classes; its complete output is `sample-output/phase4/performance-baseline.txt`. Earlier phase sections retain their original runs. Phase 3 values come from its comparison transcript, and Phase 4 values come from its full-run transcript. Random delays and different process histories mean pool sizes and times vary between runs.

| Work | Time (s) | Distinct observed threads | ThreadPool.ThreadCount |
|---|---:|---:|---:|
| Phase 1 sync validation | 35.267 | 1 | 1 |
| Phase 1 async validation + save | 53.752 | 4 | 4 |
| Phase 2 sequential pricing | 4.953 | 2 | 4 |
| Phase 2 concurrent pricing | 3.206 | 2 | 4 |
| Phase 2 concurrent validation + processing | 13.064 | 9 | 10 |
| Phase 3 sync checkout (10 dedicated threads) | 10.786 | 10 | 4 -> 4 |
| Phase 3 async checkout (10 tasks) | 9.120 | 8 | 6 -> 8 |
| Phase 4 live checkout | 16.336 | 8 | 9 |
| Phase 4 dashboard #3 | 0.659 | 4 | 9 |
| Phase 4 dashboard #99 | 0.752 | 4 | 9 |
| Phase 4 shipping #1 | 1.024 | 3 | 9 |
| Phase 4 shipping #2 | 1.202 | 3 | 9 |
| Phase 4 shipping #3 | 0.930 | 3 | 5 |
| Phase 4 shipping #4 | 1.167 | 3 | 5 |
| Phase 4 shipping #5 | 0.913 | 3 | 5 |
| Phase 4 fraud sequential | 4.044 | 1 | 5 |
| Phase 4 fraud parallel | 0.277 | 16 | 16 |
| Phase 4 email re-send | 8.110 | 5 | 16 |

Distinct observed threads means managed IDs that executed measured work/logging, not simultaneously occupied threads. CPU scoring records the thread of every iteration. ThreadPool.ThreadCount is a process-wide snapshot, not the number of busy workers; manually created threads are not included. Phase 1 async includes saves that its sync comparison explicitly omits. The Phase 2 processing measurement excludes its setup demonstrations.

### Phase 4 answers

1. **10.1:** Task.WhenEach provides completed tasks without searching/rebuilding an unfinished list. Awaiting in start order delays reports of later tasks that already finished.

   **10.2:** Task.WhenAll provides the required single group await and retains all failures. Starting all loads then awaiting individually can overlap them, but does not meet the single-await requirement and may stop observation at the first failure. 

   **10.3:** Task.WhenAny lets the first successful carrier win, regardless of request order; individual ordered awaits can wait behind a slow or failed carrier and miss the budget.

   **10.4 A:** Parallel.ForEach partitions CPU work and caps simultaneous iterations at ProcessorCount. Awaiting an arbitrary task list does not itself provide that scheduling/limit.

   **10.4 B:** Parallel.ForEachAsync supplies bounded I/O concurrency of four; starting thirty tasks first would exceed the limit, while awaiting immediately after each send would serialize them. [Microsoft: Task.WhenEach](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.wheneach?view=net-10.0), [Parallel.ForEachAsync](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.parallel.foreachasync?view=net-10.0).

2. Keep the Task returned by Task.WhenAll. After catching the await failure, inspect its Exception.Flatten().InnerExceptions to obtain every fault. Plain await normally propagates one exception, even when the group task retains several. Dashboard #99 demonstrates both the profile and payment failures. [Microsoft: Task.WhenAll](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall?view=net-10.0).

3. Losing carriers continue their delays and work unless cancelled. In a real application they could keep connections or consume service capacity and finish/fail after the caller already has a quote. WhenAny does not cancel losers automatically. Here a linked source cancels them, their logs identify cancellation, and their tasks are observed before returning.

4. Fraud scoring continuously uses the CPU; independent scores can run on different cores, reducing elapsed time until overhead or resource limits dominate. SMTP simulation spends most time waiting, so four sends can overlap even on one core; its throughput is governed mainly by the concurrency limit and I/O latency.

5. Parallel work executes computations at the same time on multiple cores. Concurrent asynchronous checkout has multiple operations in progress and interleaves ready continuations while other operations wait. Concurrency does not require a dedicated thread or core for every order, though continuations may execute in parallel when resources are available.

6. The `phase4 --cancel-after 9000` run reported `Run cancelled — 8 orders finished, 12 cancelled.` Six orders were already Rejected, one PaymentFailed, and one Completed. The twelve in-progress orders were persisted as Pending / Cancelled after compensation. Product stock equalled initial stock minus quantities from the single Completed order. Payments ended as 11 Refunded, 1 Captured and 1 Declined; cancelled orders retained no captured charge. All JSON parsed and no temporary files remained. Cancellation stops new work immediately but the process waits for required refund/release/save cleanup, explaining progress lines beyond nine seconds. See `sample-output/phase4/cancellation-output.txt`.

### Phase 4 verification and issues resolved

Build succeeded with no warnings or errors. File checks confirmed twenty unique checkout orders, expected status/payment counts, thirty distinct test email IDs each appearing once, the unchanged checkout stock rule, and a 1–20 progress counter whose order IDs differ from input order. Isolated checks covered a failed carrier finishing before a successful one, all carriers failing, a deterministic deadline fallback, caller cancellation reaching every carrier, prompt cancellation of the parallel CPU loop, and aggregation of both dashboard failures. Losing tasks are always observed. Deadline handling drains completed responses before deciding on fallback so a success that arrived within budget is not discarded merely because a failed response was observed first.

The earlier Windows file-replacement conflict was solved with a shared read/update gate for mutable JSON files; the synchronous path uses the same gate for the Phase 3 thread comparison. The three read-only seed files bypass read gates because initialization finishes before any readers start. Cancellation bookkeeping records writes before Commit delays so cleanup does not lose or duplicate stock releases. These fixes remain in use in Phase 4.

### Console evidence images

These are **browser screenshots of excerpts from actual saved console logs**, with source paths shown in each image; they are not screenshots of a live terminal window. Full unfiltered transcripts remain under sample-output. No output values were invented or changed for the images.

| Evidence | Image |
|---|---|
| Phase 1 validation | [Validation summary](screenshots/phase1-validation.png) |
| Phase 2 race / faulted task | [Race and failure](screenshots/phase2-race-and-failure.png) |
| Phase 3 timeout / refund / retries | [Payment and email](screenshots/phase3-payment-and-email.png) |
| Both fire-and-forget demonstrations | [Before and after](screenshots/phase3-fire-and-forget.png) |
| Phase 4 Task 10.1 | [Live progress](screenshots/phase4-live-progress.png) |
| Phase 4 Task 10.2 | [Dashboard](screenshots/phase4-dashboard.png) |
| Phase 4 Task 10.3 | [Shipping](screenshots/phase4-shipping.png) |
| Phase 4 Task 10.4 A and B | [Fraud and email](screenshots/phase4-fraud-and-email.png) |
| Cancelled Phase 4 run | [Cancellation](screenshots/phase4-cancelled.png) |
