namespace ZaneTask.Domain.Common;

internal static class Guard
{
    public static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException($"{field} is required.");
        if (trimmed.Length > maxLength)
            throw new DomainException($"{field} must be at most {maxLength} characters.");
        return trimmed;
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > maxLength)
            throw new DomainException($"{field} must be at most {maxLength} characters.");
        return trimmed;
    }
}
