using System.ComponentModel.DataAnnotations;

namespace Security.Dtos;

public class TryLogin
{
    [Required, StringLength(50)]
    public string Username { get; set; } = "";
    [Required, StringLength(128)]
    public string Password { get; set; } = "";
}
