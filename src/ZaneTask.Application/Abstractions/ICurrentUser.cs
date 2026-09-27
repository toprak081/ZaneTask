namespace ZaneTask.Application.Abstractions;

public interface ICurrentUser
{
    /// <summary>Id of the authenticated user making the request.</summary>
    Guid UserId { get; }
}
