using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DataAccess;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Security.Services;

namespace Security.Pages;

public class LoginModel(AppDbContext db, Argon2PasswordHasher hasher) : PageModel
{
    [BindProperty, Required, StringLength(50)]
    public string Username { get; set; } = "";
    [BindProperty, Required, StringLength(128)]
    public string Password { get; set; } = "";

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? RedirectToPage("Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var normalized = Username.ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedUsername == normalized);
        var validPassword = hasher.Verify(Password, user?.PasswordHash ?? hasher.DummyHash);
        if (user == null || !validPassword)
        {
            ModelState.AddModelError("", "Forkert brugernavn eller adgangskode.");
            return Page();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("version", user.SecurityVersion.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return RedirectToPage("Index");
    }
}
