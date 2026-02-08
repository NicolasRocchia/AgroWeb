namespace WebApplication1.Models.Recipes;

public class RecipesIndexViewModel
{
    public PagedResponse<RecipeListItemDto> Data { get; set; } = new();
    public string? Status { get; set; }
    public string? SearchText { get; set; }
    public long? RfdNumber { get; set; }
}
