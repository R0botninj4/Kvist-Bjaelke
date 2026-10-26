using System.Security.Claims;
using DataAccess.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Security.Dtos;
using Security.Services;

namespace Security.Controller;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsersController(IUserService userService) : ControllerBase
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? CurrentRole => userService.GetUserById(CurrentUserId)?.Role;

    private bool CanManage(string role)
    {
        return CurrentRole == "Admin" || (CurrentRole == "SuperUser" && role == "User");
    }

    [HttpGet]
    public ActionResult<List<User>> GetAll()
    {
        if (CurrentRole == null) return Unauthorized();
        var users = userService.GetUsers();
        if (CurrentRole != "Admin") users = users.Where(user => user.Role == "User").ToList();
        return Ok(users);
    }

    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        if (CurrentRole == null) return Unauthorized();
        var user = userService.GetUserById(id);
        if (user == null) return NotFound();
        if (CurrentRole != "Admin" && user.Role != "User") return Forbid();
        return Ok(user);
    }

    [Authorize(Roles = "SuperUser,Admin")]
    [HttpPost]
    [HttpPost("/register")]
    public IActionResult Create(CreateUserRequest request)
    {
        if (!CanManage(request.Role)) return Forbid();
        try
        {
            var user = userService.TryRegister(request);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }
        catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            return Conflict("Brugernavnet er allerede i brug.");
        }
    }

    [Authorize(Roles = "SuperUser,Admin")]
    [HttpPut("{id}")]
    public IActionResult Update(int id, UpdateUserRequest request)
    {
        var user = userService.GetUserById(id);
        if (user == null) return NotFound();
        if (!CanManage(user.Role) || !CanManage(request.Role)) return Forbid();
        if (id == CurrentUserId && request.Role != user.Role) return BadRequest("Du kan ikke ændre din egen rolle.");
        try { return Ok(userService.UpdateUser(id, request)); }
        catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            return Conflict("Brugernavnet er allerede i brug.");
        }
    }

    [Authorize(Roles = "SuperUser,Admin")]
    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        var user = userService.GetUserById(id);
        if (user == null) return NotFound();
        if (!CanManage(user.Role)) return Forbid();
        if (id == CurrentUserId) return BadRequest("Du kan ikke slette din egen konto.");
        userService.DeleteUser(id);
        return NoContent();
    }
}
