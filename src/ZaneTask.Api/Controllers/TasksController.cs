using Microsoft.AspNetCore.Mvc;
using ZaneTask.Application.Tasks;
using ZaneTask.Contracts;

namespace ZaneTask.Api.Controllers;

[ApiController]
public sealed class TasksController(TaskService tasks) : ControllerBase
{
    [HttpGet("api/projects/{projectId:guid}/tasks")]
    public Task<IReadOnlyList<TaskDto>> List(
        Guid projectId,
        [FromQuery] TaskItemStatus? status,
        [FromQuery] Guid? assigneeId,
        [FromQuery] Guid? labelId,
        [FromQuery] string? search,
        CancellationToken ct) =>
        tasks.ListAsync(projectId, new TaskFilter(status, assigneeId, labelId, search), ct);

    [HttpPost("api/projects/{projectId:guid}/tasks")]
    public async Task<ActionResult<TaskDto>> Create(Guid projectId, CreateTaskRequest request, CancellationToken ct)
    {
        var task = await tasks.CreateAsync(projectId, request, ct);
        return CreatedAtAction(nameof(Get), new { taskId = task.Id }, task);
    }

    [HttpGet("api/tasks/{taskId:guid}")]
    public Task<TaskDto> Get(Guid taskId, CancellationToken ct) => tasks.GetAsync(taskId, ct);

    [HttpPut("api/tasks/{taskId:guid}")]
    public Task<TaskDto> Update(Guid taskId, UpdateTaskRequest request, CancellationToken ct) =>
        tasks.UpdateAsync(taskId, request, ct);

    [HttpDelete("api/tasks/{taskId:guid}")]
    public async Task<NoContentResult> Delete(Guid taskId, CancellationToken ct)
    {
        await tasks.DeleteAsync(taskId, ct);
        return NoContent();
    }

    [HttpPut("api/tasks/{taskId:guid}/assignee")]
    public Task<TaskDto> Assign(Guid taskId, AssignTaskRequest request, CancellationToken ct) =>
        tasks.AssignAsync(taskId, request, ct);

    /// <summary>Moves a task on the kanban board (to another column and/or position).</summary>
    [HttpPut("api/tasks/{taskId:guid}/move")]
    public Task<TaskDto> Move(Guid taskId, MoveTaskRequest request, CancellationToken ct) =>
        tasks.MoveAsync(taskId, request, ct);

    [HttpPut("api/tasks/{taskId:guid}/labels/{labelId:guid}")]
    public Task<TaskDto> AddLabel(Guid taskId, Guid labelId, CancellationToken ct) =>
        tasks.AddLabelAsync(taskId, labelId, ct);

    [HttpDelete("api/tasks/{taskId:guid}/labels/{labelId:guid}")]
    public Task<TaskDto> RemoveLabel(Guid taskId, Guid labelId, CancellationToken ct) =>
        tasks.RemoveLabelAsync(taskId, labelId, ct);
}
