using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security.Dtos;
using Security.Services;
using IAuthorizationService = Security.Services.Authorization.IAuthorizationService;

namespace Security.Controller;

[ApiController]
public class LoginController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authorizationService;

    public LoginController(IUserService userService, IAuthorizationService authorizationService)
    {
        _userService = userService;
        _authorizationService = authorizationService;
    }

    [AllowAnonymous]
    [HttpPost("/login")]
    public IActionResult Login([FromBody] TryLogin tryLogin)
    {
        var user = _userService.TryLogin(tryLogin);
        if (user == null) return Unauthorized();
        var token = _authorizationService.GenerateToken(user);
        return Ok(new { token });
    }

    [Authorize]
    [HttpGet("/api/auth/me")]
    public IActionResult Me()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = _userService.GetUserById(id);
        if (user == null) return Unauthorized();
        return Ok(user);
    }
}
