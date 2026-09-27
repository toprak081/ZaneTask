using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ZaneTask.Api.Infrastructure;
using ZaneTask.Application;
using ZaneTask.Application.Abstractions;
using ZaneTask.Infrastructure;
using ZaneTask.Infrastructure.Persistence;

const string WebClientCors = "WebClient";

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers(options =>
    {
        // Every endpoint requires a signed-in user unless it opts out with [AllowAnonymous].
        options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter(
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionToProblemDetailsHandler>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddHostedService<ParentProcessWatcher>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthorization();

builder.Services.AddCors(options => options.AddPolicy(WebClientCors, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else if (app.Configuration.GetValue("Hosting:UseHttpsRedirection", true))
{
    // The desktop app serves plain HTTP on 127.0.0.1 and turns this off.
    app.UseHttpsRedirection();
}

app.UseCors(WebClientCors);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Serve the Blazor app from the same origin, so a single process is enough (used by the desktop app).
app.MapStaticAssets();
app.MapFallback("/api/{**path}", () => Results.NotFound()); // unknown API routes stay 404, not index.html
// no-cache: after an update the app must pick up the new index.html (and through it the new app files).
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
});

app.Run();
