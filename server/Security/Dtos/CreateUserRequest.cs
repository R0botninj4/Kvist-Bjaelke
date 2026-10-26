using System.ComponentModel.DataAnnotations;

namespace Security.Dtos;

public class CreateUserRequest
{
    [Required, RegularExpression(@"^[a-zA-Z0-9._-]{3,50}$")]
    public string Username { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = "";
    [Required, RegularExpression("^(User|SuperUser|Admin)$")]
    public string Role { get; set; } = "User";
}
