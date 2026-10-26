using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using DataAccess;
using DataAccess.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Security.Dtos;
using Security.Services;

var builder = WebApplication.CreateBuilder(args);
var secret = builder.Configuration["JwtSecret"];
if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
    throw new InvalidOperationException("Set JwtSecret to a random secret of at least 32 bytes.");

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(
    builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));
builder.Services.AddSingleton<Argon2PasswordHasher>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = TokenService.Issuer,
        ValidateAudience = true, ValidAudience = TokenService.Audience,
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var principal = context.Principal!;
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var id = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var session = await db.LoginSessions.AsNoTracking().Include(s => s.User)
                .SingleOrDefaultAsync(s => s.Id == id);
            if (session is null || session.ExpiresAt <= DateTime.UtcNow ||
                principal.FindFirstValue(ClaimTypes.NameIdentifier) != session.UserId.ToString() ||
                principal.FindFirstValue("version") != session.User.SecurityVersion.ToString() ||
                principal.FindFirstValue(ClaimTypes.Role) != session.User.Role)
            {
                context.Fail("Session is no longer valid.");
                return;
            }
            context.HttpContext.Items["CurrentUser"] = session.User;
        }
    };
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Bound simultaneous requests, including expensive Argon2 computations.
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        RateLimitPartition.GetConcurrencyLimiter("global", _ => new ConcurrencyLimiterOptions
        { PermitLimit = 4, QueueLimit = 0 }));
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
});

var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
// Never return the SPA HTML for an unknown API route.
app.Map("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (!db.Users.Any())
    {
        var username = builder.Configuration["BootstrapAdmin:Username"] ?? "admin";
        var password = builder.Configuration["BootstrapAdmin:Password"];
        var request = new CreateUserRequest { Username = username, Password = password ?? "", Role = "Admin" };
        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request,
            new System.ComponentModel.DataAnnotations.ValidationContext(request), [], true);
        if (!valid) throw new InvalidOperationException("An empty database requires BootstrapAdmin__Password (12-128 characters) and a valid BootstrapAdmin__Username.");
        db.Users.Add(new User
        {
            Username = username, NormalizedUsername = username.ToUpperInvariant(), Role = "Admin",
            PasswordHash = Argon2PasswordHasher.Hash(password!)
        });
        db.SaveChanges();
    }
}
app.Run();
