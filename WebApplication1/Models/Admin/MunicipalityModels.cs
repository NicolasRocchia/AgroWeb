namespace WebApplication1.Models.Admin
{
    public class MunicipalitiesViewModel
    {
        public List<MunicipalityAdminDto> Municipalities { get; set; } = new();
        public List<MunicipioUserDto> AvailableUsers { get; set; } = new();
    }

    public class MunicipalityAdminDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Province { get; set; }
        public string? Department { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public long? UserId { get; set; }
        public string? UserName { get; set; }
    }

    public class MunicipioUserDto
    {
        public long Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string EmailNormalized { get; set; } = string.Empty;
    }

    public class CreateMunicipalityViewModel
    {
        public List<MunicipioUserDto> AvailableUsers { get; set; } = new();

        // Preserve values on error
        public string? Name { get; set; }
        public string? Province { get; set; }
        public string? Department { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public long? UserId { get; set; }
    }

    public class EditMunicipalityViewModel
    {
        public MunicipalityAdminDto Municipality { get; set; } = new();
        public List<MunicipioUserDto> AvailableUsers { get; set; } = new();
    }
}
