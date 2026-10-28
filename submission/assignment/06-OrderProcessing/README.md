# OrderProcessing

A .NET 10 console application for an order processing assignment covering asynchronous programming, concurrency, and thread safety. Data is stored in local JSON files, with simulated database and external service latency.

The project contains the shared infrastructure, Phase 1 (data access and sequential validation), and Phase 2 (concurrent processing, invoices, and reports). Phases 3–4 are not implemented.

## Running the project

Requires the .NET 10 SDK. From the solution directory:

```powershell
dotnet build OrderProcessing.slnx
dotnet run --project OrderProcessing -- phase1
dotnet run --project OrderProcessing -- phase1 --cancel-after 4000
dotnet run --project OrderProcessing -- phase2
dotnet run --project OrderProcessing -- phase2 --cancel-after 18000
```

The entry point runs Phase 1 by default. It resets and loads the input, validates all 20 orders synchronously without saving, resets again, then validates and saves each order asynchronously in sequence. Ctrl+C or `--cancel-after <ms>` cancels the run.

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

Seeding runs before other operations, with a separate file for each task. It does not require a per-file semaphore in this usage. The synchronous update method currently assumes sequential use; concurrent synchronous writers are not supported yet.

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
