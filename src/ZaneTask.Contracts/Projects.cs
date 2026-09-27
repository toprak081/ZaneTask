using System.ComponentModel.DataAnnotations;

namespace ZaneTask.Contracts;

public enum ProjectRole
{
    Member = 0,
    Owner = 1,
}

/// <param name="Key">Optional; suggested from the name when empty.</param>
public sealed record CreateProjectRequest(
    [Required, MaxLength(100)] string Name,
    [MaxLength(2000)] string? Description,
    [RegularExpression("^[A-Za-z][A-Za-z0-9]{1,9}$", ErrorMessage = "Key must be " + ProjectKeys.Rule)] string? Key = null);

/// <param name="Key">Null keeps the current key.</param>
public sealed record UpdateProjectRequest(
    [Required, MaxLength(100)] string Name,
    [MaxLength(2000)] string? Description,
    [RegularExpression("^[A-Za-z][A-Za-z0-9]{1,9}$", ErrorMessage = "Key must be " + ProjectKeys.Rule)] string? Key = null);

public sealed record ProjectSummaryDto(
    Guid Id,
    string Name,
    string Key,
    string? Description,
    ProjectRole MyRole,
    int MemberCount,
    int OpenTaskCount,
    DateTime CreatedAt);

public sealed record ProjectDto(
    Guid Id,
    string Name,
    string Key,
    string? Description,
    ProjectRole MyRole,
    DateTime CreatedAt,
    IReadOnlyList<ProjectMemberDto> Members,
    IReadOnlyList<LabelDto> Labels,
    IReadOnlyList<BoardColumnDto> Columns);

/// <summary>A board column; <see cref="Category"/> says whether its tasks count as to do, in progress or done.</summary>
public sealed record BoardColumnDto(Guid Id, string Name, int Position, TaskItemStatus Category);

public sealed record SaveColumnRequest(
    [Required, MaxLength(40)] string Name,
    TaskItemStatus Category);

public sealed record MoveColumnRequest([Range(0, int.MaxValue)] int Position);

public sealed record ProjectMemberDto(UserDto User, ProjectRole Role, DateTime JoinedAt);

public sealed record AddMemberRequest(
    [Required, EmailAddress] string Email,
    ProjectRole Role = ProjectRole.Member);

public sealed record ChangeMemberRoleRequest(ProjectRole Role);

public sealed record LabelDto(Guid Id, string Name, string Color);

public sealed record SaveLabelRequest(
    [Required, MaxLength(50)] string Name,
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Color must be a hex color like #1E88E5.")] string Color);
