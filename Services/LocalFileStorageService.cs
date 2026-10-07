using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SecureFilesMvc.Web.Services;

/// <summary>
/// Локальное файловое хранилище.
///
/// БЕЗОПАСНОСТЬ (закрывает TH-002, TH-003, SB-002, SB-003):
///   - Имя файла на диске — GUID, а не пользовательское имя.
///     Это защищает от:
///       * path traversal (../../etc/passwd),
///       * перезаписи чужих файлов,
///       * предсказуемых URL.
///   - Оригинальное имя сохраняется только как метаданные.
///   - Все пути резолвятся в абсолютный и проверяются на принадлежность root-папке.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _root;
    private readonly ILogger<LocalFileStorageService> _logger;

    // В реальном проекте — БД. Здесь in-memory для примера.
    private static readonly ConcurrentDictionary<string, StoredFile> _index = new();

    public LocalFileStorageService(IWebHostEnvironment env, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        // ВАЖНО: храним ВНЕ wwwroot, чтобы файлы не были доступны напрямую по URL.
        _root = Path.Combine(env.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(Stream content, string originalName, string contentType, CancellationToken ct)
    {
        // 1. Генерируем безопасный id — GUID без дефисов.
        var id = Guid.NewGuid().ToString("N");

        // 2. Санитайзим расширение. Берём только ".ext", отбрасываем всё остальное.
        //    Это защищает от "file.php.jpg", "file.asp;.jpg" и подобных трюков.
        var ext = Path.GetExtension(originalName);
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 10 || ext.Contains(Path.DirectorySeparatorChar))
            ext = ".bin";

        // 3. Итоговое имя — только GUID + расширение.
        var fileName = id + ext.ToLowerInvariant();
        var fullPath = Path.Combine(_root, fileName);

        // 4. Защита от path traversal: убеждаемся, что итоговый путь внутри _root.
        //    Если атакующий как-то передал "../" — резолв покажет другое место.
        var resolved = Path.GetFullPath(fullPath);
        if (!resolved.StartsWith(Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path traversal detected");

        // 5. Пишем поток на диск.
        await using (var fs = File.Create(resolved))
        {
            await content.CopyToAsync(fs, ct);
        }

        var info = new FileInfo(resolved);
        var meta = new StoredFile(id, Path.GetFileName(originalName), resolved, info.Length, contentType);
        _index[id] = meta;

        _logger.LogInformation("File stored. Id={Id} Size={Size} Type={Type}", id, info.Length, contentType);
        return meta;
    }

    public Task<(Stream stream, StoredFile meta)?> OpenAsync(string id, CancellationToken ct)
    {
        // 1. Валидация формата id. GUID без дефисов содержит только hex.
        //    Это — вторая линия защиты от path traversal через параметр id.
        if (string.IsNullOrEmpty(id) || id.Length != 32 || !id.All(Uri.IsHexDigit))
            return Task.FromResult<(Stream, StoredFile)?>(null);

        if (!_index.TryGetValue(id, out var meta))
            return Task.FromResult<(Stream, StoredFile)?>(null);

        var resolved = Path.GetFullPath(meta.StoredPath);
        if (!resolved.StartsWith(Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<(Stream, StoredFile)?>(null);

        if (!File.Exists(resolved))
            return Task.FromResult<(Stream, StoredFile)?>(null);

        Stream stream = File.OpenRead(resolved);
        return Task.FromResult<(Stream, StoredFile)?>((stream, meta));
    }

    public Task<bool> ExistsAsync(string id, CancellationToken ct)
        => Task.FromResult(_index.ContainsKey(id));
}
