using ZaneTask.Domain.Common;

namespace ZaneTask.Domain.Tasks;

public class Comment : Entity
{
    public const int BodyMaxLength = 4000;

    private Comment()
    {
        Body = null!;
    }

    internal Comment(Guid taskId, Guid authorId, string body, DateTime now)
    {
        TaskId = taskId;
        AuthorId = authorId;
        Body = Guard.Required(body, "Comment", BodyMaxLength);
        CreatedAt = now;
    }

    public Guid TaskId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? EditedAt { get; private set; }

    public void Edit(Guid editorId, string body, DateTime now)
    {
        if (editorId != AuthorId)
            throw new DomainException("Only the author can edit a comment.");
        Body = Guard.Required(body, "Comment", BodyMaxLength);
        EditedAt = now;
    }
}
