namespace WebApplication1.Models.Users
{
    public class UserListItemDto
    {
        public long Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? TaxId { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsBlocked { get; set; }
        public List<string> Roles { get; set; } = new();
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
