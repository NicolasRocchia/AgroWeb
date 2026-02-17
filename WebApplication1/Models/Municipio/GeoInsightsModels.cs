namespace WebApplication1.Models.Municipio
{
    public class GeoInsightsViewModel
    {
        public string JsonData { get; set; } = "{}";
    }

    // Mirror DTOs para deserializar la respuesta del API
    public class GeoInsightsResponse
    {
        public List<GeoApplicationDto> Applications { get; set; } = new();
        public List<GeoSensitivePointDto> SensitivePoints { get; set; } = new();
        public GeoInsightsKpis Kpis { get; set; } = new();
        public List<GeoAlertDto> Alerts { get; set; } = new();
        public GeoFiltersAvailable AvailableFilters { get; set; } = new();
    }

    public class GeoApplicationDto
    {
        public long RecipeId { get; set; }
        public long RfdNumber { get; set; }
        public string Status { get; set; } = null!;
        public DateTime IssueDate { get; set; }
        public long LotId { get; set; }
        public string LotName { get; set; } = null!;
        public string? Locality { get; set; }
        public string? Department { get; set; }
        public decimal? SurfaceHa { get; set; }
        public List<GeoVertexDto> Vertices { get; set; } = new();
        public decimal CenterLat { get; set; }
        public decimal CenterLng { get; set; }
        public List<GeoProductDto> Products { get; set; } = new();
        public string? Crop { get; set; }
        public string? AdvisorName { get; set; }
        public string? RequesterName { get; set; }
        public string? MaxToxClass { get; set; }
        public int ToxScore { get; set; }
    }

    public class GeoVertexDto
    {
        public decimal Lat { get; set; }
        public decimal Lng { get; set; }
    }

    public class GeoProductDto
    {
        public string ProductName { get; set; } = null!;
        public string? ToxicologicalClass { get; set; }
        public string? SenasaRegistry { get; set; }
    }

    public class GeoSensitivePointDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Type { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string? Locality { get; set; }
        public int NearbyApplicationsCount { get; set; }
        public int NearbyHighToxCount { get; set; }
    }

    public class GeoInsightsKpis
    {
        public int TotalApplications { get; set; }
        public decimal TotalHectares { get; set; }
        public int UniqueProducts { get; set; }
        public int HighToxApplications { get; set; }
        public int SensitivePointsAtRisk { get; set; }
        public int UniqueAdvisors { get; set; }
    }

    public class GeoAlertDto
    {
        public string Level { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }

    public class GeoFiltersAvailable
    {
        public List<string> Crops { get; set; } = new();
        public List<string> ToxClasses { get; set; } = new();
        public List<string> Products { get; set; } = new();
        public List<string> Advisors { get; set; } = new();
        public DateTime? EarliestDate { get; set; }
        public DateTime? LatestDate { get; set; }
    }
}
