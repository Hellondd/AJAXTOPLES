using Microsoft.AspNetCore.Mvc;

namespace SecureFilesMvc.Web.Controllers;

/// <summary>
/// MVC-страница для загрузки файлов (тонкий UI поверх FilesClient).
/// </summary>
public class FilesController : Controller
{
    public IActionResult Index() => View();
}
