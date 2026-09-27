using System.Net;
using System.Net.Http.Json;
using ZaneTask.Contracts;
using static ZaneTask.Api.IntegrationTests.ApiFactory;

namespace ZaneTask.Api.IntegrationTests;

public sealed class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Endpoints_require_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_login_and_me()
    {
        var client = factory.CreateClient();
        var email = $"dana-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Passw0rd!", "Dana"), Json);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email.ToUpperInvariant(), "Passw0rd!"), Json);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));

        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me", Json);
        Assert.Equal("Dana", me!.DisplayName);
        Assert.Equal(email, me.Email);
    }

    [Fact]
    public async Task Duplicate_email_is_a_conflict()
    {
        var client = factory.CreateClient();
        var request = new RegisterRequest($"eve-{Guid.NewGuid():N}@example.com", "Passw0rd!", "Eve");
        await client.PostAsJsonAsync("/api/auth/register", request, Json);

        var again = await client.PostAsJsonAsync("/api/auth/register", request, Json);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_unauthorized()
    {
        var client = factory.CreateClient();
        var email = $"finn-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Passw0rd!", "Finn"), Json);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "nope-nope"), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Invalid_registration_is_a_validation_problem()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest("not-an-email", "short", ""), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Password", body);
        Assert.Contains("Email", body);
    }
}
