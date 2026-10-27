# OrderProcessing

A .NET 10 console application for an order processing assignment covering asynchronous programming, concurrency, and thread safety. Data is stored in local JSON files, with simulated database and external service latency.

The project currently contains the shared infrastructure and initial data setup. Order processing phases are not implemented yet.

## Running the project

Requires the .NET 10 SDK. From the solution directory:

```powershell
dotnet build OrderProcessing.slnx
dotnet run --project OrderProcessing
```

The current entry point resets the data and prints the number of records created in each file. Ctrl+C cancels initialization.

Data is created under `Path.Combine(AppContext.BaseDirectory, "Data")`. For a Debug build, this is normally `OrderProcessing/bin/Debug/net10.0/Data/`.

Each run deletes and recreates this directory. Reset runs before any other operation uses the files.

## Project structure

```text
OrderProcessing.slnx
OrderProcessing/
|-- OrderProcessing.csproj
|-- Program.cs
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

`RunCancellation` owns the cancellation source and handles Ctrl+C. It also supports a cancellation timeout, although command-line timeout parsing is not connected to the entry point yet.

`OrderCancellationCleanup` contains stock release, payment refund, and cancelled-order persistence logic. It is not yet connected to an order processor.

The custom exceptions are `CustomerNotFoundException`, `InvalidCouponException`, `ShippingProviderUnavailableException`, `DatabaseWriteException`, `SmtpUnavailableException`, and `DataFileCorruptedException`.

## Verification

The shared infrastructure builds successfully with no warnings or errors. Initialization produced the eight JSON collections with the counts shown above.

Storage checks covered missing files, invalid JSON after an early matching record, failed serialization, cancellation before seeding, concurrent async updates, and rejection of an existing seed file.

Two storage issues were addressed during setup:

- Returning from a streamed lookup at the first match could leave malformed JSON later in the file undetected. The lookup now parses the complete document before searching.
- Writing seed data directly to the destination could leave partial JSON after a failure. Seeding now uses the temporary-file write path.

No phase performance measurements have been recorded yet.
