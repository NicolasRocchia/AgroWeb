using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using WebApplication1.Models.Recipes;

namespace WebApplication1.Controllers;

[Authorize(Roles = "Aplicador")]
public class RecipeViewController : Controller
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // =============================================
    // MIS RECETAS (listado)
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromServices] IHttpClientFactory httpClientFactory,
        int page = 1,
        int pageSize = 20,
        string? status = null,
        string? searchText = null,
        long? rfdNumber = null)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        var qs = new List<string>
        {
            $"Page={page}",
            $"PageSize={pageSize}",
        };

        if (!string.IsNullOrWhiteSpace(status))
            qs.Add($"Status={UrlEncoder.Default.Encode(status)}");

        if (!string.IsNullOrWhiteSpace(searchText))
            qs.Add($"SearchText={UrlEncoder.Default.Encode(searchText)}");

        if (rfdNumber.HasValue)
            qs.Add($"RfdNumber={rfdNumber.Value}");

        var url = "/api/recipes?" + string.Join("&", qs);

        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
        {
            ViewBag.Error = $"No se pudo obtener el listado de recetas. HTTP {(int)resp.StatusCode}";
            return View(new RecipesIndexViewModel());
        }

        var data = await resp.Content.ReadFromJsonAsync<PagedResponse<RecipeListItemDto>>(JsonOpts)
                   ?? new PagedResponse<RecipeListItemDto>();

        return View(new RecipesIndexViewModel
        {
            Data = data,
            Status = status,
            SearchText = searchText,
            RfdNumber = rfdNumber
        });
    }

    // =============================================
    // DETALLE DE RECETA
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Details(
        [FromServices] IHttpClientFactory httpClientFactory,
        long id)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        var resp = await client.GetAsync($"/api/recipes/{id}");

        if (!resp.IsSuccessStatusCode)
        {
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["Error"] = "La receta solicitada no existe.";
                return RedirectToAction("Index");
            }

            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                TempData["Error"] = "No tenés permisos para ver esta receta.";
                return RedirectToAction("Index");
            }

            ViewBag.Error = $"No se pudo obtener el detalle de la receta. HTTP {(int)resp.StatusCode}";
            return View(new RecipeDetailViewModel());
        }

        var data = await resp.Content.ReadFromJsonAsync<RecipeDetailDto>(JsonOpts);
        if (data == null)
        {
            ViewBag.Error = "No se pudo procesar la respuesta del servidor.";
            return View(new RecipeDetailViewModel());
        }

        return View(new RecipeDetailViewModel { Recipe = data });
    }

    // =============================================
    // SUBIR PDF
    // =============================================
    [HttpGet]
    public IActionResult Upload()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload(
        IFormFile pdf,
        bool dryRun,
        [FromServices] IHttpClientFactory httpClientFactory)
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

        var client = httpClientFactory.CreateClient("AgroApi");

        using var content = new MultipartFormDataContent();

        // Archivo PDF
        var streamContent = new StreamContent(pdf.OpenReadStream());
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(pdf.ContentType);
        content.Add(streamContent, "Pdf", pdf.FileName);

        // DryRun flag
        content.Add(new StringContent(dryRun.ToString().ToLower()), "DryRun");

        var resp = await client.PostAsync("/api/recipes/import-pdf", content);

        if (resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);

            if (dryRun)
            {
                ViewBag.Success = "Vista previa generada correctamente. Revisá los datos y volvé a subir sin 'Solo vista previa' para confirmar.";
                ViewBag.Preview = body;
            }
            else
            {
                var recipeId = doc.RootElement.TryGetProperty("recipeId", out var rid) ? rid.GetInt64() : 0;
                var rfdNumber = doc.RootElement.TryGetProperty("rfdNumber", out var rfd) ? rfd.GetInt64() : 0;

                TempData["Success"] = $"Receta RFD #{rfdNumber} importada exitosamente (ID: {recipeId}).";
                return RedirectToAction("Upload");
            }
        }
        else
        {
            try
            {
                var body = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);

                if (doc.RootElement.TryGetProperty("error", out var errorProp))
                {
                    ViewBag.Error = errorProp.GetString();
                }
                else
                {
                    ViewBag.Error = $"Error al importar el PDF. (HTTP {(int)resp.StatusCode})";
                }
            }
            catch
            {
                ViewBag.Error = $"Error al importar el PDF. (HTTP {(int)resp.StatusCode})";
            }
        }

        return View();
    }
}
