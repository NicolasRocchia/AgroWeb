using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Json;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[Authorize]
public class JobsController : Controller
{
    private readonly AgroApiClient _api;

    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JobsController(AgroApiClient api) => _api = api;

    // =============================================
    // PRODUCTOR: Mis trabajos publicados
    // =============================================

    [HttpGet]
    [Authorize(Roles = "Productor")]
    public async Task<IActionResult> MyJobs()
    {
        var result = await _api.GetMyJobsAsync();
        ViewBag.JobsJson = result.Success ? result.Data : "[]";
        if (!result.Success) ViewBag.Error = result.Error;
        return View();
    }

    // =============================================
    // PRODUCTOR: Crear trabajo
    // =============================================

    [HttpGet]
    [Authorize(Roles = "Productor")]
    public async Task<IActionResult> Create()
    {
        // Cargar recetas del productor
        var recipesResult = await _api.GetRecipesAsync(page: 1, pageSize: 100);

        // Cargar jobs activos para filtrar recetas que ya tienen trabajo
        var jobsResult = await _api.GetMyJobsAsync();
        var usedRecipeIds = new HashSet<long>();
        if (jobsResult.Success && !string.IsNullOrEmpty(jobsResult.Data))
        {
            try
            {
                var jobs = JsonDocument.Parse(jobsResult.Data);
                foreach (var j in jobs.RootElement.EnumerateArray())
                {
                    var status = j.GetProperty("status").GetString() ?? "";
                    if (status != "COMPLETADO" && status != "CANCELADO" &&
                        j.TryGetProperty("recipeId", out var rid) && rid.ValueKind == JsonValueKind.Number)
                    {
                        usedRecipeIds.Add(rid.GetInt64());
                    }
                }
            }
            catch { /* ignore parse errors */ }
        }

        ViewBag.RecipesJson = recipesResult.Success && recipesResult.Data?.Items != null
            ? JsonSerializer.Serialize(recipesResult.Data.Items
                .Where(r => !usedRecipeIds.Contains(r.Id))
                .Select(r => (object)new
                {
                    r.Id,
                    r.RfdNumber,
                    r.PublicCode,
                    r.Crop,
                    r.ApplicationType,
                    r.UnitSurfaceHa,
                    r.Status
                }).ToList(), CamelCase)
            : "[]";

        // Cargar lotes del productor
        var lotsResult = await _api.GetMyLotsAsync();
        ViewBag.LotsJson = lotsResult.Success ? lotsResult.Data : "[]";

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Productor")]
    public async Task<IActionResult> Create(
        string title, string? description, string? crop,
        string? applicationType, decimal? surfaceHa,
        string locationLat, string locationLng,
        string? locality, string? department,
        long? recipeId,
        DateTime? dateFrom, DateTime? dateTo)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            ViewBag.Error = "El título es obligatorio.";
            return View();
        }

        if (string.IsNullOrWhiteSpace(locationLat) || string.IsNullOrWhiteSpace(locationLng))
        {
            ViewBag.Error = "Seleccioná una ubicación en el mapa.";
            return View();
        }

        if (!decimal.TryParse(locationLat.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) ||
            !decimal.TryParse(locationLng.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var lng))
        {
            ViewBag.Error = "Coordenadas inválidas.";
            return View();
        }

        var result = await _api.CreateJobPostingAsync(new
        {
            recipeId,
            title = title.Trim(),
            description = description?.Trim(),
            crop = crop?.Trim(),
            applicationType = applicationType?.Trim(),
            surfaceHa,
            latitude = lat,
            longitude = lng,
            locality = locality?.Trim(),
            department = department?.Trim(),
            dateFrom,
            dateTo
        });

        if (result.Success)
        {
            TempData["Success"] = "Trabajo publicado correctamente.";
            return RedirectToAction("MyJobs");
        }

        ViewBag.Error = result.Error;
        ViewBag.Title = title;
        ViewBag.Description = description;
        ViewBag.Crop = crop;
        ViewBag.ApplicationType = applicationType;
        ViewBag.SurfaceHa = surfaceHa;
        ViewBag.Locality = locality;
        ViewBag.Department = department;
        return View();
    }

    // =============================================
    // PROXY: Detalle de receta (JSON, para AJAX)
    // =============================================

    [HttpGet]
    [Authorize(Roles = "Productor")]
    public async Task<IActionResult> RecipeDetail(long id)
    {
        var result = await _api.GetRecipeAsync(id);

        if (!result.Success)
            return NotFound(new { error = "Receta no encontrada." });

        var r = result.Data!;
        var firstLot = r.Lots?.FirstOrDefault();

        return Json(new
        {
            r.Id,
            r.Crop,
            r.ApplicationType,
            r.UnitSurfaceHa,
            lot = firstLot != null ? new
            {
                firstLot.LotName,
                firstLot.Locality,
                firstLot.Department,
                firstLot.SurfaceHa,
                vertices = firstLot.Vertices?.Select(v => new
                {
                    v.Order,
                    v.Latitude,
                    v.Longitude
                }).OrderBy(v => v.Order).ToList()
            } : null
        }, CamelCase);
    }

    // =============================================
    // DETALLE DEL TRABAJO (ambos roles)
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Detail(long id)
    {
        var result = await _api.GetJobDetailAsync(id);

        if (!result.Success)
        {
            TempData["Error"] = result.Error ?? "Trabajo no encontrado.";
            return RedirectToAction("MyJobs");
        }

        ViewBag.JobJson = result.Data;
        ViewBag.IsProductor = User.IsInRole("Productor");
        ViewBag.IsAplicador = User.IsInRole("Aplicador");
        return View();
    }

    // =============================================
    // PRODUCTOR: Asignar aplicador
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Productor")]
    public async Task<IActionResult> Assign(long jobId, long applicationId)
    {
        var result = await _api.AssignApplicatorAsync(jobId, applicationId);

        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Aplicador asignado correctamente." : result.Error;

        return RedirectToAction("Detail", new { id = jobId });
    }

    // =============================================
    // PRODUCTOR: Cambiar estado
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Productor")]
    public async Task<IActionResult> ChangeStatus(long jobId, string status)
    {
        var result = await _api.ChangeJobStatusAsync(jobId, status);

        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Estado actualizado." : result.Error;

        return RedirectToAction("Detail", new { id = jobId });
    }

    // =============================================
    // APLICADOR: Mis tareas asignadas
    // =============================================

    [HttpGet]
    [Authorize(Roles = "Aplicador")]
    public async Task<IActionResult> MyTasks()
    {
        var result = await _api.GetMyTasksAsync();
        ViewBag.TasksJson = result.Success ? result.Data : "[]";
        if (!result.Success) ViewBag.Error = result.Error;
        return View();
    }

    // =============================================
    // APLICADOR: Trabajos disponibles
    // =============================================

    [HttpGet]
    [Authorize(Roles = "Aplicador")]
    public async Task<IActionResult> Available()
    {
        var result = await _api.GetAvailableJobsAsync();
        ViewBag.JobsJson = result.Success ? result.Data : "[]";
        if (!result.Success) ViewBag.Error = result.Error;
        return View();
    }

    // =============================================
    // APLICADOR: Postularse
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Aplicador")]
    public async Task<IActionResult> Apply(long jobId, string? message, decimal? proposedPrice)
    {
        var result = await _api.ApplyToJobAsync(jobId, message, proposedPrice);

        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Postulación enviada." : result.Error;

        return RedirectToAction("Detail", new { id = jobId });
    }

    // =============================================
    // APLICADOR: Retirar postulación
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Aplicador")]
    public async Task<IActionResult> Withdraw(long jobId)
    {
        var result = await _api.WithdrawJobApplicationAsync(jobId);

        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Postulación retirada." : result.Error;

        return RedirectToAction("Detail", new { id = jobId });
    }
}