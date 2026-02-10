namespace WebApplication1.Models.Users
{
    public class RoleDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public short AccessLevel { get; set; }
        public string? Description { get; set; }
    }
}
