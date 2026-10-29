using OrderProcessing.Shared.Exceptions;
using System.Collections.Concurrent;
using System.Text.Json;

namespace OrderProcessing.Shared.Storage;


public sealed class JsonFileStore
{
    private const int BufferSize = 4096;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileGates = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path);
    }

    private static SemaphoreSlim GateFor(string path) =>
        FileGates.GetOrAdd(path, static _ => new SemaphoreSlim(1, 1));

    private static bool IsReadOnlyPath(string path)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        // These files are seeded and read only files not need gate for it there is no problem if to many thread read this safely (thread safe)
        return string.Equals(path, DataFiles.PathFor(DataFiles.Customers), comparison)
            || string.Equals(path, DataFiles.PathFor(DataFiles.Coupons), comparison)
            || string.Equals(path, DataFiles.PathFor(DataFiles.IncomingOrders), comparison);
    }

    public T? GetRecord<T>(string path, Predicate<T> filter)
    {
        path = Normalize(path);
        if (IsReadOnlyPath(path))
            return ReadRecord(path, filter);
        SemaphoreSlim gate = GateFor(path);
        gate.Wait();
        try { return ReadRecord(path, filter); }
        finally { gate.Release(); }
    }

    private T? ReadRecord<T>(string path, Predicate<T> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        path = Normalize(path);
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, BufferSize, FileOptions.SequentialScan);

            using JsonDocument document = JsonDocument.Parse(stream);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new JsonException("Expected a JSON array.");

            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                T? record = element.Deserialize<T>(JsonSettings.Options);

                if (record is not null && filter(record)) 
                    return record;
            }
            return default;
        }
        catch (FileNotFoundException) 
        { 
            return default;
        }
        catch (DirectoryNotFoundException) 
        { 
            return default;
        }
        catch (JsonException ex) 
        { 
            throw new DataFileCorruptedException(path, ex);
        }
    }

    public async Task<T?> GetRecordAsync<T>(string path, Predicate<T> filter,
        CancellationToken ct = default)
    {
        path = Normalize(path);

        if (IsReadOnlyPath(path))
            return await ReadRecordAsync(path, filter, ct);

        SemaphoreSlim gate = GateFor(path);

        await gate.WaitAsync(ct);

        try
        {
            return await ReadRecordAsync(path, filter, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<T?> ReadRecordAsync<T>(string path, Predicate<T> filter,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);

        path = Normalize(path);

        ct.ThrowIfCancellationRequested();

        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new JsonException("Expected a JSON array.");

            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();

                T? record = element.Deserialize<T>(JsonSettings.Options);

                if (record is not null && filter(record))
                    return record;
            }

            return default;
        }
        catch (FileNotFoundException) 
        { 
            return default;
        }
        catch (DirectoryNotFoundException)
        {
            return default; 
        }
        catch (JsonException ex) 
        { 
            throw new DataFileCorruptedException(path, ex);
        }
    }

    public List<T> GetAllRecords<T>(string path)
    {
        path = Normalize(path);
        if (IsReadOnlyPath(path))
            return ReadAllRecords<T>(path);
        SemaphoreSlim gate = GateFor(path);
        gate.Wait();
        try { return ReadAllRecords<T>(path); }
        finally { gate.Release(); }
    }

    private List<T> ReadAllRecords<T>(string path)
    {
        path = Normalize(path);

        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, BufferSize, FileOptions.SequentialScan);

            return JsonSerializer.Deserialize<List<T>>(stream, JsonSettings.Options)
                ?? throw new JsonException("Expected a JSON array, not null.");
        }
        catch (FileNotFoundException) 
        { 
            return []; 
        }
        catch (DirectoryNotFoundException) 
        {
            return []; 
        }
        catch (JsonException ex) 
        { 
            throw new DataFileCorruptedException(path, ex);
        }
    }

    public async Task<List<T>> GetAllRecordsAsync<T>(string path,
        CancellationToken ct = default)
    {
        path = Normalize(path);

        if (IsReadOnlyPath(path))
            return await ReadAllRecordsAsync<T>(path, ct);

        SemaphoreSlim gate = GateFor(path);

        await gate.WaitAsync(ct);
        
        try
        {
            return await ReadAllRecordsAsync<T>(path, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<T>> ReadAllRecordsAsync<T>(string path, CancellationToken ct)
    {
        path = Normalize(path);

        ct.ThrowIfCancellationRequested();

        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            return await JsonSerializer.DeserializeAsync<List<T>>(
                stream, JsonSettings.Options, ct)
                ?? throw new JsonException("Expected a JSON array, not null.");
        }
        catch (FileNotFoundException) 
        { 
            return [];
        }
        catch (DirectoryNotFoundException) 
        { 
            return []; 
        }
        catch (JsonException ex) 
        { 
            throw new DataFileCorruptedException(path, ex);
        }
    }

    public bool UpdateRecords<T>(string path, Func<List<T>, bool> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);

        path = Normalize(path);

        SemaphoreSlim gate = GateFor(path);
        gate.Wait();
        try
        {
            List<T> records = ReadAllRecords<T>(path);

            if (!mutator(records))
                return false;

            ReplaceAtomically(path, records);

            return true;
        }
        finally 
        { 
            gate.Release();
        }
    }

    public async Task<bool> UpdateRecordsAsync<T>(string path,
        Func<List<T>, bool> mutator, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutator);

        path = Normalize(path);

        SemaphoreSlim gate = GateFor(path);

        await gate.WaitAsync(ct);

        try
        {
            List<T> records = await ReadAllRecordsAsync<T>(path, ct);

            ct.ThrowIfCancellationRequested();

            if (!mutator(records)) 
                return false;

            ct.ThrowIfCancellationRequested();

            await ReplaceAtomicallyAsync(path, records, ct);

            return true;
        }
        finally 
        { 
            gate.Release();
        }
    }

    private static void ReplaceAtomically<T>(string path, IReadOnlyCollection<T> records)
    {
        string tempPath = MakeTempPath(path);
        try
        {
            using (FileStream stream = new(tempPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, BufferSize, FileOptions.None))
            {
                JsonSerializer.Serialize(stream, records, JsonSettings.Options);
                stream.Flush(flushToDisk: true);
            }
            Promote(tempPath, path);
        }
        finally 
        { 
            TryDeleteTemp(tempPath);
        }
    }

    private static async Task ReplaceAtomicallyAsync<T>(string path,
        IReadOnlyCollection<T> records, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        string tempPath = MakeTempPath(path);

        try
        {
            await using (FileStream stream = new(tempPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, records, JsonSettings.Options, ct);
                await stream.FlushAsync(ct);
            }
            ct.ThrowIfCancellationRequested();

            Promote(tempPath, path);
        }
        finally 
        { 
            TryDeleteTemp(tempPath);
        }
    }

    public async Task SeedAsync<T>(
      string path,
      IReadOnlyCollection<T> records,
      CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        path = Normalize(path);

        ct.ThrowIfCancellationRequested();

        if (File.Exists(path))
            throw new IOException($"Seed file already exists: '{path}'.");

        await ReplaceAtomicallyAsync(path, records, ct);
    }

    private static void Promote(string tempPath, string path)
    {
        if (File.Exists(path)) 
            File.Replace(tempPath, path, null);

        else 
            File.Move(tempPath, path);
    }

    private static string MakeTempPath(string path) => Path.Combine(
        Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

    private static void TryDeleteTemp(string path)
    {
        try 
        { 
            if (File.Exists(path)) 
                File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

  
}
