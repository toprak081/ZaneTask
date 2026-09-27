using ZaneTask.Domain.Common;
using ZaneTask.Domain.Projects;
using ZaneTask.Domain.Tasks;

namespace ZaneTask.Domain.Tests;

public class TaskItemTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Project _project;

    public TaskItemTests()
    {
        _project = Project.Create("P", null, "PRJ", _owner, Now);
    }

    private TaskItem NewTask(string title = "Task", Guid? assignee = null) =>
        TaskItem.Create(_project, title, null, TaskType.Task, TaskItemStatus.Todo, TaskPriority.Medium, null, assignee, _owner, 0, Now);

    [Fact]
    public void Create_requires_a_title()
    {
        Assert.Throws<DomainException>(() => NewTask(title: " "));
    }

    [Fact]
    public void Create_rejects_title_over_max_length()
    {
        Assert.Throws<DomainException>(() => NewTask(title: new string('x', TaskItem.TitleMaxLength + 1)));
    }

    [Fact]
    public void Tasks_can_only_be_assigned_to_members()
    {
        var outsider = Guid.NewGuid();

        Assert.Throws<DomainException>(() => NewTask(assignee: outsider));

        var task = NewTask();
        Assert.Throws<DomainException>(() => task.Assign(_project, outsider, Now));
    }

    [Fact]
    public void Assign_and_unassign_member()
    {
        var member = Guid.NewGuid();
        _project.AddMember(member, ProjectRole.Member, Now);
        var task = NewTask();

        task.Assign(_project, member, Now.AddMinutes(1));
        Assert.Equal(member, task.AssigneeId);
        Assert.Equal(Now.AddMinutes(1), task.UpdatedAt);

        task.Assign(_project, null, Now);
        Assert.Null(task.AssigneeId);
    }

    [Fact]
    public void Labels_from_another_project_are_rejected()
    {
        var otherProject = Project.Create("Other", null, "PRJ", _owner, Now);
        var foreignLabel = otherProject.AddLabel("Bug", "#FF0000");
        var task = NewTask();

        Assert.Throws<DomainException>(() => task.AddLabel(foreignLabel));
    }

    [Fact]
    public void Adding_the_same_label_twice_is_idempotent()
    {
        var label = _project.AddLabel("Bug", "#FF0000");
        var task = NewTask();

        task.AddLabel(label);
        task.AddLabel(label);

        Assert.Single(task.Labels);
    }

    [Fact]
    public void Only_the_author_can_edit_a_comment()
    {
        var task = NewTask();
        var comment = task.AddComment(_owner, "Hello", Now);

        Assert.Throws<DomainException>(() => comment.Edit(Guid.NewGuid(), "Hijacked", Now));

        comment.Edit(_owner, "Hello again", Now.AddMinutes(5));
        Assert.Equal("Hello again", comment.Body);
        Assert.Equal(Now.AddMinutes(5), comment.EditedAt);
    }
}
