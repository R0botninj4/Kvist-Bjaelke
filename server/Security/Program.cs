using System.Security.Claims;
using System.Text;
using DataAccess;
using DataAccess.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Security.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));
builder.Services.AddSingleton<Argon2PasswordHasher>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    var secret = builder.Configuration["JwtSecret"];
    if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        throw new InvalidOperationException("Angiv JwtSecret med mindst 32 tilfældige tegn.");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = "kvist-bjaelke",
        ValidateAudience = true, ValidAudience = "kvist-bjaelke",
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var id = int.Parse(context.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await db.Users.FindAsync(id);
            if (user == null || context.Principal!.FindFirstValue("version") != user.SecurityVersion.ToString())
                context.Fail("Kontoen er ændret eller slettet. Log ind igen.");
        }
    };
});

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
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
public partial class Program { }
