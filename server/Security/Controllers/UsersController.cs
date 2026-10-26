using System.Security.Claims;
using DataAccess;
using DataAccess.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Security.Dtos;
using Security.Services;

namespace Security.Controllers;

[ApiController, Authorize, Route("api/[controller]")]
public class UsersController(AppDbContext db) : ControllerBase
{
    private bool CanManage(string role) => User.IsInRole("Admin") || (User.IsInRole("SuperUser") && role == "User");
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<List<User>> GetAll() => await db.Users.AsNoTracking()
        .Where(u => User.IsInRole("Admin") || u.Role == "User").OrderBy(u => u.Username).ToListAsync();

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (!User.IsInRole("Admin") && user.Role != "User") return Forbid();
        return Ok(user);
    }

    [Authorize(Roles = "SuperUser,Admin"), HttpPost]
    public async Task<IActionResult> Create(UserRequest request)
    {
        if (!CanManage(request.Role)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest("Angiv en adgangskode.");
        var user = new User
        {
            Username = request.Username, NormalizedUsername = request.Username.ToUpperInvariant(),
            Role = request.Role, PasswordHash = Argon2PasswordHasher.Hash(request.Password)
        };
        db.Users.Add(user);
        if (!await Save()) return Conflict("Brugernavnet er allerede i brug.");
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [Authorize(Roles = "SuperUser,Admin"), HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UserRequest request)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (!CanManage(user.Role) || !CanManage(request.Role)) return Forbid();
        if (id == CurrentUserId && request.Role != user.Role) return BadRequest("Du kan ikke ændre din egen rolle.");
        user.Username = request.Username;
        user.NormalizedUsername = request.Username.ToUpperInvariant();
        user.Role = request.Role;
        if (request.Password != null) user.PasswordHash = Argon2PasswordHasher.Hash(request.Password);
        user.SecurityVersion++;
        if (!await Save()) return Conflict("Brugernavnet er allerede i brug.");
        return Ok(user);
    }

    [Authorize(Roles = "SuperUser,Admin"), HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (!CanManage(user.Role)) return Forbid();
        if (id == CurrentUserId) return BadRequest("Du kan ikke slette din egen konto.");
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> Save()
    {
        try { await db.SaveChangesAsync(); return true; }
        catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteErrorCode: 19 }) { return false; }
    }
}
