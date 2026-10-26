using System.Security.Claims;
using DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Security.Dtos;
using Security.Services;

namespace Security.Controllers;

[ApiController]
public class LoginController(AppDbContext db, Argon2PasswordHasher hasher, TokenService tokens) : ControllerBase
{
    [HttpPost("/login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var name = request.Username.ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedUsername == name);
        var valid = hasher.Verify(request.Password, user?.PasswordHash ?? hasher.DummyHash);
        if (user == null || !valid) return Unauthorized("Forkert brugernavn eller adgangskode.");
        return Ok(new { token = tokens.Create(user), user });
    }

    [Authorize, HttpGet("/api/auth/me")]
    public async Task<IActionResult> Me()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(id);
        return Ok(user);
    }
}
