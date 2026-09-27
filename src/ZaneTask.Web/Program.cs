using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ZaneTask.Web;
using ZaneTask.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Empty = same origin (the app is served by the API, e.g. in the desktop app).
// Development points at the separately running API in wwwroot/appsettings.Development.json.
var configuredApiUrl = builder.Configuration["ApiBaseUrl"];
var apiBaseUrl = string.IsNullOrWhiteSpace(configuredApiUrl) ? builder.HostEnvironment.BaseAddress : configuredApiUrl;

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<SessionStore>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());

builder.Services.AddScoped(sp =>
{
    var handler = new BearerTokenHandler(
        sp.GetRequiredService<SessionStore>(),
        sp.GetRequiredService<JwtAuthenticationStateProvider>())
    {
        InnerHandler = new HttpClientHandler(),
    };
    return new ApiClient(new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) });
});

builder.Services.AddSingleton<ToastService>();

await builder.Build().RunAsync();
