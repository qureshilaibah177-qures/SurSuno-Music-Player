namespace SurSuno.Api.Models;

public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = "Admin";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
