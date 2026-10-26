using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DataAccess.Entities;

[Table("Users")]
public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    [JsonIgnore]
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User";
}
