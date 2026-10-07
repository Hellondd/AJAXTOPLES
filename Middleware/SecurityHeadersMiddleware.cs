namespace SecureFilesMvc.Web.Middleware;

/// <summary>
/// Добавляет security-заголовки к каждому ответу.
/// Покрывает часть угроз STRIDE (Information Disclosure, XSS, clickjacking).
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // CSP: запрещаем inline-скрипты и сторонние источники.
        // Если ваш фронт использует только внешний files-client.js — это безопасный дефолт.
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'self'";

        // Запрет MIME-sniffing — браузер не будет угадывать тип.
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Запрет встраивания сайта в iframe (clickjacking).
        context.Response.Headers["X-Frame-Options"] = "DENY";

        // Ограничение передаваемого Referer.
        context.Response.Headers["Referrer-Policy"] = "no-referrer";

        await _next(context);
    }
}
