using System.Text.Json.Serialization;

namespace DataAccess.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string NormalizedUsername { get; set; } = "";
    [JsonIgnore] public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User";
    // Øges ved ændringer, så gamle login-cookies bliver ugyldige.
    public int SecurityVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
