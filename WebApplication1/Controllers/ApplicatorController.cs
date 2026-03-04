using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplication1.Models.Recipes;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[Authorize(Roles = "Productor")]
public class ApplicatorController : Controller
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AgroApiClient _api;
    public ApplicatorController(AgroApiClient api) => _api = api;

    // =============================================
    // MIS RECETAS (listado)
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1, int pageSize = 20,
        string? status = null, string? searchText = null, long? rfdNumber = null)
    {
        var result = await _api.GetRecipesAsync(page, pageSize, status, searchText, rfdNumber);

        if (!result.Success)
            ViewBag.Error = $"No se pudo obtener el listado de recetas. {result.Error}";

        return View(new RecipesIndexViewModel
        {
            Data = result.Data ?? new(),
            Status = status,
            SearchText = searchText,
            RfdNumber = rfdNumber
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

        if (result.IsForbidden)
        {
            TempData["Error"] = "No tenés permisos para ver esta receta.";
            return RedirectToAction("Index");
        }

        if (!result.Success)
        {
            ViewBag.Error = $"No se pudo obtener el detalle de la receta. {result.Error}";
            return View(new RecipeDetailViewModel());
        }

        return View(new RecipeDetailViewModel { Recipe = result.Data! });
    }

    // =============================================
    // CAMBIAR ESTADO DE RECETA
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(long id, string status)
    {
        var result = await _api.ChangeRecipeStatusAsync(id, status);

        if (result.Success)
        {
            TempData["Success"] = status.ToUpper() switch
            {
                "CERRADA" => "La receta fue cerrada correctamente.",
                "ANULADA" => "La receta fue anulada correctamente.",
                _ => "Estado actualizado."
            };
        }
        else
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction("Details", new { id });
    }

    // =============================================
    // ENVIAR A MUNICIPIO
    // =============================================

    [HttpGet]
    public async Task<IActionResult> AssignMunicipality(long id)
    {
        var recipeResult = await _api.GetRecipeAsync(id);
        if (!recipeResult.Success)
        {
            TempData["Error"] = "No se pudo obtener la receta.";
            return RedirectToAction("Details", new { id });
        }

        var recipe = recipeResult.Data!;
        var municipalities = new List<MunicipalityDto>();
        var firstLot = recipe.Lots?.FirstOrDefault();
        var hasCoords = firstLot?.Vertices?.Any() == true;

        if (hasCoords)
        {
            var centroidLat = firstLot!.Vertices.Average(v => v.Latitude);
            var centroidLng = firstLot!.Vertices.Average(v => v.Longitude);
            var nearbyResult = await _api.GetNearbyMunicipalitiesAsync(centroidLat, centroidLng);

            if (nearbyResult.Success)
                municipalities = nearbyResult.Data ?? new();
        }

        ViewBag.Recipe = recipe;
        ViewBag.Municipalities = municipalities;
        ViewBag.HasCoords = hasCoords;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignMunicipality(long id, long municipalityId)
    {
        var result = await _api.AssignRecipeToMunicipalityAsync(id, municipalityId);

        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? "Receta enviada al municipio correctamente. ✅" : result.Error;

        return RedirectToAction("Details", new { id });
    }

    // =============================================
    // ENVIAR MENSAJE EN RECETA
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

    // =============================================
    // SUBIR PDF
    // =============================================

    [HttpGet]
    public IActionResult Upload() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload(IFormFile pdf, bool dryRun)
    {
        if (pdf == null || pdf.Length == 0)
        {
            ViewBag.Error = "Seleccioná un archivo PDF.";
            return View();
        }

        if (!pdf.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) &&
            !pdf.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            ViewBag.Error = "El archivo debe ser un PDF.";
            return View();
        }

        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(pdf.OpenReadStream());
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(pdf.ContentType);
        content.Add(streamContent, "Pdf", pdf.FileName);
        content.Add(new StringContent(dryRun.ToString().ToLower()), "DryRun");

        var result = await _api.ImportPdfAsync(content);

        if (result.Success)
        {
            if (dryRun)
            {
                ViewBag.Success = "Vista previa generada correctamente. Revisá los datos y volvé a subir sin 'Solo vista previa' para confirmar.";
                ViewBag.Preview = result.Data;
            }
            else
            {
                long recipeId = 0;
                try
                {
                    var doc = JsonDocument.Parse(result.Data!);
                    recipeId = doc.RootElement.TryGetProperty("recipeId", out var rid) ? rid.GetInt64() : 0;
                    var rfdNumber = doc.RootElement.TryGetProperty("rfdNumber", out var rfd) ? rfd.GetInt64() : 0;
                    TempData["Success"] = $"Receta RFD #{rfdNumber} importada exitosamente (ID: {recipeId}).";
                }
                catch
                {
                    TempData["Success"] = "Receta importada exitosamente.";
                }

                if (recipeId > 0)
                    return RedirectToAction("Details", new { id = recipeId });

                return RedirectToAction("Index");
            }
        }
        else
        {
            ViewBag.Error = result.Error;
        }

        return View();
    }

    // =============================================
    // REGISTRARME COMO APLICADOR (Ofrecer Servicios)
    // =============================================

    [HttpGet]
    public async Task<IActionResult> BecomeApplicator()
    {
        var profileResult = await _api.GetApplicatorProfileAsync();
        if (profileResult.Success && !string.IsNullOrEmpty(profileResult.Data))
        {
            TempData["Success"] = "Ya tenés un perfil de aplicador.";
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BecomeApplicator(
        string businessName, string taxId, string? contactPhone,
        string? contactEmail, string? description,
        string? machineTypes,
        string? locationName, string? locationAddress,
        string? locationLat, string? locationLng)
    {
        if (string.IsNullOrWhiteSpace(businessName) || string.IsNullOrWhiteSpace(taxId))
        {
            ViewBag.Error = "La razón social y el CUIT son obligatorios.";
            return View();
        }

        var machines = (machineTypes ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Trim())
            .Where(m => !string.IsNullOrEmpty(m))
            .ToList();

        var locations = new List<object>();
        if (!string.IsNullOrWhiteSpace(locationName) &&
            !string.IsNullOrWhiteSpace(locationLat) &&
            !string.IsNullOrWhiteSpace(locationLng))
        {
            if (decimal.TryParse(locationLat.Replace(",", "."),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var lat) &&
                decimal.TryParse(locationLng.Replace(",", "."),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var lng))
            {
                locations.Add(new
                {
                    name = locationName.Trim(),
                    address = locationAddress?.Trim(),
                    latitude = lat,
                    longitude = lng,
                    isPrimary = true
                });
            }
        }

        var result = await _api.SaveApplicatorProfileAsync(new
        {
            businessName = businessName.Trim(),
            taxId = taxId.Trim(),
            contactPhone = contactPhone?.Trim(),
            contactEmail = contactEmail?.Trim(),
            description = description?.Trim(),
            machineTypes = machines,
            locations
        });

        if (result.Success)
        {
            TempData["Success"] = "¡Perfil de aplicador creado! Un administrador verificará tu información pronto.";
            return RedirectToAction("Index", "Home");
        }

        ViewBag.Error = result.Error;
        ViewBag.BusinessName = businessName;
        ViewBag.TaxId = taxId;
        ViewBag.ContactPhone = contactPhone;
        ViewBag.ContactEmail = contactEmail;
        ViewBag.Description = description;
        return View();
    }

    // =============================================
    // CREAR RECETA MANUAL (INFORMAL)
    // =============================================

    [HttpGet]
    public IActionResult CreateManual() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateManual(
        string Crop,
        string? ApplicationType,
        string? Diagnosis,
        string? Treatment,
        string? MachineToUse,
        decimal? UnitSurfaceHa,
        string? Notes,
        DateTime? IssueDate,
        string? LotName,
        string? LotLocality,
        string? LotDepartment,
        decimal? LotSurfaceHa,
        long? ExistingLotId)
    {
        if (string.IsNullOrWhiteSpace(Crop))
        {
            ViewBag.Error = "El cultivo es obligatorio.";
            return View();
        }

        try
        {
            // Parse polygon vertices from form
            var lotVertices = new List<object>();
            int v = 0;
            while (Request.Form.ContainsKey($"LotVertices[{v}].Latitude"))
            {
                var latStr = Request.Form[$"LotVertices[{v}].Latitude"].ToString().Replace(",", ".");
                var lngStr = Request.Form[$"LotVertices[{v}].Longitude"].ToString().Replace(",", ".");
                var orderStr = Request.Form[$"LotVertices[{v}].Order"].ToString();

                if (decimal.TryParse(latStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var vLat) &&
                    decimal.TryParse(lngStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var vLng) &&
                    int.TryParse(orderStr, out var vOrder))
                {
                    lotVertices.Add(new { order = vOrder, latitude = vLat, longitude = vLng });
                }
                v++;
            }

            // Recolectar productos del form dinámico
            var products = new List<object>();
            int i = 0;
            while (Request.Form.ContainsKey($"Products[{i}].ProductName"))
            {
                var productName = Request.Form[$"Products[{i}].ProductName"].ToString();
                if (string.IsNullOrWhiteSpace(productName)) { i++; continue; }

                decimal? doseVal = null;
                decimal? totalVal = null;

                if (decimal.TryParse(Request.Form[$"Products[{i}].DoseValue"],
                        NumberStyles.Any, CultureInfo.InvariantCulture, out var dv) && dv > 0)
                    doseVal = dv;

                if (decimal.TryParse(Request.Form[$"Products[{i}].TotalValue"],
                        NumberStyles.Any, CultureInfo.InvariantCulture, out var tv) && tv > 0)
                    totalVal = tv;

                products.Add(new
                {
                    productName,
                    senasaRegistry = Request.Form[$"Products[{i}].SenasaRegistry"].ToString(),
                    toxicologicalClass = Request.Form[$"Products[{i}].ToxicologicalClass"].ToString(),
                    doseValue = doseVal,
                    doseUnit = Request.Form[$"Products[{i}].DoseUnit"].ToString(),
                    totalValue = totalVal,
                    totalUnit = Request.Form[$"Products[{i}].TotalUnit"].ToString()
                });
                i++;
            }

            var payload = new
            {
                crop = Crop.Trim(),
                applicationType = ApplicationType?.Trim(),
                diagnosis = Diagnosis?.Trim(),
                treatment = Treatment?.Trim(),
                machineToUse = MachineToUse?.Trim(),
                unitSurfaceHa = UnitSurfaceHa,
                notes = Notes?.Trim(),
                issueDate = IssueDate,
                existingLotId = ExistingLotId,
                lotName = LotName?.Trim(),
                lotLocality = LotLocality?.Trim(),
                lotDepartment = LotDepartment?.Trim(),
                lotSurfaceHa = LotSurfaceHa,
                lotVertices,
                products
            };

            var result = await _api.CreateManualRecipeAsync(payload);

            if (result.Success)
            {
                TempData["Success"] = "Receta de registro creada correctamente.";
                return RedirectToAction("Index");
            }

            ViewBag.Error = result.Error ?? "Error desconocido al crear la receta.";
            return View();
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"Error al crear receta: {ex.Message}";
            return View();
        }
    }

    // =============================================
    // BÚSQUEDA DE PRODUCTOS (proxy para autocomplete)
    // =============================================

    [HttpGet]
    public async Task<IActionResult> SearchProducts(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Json(Array.Empty<object>());

        var result = await _api.SearchProductsAsync(q);

        if (result.Success && !string.IsNullOrEmpty(result.Data))
        {
            return Content(result.Data, "application/json");
        }

        // Si la API falla, devolver array vacío (no exponer detalles internos)
        return Json(Array.Empty<object>());
    }

    [HttpGet]
    public async Task<IActionResult> MyLots()
    {
        try
        {
            var result = await _api.GetMyLotsAsync();

            if (result.Success && !string.IsNullOrEmpty(result.Data))
            {
                return Content(result.Data, "application/json");
            }

            return Json(Array.Empty<object>());
        }
        catch
        {
            return Json(Array.Empty<object>());
        }
    }
}