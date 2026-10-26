using System.ComponentModel.DataAnnotations;
using DataAccess.Entities;

namespace Security.Dtos;

public class LoginRequest
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = "";
    [Required, StringLength(128)]
    public string Password { get; set; } = "";
}

public class CreateUserRequest
{
    [Required, RegularExpression(@"^[a-zA-Z0-9._-]{3,50}$",
        ErrorMessage = "Brugernavnet skal være 3-50 tegn: bogstaver, tal, punktum, bindestreg eller underscore.")]
    public string Username { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = "";
    [Required, RegularExpression("^(User|SuperUser|Admin)$")]
    public string Role { get; set; } = "User";
}

public class UpdateUserRequest
{
    [Required, RegularExpression(@"^[a-zA-Z0-9._-]{3,50}$")]
    public string Username { get; set; } = "";
    // Null means keep the current hash. Empty or short passwords are rejected.
    [StringLength(128, MinimumLength = 12)]
    public string? Password { get; set; }
    [Required, RegularExpression("^(User|SuperUser|Admin)$")]
    public string Role { get; set; } = "User";
}

public record UserResponse(int Id, string Username, string Role, DateTime CreatedAt)
{
    public static UserResponse From(User user) => new(user.Id, user.Username, user.Role, user.CreatedAt);
}
