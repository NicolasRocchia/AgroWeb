namespace WebApplication1.Models.Admin;

public class InsightsDto
{
    public int TotalRecipes { get; set; }
    public int TotalUsers { get; set; }
    public int TotalProducts { get; set; }
    public int RecipesLastMonth { get; set; }

    public List<MonthlyCountDto> RecipesByMonth { get; set; } = new();
    public List<NameCountDto> RecipesByStatus { get; set; } = new();
    public List<NameCountDto> TopProducts { get; set; } = new();
    public List<NameCountDto> ByToxicologicalClass { get; set; } = new();
    public List<NameCountDto> TopRequesters { get; set; } = new();
    public List<NameCountDto> TopAdvisors { get; set; } = new();
}

public class MonthlyCountDto
{
    public string Month { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class NameCountDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
