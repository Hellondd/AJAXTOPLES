using Microsoft.AspNetCore.Mvc;
using SecureFilesMvc.Web.Models.ViewModels;
using SecureFilesMvc.Web.Services;

namespace SecureFilesMvc.Web.Controllers;

/// <summary>Главная страница: краткий обзор threat model.</summary>
public class HomeController : Controller
{
    private readonly IThreatCatalogService _catalog;

    public HomeController(IThreatCatalogService catalog) => _catalog = catalog;

    public IActionResult Index()
    {
        var vm = new ThreatModelViewModel
        {
            Boundaries = _catalog.GetTrustBoundaries(),
            Threats = _catalog.GetThreats(),
            Backlog = _catalog.GetBacklog()
        };
        return View(vm);
    }

    public IActionResult Error() => Content("Произошла ошибка. Подробности — в логах по X-Request-Id.");
}
