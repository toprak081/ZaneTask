namespace ZaneTask.Application.Common;

/// <summary>The resource does not exist or the current user may not see it.</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.");

/// <summary>The current user can see the resource but may not perform the operation.</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>The request conflicts with existing state, e.g. a duplicate email.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Credentials were missing or invalid.</summary>
public sealed class AuthenticationFailedException(string message) : Exception(message);

/// <summary>Input failed validation; <see cref="Errors"/> is keyed by field name.</summary>
public sealed class ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
