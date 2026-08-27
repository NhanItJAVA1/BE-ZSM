using System.Net;
using BE_ZSM.Enums;

namespace BE_ZSM.Tests;

public sealed class TodoActivityTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateTodos_ShouldCreateCreatedActivity()
    {
        var todo = await CreateTodoAsync(UserAClient, "Activity create todo");

        var response = await UserAClient.GetAsync($"/api/todos/{Id(todo)}/activities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var activities = await ReadJsonAsync(response);
        var activity = Assert.Single(activities.EnumerateArray());
        Assert.Equal("Created", activity.GetProperty("type").GetString());
        Assert.Equal("Todo created", activity.GetProperty("description").GetString());
    }

    [Fact]
    public async Task UpdateTodo_ChangingPriority_ShouldCreateUpdatedActivity()
    {
        var todo = await CreateTodoAsync(UserAClient, "Priority activity todo", priority: TodoPriority.Low);

        var update = await UserAClient.PutAsJsonAsync($"/api/todos/{Id(todo)}",
            TodoRequest("Priority activity todo", priority: TodoPriority.High),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var activities = await ReadJsonAsync(await UserAClient.GetAsync($"/api/todos/{Id(todo)}/activities"));
        Assert.Contains(activities.EnumerateArray(), a =>
            a.GetProperty("type").GetString() == "Updated" &&
            a.GetProperty("description").GetString() == "Priority changed from Low to High");
    }

    [Fact]
    public async Task UpdateStatus_ChangingStatus_ShouldCreateStatusChangedActivity()
    {
        var todo = await CreateTodoAsync(UserAClient, "Status activity todo");

        var update = await UserAClient.PatchAsJsonAsync($"/api/todos/{Id(todo)}/status", new { status = TodoStatus.InProgress }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var activities = await ReadJsonAsync(await UserAClient.GetAsync($"/api/todos/{Id(todo)}/activities"));
        Assert.Contains(activities.EnumerateArray(), a =>
            a.GetProperty("type").GetString() == "StatusChanged" &&
            a.GetProperty("description").GetString() == "Status changed from Todo to InProgress");
    }

    [Fact]
    public async Task UpdateTodo_ChangingCategory_ShouldCreateCategoryChangedActivity()
    {
        var firstCategory = await CreateCategoryAsync(UserAClient, "First activity category");
        var secondCategory = await CreateCategoryAsync(UserAClient, "Second activity category");
        var todo = await CreateTodoAsync(UserAClient, "Category activity todo", priority: TodoPriority.Medium, categoryId: Id(firstCategory));

        var update = await UserAClient.PutAsJsonAsync($"/api/todos/{Id(todo)}",
            TodoRequest("Category activity todo", priority: TodoPriority.Medium, categoryId: Id(secondCategory)),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var activities = await ReadJsonAsync(await UserAClient.GetAsync($"/api/todos/{Id(todo)}/activities"));
        Assert.Contains(activities.EnumerateArray(), a =>
            a.GetProperty("type").GetString() == "CategoryChanged" &&
            a.GetProperty("description").GetString() == "Category changed");
    }

    [Fact]
    public async Task GetActivities_ShouldReturnNewestFirst()
    {
        var todo = await CreateTodoAsync(UserAClient, "Activity order todo", priority: TodoPriority.Low);
        await Task.Delay(5);
        await UserAClient.PutAsJsonAsync($"/api/todos/{Id(todo)}", TodoRequest("Activity order todo", priority: TodoPriority.High), JsonOptions);
        await Task.Delay(5);
        await UserAClient.PatchAsJsonAsync($"/api/todos/{Id(todo)}/status", new { status = TodoStatus.InProgress }, JsonOptions);

        var activities = await ReadJsonAsync(await UserAClient.GetAsync($"/api/todos/{Id(todo)}/activities"));
        var types = activities.EnumerateArray().Select(a => a.GetProperty("type").GetString()).ToList();

        Assert.Equal(new[] { "StatusChanged", "Updated", "Created" }, types);
    }

    [Fact]
    public async Task GetActivities_FromAnotherUser_ShouldReturnForbidden()
    {
        var userBTodo = await CreateTodoAsync(UserBClient, "Foreign activity todo");

        var response = await UserAClient.GetAsync($"/api/todos/{Id(userBTodo)}/activities");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("TODO_FORBIDDEN", ErrorCode(json));
    }
}
