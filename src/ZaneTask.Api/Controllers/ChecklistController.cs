using Microsoft.AspNetCore.Mvc;
using ZaneTask.Application.Tasks;
using ZaneTask.Contracts;

namespace ZaneTask.Api.Controllers;

/// <summary>Checklist steps of a task. Every change returns the task's whole checklist.</summary>
[ApiController]
public sealed class ChecklistController(ChecklistService checklist) : ControllerBase
{
    [HttpGet("api/tasks/{taskId:guid}/checklist")]
    public Task<IReadOnlyList<ChecklistItemDto>> List(Guid taskId, CancellationToken ct) =>
        checklist.ListAsync(taskId, ct);

    [HttpPost("api/tasks/{taskId:guid}/checklist")]
    public Task<IReadOnlyList<ChecklistItemDto>> Add(Guid taskId, AddChecklistItemRequest request, CancellationToken ct) =>
        checklist.AddAsync(taskId, request, ct);

    [HttpPut("api/checklist/{itemId:guid}")]
    public Task<IReadOnlyList<ChecklistItemDto>> Update(Guid itemId, UpdateChecklistItemRequest request, CancellationToken ct) =>
        checklist.UpdateAsync(itemId, request, ct);

    [HttpPut("api/checklist/{itemId:guid}/move")]
    public Task<IReadOnlyList<ChecklistItemDto>> Move(Guid itemId, MoveChecklistItemRequest request, CancellationToken ct) =>
        checklist.MoveAsync(itemId, request, ct);

    [HttpDelete("api/checklist/{itemId:guid}")]
    public Task<IReadOnlyList<ChecklistItemDto>> Delete(Guid itemId, CancellationToken ct) =>
        checklist.DeleteAsync(itemId, ct);
}
