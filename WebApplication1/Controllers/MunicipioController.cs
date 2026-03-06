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
    // DASHBOARD MUNICIPAL (BANDEJA DE ENTRADA)
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var result = await _api.GetMunicipalDashboardAsync();
        ViewBag.DashboardJson = result.Success ? result.Data : "{}";
        if (!result.Success) ViewBag.Error = result.Error;
        return View();
    }

    // =============================================
    // DASHBOARD GEOESPACIAL DE FISCALIZACIÓN
    // =============================================

    [HttpGet]
    public async Task<IActionResult> GeoInsights(
        string? dateFrom = null, string? dateTo = null,
        string? crop = null, string? toxClass = null,
        string? productName = null, string? advisorName = null,
        int? nearSensitivePointMeters = null)
    {
        var result = await _api.GetGeoInsightsAsync(
            dateFrom: dateFrom, dateTo: dateTo,
            crop: crop, toxClass: toxClass,
            productName: productName, advisorName: advisorName,
            nearSensitivePointMeters: nearSensitivePointMeters);

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
    public async Task<IActionResult> Details(string code)
    {
        var result = await _api.GetRecipeByCodeAsync(code);

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
        long id, string action, string? observation, long? targetMunicipalityId, string code)
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

        return RedirectToAction("Details", new { code });
    }

    // =============================================
    // ENVIAR MENSAJE
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(long id, string message, string code)
    {
        var result = await _api.SendRecipeMessageAsync(id, message);

        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Mensaje enviado." : result.Error;

        return RedirectToAction("Details", new { code });
    }

    // =============================================
    // ZONAS DE EXCLUSIÓN
    // =============================================

    [HttpGet]
    public async Task<IActionResult> ExclusionZones()
    {
        var result = await _api.GetMyExclusionZonesAsync();
        ViewBag.ZonesJson = result.Success ? result.Data : "[]";
        if (!result.Success) ViewBag.Error = result.Error;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExclusionZone(
        string name, string? description, string type, string restriction, string verticesJson)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "El nombre es obligatorio.";
            return RedirectToAction("ExclusionZones");
        }

        try
        {
            var vertices = JsonSerializer.Deserialize<List<object>>(verticesJson ?? "[]",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (vertices == null || vertices.Count < 3)
            {
                TempData["Error"] = "Dibujá al menos 3 puntos en el mapa.";
                return RedirectToAction("ExclusionZones");
            }

            var result = await _api.CreateExclusionZoneAsync(new
            {
                name = name.Trim(),
                description = description?.Trim(),
                type = (type ?? "CUSTOM").Trim().ToUpper(),
                restriction = (restriction ?? "PROHIBIDA").Trim().ToUpper(),
                vertices
            });

            TempData[result.Success ? "Success" : "Error"] =
                result.Success ? "Zona de exclusión creada correctamente." : result.Error;
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al crear la zona: {ex.Message}";
        }

        return RedirectToAction("ExclusionZones");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExclusionZone(long id)
    {
        var result = await _api.DeleteExclusionZoneAsync(id);

        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Zona eliminada." : result.Error;

        return RedirectToAction("ExclusionZones");
    }

    // =============================================
    // PUNTOS SENSIBLES
    // =============================================

    [HttpGet]
    public async Task<IActionResult> SensitivePoints()
    {
        var result = await _api.GetMySensitivePointsAsync();
        ViewBag.PointsJson = result.Success ? result.Data : "[]";
        if (!result.Success) ViewBag.Error = result.Error;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSensitivePoint(
        string name, string? type, string? locality, string? department,
        string latitude, string longitude,
        int? distanceClassIa, int? distanceClassIb, int? distanceClassII,
        int? distanceClassIII, int? distanceClassIV)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "El nombre es obligatorio.";
            return RedirectToAction("SensitivePoints");
        }

        if (!decimal.TryParse(latitude?.Replace(",", "."), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var lat) ||
            !decimal.TryParse(longitude?.Replace(",", "."), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var lng))
        {
            TempData["Error"] = "Coordenadas inválidas. Hacé click en el mapa para seleccionar la ubicación.";
            return RedirectToAction("SensitivePoints");
        }

        var result = await _api.CreateSensitivePointAsync(new
        {
            name = name.Trim(),
            type = type?.Trim(),
            locality = locality?.Trim(),
            department = department?.Trim(),
            latitude = lat,
            longitude = lng,
            distanceClassIa,
            distanceClassIb,
            distanceClassII,
            distanceClassIII,
            distanceClassIV
        });

        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Punto sensible creado correctamente." : result.Error;

        return RedirectToAction("SensitivePoints");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSensitivePoint(long id)
    {
        var result = await _api.DeleteSensitivePointAsync(id);

        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Punto sensible eliminado." : result.Error;

        return RedirectToAction("SensitivePoints");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(long id, string? code)
    {
        var (data, statusCode, error) = await _api.GetBytesAsync($"/api/recipes/{id}/export-pdf");
        if (data == null)
        {
            TempData["Error"] = error ?? "No se pudo generar el PDF.";
            return RedirectToAction("Details", new { id });
        }
        var fileName = $"Expediente_{code ?? id.ToString()}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        return File(data, "application/pdf", fileName);
    }
}