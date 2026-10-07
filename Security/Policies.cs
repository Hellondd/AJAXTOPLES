namespace SecureFilesMvc.Web.Security;

/// <summary>
/// Имена политик авторизации, чтобы не использовать "магические строки" в контроллерах.
/// </summary>
public static class Policies
{
    public const string ApiKey = "ApiKeyPolicy";
    public const string ReadFiles = "ReadFilesPolicy";
    public const string WriteFiles = "WriteFilesPolicy";
}
