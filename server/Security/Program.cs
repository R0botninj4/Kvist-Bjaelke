using DataAccess;
using Security;
using Security.Dtos;
using Security.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureInfrastructureServices(
    builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db");
builder.Services.ConfigureDomainServices(builder.Configuration["JwtSecret"]!);
builder.Services.AddControllers();
builder.Services.AddCors(options => options.AddPolicy("CorsPolicy", policy => policy
    .WithOrigins("http://localhost:3500", "http://127.0.0.1:3500")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (!db.Users.Any())
    {
        var password = builder.Configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 128)
            throw new InvalidOperationException("Angiv BootstrapAdmin__Password med 12-128 tegn.");
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        userService.TryRegister(new CreateUserRequest
        {
            Username = builder.Configuration["BootstrapAdmin:Username"] ?? "admin",
            Password = password,
            Role = "Admin"
        });
    }
}
app.MapControllers();
app.MapGet("/health", () => "OK");
app.Run();
public partial class Program { }
