using ZaneTask.Domain.Common;

namespace ZaneTask.Domain.Projects;

public class Project : Entity
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 2000;

    private readonly List<ProjectMember> _members = [];
    private readonly List<Label> _labels = [];

    private Project()
    {
        Name = null!;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyCollection<ProjectMember> Members => _members;
    public IReadOnlyCollection<Label> Labels => _labels;

    public static Project Create(string name, string? description, Guid ownerId, DateTime now)
    {
        var project = new Project
        {
            Name = Guard.Required(name, "Project name", NameMaxLength),
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
}
