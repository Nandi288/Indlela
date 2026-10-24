using Indlela.Services.TechPulse;
using Indlela.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Indlela.Controllers;

public class TechPulseController(ITechPulseService techPulse) : Controller
{
    // ---------- UC-23 (+ UC-22 caching, UC-24 skill chips) ----------
    [HttpGet("/tech-pulse")]
    public async Task<IActionResult> Index(string? category, CancellationToken ct)
    {
        var snapshot = await techPulse.GetAsync(ct); // served from IMemoryCache for 6 hours

        var categories = snapshot.Articles.Select(a => a.Category).Distinct().Order().ToList();
        var selected = categories.FirstOrDefault(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase));

        return View(new TechPulseVm
        {
            Articles = selected is null
                ? snapshot.Articles
                : snapshot.Articles.Where(a => a.Category == selected).ToList(),
            Categories = categories,
            SelectedCategory = selected,
            LastUpdatedUtc = snapshot.LastUpdatedUtc
        });
    }
}
