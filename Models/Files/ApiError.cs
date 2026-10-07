namespace SecureFilesMvc.Web.Models.Files;

/// <summary>
/// Унифицированный формат ошибки API.
///
/// ВАЖНО: не возвращаем пользователю внутренние исключения — только
/// код и безопасное сообщение. Детали — в логах по correlation-id.
/// </summary>
public record ApiError(string Code, string Message, string? CorrelationId = null);
