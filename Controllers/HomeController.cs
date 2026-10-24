using System.Diagnostics;
using Indlela.Models;
using Indlela.Services.Catalog;
using Indlela.Services.Metrics;
using Indlela.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Indlela.Controllers;

public class HomeController(
    ICourseCatalogService catalog,
    IImpactMetricsService metrics) : Controller
{
    /// <summary>Landing page: brand, hero, impact metrics and featured courses.</summary>
    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var (providers, categories) = await catalog.FacetsAsync(ct);

        var vm = new HomeLandingVm
        {
            Metrics = await metrics.GetAsync(ct),
            FeaturedCourses = await catalog.FeaturedAsync(6, ct),
            Categories = categories,
            ProviderCount = providers.Count,
            PublishedCourseCount = await catalog.Listable().CountAsync(ct)
        };

        return View(vm);
    }

    [HttpGet("/privacy")]
    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
