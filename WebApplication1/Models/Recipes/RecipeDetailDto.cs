namespace WebApplication1.Models.Recipes;

public class RecipeDetailDto
{
    public long Id { get; set; }
    public long RfdNumber { get; set; }
    public string Status { get; set; } = string.Empty;

    // Fechas
    public DateTime IssueDate { get; set; }
    public DateTime? PossibleStartDate { get; set; }
    public DateTime? RecommendedDate { get; set; }
    public DateTime? ExpirationDate { get; set; }

    // Actores
    public RequesterDto Requester { get; set; } = new();
    public AdvisorDto Advisor { get; set; } = new();

    // Información de cultivo
    public string? ApplicationType { get; set; }
    public string? Crop { get; set; }
    public string? Diagnosis { get; set; }
    public string? Treatment { get; set; }
    public string? MachineToUse { get; set; }
    public decimal? UnitSurfaceHa { get; set; }

    // Condiciones ambientales
    public decimal? TempMin { get; set; }
    public decimal? TempMax { get; set; }
    public decimal? HumidityMin { get; set; }
    public decimal? HumidityMax { get; set; }
    public decimal? WindMinKmh { get; set; }
    public decimal? WindMaxKmh { get; set; }
    public string? WindDirection { get; set; }

    // Información de máquina
    public string? MachinePlate { get; set; }
    public string? MachineLegalName { get; set; }
    public string? MachineType { get; set; }

    public string? Notes { get; set; }

    // Relaciones
    public List<RecipeProductDto> Products { get; set; } = new();
    public List<RecipeLotDto> Lots { get; set; } = new();
    public List<RecipeSensitivePointDto> SensitivePoints { get; set; } = new();

    // Auditoría
    public DateTime CreatedAt { get; set; }
    public long? CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedByUserId { get; set; }
    public string? UpdatedByUserName { get; set; }
}

public class RequesterDto
{
    public long Id { get; set; }
    public string LegalName { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Contact { get; set; }
}

public class AdvisorDto
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string? Contact { get; set; }
}

public class RecipeProductDto
{
    public long Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? SenasaRegistry { get; set; }
    public string? ToxicologicalClass { get; set; }
    public string? ProductType { get; set; }

    public decimal? DoseValue { get; set; }
    public string? DoseUnit { get; set; }
    public string? DosePerUnit { get; set; }

    public decimal? TotalValue { get; set; }
    public string? TotalUnit { get; set; }
}

public class RecipeLotDto
{
    public long Id { get; set; }
    public string LotName { get; set; } = string.Empty;
    public string? Locality { get; set; }
    public string? Department { get; set; }
    public decimal? SurfaceHa { get; set; }

    public List<RecipeLotVertexDto> Vertices { get; set; } = new();
}

public class RecipeLotVertexDto
{
    public long Id { get; set; }
    public int Order { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}

public class RecipeSensitivePointDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? Locality { get; set; }
    public string? Department { get; set; }
}