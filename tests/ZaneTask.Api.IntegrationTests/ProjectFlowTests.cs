using System.Net;
using System.Net.Http.Json;
using ZaneTask.Contracts;
using static ZaneTask.Api.IntegrationTests.ApiFactory;

namespace ZaneTask.Api.IntegrationTests;

public sealed class ProjectFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Team_can_plan_and_work_a_task_end_to_end()
    {
        var (alice, _) = await factory.RegisterAsync("Alice");
        var (bob, bobUser) = await factory.RegisterAsync("Bob");
        var (carol, _) = await factory.RegisterAsync("Carol");

        // Alice creates a project and invites Bob.
        var create = await alice.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Website", "Relaunch"), Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var project = (await create.Content.ReadFromJsonAsync<ProjectDto>(Json))!;

        var addBob = await alice.PostAsJsonAsync($"/api/projects/{project.Id}/members", new AddMemberRequest(bobUser.Email), Json);
        Assert.Equal(HttpStatusCode.OK, addBob.StatusCode);

        var label = await PostAsync<LabelDto>(alice, $"/api/projects/{project.Id}/labels", new SaveLabelRequest("Design", "#8E24AA"));

        // Alice creates a task for Bob.
        var task = await PostAsync<TaskDto>(alice, $"/api/projects/{project.Id}/tasks",
            new CreateTaskRequest("Landing page", null, Priority: TaskPriority.High, AssigneeId: bobUser.Id, LabelIds: [label.Id]));
        Assert.Equal(bobUser.Id, task.Assignee?.Id);
        Assert.Equal([label], task.Labels);

        // Bob picks it up on the board and comments.
        var move = await bob.PutAsJsonAsync($"/api/tasks/{task.Id}/move", new MoveTaskRequest(TaskItemStatus.InProgress, 0), Json);
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);

        var comment = await PostAsync<CommentDto>(bob, $"/api/tasks/{task.Id}/comments", new SaveCommentRequest("On it"));
        Assert.Equal("Bob", comment.Author.DisplayName);

        var board = await alice.GetFromJsonAsync<List<TaskDto>>($"/api/projects/{project.Id}/tasks", Json);
        var onBoard = Assert.Single(board!);
        Assert.Equal(TaskItemStatus.InProgress, onBoard.Status);
        Assert.Equal(1, onBoard.CommentCount);

        // Carol is not a member and cannot see any of it.
        Assert.Equal(HttpStatusCode.NotFound, (await carol.GetAsync($"/api/projects/{project.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await carol.GetAsync($"/api/tasks/{task.Id}")).StatusCode);
        Assert.Empty((await carol.GetFromJsonAsync<List<ProjectSummaryDto>>("/api/projects", Json))!);

        // Bob is a member, not an owner.
        var rename = await bob.PutAsJsonAsync($"/api/projects/{project.Id}", new UpdateProjectRequest("Mine now", null), Json);
        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
    }

    [Fact]
    public async Task Business_rule_violations_are_bad_requests()
    {
        var (alice, _) = await factory.RegisterAsync("Alice");
        var (_, outsider) = await factory.RegisterAsync("Outsider");
        var project = await PostAsync<ProjectDto>(alice, "/api/projects", new CreateProjectRequest("P", null));

        var response = await alice.PostAsJsonAsync($"/api/projects/{project.Id}/tasks",
            new CreateTaskRequest("T", null, AssigneeId: outsider.Id), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("project members", await response.Content.ReadAsStringAsync());
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, Json);
        Assert.True(response.IsSuccessStatusCode, $"{url} -> {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
