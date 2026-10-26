using System.Security.Claims;
using DataAccess;
using DataAccess.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Security.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));
builder.Services.AddSingleton<Argon2PasswordHasher>();
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        // Kontoændringer gør tidligere login ugyldigt.
        options.Events.OnValidatePrincipal = async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var id = int.Parse(context.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(id);
            if (user == null || context.Principal!.FindFirstValue("version") != user.SecurityVersion.ToString())
                context.RejectPrincipal();
        };
    });

// Docker gemmer cookie-nøgler sammen med databasen.
var keysPath = builder.Configuration["DataProtectionKeys"];
if (!string.IsNullOrEmpty(keysPath))
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath));

var app = builder.Build();
app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapGet("/health", () => "OK");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (!db.Users.Any())
    {
        var password = builder.Configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 128)
            throw new InvalidOperationException("Angiv BootstrapAdmin__Password med 12-128 tegn.");
        var username = builder.Configuration["BootstrapAdmin:Username"] ?? "admin";
        db.Users.Add(new User
        {
            Username = username, NormalizedUsername = username.ToUpperInvariant(),
            Role = "Admin", PasswordHash = Argon2PasswordHasher.Hash(password)
        });
        db.SaveChanges();
    }
}
app.Run();

// Gør webprojektet tilgængeligt for C#-testprojektet.
public partial class Program { }
