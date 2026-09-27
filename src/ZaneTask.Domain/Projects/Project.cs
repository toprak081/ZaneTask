using System.Text.RegularExpressions;
using ZaneTask.Domain.Common;

namespace ZaneTask.Domain.Projects;

public partial class Project : Entity
{
    public const int NameMaxLength = 100;
    public const int KeyMaxLength = 10;
    public const int DescriptionMaxLength = 2000;

    private readonly List<ProjectMember> _members = [];
    private readonly List<Label> _labels = [];

    private Project()
    {
        Name = null!;
        Key = null!;
    }

    public string Name { get; private set; }

    /// <summary>Short uppercase code that prefixes task numbers, e.g. WEB in WEB-12.</summary>
    public string Key { get; private set; }

    /// <summary>Number the next created task will get.</summary>
    public int NextTaskNumber { get; private set; } = 1;

    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyCollection<ProjectMember> Members => _members;
    public IReadOnlyCollection<Label> Labels => _labels;

    public static Project Create(string name, string? description, string key, Guid ownerId, DateTime now)
    {
        var project = new Project
        {
            Name = Guard.Required(name, "Project name", NameMaxLength),
            Key = NormalizeKey(key),
            Description = Guard.Optional(description, "Description", DescriptionMaxLength),
            CreatedAt = now,
        };
        project._members.Add(new ProjectMember(project.Id, ownerId, ProjectRole.Owner, now));
        return project;
    }

    public void Update(string name, string? description)
    {
        Name = Guard.Required(name, "Project name", NameMaxLength);
        Description = Guard.Optional(description, "Description", DescriptionMaxLength);
    }

    public void ChangeKey(string key) => Key = NormalizeKey(key);

    /// <summary>Hands out the next task number (1, 2, 3…); numbers are never reused.</summary>
    public int AllocateTaskNumber() => NextTaskNumber++;

    public static string NormalizeKey(string? key)
    {
        var normalized = key?.Trim().ToUpperInvariant();
        if (normalized is null || !KeyRegex().IsMatch(normalized))
            throw new DomainException("Project key must be 2-10 letters or digits and start with a letter, like WEB.");
        return normalized;
    }

    public bool IsMember(Guid userId) => _members.Any(m => m.UserId == userId);

    public bool IsOwner(Guid userId) => _members.Any(m => m.UserId == userId && m.Role == ProjectRole.Owner);

    public ProjectMember AddMember(Guid userId, ProjectRole role, DateTime now)
    {
        if (IsMember(userId))
            throw new DomainException("User is already a member of this project.");

        var member = new ProjectMember(Id, userId, role, now);
        _members.Add(member);
        return member;
    }

    public void ChangeMemberRole(Guid userId, ProjectRole role)
    {
        var member = GetMember(userId);
        if (member.Role == ProjectRole.Owner && role != ProjectRole.Owner)
            EnsureAnotherOwnerExists(userId);
        member.Role = role;
    }

    public void RemoveMember(Guid userId)
    {
        var member = GetMember(userId);
        if (member.Role == ProjectRole.Owner)
            EnsureAnotherOwnerExists(userId);
        _members.Remove(member);
    }

    public Label AddLabel(string name, string color)
    {
        var label = new Label(Id, name, color);
        EnsureLabelNameIsUnique(label.Name, exceptId: null);
        _labels.Add(label);
        return label;
    }

    public Label UpdateLabel(Guid labelId, string name, string color)
    {
        var label = GetLabel(labelId);
        label.Update(name, color);
        EnsureLabelNameIsUnique(label.Name, exceptId: labelId);
        return label;
    }

    public void RemoveLabel(Guid labelId) => _labels.Remove(GetLabel(labelId));

    public Label GetLabel(Guid labelId) =>
        _labels.FirstOrDefault(l => l.Id == labelId)
        ?? throw new DomainException("Label does not belong to this project.");

    private ProjectMember GetMember(Guid userId) =>
        _members.FirstOrDefault(m => m.UserId == userId)
        ?? throw new DomainException("User is not a member of this project.");

    private void EnsureAnotherOwnerExists(Guid userId)
    {
        if (!_members.Any(m => m.UserId != userId && m.Role == ProjectRole.Owner))
            throw new DomainException("A project must have at least one owner.");
    }

    private void EnsureLabelNameIsUnique(string name, Guid? exceptId)
    {
        if (_labels.Any(l => l.Id != exceptId && string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"A label named '{name}' already exists.");
    }

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,9}$")]
    private static partial Regex KeyRegex();
}
