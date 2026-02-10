namespace WebApplication1.Models.Users
{
    public class UsersIndexViewModel
    {
        public List<UserListItemDto> Users { get; set; } = new();
        public List<RoleDto> Roles { get; set; } = new();
    }
}
