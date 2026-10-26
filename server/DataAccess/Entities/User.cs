using System.Text.Json.Serialization;

namespace DataAccess.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string NormalizedUsername { get; set; } = "";
    [JsonIgnore] public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User";
    public int SecurityVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class LoginSession
{
    public string Id { get; set; } = "";
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}
