using Microsoft.AspNetCore.Mvc;
using ZaneTask.Application.Tasks;
using ZaneTask.Contracts;

namespace ZaneTask.Api.Controllers;

[ApiController]
public sealed class CommentsController(CommentService comments) : ControllerBase
{
    [HttpGet("api/tasks/{taskId:guid}/comments")]
    public Task<IReadOnlyList<CommentDto>> List(Guid taskId, CancellationToken ct) => comments.ListAsync(taskId, ct);

    [HttpPost("api/tasks/{taskId:guid}/comments")]
    public async Task<ActionResult<CommentDto>> Add(Guid taskId, SaveCommentRequest request, CancellationToken ct)
    {
        var comment = await comments.AddAsync(taskId, request, ct);
        return StatusCode(StatusCodes.Status201Created, comment);
    }

    [HttpPut("api/comments/{commentId:guid}")]
    public Task<CommentDto> Update(Guid commentId, SaveCommentRequest request, CancellationToken ct) =>
        comments.UpdateAsync(commentId, request, ct);

    [HttpDelete("api/comments/{commentId:guid}")]
    public async Task<NoContentResult> Delete(Guid commentId, CancellationToken ct)
    {
        await comments.DeleteAsync(commentId, ct);
        return NoContent();
    }
}
