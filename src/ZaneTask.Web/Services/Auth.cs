using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using ZaneTask.Contracts;

namespace ZaneTask.Web.Services;

/// <summary>Persists the signed-in session in localStorage so it survives page reloads.</summary>
public sealed class SessionStore(IJSRuntime js)
{
    private const string Key = "zanetask.session";
    private AuthResponse? _session;
    private bool _loaded;

    public async ValueTask<AuthResponse?> GetAsync()
    {
        if (!_loaded)
        {
            _loaded = true;
            try
            {
                var json = await js.InvokeAsync<string?>("localStorage.getItem", Key);
                _session = json is null ? null : JsonSerializer.Deserialize<AuthResponse>(json);
            }
            catch (Exception e) when (e is JSException or JsonException)
            {
                _session = null;
            }
        }

        if (_session is not null && _session.ExpiresAt <= DateTime.UtcNow)
            await ClearAsync();

        return _session;
    }

    public async ValueTask SetAsync(AuthResponse session)
    {
        _session = session;
        _loaded = true;
        await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(session));
    }

    public async ValueTask ClearAsync()
    {
        _session = null;
        _loaded = true;
        await js.InvokeVoidAsync("localStorage.removeItem", Key);
    }
}

public sealed class JwtAuthenticationStateProvider(SessionStore store) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var session = await store.GetAsync();
        if (session is null)
            return Anonymous;

        var identity = new ClaimsIdentity(
            [
                new Claim("sub", session.User.Id.ToString()),
                new Claim("email", session.User.Email),
                new Claim("name", session.User.DisplayName),
            ],
            authenticationType: "jwt",
            nameType: "name",
            roleType: "role");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public async Task SignInAsync(AuthResponse session)
    {
        await store.SetAsync(session);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SignOutAsync()
    {
        await store.ClearAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }
}

/// <summary>Attaches the bearer token and signs the user out when the API rejects it.</summary>
public sealed class BearerTokenHandler(SessionStore store, JwtAuthenticationStateProvider auth) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var session = await store.GetAsync();
        if (session is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized && session is not null)
            await auth.SignOutAsync();
        return response;
    }
}

public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}
