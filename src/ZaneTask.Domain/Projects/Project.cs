using System.Text.RegularExpressions;
using ZaneTask.Domain.Common;
using ZaneTask.Domain.Tasks;

namespace ZaneTask.Domain.Projects;

public partial class Project : Entity
{
    public const int NameMaxLength = 100;
    public const int KeyMaxLength = 10;
    public const int DescriptionMaxLength = 2000;
    public const int MaxColumns = 12;

    private readonly List<ProjectMember> _members = [];
    private readonly List<Label> _labels = [];
    private readonly List<BoardColumn> _columns = [];

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

    /// <summary>Board columns; see <see cref="OrderedColumns"/> for board order.</summary>
    public IReadOnlyCollection<BoardColumn> Columns => _columns;

    public IEnumerable<BoardColumn> OrderedColumns => _columns.OrderBy(c => c.Position);

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
        project.AddColumn("To do", TaskItemStatus.Todo);
        project.AddColumn("In progress", TaskItemStatus.InProgress);
        project.AddColumn("Done", TaskItemStatus.Done);
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

    public BoardColumn AddColumn(string name, TaskItemStatus category)
    {
        if (_columns.Count >= MaxColumns)
            throw new DomainException($"A board can have at most {MaxColumns} columns.");

        var column = new BoardColumn(Id, name, category, _columns.Count);
        EnsureColumnNameIsUnique(column.Name, exceptId: null);
        _columns.Add(column);
        return column;
    }

    public BoardColumn RenameColumn(Guid columnId, string name)
    {
        var column = GetColumn(columnId);
        column.Rename(name);
        EnsureColumnNameIsUnique(column.Name, exceptId: columnId);
        return column;
    }

    /// <summary>Changes what a column means. Callers must re-sync the category of the column's tasks.</summary>
    public BoardColumn SetColumnCategory(Guid columnId, TaskItemStatus category)
    {
        var column = GetColumn(columnId);
        column.Category = category;
        return column;
    }

    /// <summary>Moves a column to <paramref name="position"/> (clamped) and renumbers the others.</summary>
    public void MoveColumn(Guid columnId, int position)
    {
        if (position < 0)
            throw new DomainException("Position cannot be negative.");

        var column = GetColumn(columnId);
        var ordered = OrderedColumns.Where(c => c != column).ToList();
        ordered.Insert(Math.Min(position, ordered.Count), column);
        RenumberColumns(ordered);
    }

    /// <summary>Removes an empty column. Move its tasks to another column first.</summary>
    public void RemoveColumn(Guid columnId)
    {
        if (_columns.Count <= 1)
            throw new DomainException("A board needs at least one column.");

        _columns.Remove(GetColumn(columnId));
        RenumberColumns(OrderedColumns.ToList());
    }

    public BoardColumn GetColumn(Guid columnId) =>
        _columns.FirstOrDefault(c => c.Id == columnId)
        ?? throw new DomainException("Column does not belong to this project.");

    /// <summary>The leftmost column with <paramref name="category"/>, or the leftmost column if there is none.</summary>
    public BoardColumn DefaultColumn(TaskItemStatus category) =>
        OrderedColumns.FirstOrDefault(c => c.Category == category)
        ?? OrderedColumns.FirstOrDefault()
        ?? throw new DomainException("The project has no columns.");

    private static void RenumberColumns(List<BoardColumn> columns)
    {
        for (var i = 0; i < columns.Count; i++)
            columns[i].Position = i;
    }

    private void EnsureColumnNameIsUnique(string name, Guid? exceptId)
    {
        if (_columns.Any(c => c.Id != exceptId && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"A column named '{name}' already exists.");
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
