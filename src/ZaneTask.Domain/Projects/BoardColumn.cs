using ZaneTask.Domain.Common;
using ZaneTask.Domain.Tasks;

namespace ZaneTask.Domain.Projects;

/// <summary>
/// A column of a project's board, e.g. "Code review". Its <see cref="Category"/> says what the column means
/// (to do / in progress / done), so "open tasks" and "My tasks" work no matter how columns are named.
/// </summary>
public class BoardColumn : Entity
{
    public const int NameMaxLength = 40;

    private BoardColumn()
    {
        Name = null!;
    }

    internal BoardColumn(Guid projectId, string name, TaskItemStatus category, int position)
    {
        ProjectId = projectId;
        Name = Guard.Required(name, "Column name", NameMaxLength);
        Category = category;
        Position = position;
    }

    public Guid ProjectId { get; private set; }
    public string Name { get; private set; }
    public TaskItemStatus Category { get; internal set; }

    /// <summary>Zero-based order of the column on the board.</summary>
    public int Position { get; internal set; }

    internal void Rename(string name) => Name = Guard.Required(name, "Column name", NameMaxLength);
}
