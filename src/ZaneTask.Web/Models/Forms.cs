using System.ComponentModel.DataAnnotations;
using ZaneTask.Contracts;

namespace ZaneTask.Web.Models;

// Mutable form models for EditForm; mapped to the immutable Contracts requests on submit.

public sealed class LoginForm
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address, like name@company.com.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Enter your password.")]
    public string Password { get; set; } = "";
}

public sealed class RegisterForm
{
    [Required(ErrorMessage = "Enter your name.")]
    [MaxLength(100, ErrorMessage = "Name must be 100 characters or fewer.")]
    public string DisplayName { get; set; } = "";

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address, like name@company.com.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Choose a password.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = "";
}

public sealed class ProjectForm
{
    [Required(ErrorMessage = "Give the project a name.")]
    [MaxLength(100, ErrorMessage = "Name must be 100 characters or fewer.")]
    public string Name { get; set; } = "";

    [MaxLength(2000, ErrorMessage = "Description must be 2000 characters or fewer.")]
    public string? Description { get; set; }
}

public sealed class TaskForm
{
    [Required(ErrorMessage = "Give the task a title.")]
    [MaxLength(200, ErrorMessage = "Title must be 200 characters or fewer.")]
    public string Title { get; set; } = "";

    [MaxLength(10000, ErrorMessage = "Description is too long.")]
    public string? Description { get; set; }

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateOnly? DueDate { get; set; }

    /// <summary>Bound to a select; empty string means unassigned.</summary>
    public string AssigneeId { get; set; } = "";

    public HashSet<Guid> LabelIds { get; set; } = [];

    public Guid? AssigneeGuid => Guid.TryParse(AssigneeId, out var id) ? id : null;

    public static TaskForm From(TaskDto task) => new()
    {
        Title = task.Title,
        Description = task.Description,
        Status = task.Status,
        Priority = task.Priority,
        DueDate = task.DueDate,
        AssigneeId = task.Assignee?.Id.ToString() ?? "",
        LabelIds = task.Labels.Select(l => l.Id).ToHashSet(),
    };
}

public sealed class MemberForm
{
    [Required(ErrorMessage = "Enter the email of a registered user.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = "";

    public ProjectRole Role { get; set; } = ProjectRole.Member;
}

public sealed class LabelForm
{
    [Required(ErrorMessage = "Give the label a name.")]
    [MaxLength(50, ErrorMessage = "Name must be 50 characters or fewer.")]
    public string Name { get; set; } = "";

    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Pick a color.")]
    public string Color { get; set; } = "#4F46E5";
}
