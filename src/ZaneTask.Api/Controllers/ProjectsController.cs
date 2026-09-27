using Microsoft.AspNetCore.Mvc;
using ZaneTask.Application.Projects;
using ZaneTask.Contracts;

namespace ZaneTask.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(ProjectService projects) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProjectSummaryDto>> List(CancellationToken ct) => projects.ListAsync(ct);

    [HttpGet("{projectId:guid}")]
    public Task<ProjectDto> Get(Guid projectId, CancellationToken ct) => projects.GetAsync(projectId, ct);

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var project = await projects.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { projectId = project.Id }, project);
    }

    [HttpPut("{projectId:guid}")]
    public Task<ProjectDto> Update(Guid projectId, UpdateProjectRequest request, CancellationToken ct) =>
        projects.UpdateAsync(projectId, request, ct);

    [HttpDelete("{projectId:guid}")]
    public async Task<NoContentResult> Delete(Guid projectId, CancellationToken ct)
    {
        await projects.DeleteAsync(projectId, ct);
        return NoContent();
    }

    [HttpPost("{projectId:guid}/members")]
    public Task<ProjectMemberDto> AddMember(Guid projectId, AddMemberRequest request, CancellationToken ct) =>
        projects.AddMemberAsync(projectId, request, ct);

    [HttpPut("{projectId:guid}/members/{userId:guid}")]
    public Task<ProjectMemberDto> ChangeMemberRole(
        Guid projectId, Guid userId, ChangeMemberRoleRequest request, CancellationToken ct) =>
        projects.ChangeMemberRoleAsync(projectId, userId, request, ct);

    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    public async Task<NoContentResult> RemoveMember(Guid projectId, Guid userId, CancellationToken ct)
    {
        await projects.RemoveMemberAsync(projectId, userId, ct);
        return NoContent();
    }

    [HttpPost("{projectId:guid}/labels")]
    public Task<LabelDto> CreateLabel(Guid projectId, SaveLabelRequest request, CancellationToken ct) =>
        projects.CreateLabelAsync(projectId, request, ct);

    [HttpPut("{projectId:guid}/labels/{labelId:guid}")]
    public Task<LabelDto> UpdateLabel(Guid projectId, Guid labelId, SaveLabelRequest request, CancellationToken ct) =>
        projects.UpdateLabelAsync(projectId, labelId, request, ct);

    [HttpDelete("{projectId:guid}/labels/{labelId:guid}")]
    public async Task<NoContentResult> DeleteLabel(Guid projectId, Guid labelId, CancellationToken ct)
    {
        await projects.DeleteLabelAsync(projectId, labelId, ct);
        return NoContent();
    }
}
