using Microsoft.AspNetCore.Authorization;

namespace SecureFilesMvc.Web.Security;

/// <summary>
/// Кастомное требование авторизации: у пользователя должна быть нужная роль-разрешение.
/// Заготовка под SB-003 (ownership-модель: проверка владельца файла).
/// Чтобы использовать — зарегистрируйте handler в Program.cs:
///   builder.Services.AddSingleton&lt;IAuthorizationHandler, FileAccessHandler&gt;();
/// и политику: p.AddRequirements(new FileAccessRequirement("Files.Read")).
/// </summary>
public class FileAccessRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public FileAccessRequirement(string permission) => Permission = permission;
}

public class FileAccessHandler : AuthorizationHandler<FileAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, FileAccessRequirement requirement)
    {
        if (context.User.IsInRole(requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
