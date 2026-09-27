using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZaneTask.Contracts;

namespace ZaneTask.Web.Services;

/// <summary>Typed wrapper around the ZaneTask REST API. Non-success responses throw <see cref="ApiException"/>.</summary>
public sealed class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // Auth
    public Task<AuthResponse> RegisterAsync(RegisterRequest request) => SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/register", request);
    public Task<AuthResponse> LoginAsync(LoginRequest request) => SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/login", request);

    // Projects
    public Task<List<ProjectSummaryDto>> GetProjectsAsync() => SendAsync<List<ProjectSummaryDto>>(HttpMethod.Get, "api/projects");
    public Task<ProjectDto> GetProjectAsync(Guid id) => SendAsync<ProjectDto>(HttpMethod.Get, $"api/projects/{id}");
    public Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request) => SendAsync<ProjectDto>(HttpMethod.Post, "api/projects", request);
    public Task<ProjectDto> UpdateProjectAsync(Guid id, UpdateProjectRequest request) => SendAsync<ProjectDto>(HttpMethod.Put, $"api/projects/{id}", request);
    public Task DeleteProjectAsync(Guid id) => SendAsync(HttpMethod.Delete, $"api/projects/{id}");

    public Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddMemberRequest request) =>
        SendAsync<ProjectMemberDto>(HttpMethod.Post, $"api/projects/{projectId}/members", request);
    public Task<ProjectMemberDto> ChangeMemberRoleAsync(Guid projectId, Guid userId, ProjectRole role) =>
        SendAsync<ProjectMemberDto>(HttpMethod.Put, $"api/projects/{projectId}/members/{userId}", new ChangeMemberRoleRequest(role));
    public Task RemoveMemberAsync(Guid projectId, Guid userId) => SendAsync(HttpMethod.Delete, $"api/projects/{projectId}/members/{userId}");

    public Task<LabelDto> CreateLabelAsync(Guid projectId, SaveLabelRequest request) =>
        SendAsync<LabelDto>(HttpMethod.Post, $"api/projects/{projectId}/labels", request);
    public Task<LabelDto> UpdateLabelAsync(Guid projectId, Guid labelId, SaveLabelRequest request) =>
        SendAsync<LabelDto>(HttpMethod.Put, $"api/projects/{projectId}/labels/{labelId}", request);
    public Task DeleteLabelAsync(Guid projectId, Guid labelId) => SendAsync(HttpMethod.Delete, $"api/projects/{projectId}/labels/{labelId}");

    // Tasks
    public Task<List<MyTaskDto>> GetMyTasksAsync(bool includeDone) =>
        SendAsync<List<MyTaskDto>>(HttpMethod.Get, $"api/me/tasks?includeDone={(includeDone ? "true" : "false")}");
    public Task<List<TaskDto>> GetTasksAsync(Guid projectId) => SendAsync<List<TaskDto>>(HttpMethod.Get, $"api/projects/{projectId}/tasks");
    public Task<TaskDto> CreateTaskAsync(Guid projectId, CreateTaskRequest request) =>
        SendAsync<TaskDto>(HttpMethod.Post, $"api/projects/{projectId}/tasks", request);
    public Task<TaskDto> UpdateTaskAsync(Guid taskId, UpdateTaskRequest request) => SendAsync<TaskDto>(HttpMethod.Put, $"api/tasks/{taskId}", request);
    public Task DeleteTaskAsync(Guid taskId) => SendAsync(HttpMethod.Delete, $"api/tasks/{taskId}");
    public Task<TaskDto> AssignTaskAsync(Guid taskId, Guid? assigneeId) =>
        SendAsync<TaskDto>(HttpMethod.Put, $"api/tasks/{taskId}/assignee", new AssignTaskRequest(assigneeId));
    public Task<TaskDto> MoveTaskAsync(Guid taskId, TaskItemStatus status, int position) =>
        SendAsync<TaskDto>(HttpMethod.Put, $"api/tasks/{taskId}/move", new MoveTaskRequest(status, position));
    public Task<TaskDto> AddTaskLabelAsync(Guid taskId, Guid labelId) => SendAsync<TaskDto>(HttpMethod.Put, $"api/tasks/{taskId}/labels/{labelId}");
    public Task<TaskDto> RemoveTaskLabelAsync(Guid taskId, Guid labelId) => SendAsync<TaskDto>(HttpMethod.Delete, $"api/tasks/{taskId}/labels/{labelId}");

    // Checklist (every change returns the whole list)
    public Task<List<ChecklistItemDto>> GetChecklistAsync(Guid taskId) =>
        SendAsync<List<ChecklistItemDto>>(HttpMethod.Get, $"api/tasks/{taskId}/checklist");
    public Task<List<ChecklistItemDto>> AddChecklistItemAsync(Guid taskId, string text) =>
        SendAsync<List<ChecklistItemDto>>(HttpMethod.Post, $"api/tasks/{taskId}/checklist", new AddChecklistItemRequest(text));
    public Task<List<ChecklistItemDto>> UpdateChecklistItemAsync(Guid itemId, UpdateChecklistItemRequest request) =>
        SendAsync<List<ChecklistItemDto>>(HttpMethod.Put, $"api/checklist/{itemId}", request);
    public Task<List<ChecklistItemDto>> MoveChecklistItemAsync(Guid itemId, int position) =>
        SendAsync<List<ChecklistItemDto>>(HttpMethod.Put, $"api/checklist/{itemId}/move", new MoveChecklistItemRequest(position));
    public Task<List<ChecklistItemDto>> DeleteChecklistItemAsync(Guid itemId) =>
        SendAsync<List<ChecklistItemDto>>(HttpMethod.Delete, $"api/checklist/{itemId}");

    // Comments
    public Task<List<CommentDto>> GetCommentsAsync(Guid taskId) => SendAsync<List<CommentDto>>(HttpMethod.Get, $"api/tasks/{taskId}/comments");
    public Task<CommentDto> AddCommentAsync(Guid taskId, string body) =>
        SendAsync<CommentDto>(HttpMethod.Post, $"api/tasks/{taskId}/comments", new SaveCommentRequest(body));
    public Task<CommentDto> UpdateCommentAsync(Guid commentId, string body) =>
        SendAsync<CommentDto>(HttpMethod.Put, $"api/comments/{commentId}", new SaveCommentRequest(body));
    public Task DeleteCommentAsync(Guid commentId) => SendAsync(HttpMethod.Delete, $"api/comments/{commentId}");

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var response = await SendCoreAsync(method, url, body);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var response = await SendCoreAsync(method, url, body);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string url, object? body)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            throw ApiException.Network();
        }

        if (!response.IsSuccessStatusCode)
        {
            using (response)
                throw await ApiException.FromResponseAsync(response);
        }
        return response;
    }
}
