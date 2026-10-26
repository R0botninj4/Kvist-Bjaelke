using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DataAccess;
using DataAccess.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Security.Services;

namespace Security.Pages;

[Authorize]
public class IndexModel(AppDbContext db) : PageModel
{
    public List<User> Users { get; set; } = [];
    [BindProperty] public UserInput Input { get; set; } = new();
    [TempData] public string? Message { get; set; }
    public bool ShowForm { get; set; }
    public bool IsAdmin => User.IsInRole("Admin");
    public bool CanEdit => IsAdmin || User.IsInRole("SuperUser");
    public int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // SuperUser må kun arbejde med almindelige User-konti.
    private bool CanManage(string role) => IsAdmin || (User.IsInRole("SuperUser") && role == "User");

    private async Task LoadUsers()
    {
        var query = db.Users.AsNoTracking();
        if (!IsAdmin) query = query.Where(u => u.Role == "User");
        Users = await query.OrderBy(u => u.Username).ToListAsync();
    }

    public async Task<IActionResult> OnGetAsync(int? editId, bool create = false)
    {
        await LoadUsers();
        ShowForm = create || editId.HasValue;
        if (ShowForm && !CanEdit) return Forbid();
        if (editId.HasValue)
        {
            var user = await db.Users.FindAsync(editId.Value);
            if (user == null) return NotFound();
            if (!CanManage(user.Role)) return Forbid();
            Input = new UserInput { Id = user.Id, Username = user.Username, Role = user.Role };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!CanEdit || !CanManage(Input.Role)) return Forbid();
        var user = Input.Id == 0 ? new User() : await db.Users.FindAsync(Input.Id);
        if (user == null) return NotFound();
        if (Input.Id != 0 && !CanManage(user.Role)) return Forbid();
        if (Input.Id == CurrentUserId && Input.Role != user.Role)
            ModelState.AddModelError("", "Du kan ikke ændre din egen rolle.");
        if (Input.Id == 0 && string.IsNullOrWhiteSpace(Input.Password))
            ModelState.AddModelError("Input.Password", "Angiv en adgangskode til den nye bruger.");
        Input.Username ??= "";
        var normalized = Input.Username.ToUpperInvariant();
        if (await db.Users.AnyAsync(u => u.NormalizedUsername == normalized && u.Id != Input.Id))
            ModelState.AddModelError("Input.Username", "Brugernavnet er allerede i brug.");

        if (!ModelState.IsValid)
        {
            ShowForm = true;
            await LoadUsers();
            return Page();
        }
        user.Username = Input.Username;
        user.NormalizedUsername = normalized;
        user.Role = Input.Role;
        if (!string.IsNullOrEmpty(Input.Password)) user.PasswordHash = Argon2PasswordHasher.Hash(Input.Password);
        user.SecurityVersion++;
        if (Input.Id == 0) db.Users.Add(user);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            ModelState.AddModelError("", "Brugernavnet er allerede i brug.");
            ShowForm = true;
            await LoadUsers();
            return Page();
        }
        Message = Input.Id == 0 ? "Brugeren er oprettet." : "Brugeren er opdateret.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (!CanManage(user.Role)) return Forbid();
        if (id == CurrentUserId) return BadRequest("Du kan ikke slette din egen konto.");
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        Message = "Brugeren er slettet.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync();
        return RedirectToPage("Login");
    }
}

// Formularen må kun ændre disse felter. PasswordHash vises aldrig.
public class UserInput
{
    public int Id { get; set; }
    [Required, RegularExpression(@"^[a-zA-Z0-9._-]{3,50}$", ErrorMessage = "Brugernavn: 3-50 bogstaver, tal, punktum, bindestreg eller underscore.")]
    public string Username { get; set; } = "";
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Adgangskoden skal være 12-128 tegn.")]
    public string? Password { get; set; }
    [Required, RegularExpression("^(User|SuperUser|Admin)$")]
    public string Role { get; set; } = "User";
}
