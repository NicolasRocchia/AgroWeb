namespace WebApplication1.Models.Users
{
    public class CreateUserViewModel
    {
        public List<RoleDto> Roles { get; set; } = new();

        // Para preservar valores en caso de error
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? TaxId { get; set; }
        public string? PhoneNumber { get; set; }
        public long? RoleId { get; set; }
    }
}
