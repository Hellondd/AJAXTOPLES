using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFilesMvc.Web.Models.Files;
using SecureFilesMvc.Web.Security;
using SecureFilesMvc.Web.Services;

namespace SecureFilesMvc.Web.Controllers.Api;

/// <summary>
/// REST API для работы с файлами.
///
/// Это ГРАНИЦА ДОВЕРИЯ (TB-01). На входе — недоверенные данные,
/// поэтому контроллер обязан:
///   1. Аутентифицировать клиента (ApiKeyAuthenticationHandler).
///   2. Авторизовать действие (политики ReadFiles/WriteFiles).
///   3. Валидировать ВСЁ: размер, тип, имя, содержимое.
///   4. Возвращать безопасные ошибки без утечки деталей.
///   5. Логировать с correlation-id (TH-006).
/// </summary>
[ApiController]
[Route("api/files")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
public class FilesApiController : ControllerBase
{
    private readonly IFileStorageService _storage;
    private readonly IFileValidationService _validator;
    private readonly ILogger<FilesApiController> _logger;

    public FilesApiController(
        IFileStorageService storage,
        IFileValidationService validator,
        ILogger<FilesApiController> logger)
    {
        _storage = storage;
        _validator = validator;
        _logger = logger;
    }

    // -----------------------------------------------------------------------
    // POST /api/files  — загрузка
    // -----------------------------------------------------------------------
    [HttpPost]
    [Authorize(Policy = Policies.WriteFiles)]
    [RequestSizeLimit(FileValidationService.MaxSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileValidationService.MaxSizeBytes)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        // 1. Базовая проверка входных данных.
        //    Не доверяем даже факту наличия файла.
        if (file is null || file.Length == 0)
            return BadRequest(new ApiError("no_file", "Файл не передан"));

        // 2. Content-Type от клиента — лишь подсказка. Доверять нельзя.
        var claimedType = file.ContentType ?? "application/octet-stream";

        // 3. Валидация содержимого.
        await using var stream = file.OpenReadStream();
        var validation = await _validator.ValidateAsync(stream, file.FileName, claimedType, file.Length, ct);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Upload rejected: {Reason} Name={Name}", validation.Error, file.FileName);
            return BadRequest(new ApiError("validation_failed", validation.Error!));
        }

        // 4. Сохраняем через сервис, который сам санитайзит имя и путь.
        var stored = await _storage.SaveAsync(stream, file.FileName, claimedType, ct);

        // 5. Отдаём метаданные. URL формируем от id, а не от имени файла —
        //    чтобы не утекали оригинальные имена и не было предсказуемости.
        return CreatedAtAction(nameof(Download), new { id = stored.Id }, new
        {
            id = stored.Id,
            name = stored.OriginalName,
            size = stored.Size,
            contentType = stored.ContentType
        });
    }

    // -----------------------------------------------------------------------
    // GET /api/files/{id}  — скачивание
    // -----------------------------------------------------------------------
    [HttpGet("{id}")]
    [Authorize(Policy = Policies.ReadFiles)]
    public async Task<IActionResult> Download(string id, CancellationToken ct)
    {
        // id валидируется внутри FileStorageService (hex, длина 32).
        var result = await _storage.OpenAsync(id, ct);
        if (result is null)
        {
            // Не различаем "не найден" и "нет доступа" — не даём атакующему
            // возможность перебором определить, какие id существуют.
            return NotFound(new ApiError("not_found", "Файл не найден"));
        }

        var (stream, meta) = result.Value;

        // Content-Disposition: attachment — чтобы браузер не пытался
        // интерпретировать содержимое как HTML/JS (защита от XSS).
        // filename* — RFC 5987 для корректной передачи не-ASCII имён.
        Response.Headers.ContentDisposition =
            $"attachment; filename*=UTF-8''{Uri.EscapeDataString(meta.OriginalName)}";

        // Явно указываем тип, чтобы браузер не угадывал.
        return File(stream, meta.ContentType, enableRangeProcessing: true);
    }
}
