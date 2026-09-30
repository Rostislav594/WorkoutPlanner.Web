using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GymPlanner.Mobile.Offline;

/// <summary>
/// Локальная копия данных пользователя: по JSON-файлу на документ, по папке на аккаунт.
/// </summary>
/// <remarks>
/// Документы держатся в памяти после первого чтения, запись идёт через временный
/// файл, чтобы обрыв посреди записи не оставил половину JSON. Все операции
/// последовательны: данных мало, а гонки между экранами и синхронизацией дороже.
/// </remarks>
public sealed class OfflineDocumentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _rootDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, object?> _memory = new(StringComparer.Ordinal);
    private string? _accountDirectory;

    public OfflineDocumentStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = rootDirectory;
    }

    /// <summary>Есть ли выбранный аккаунт. Без него хранилище ничего не читает и не пишет.</summary>
    public bool HasAccount => Volatile.Read(ref _accountDirectory) is not null;

    /// <summary>
    /// Переключает хранилище на данные аккаунта; <c>null</c> — никто не вошёл.
    /// </summary>
    public async Task UseAccountAsync(string? accountEmail, CancellationToken cancellationToken = default)
    {
        var directory = string.IsNullOrWhiteSpace(accountEmail)
            ? null
            : Path.Combine(_rootDirectory, AccountFolderName(accountEmail));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (string.Equals(directory, _accountDirectory, StringComparison.Ordinal))
                return;

            _memory.Clear();
            Volatile.Write(ref _accountDirectory, directory);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadCoreAsync<T>(key, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await WriteCoreAsync(key, value, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Читает документ, передаёт его в <paramref name="update"/> и сохраняет результат
    /// одним шагом, чтобы параллельная запись не потерялась между чтением и сохранением.
    /// </summary>
    public async Task<T?> UpdateAsync<T>(
        string key,
        Func<T?, T> update,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_accountDirectory is null)
                return default;

            var updated = update(await ReadCoreAsync<T>(key, cancellationToken));
            await WriteCoreAsync(key, updated, cancellationToken);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_accountDirectory is null)
                return;

            _memory.Remove(key);
            DeleteIfExists(DocumentPath(key));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Удаляет документы, чьи ключи начинаются с <paramref name="prefix"/>.</summary>
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_accountDirectory is null || !Directory.Exists(_accountDirectory))
                return;

            foreach (var key in _memory.Keys.Where(x => x.StartsWith(prefix, StringComparison.Ordinal)).ToList())
                _memory.Remove(key);

            foreach (var file in Directory.EnumerateFiles(_accountDirectory, prefix + "*.json"))
                DeleteIfExists(file);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Ключи всего, что лежит у текущего аккаунта: и документов, и файлов.</summary>
    public async Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_accountDirectory is null || !Directory.Exists(_accountDirectory))
                return [];

            return Directory.EnumerateFiles(_accountDirectory)
                .Where(x => x.EndsWith(".json", StringComparison.Ordinal) || x.EndsWith(".bin", StringComparison.Ordinal))
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<byte[]?> ReadBytesAsync(string key, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_accountDirectory is null)
                return null;

            var path = BinaryPath(key);
            return File.Exists(path)
                ? await File.ReadAllBytesAsync(path, cancellationToken)
                : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteBytesAsync(string key, byte[] content, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_accountDirectory is null)
                return;

            Directory.CreateDirectory(_accountDirectory);
            await WriteAtomicallyAsync(BinaryPath(key), content, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveBytesAsync(string key, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_accountDirectory is not null)
                DeleteIfExists(BinaryPath(key));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Стирает всё, что хранится для текущего аккаунта (выход, удаление аккаунта).</summary>
    public async Task DeleteAccountDataAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _memory.Clear();
            if (_accountDirectory is not null && Directory.Exists(_accountDirectory))
                Directory.Delete(_accountDirectory, recursive: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<T?> ReadCoreAsync<T>(string key, CancellationToken cancellationToken)
    {
        if (_accountDirectory is null)
            return default;

        if (_memory.TryGetValue(key, out var cached))
            return cached is T typed ? typed : default;

        var path = DocumentPath(key);
        T? value = default;
        if (File.Exists(path))
        {
            try
            {
                await using var stream = File.OpenRead(path);
                value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                // Испорченная копия хуже пустой: она перечитается с сервера при связи.
                DeleteIfExists(path);
            }
        }

        _memory[key] = value;
        return value;
    }

    private async Task WriteCoreAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        if (_accountDirectory is null)
            return;

        Directory.CreateDirectory(_accountDirectory);
        var content = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        await WriteAtomicallyAsync(DocumentPath(key), content, cancellationToken);
        _memory[key] = value;
    }

    private static async Task WriteAtomicallyAsync(string path, byte[] content, CancellationToken cancellationToken)
    {
        var temporaryPath = path + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, content, cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private string DocumentPath(string key) => Path.Combine(_accountDirectory!, ValidateKey(key) + ".json");

    private string BinaryPath(string key) => Path.Combine(_accountDirectory!, ValidateKey(key) + ".bin");

    private static string ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) ||
            key.Any(x => !(char.IsAsciiLetterOrDigit(x) || x is '-' or '_' or '.')) ||
            key.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unsupported offline document key '{key}'.", nameof(key));
        }

        return key;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string AccountFolderName(string email)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return "account-" + Convert.ToHexString(hash, 0, 12).ToLowerInvariant();
    }
}
