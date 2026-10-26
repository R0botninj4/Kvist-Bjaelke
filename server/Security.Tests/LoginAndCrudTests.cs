using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DataAccess;
using DataAccess.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Security.Tests;

// HTTP-tests med en midlertidig database. Din lokale app.db ændres ikke.
public class LoginAndCrudTests : IDisposable
{
    private const string Password = "Test-password-12345";
    private readonly TestApp app = new();
    private HttpClient Client() => app.CreateClient();
    private static object Input(string username, string role = "User", string? password = Password) => new { username, role, password };

    private static async Task Login(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/login", new { username, password = Password });
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data!.Token);
    }

    [Fact]
    public async Task LoginRequiresCorrectPassword()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
        var wrong = await client.PostAsJsonAsync("/login", new { username = "admin", password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        await Login(client, "admin");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task AdminHasFullCrudForAllThreeRoles()
    {
        using var client = Client();
        await Login(client, "admin");
        foreach (var role in new[] { "User", "SuperUser", "Admin" })
        {
            var created = await client.PostAsJsonAsync("/api/users", Input("test-" + role, role));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var user = await created.Content.ReadFromJsonAsync<User>();
            var path = $"/api/users/{user!.Id}";
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/users", Input("test-" + role, role))).StatusCode);
            var json = await client.GetStringAsync("/api/users");
            Assert.DoesNotContain("password", json.ToLowerInvariant());
            Assert.DoesNotContain(Password, json);
            using (var scope = app.Services.CreateScope())
                Assert.Equal(2, scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Find(user.Id)!.PasswordHash.Split('.').Length);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, Input("updated-" + role, role, null))).StatusCode);
            using var oldLogin = Client();
            if (role == "Admin")
            {
                await Login(oldLogin, "updated-Admin");
                await client.PutAsJsonAsync(path, Input("updated-Admin", "User", null));
                Assert.Equal(HttpStatusCode.Forbidden, (await oldLogin.PostAsJsonAsync("/api/users", Input("not-allowed"))).StatusCode);
            }
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
            if (role == "Admin") Assert.Equal(HttpStatusCode.Unauthorized, (await oldLogin.GetAsync("/api/users")).StatusCode);
        }
    }

    [Theory]
    [InlineData("User", false)]
    [InlineData("SuperUser", true)]
    public async Task LowerRolesCannotManageHigherRoles(string role, bool mayCreate)
    {
        using var admin = Client();
        await Login(admin, "admin");
        var root = await admin.GetFromJsonAsync<User>("/api/auth/me");
        await admin.PostAsJsonAsync("/api/users", Input("test-account", role));
        using var client = Client();
        await Login(client, "test-account");
        var list = await client.GetFromJsonAsync<List<User>>("/api/users");
        Assert.All(list!, user => Assert.Equal("User", user.Role));
        Assert.Equal(mayCreate ? HttpStatusCode.Created : HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/users", Input("new-user"))).StatusCode);
        foreach (var higherRole in new[] { "SuperUser", "Admin" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/users", Input("fake-admin", higherRole))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/users/{root!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/users/{root.Id}", Input("changed", "User", null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/users/{root.Id}")).StatusCode);
        if (mayCreate)
        {
            var normal = (await client.GetFromJsonAsync<List<User>>("/api/users"))!.Single();
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/users/{normal.Id}", Input(normal.Username, "Admin", null))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/users/{normal.Id}", Input("renamed", password: null))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/users/{normal.Id}")).StatusCode);
        }
    }

    public void Dispose() => app.Dispose();
    private record LoginResponse(string Token);

    private class TestApp : WebApplicationFactory<Program>
    {
        private readonly string database = Path.Combine(Path.GetTempPath(), $"kvist-{Guid.NewGuid():N}.db");
        public TestApp()
        {
            Environment.SetEnvironmentVariable("JwtSecret", new string('a', 48));
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("JwtSecret", new string('a', 48));
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
