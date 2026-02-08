using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Text.Json;
using WebApplication1.Models.Recipes;

namespace WebApplication1.Controllers;

[Authorize(Roles = "Admin")]
public class RecipesController : Controller
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

        // Construir querystring de forma segura
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

        var url = "/api/recipes" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");

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
}
