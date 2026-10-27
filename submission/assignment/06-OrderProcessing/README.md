# OrderProcessing

A .NET 10 console application for an order processing assignment covering asynchronous programming, concurrency, and thread safety. Data is stored in local JSON files, with simulated database and external service latency.

The project contains the shared infrastructure and Phase 1: asynchronous data access and sequential order validation. Phases 2–4 are not implemented.

## Running the project

Requires the .NET 10 SDK. From the solution directory:

```powershell
dotnet build OrderProcessing.slnx
dotnet run --project OrderProcessing -- phase1
dotnet run --project OrderProcessing -- phase1 --cancel-after 4000
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

The `invoices/` and `reports/` directories are also created and are currently empty.

Collections are UTF-8 JSON arrays. Empty collections are written as `[]`. Serialization uses `System.Text.Json` with indentation, camelCase property names, and string enum values.

### JsonFileStore API

- `GetAllRecordsAsync<T>` reads a collection and returns an empty list when the file is missing.
- `GetRecordAsync<T>` validates the JSON document and returns the first matching record, or the default value when no match exists.
- `SeedAsync<T>` creates initial data and rejects an existing destination file.
- `UpdateRecordsAsync<T>` applies a callback to the collection and saves it when the callback returns `true`.
- Synchronous read and update methods support synchronous comparisons.

Invalid JSON raises `DataFileCorruptedException` with the affected file name. Async methods accept a `CancellationToken` as the last parameter.

Async updates use a semaphore per file path to protect the complete read-modify-write operation. This prevents concurrent updates from overwriting each other's changes. Writes first serialize to a temporary file in the same directory, then replace the destination, so failed serialization does not publish partial JSON.

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
