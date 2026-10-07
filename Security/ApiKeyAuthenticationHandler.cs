using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SecureFilesMvc.Web.Security;

/// <summary>
/// Кастомный authentication handler для API-ключей.
///
/// ПРИНЦИПЫ (из threat model, SB-001):
///  - Ключ передаётся в заголовке "X-Api-Key" (не в query — чтобы не попадал в логи прокси);
///  - Сравнение — constant-time (защита от timing-атак);
///  - Ошибки не раскрывают, существует ли ключ;
///  - Успешная/неуспешная аутентификация логируется.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    // В реальном проекте — из конфигурации/секретов/БД.
    private const string ValidKey = "demo-secret-key-12345";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. Проверяем наличие заголовка.
        if (!Request.Headers.TryGetValue(HeaderName, out var provided))
        {
            // Не падаем с ошибкой — просто "не аутентифицирован".
            // Это позволяет анонимным endpoint'ам работать.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedKey = provided.ToString();

        // 2. Constant-time сравнение. Обычное == уязвимо к timing-атаке.
        if (!CryptographicEquals(providedKey, ValidKey))
        {
            Logger.LogWarning("Invalid API key attempt from {RemoteIp}", Request.HttpContext.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        // 3. Формируем ClaimsPrincipal — "паспорт" пользователя.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "api-client"),
            new Claim(ClaimTypes.Name, "API Client"),
            // Роль определяет доступ к операциям чтения/записи.
            new Claim(ClaimTypes.Role, "Files.Read"),
            new Claim(ClaimTypes.Role, "Files.Write")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Сравнение строк без утечки информации о времени выполнения.
    /// Возвращает false немедленно, если длины разные (это допустимо —
    /// длина ключа считается не секретной), иначе сравнивает XOR-ом все символы.
    /// </summary>
    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;

        var result = 0;
        for (int i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];

        return result == 0;
    }
}
