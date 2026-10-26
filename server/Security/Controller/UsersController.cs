using DataAccess;
using DataAccess.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Security.Dtos;
using Security.Services;

namespace Security.Controller;

[ApiController, Authorize, Route("api/users")]
public class UsersController(AppDbContext db) : ControllerBase
{
    private User Actor => (User)HttpContext.Items["CurrentUser"]!;
    private bool CanManage(string role) => Actor.Role == "Admin" || (Actor.Role == "SuperUser" && role == "User");
    private static bool IsDuplicate(DbUpdateException e) => e.InnerException is SqliteException { SqliteErrorCode: 19 };

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var query = db.Users.AsNoTracking();
        if (Actor.Role != "Admin") query = query.Where(u => u.Role == "User");
        var users = await query.OrderBy(u => u.Username).ToListAsync();
        return Ok(users.Select(UserResponse.From));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var target = await db.Users.FindAsync(id);
        if (target is null) return NotFound();
        if (Actor.Role != "Admin" && target.Role != "User") return Forbid();
        return Ok(UserResponse.From(target));
    }

    [Authorize(Roles = "SuperUser,Admin"), HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        if (!CanManage(request.Role)) return Forbid();
        var user = new User
        {
            Username = request.Username, NormalizedUsername = request.Username.ToUpperInvariant(),
            Role = request.Role, PasswordHash = Argon2PasswordHasher.Hash(request.Password)
        };
        db.Users.Add(user);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException e) when (IsDuplicate(e))
        { return Conflict(new { message = "Brugernavnet er allerede i brug." }); }
        return CreatedAtAction(nameof(Get), new { id = user.Id }, UserResponse.From(user));
    }

    [Authorize(Roles = "SuperUser,Admin"), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateUserRequest request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var target = await db.Users.FindAsync(id);
        if (target is null) return NotFound();
        // Check both the existing role and the requested role to block elevation.
        if (!CanManage(target.Role) || !CanManage(request.Role)) return Forbid();
        if (id == Actor.Id && request.Role != Actor.Role)
            return Conflict(new { message = "Du kan ikke ændre din egen rolle." });
        if (target.Role == "Admin" && request.Role != "Admin" && await db.Users.CountAsync(u => u.Role == "Admin") <= 1)
            return Conflict(new { message = "Den sidste administrator skal bevares." });
        target.Username = request.Username;
        target.NormalizedUsername = request.Username.ToUpperInvariant();
        target.Role = request.Role;
        if (request.Password is not null) target.PasswordHash = Argon2PasswordHasher.Hash(request.Password);
        target.SecurityVersion++;
        await db.LoginSessions.Where(s => s.UserId == id).ExecuteDeleteAsync();
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException e) when (IsDuplicate(e))
        { return Conflict(new { message = "Brugernavnet er allerede i brug." }); }
        await transaction.CommitAsync();
        return Ok(UserResponse.From(target));
    }

    [Authorize(Roles = "SuperUser,Admin"), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var target = await db.Users.FindAsync(id);
        if (target is null) return NotFound();
        if (!CanManage(target.Role)) return Forbid();
        if (id == Actor.Id) return Conflict(new { message = "Du kan ikke slette din egen konto." });
        if (target.Role == "Admin" && await db.Users.CountAsync(u => u.Role == "Admin") <= 1)
            return Conflict(new { message = "Den sidste administrator skal bevares." });
        db.Users.Remove(target);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }
}
