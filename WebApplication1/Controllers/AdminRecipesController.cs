using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WebApplication1.Models.Recipes;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[Authorize(Roles = "Admin")]
public class AdminRecipesController : Controller
{
    private readonly AgroApiClient _api;
    public AdminRecipesController(AgroApiClient api) => _api = api;

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
            ViewBag.Error = $"No se pudo obtener el detalle de la receta. {result.Error}";
            return View(new RecipeDetailViewModel());
        }

        return View(new RecipeDetailViewModel { Recipe = result.Data! });
    }
}