using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[Authorize]
public class ExecutionsController : Controller
{
    private readonly AgroApiClient _api;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ExecutionsController(AgroApiClient api) => _api = api;

    // =============================================
    // APLICADOR: Mis ejecuciones
    // =============================================

    [HttpGet]
    [Authorize(Roles = "Aplicador")]
    public async Task<IActionResult> MyExecutions(string? status)
    {
        var result = await _api.GetMyExecutionsAsync(status);
        ViewBag.ExecutionsJson = result.Success ? result.Data : "[]";
        ViewBag.StatusFilter = status;
        if (!result.Success) ViewBag.Error = result.Error;
        return View();
    }

    // =============================================
    // OPERARIO: Mis ejecuciones asignadas
    // =============================================

    [HttpGet]
    [Authorize(Roles = "Operario")]
    public async Task<IActionResult> MyOperatorExecutions(string? status)
    {
        var result = await _api.GetMyOperatorExecutionsAsync(status);
        ViewBag.ExecutionsJson = result.Success ? result.Data : "[]";
        ViewBag.StatusFilter = status;
        if (!result.Success) ViewBag.Error = result.Error;
        return View("MyExecutions"); // Reutiliza la misma vista
    }

    // =============================================
    // PRODUCTOR: Mis asignaciones
    // =============================================

    [HttpGet]
    [Authorize(Roles = "Productor")]
    public async Task<IActionResult> MyAssignments(string? status)
    {
        var result = await _api.GetMyAssignmentsAsync(status);
        ViewBag.ExecutionsJson = result.Success ? result.Data : "[]";
        ViewBag.StatusFilter = status;
        if (!result.Success) ViewBag.Error = result.Error;
        return View();
    }

    // =============================================
    // DETALLE DE EJECUCIÓN (ambos roles)
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Detail(long id)
    {
        var result = await _api.GetExecutionDetailAsync(id);
        if (!result.Success)
        {
            TempData["Error"] = result.Error ?? "No se pudo obtener el detalle.";
            return RedirectToAction("MyExecutions");
        }
        ViewBag.ExecutionJson = result.Data;
        ViewBag.IsApplicator = User.IsInRole("Aplicador");
        ViewBag.IsProducer = User.IsInRole("Productor");
        ViewBag.IsOperator = User.IsInRole("Operario");

        // Cargar operarios para dropdown de asignación (solo para Aplicador)
        if (User.IsInRole("Aplicador"))
        {
            var opsResult = await _api.GetMyOperatorsAsync();
            ViewBag.OperatorsJson = opsResult.Success ? opsResult.Data : "[]";
        }
        else
        {
            ViewBag.OperatorsJson = "[]";
        }

        return View();
    }

    // =============================================
    // PRODUCTOR: Asignación directa
    // =============================================

    [HttpPost]
    [Authorize(Roles = "Productor")]
    public async Task<IActionResult> CreateDirect(long recipeId, long applicatorProfileId)
    {
        var result = await _api.CreateDirectExecutionAsync(new { recipeId, applicatorProfileId });
        if (result.Success)
        {
            // Extraer ID de la ejecución creada
            try
            {
                var doc = JsonDocument.Parse(result.Data!);
                var execId = doc.RootElement.GetProperty("id").GetInt64();
                TempData["Success"] = "Aplicador asignado correctamente.";
                return RedirectToAction("Detail", new { id = execId });
            }
            catch
            {
                TempData["Success"] = "Aplicador asignado correctamente.";
                return RedirectToAction("MyAssignments");
            }
        }
        TempData["Error"] = result.Error ?? "Error al asignar aplicador.";
        return RedirectToAction("Index", "Applicator");
    }

    // =============================================
    // APLICADOR: Transiciones de estado
    // =============================================

    [HttpPost]
    [Authorize(Roles = "Aplicador,Operario")]
    public async Task<IActionResult> Transition(long id, string action, string? gpsLat, string? gpsLng,
        string? pauseReason, string? notes)
    {
        // GPS viene del JS con punto decimal — parsear con InvariantCulture
        decimal? lat = null, lng = null;
        if (!string.IsNullOrEmpty(gpsLat) && decimal.TryParse(gpsLat, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedLat))
            lat = parsedLat;
        if (!string.IsNullOrEmpty(gpsLng) && decimal.TryParse(gpsLng, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedLng))
            lng = parsedLng;

        var body = new { gpsLat = lat, gpsLng = lng, pauseReason, notes };
        var result = await _api.ExecutionTransitionAsync(id, action, body);

        if (!result.Success)
            TempData["Error"] = result.Error ?? "Error al actualizar el estado.";

        return RedirectToAction("Detail", new { id });
    }

    // =============================================
    // APLICADOR: Checklist pre-aplicación
    // =============================================

    [HttpPost]
    [Authorize(Roles = "Aplicador,Operario")]
    public async Task<IActionResult> SubmitChecklist(long id, bool equipmentCalibrated, bool ppeEquipped,
        bool mixturePrepared, bool exclusionZonesVerified, bool windConditionsOk, string? customNotes)
    {
        var body = new
        {
            equipmentCalibrated,
            ppeEquipped,
            mixturePrepared,
            exclusionZonesVerified,
            windConditionsOk,
            customNotes
        };
        var result = await _api.SubmitExecutionChecklistAsync(id, body);

        if (result.Success)
            TempData["Success"] = "Checklist completado.";
        else
            TempData["Error"] = result.Error ?? "Error al guardar el checklist.";

        return RedirectToAction("Detail", new { id });
    }

    // =============================================
    // APLICADOR: Asignar operario a ejecución
    // =============================================

    [HttpPost]
    [Authorize(Roles = "Aplicador")]
    public async Task<IActionResult> AssignOperator(long id, long operatorProfileId)
    {
        var result = await _api.AssignOperatorToExecutionAsync(id, new { operatorProfileId });
        if (result.Success)
            TempData["Success"] = "Operario asignado correctamente.";
        else
            TempData["Error"] = result.Error ?? "Error al asignar operario.";
        return RedirectToAction("Detail", new { id });
    }

    // =============================================
    // PRODUCTOR: Calificar aplicador
    // =============================================

    [HttpPost]
    [Authorize(Roles = "Productor")]
    public async Task<IActionResult> Review(long id, int rating, string? comment)
    {
        var result = await _api.CreateExecutionReviewAsync(id, new { rating, comment });

        if (result.Success)
            TempData["Success"] = "Calificación enviada.";
        else
            TempData["Error"] = result.Error ?? "Error al enviar la calificación.";

        return RedirectToAction("Detail", new { id });
    }
}
