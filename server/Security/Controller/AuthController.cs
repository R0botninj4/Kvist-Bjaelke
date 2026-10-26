using System.IdentityModel.Tokens.Jwt;
using DataAccess;
using DataAccess.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Security.Dtos;
using Security.Services;

namespace Security.Controller;

[ApiController, Route("api/auth")]
public class AuthController(AppDbContext db, Argon2PasswordHasher hasher, TokenService tokens) : ControllerBase
{
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var normalized = request.Username.ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedUsername == normalized);
        var valid = hasher.Verify(request.Password, user?.PasswordHash ?? hasher.DummyHash);
        if (user is null || !valid)
            return Unauthorized(new { message = "Forkert brugernavn eller adgangskode." });

        var session = new LoginSession
        {
            Id = Guid.NewGuid().ToString("N"), UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        await db.LoginSessions.Where(s => s.ExpiresAt < DateTime.UtcNow).ExecuteDeleteAsync();
        db.LoginSessions.Add(session);
        await db.SaveChangesAsync();
        return Ok(new { token = tokens.Create(user, session), expiresAt = session.ExpiresAt, user = UserResponse.From(user) });
    }

    [Authorize, HttpGet("me")]
    public IActionResult Me() => Ok(UserResponse.From((User)HttpContext.Items["CurrentUser"]!));

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var id = User.FindFirst(JwtRegisteredClaimNames.Jti)!.Value;
        await db.LoginSessions.Where(s => s.Id == id).ExecuteDeleteAsync();
        return NoContent();
    }
}
