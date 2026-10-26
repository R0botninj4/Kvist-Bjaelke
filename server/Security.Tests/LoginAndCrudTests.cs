using System.Net;
using System.Text.RegularExpressions;
using DataAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Security.Services;
using Xunit;

namespace Security.Tests;

// Tester de rigtige C#-sider med en separat, midlertidig database.
public class LoginAndCrudTests : IDisposable
{
    private const string Password = "Test-password-12345";
    private readonly TestApp app = new();
    private HttpClient Client() => app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, Dictionary<string, string> fields)
    {
        var html = await client.GetStringAsync(path.StartsWith("/Login") ? "/Login" : "/");
        fields["__RequestVerificationToken"] = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private static Task<HttpResponseMessage> Login(HttpClient client, string username, string password = Password) =>
        Post(client, "/Login", new() { ["Username"] = username, ["Password"] = password });

    private static Dictionary<string, string> UserForm(string name, string role = "User", int id = 0) => new()
    {
        ["Input.Id"] = id.ToString(), ["Input.Username"] = name,
        ["Input.Role"] = role, ["Input.Password"] = id == 0 ? Password : ""
    };

    private static string RedirectPath(HttpResponseMessage response) => response.Headers.Location!.IsAbsoluteUri
        ? response.Headers.Location.AbsolutePath : response.Headers.Location.OriginalString.Split('?')[0];

    [Fact]
    public async Task LoginRequiresCorrectPassword()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).StatusCode);
        var wrong = await Login(client, "admin", "wrong-password");
        Assert.Contains("Forkert brugernavn", await wrong.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await Login(client, "admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task AdminCanCreateReadUpdateAndDelete()
    {
        using var client = Client();
        await Login(client, "admin");
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/?handler=Save", UserForm("test-user"))).StatusCode);
        var html = await client.GetStringAsync("/");
        Assert.Contains("test-user", html);
        Assert.DoesNotContain("$argon2id", html);
        Assert.DoesNotContain(Password, html);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.Single(u => u.Username == "test-user");
        Assert.StartsWith("$argon2id$", user.PasswordHash);
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/?handler=Save", UserForm("updated-user", id: user.Id))).StatusCode);
        Assert.Contains("updated-user", await client.GetStringAsync("/"));
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/?handler=Delete", new() { ["id"] = user.Id.ToString() })).StatusCode);
        Assert.DoesNotContain("updated-user", await client.GetStringAsync("/"));
    }

    [Theory]
    [InlineData("User", false)]
    [InlineData("SuperUser", true)]
    public async Task RolesAreCheckedOnTheServer(string role, bool mayCreate)
    {
        using var admin = Client();
        await Login(admin, "admin");
        await Post(admin, "/?handler=Save", UserForm("test-account", role));
        using var client = Client();
        await Login(client, "test-account");
        var result = await Post(client, "/?handler=Save", UserForm("new-user"));
        Assert.Equal(mayCreate ? "/" : "/Login", RedirectPath(result));
        // Hverken User eller SuperUser må oprette Admin.
        var forbidden = await Post(client, "/?handler=Save", UserForm("fake-admin", "Admin"));
        Assert.Equal("/Login", RedirectPath(forbidden));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(db.Users.Any(u => u.Username == "fake-admin"));
        Assert.Equal(mayCreate, db.Users.Any(u => u.Username == "new-user"));
    }

    public void Dispose() => app.Dispose();

    private class TestApp : WebApplicationFactory<Program>
    {
        private readonly string database = Path.Combine(Path.GetTempPath(), $"kvist-{Guid.NewGuid():N}.db");
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("BootstrapAdmin:Password", Password);
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={database}");
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            SqliteConnection.ClearAllPools();
            foreach (var path in new[] { database, database + "-wal", database + "-shm" })
                if (File.Exists(path)) File.Delete(path);
        }
    }
}
