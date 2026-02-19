using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebApplication1.Models.Municipio;
using WebApplication1.Models.Recipes;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[Authorize(Roles = "Municipio")]
public class MunicipioController : Controller
{
    private readonly AgroApiClient _api;
    public MunicipioController(AgroApiClient api) => _api = api;

    // =============================================
    // DASHBOARD GEOESPACIAL DE FISCALIZACIÓN
    // =============================================

    [HttpGet]
    public async Task<IActionResult> GeoInsights(
        string? dateFrom = null, string? dateTo = null,
        string? crop = null, string? toxClass = null,
        string? productName = null, string? advisorName = null)
    {
        var result = await _api.GetGeoInsightsAsync(
            dateFrom: dateFrom, dateTo: dateTo,
            crop: crop, toxClass: toxClass,
            productName: productName, advisorName: advisorName);

        if (!result.Success)
            ViewBag.Error = $"No se pudieron obtener datos geoespaciales. {result.Error}";

        return View(new GeoInsightsViewModel { JsonData = result.Data ?? "" });
    }

    // =============================================
    // RECETAS ASIGNADAS A MI MUNICIPIO
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1, int pageSize = 20,
        string? status = null, string? searchText = null)
    {
        var result = await _api.GetRecipesAsync(page, pageSize, status, searchText);

        if (!result.Success)
            ViewBag.Error = $"No se pudo obtener el listado de recetas. {result.Error}";

        return View(new RecipesIndexViewModel
        {
            Data = result.Data ?? new(),
            Status = status,
            SearchText = searchText
        });
    }

    // =============================================
    // DETALLE DE RECETA
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        var result = await _api.GetRecipeAsync(id);

        if (result.IsNotFound)
        {
            TempData["Error"] = "La receta solicitada no existe.";
            return RedirectToAction("Index");
        }

        if (!result.Success)
        {
            ViewBag.Error = $"No se pudo obtener el detalle. {result.Error}";
            return View(new RecipeDetailViewModel());
        }

        return View(new RecipeDetailViewModel { Recipe = result.Data! });
    }

    // =============================================
    // REVISAR RECETA (Aprobar / Rechazar / Observar)
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(
        long id, string action, string? observation, long? targetMunicipalityId)
    {
        var result = await _api.ReviewRecipeAsync(id, action, observation, targetMunicipalityId);

        if (result.Success)
        {
            TempData["Success"] = action.ToUpper() switch
            {
                "APROBADA" => "La receta fue aprobada correctamente. ✅",
                "RECHAZADA" => "La receta fue rechazada.",
                "OBSERVADA" => "Se solicitó información adicional al aplicador.",
                "REDIRIGIDA" => "La receta fue redirigida a otro municipio.",
                _ => "Acción realizada."
            };
        }
        else
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction("Details", new { id });
    }

    // =============================================
    // ENVIAR MENSAJE
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(long id, string message)
    {
        var result = await _api.SendRecipeMessageAsync(id, message);

        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Mensaje enviado." : result.Error;

        return RedirectToAction("Details", new { id });
    }
}