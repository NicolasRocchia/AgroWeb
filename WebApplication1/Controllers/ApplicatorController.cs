using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplication1.Models.Recipes;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[Authorize(Roles = "Aplicador")]
public class ApplicatorController : Controller
{
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
                try
                {
                    var doc = JsonDocument.Parse(result.Data!);
                    var recipeId = doc.RootElement.TryGetProperty("recipeId", out var rid) ? rid.GetInt64() : 0;
                    var rfdNumber = doc.RootElement.TryGetProperty("rfdNumber", out var rfd) ? rfd.GetInt64() : 0;
                    TempData["Success"] = $"Receta RFD #{rfdNumber} importada exitosamente (ID: {recipeId}).";
                }
                catch
                {
                    TempData["Success"] = "Receta importada exitosamente.";
                }
                return RedirectToAction("Upload");
            }
        }
        else
        {
            ViewBag.Error = result.Error;
        }

        return View();
    }
}