using System.Net;
using System.Text.Json;

namespace ZaneTask.Web.Services;

/// <summary>A non-success API response, parsed from the server's ProblemDetails body when present.</summary>
public sealed class ApiException(
    HttpStatusCode status,
    string message,
    IReadOnlyDictionary<string, string[]> errors) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;

    /// <summary>Field-level validation errors keyed by property name (may be empty).</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static async Task<ApiException> FromResponseAsync(HttpResponseMessage response)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        string? message = null;

        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                message = detail.GetString();
            else if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                message = title.GetString();

            if (root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errs.EnumerateObject())
                {
                    var key = field.Name.TrimStart('$', '.');
                    errors[key] = field.Value.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
                }
            }
        }
        catch (JsonException)
        {
            // Not a ProblemDetails body; fall back to a generic message below.
        }

        message ??= response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
            HttpStatusCode.Forbidden => "You don't have permission to do that.",
            HttpStatusCode.NotFound => "That item no longer exists.",
            _ => "Something went wrong. Please try again.",
        };

        return new ApiException(response.StatusCode, message, errors);
    }

    public static ApiException Network() =>
        new(0, "Can't reach the server. Check your connection and try again.", new Dictionary<string, string[]>());
}
