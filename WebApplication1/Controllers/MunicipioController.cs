using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebApplication1.Models.Recipes;
using WebApplication1.Models.Municipio;

namespace WebApplication1.Controllers;

[Authorize(Roles = "Municipio")]
public class MunicipioController : Controller
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // =============================================
    // DASHBOARD GEOESPACIAL DE FISCALIZACIÓN
    // =============================================

    [HttpGet]
    public async Task<IActionResult> GeoInsights(
        [FromServices] IHttpClientFactory httpClientFactory,
        string? dateFrom = null,
        string? dateTo = null,
        string? crop = null,
        string? toxClass = null,
        string? productName = null,
        string? advisorName = null)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(dateFrom)) qs.Add($"DateFrom={dateFrom}");
        if (!string.IsNullOrWhiteSpace(dateTo)) qs.Add($"DateTo={dateTo}");
        if (!string.IsNullOrWhiteSpace(crop)) qs.Add($"Crop={System.Text.Encodings.Web.UrlEncoder.Default.Encode(crop)}");
        if (!string.IsNullOrWhiteSpace(toxClass)) qs.Add($"ToxClass={System.Text.Encodings.Web.UrlEncoder.Default.Encode(toxClass)}");
        if (!string.IsNullOrWhiteSpace(productName)) qs.Add($"ProductName={System.Text.Encodings.Web.UrlEncoder.Default.Encode(productName)}");
        if (!string.IsNullOrWhiteSpace(advisorName)) qs.Add($"AdvisorName={System.Text.Encodings.Web.UrlEncoder.Default.Encode(advisorName)}");

        var url = "/api/recipes/geo-insights" + (qs.Any() ? "?" + string.Join("&", qs) : "");

        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            ViewBag.Error = $"No se pudieron obtener datos geoespaciales. HTTP {(int)resp.StatusCode}";
            return View(new GeoInsightsViewModel());
        }

        var jsonData = await resp.Content.ReadAsStringAsync();

        return View(new GeoInsightsViewModel
        {
            JsonData = jsonData
        });
    }

    // =============================================
    // RECETAS ASIGNADAS A MI MUNICIPIO
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromServices] IHttpClientFactory httpClientFactory,
        int page = 1,
        int pageSize = 20,
        string? status = null,
        string? searchText = null)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        var qs = new List<string>
        {
            $"Page={page}",
            $"PageSize={pageSize}",
        };

        if (!string.IsNullOrWhiteSpace(status))
            qs.Add($"Status={System.Text.Encodings.Web.UrlEncoder.Default.Encode(status)}");

        if (!string.IsNullOrWhiteSpace(searchText))
            qs.Add($"SearchText={System.Text.Encodings.Web.UrlEncoder.Default.Encode(searchText)}");

        var url = "/api/recipes?" + string.Join("&", qs);

        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
        {
            ViewBag.Error = $"No se pudo obtener el listado de recetas. HTTP {(int)resp.StatusCode}";
            return View(new RecipesIndexViewModel());
        }

        var data = await resp.Content.ReadFromJsonAsync<PagedResponse<RecipeListItemDto>>(JsonOpts)
                   ?? new PagedResponse<RecipeListItemDto>();

        // Filtrar solo las asignadas a mi municipio (el filtrado real debería hacerse en la API,
        // pero por ahora filtramos client-side las que tienen municipio asignado)
        return View(new RecipesIndexViewModel
        {
            Data = data,
            Status = status,
            SearchText = searchText
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
            ViewBag.Error = $"No se pudo obtener el detalle. HTTP {(int)resp.StatusCode}";
            return View(new RecipeDetailViewModel());
        }

        var data = await resp.Content.ReadFromJsonAsync<RecipeDetailDto>(JsonOpts);
        if (data == null)
        {
            ViewBag.Error = "No se pudo procesar la respuesta.";
            return View(new RecipeDetailViewModel());
        }

        return View(new RecipeDetailViewModel { Recipe = data });
    }

    // =============================================
    // REVISAR RECETA (Aprobar / Rechazar / Observar)
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(
        long id,
        string action,
        string? observation,
        long? targetMunicipalityId,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        var payload = new
        {
            action,
            observation,
            targetMunicipalityId
        };

        var resp = await client.PostAsJsonAsync($"/api/recipes/{id}/review", payload);

        if (resp.IsSuccessStatusCode)
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
            try
            {
                var body = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);
                TempData["Error"] = doc.RootElement.TryGetProperty("error", out var err)
                    ? err.GetString()
                    : $"Error al revisar la receta. (HTTP {(int)resp.StatusCode})";
            }
            catch
            {
                TempData["Error"] = $"Error al revisar la receta. (HTTP {(int)resp.StatusCode})";
            }
        }

        return RedirectToAction("Details", new { id });
    }

    // =============================================
    // ENVIAR MENSAJE
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(
        long id,
        string message,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("AgroApi");

        var payload = new { message };
        var resp = await client.PostAsJsonAsync($"/api/recipes/{id}/messages", payload);

        if (resp.IsSuccessStatusCode)
        {
            TempData["Success"] = "Mensaje enviado.";
        }
        else
        {
            TempData["Error"] = "No se pudo enviar el mensaje.";
        }

        return RedirectToAction("Details", new { id });
    }
}
