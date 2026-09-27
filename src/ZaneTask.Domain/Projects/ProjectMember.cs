namespace ZaneTask.Domain.Projects;

public class ProjectMember
{
    private ProjectMember() { }

    internal ProjectMember(Guid projectId, Guid userId, ProjectRole role, DateTime joinedAt)
    {
        ProjectId = projectId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public ProjectRole Role { get; internal set; }
    public DateTime JoinedAt { get; private set; }
}
