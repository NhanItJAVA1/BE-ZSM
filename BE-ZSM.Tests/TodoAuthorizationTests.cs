using System.Net;
using BE_ZSM.Enums;

namespace BE_ZSM.Tests;

public sealed class TodoAuthorizationTests : IntegrationTestBase
{
    [Fact]
    public async Task TodoEndpoints_WithoutJwt_ShouldReturnUnauthorized()
    {
        using var anonymousClient = Factory.CreateClient();

        var getResponse = await anonymousClient.GetAsync("/api/todos");
        var postResponse = await anonymousClient.PostAsJsonAsync("/api/todos", new[] { TodoRequest("Anonymous todo") }, JsonOptions);
        var putResponse = await anonymousClient.PutAsJsonAsync("/api/todos/1", TodoRequest("Anonymous update", priority: TodoPriority.Low), JsonOptions);
        var patchResponse = await anonymousClient.PatchAsJsonAsync("/api/todos/1/status", new { status = TodoStatus.Done }, JsonOptions);
        var deleteResponse = await anonymousClient.DeleteAsync("/api/todos/1");
        var activitiesResponse = await anonymousClient.GetAsync("/api/todos/1/activities");

        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, putResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, patchResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, activitiesResponse.StatusCode);
    }

    [Fact]
    public async Task GetTodo_AnotherUsersTodo_ShouldReturnNotFound()
    {
        var userBTodo = await CreateTodoAsync(UserBClient, "User B private todo");

        var response = await UserAClient.GetAsync($"/api/todos/{Id(userBTodo)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("TODO_NOT_FOUND", ErrorCode(json));
    }

    [Fact]
    public async Task UpdateTodo_AnotherUsersTodo_ShouldReturnForbidden()
    {
        var userBTodo = await CreateTodoAsync(UserBClient, "User B update protected todo");

        var response = await UserAClient.PutAsJsonAsync($"/api/todos/{Id(userBTodo)}",
            TodoRequest("Cross-user update", priority: TodoPriority.High),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("TODO_FORBIDDEN", ErrorCode(json));
    }

    [Fact]
    public async Task DeleteTodo_AnotherUsersTodo_ShouldReturnForbidden()
    {
        var userBTodo = await CreateTodoAsync(UserBClient, "User B delete protected todo");

        var response = await UserAClient.DeleteAsync($"/api/todos/{Id(userBTodo)}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("TODO_FORBIDDEN", ErrorCode(json));
    }

    [Fact]
    public async Task UpdateStatus_AnotherUsersTodo_ShouldReturnForbidden()
    {
        var userBTodo = await CreateTodoAsync(UserBClient, "User B status protected todo");

        var response = await UserAClient.PatchAsJsonAsync($"/api/todos/{Id(userBTodo)}/status", new { status = TodoStatus.Done }, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("TODO_FORBIDDEN", ErrorCode(json));
    }
}
