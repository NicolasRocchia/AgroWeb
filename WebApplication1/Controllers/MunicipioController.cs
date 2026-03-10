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

        // Load territorial context for the map
        var spResult = await _api.GetMySensitivePointsAsync();
        ViewBag.MunicipalSensitivePointsJson = spResult.Success ? spResult.Data : "[]";

        var ezResult = await _api.GetMyExclusionZonesAsync();
        ViewBag.MunicipalExclusionZonesJson = ezResult.Success ? ezResult.Data : "[]";

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
    public async Task<IActionResult> ExportPdf(long id, string code)
    {
        var (data, statusCode, error) = await _api.GetBytesAsync($"/api/recipes/{id}/export-pdf");

        if (data == null)
        {
            TempData["Error"] = error ?? "No se pudo generar el PDF.";
            // Redirección limpia forzando el uso de 'code'
            return RedirectToAction("Details", new { code });
        }

        var fileName = $"Expediente_{code}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        return File(data, "application/pdf", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ReportPdf(string? dateFrom, string? dateTo)
    {
        var qs = BuildReportQuery(dateFrom, dateTo, "pdf");
        var (data, statusCode, error) = await _api.GetBytesAsync($"/api/recipes/municipal-report{qs}");
        if (data == null)
        {
            TempData["Error"] = error ?? "No se pudo generar el reporte PDF.";
            return RedirectToAction("Dashboard");
        }
        return File(data, "application/pdf", $"Reporte_Municipal_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ReportExcel(string? dateFrom, string? dateTo)
    {
        var qs = BuildReportQuery(dateFrom, dateTo, "excel");
        var (data, statusCode, error) = await _api.GetBytesAsync($"/api/recipes/municipal-report{qs}");
        if (data == null)
        {
            TempData["Error"] = error ?? "No se pudo generar el reporte Excel.";
            return RedirectToAction("Dashboard");
        }
        return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Reporte_Municipal_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    // =============================================
    // JURISDICCIÓN MUNICIPAL
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Jurisdiction()
    {
        var result = await _api.GetRawJsonAsync("/api/jurisdiction");
        ViewBag.JurisdictionJson = result.Success ? result.Data : "{}";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveJurisdictionRadius(string radiusKm, string? centerLat, string? centerLng)
    {
        if (!decimal.TryParse(radiusKm?.Replace(",", "."),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var radius))
        {
            TempData["Error"] = "Radio inválido.";
            return RedirectToAction("Jurisdiction");
        }

        decimal? lat = null, lng = null;
        if (!string.IsNullOrEmpty(centerLat) && !string.IsNullOrEmpty(centerLng))
        {
            decimal.TryParse(centerLat.Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedLat);
            decimal.TryParse(centerLng.Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedLng);
            lat = parsedLat;
            lng = parsedLng;
        }

        var result = await _api.PutAsync("/api/jurisdiction/radius", new { radiusKm = radius, centerLat = lat, centerLng = lng });
        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Jurisdicción por radio guardada." : result.Error;
        return RedirectToAction("Jurisdiction");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveJurisdictionPolygon(string verticesJson)
    {
        if (string.IsNullOrWhiteSpace(verticesJson))
        {
            TempData["Error"] = "No se recibieron vértices.";
            return RedirectToAction("Jurisdiction");
        }

        var vertices = System.Text.Json.JsonSerializer.Deserialize<List<object>>(verticesJson);
        var result = await _api.PutAsync("/api/jurisdiction/polygon", new { vertices = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(verticesJson) });
        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Jurisdicción por polígono guardada." : result.Error;
        return RedirectToAction("Jurisdiction");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearJurisdiction()
    {
        var result = await _api.DeleteAsync("/api/jurisdiction");
        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Jurisdicción eliminada." : result.Error;
        return RedirectToAction("Jurisdiction");
    }

    private static string BuildReportQuery(string? dateFrom, string? dateTo, string format)
    {
        var parts = new List<string> { $"format={format}" };
        if (!string.IsNullOrEmpty(dateFrom)) parts.Add($"dateFrom={dateFrom}");
        if (!string.IsNullOrEmpty(dateTo)) parts.Add($"dateTo={dateTo}");
        return "?" + string.Join("&", parts);
    }
}