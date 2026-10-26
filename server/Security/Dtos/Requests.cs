using System.ComponentModel.DataAnnotations;

namespace Security.Dtos;

public class LoginRequest
{
    [Required, StringLength(50)] public string Username { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
}

public class UserRequest
{
    [Required, RegularExpression(@"^[a-zA-Z0-9._-]{3,50}$")]
    public string Username { get; set; } = "";
    [StringLength(128, MinimumLength = 12)]
    public string? Password { get; set; }
    [Required, RegularExpression("^(User|SuperUser|Admin)$")]
    public string Role { get; set; } = "User";
}
