using System.Net;
using BE_ZSM.Enums;

namespace BE_ZSM.Tests;

public sealed class TodoApiTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateTodos_WithValidData_ShouldReturnSuccess()
    {
        var response = await UserAClient.PostAsJsonAsync("/api/todos", new[]
        {
            TodoRequest("Create API todo", "created through API", TodoPriority.High, DateTime.UtcNow.AddDays(1))
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("Todos created successfully", json.GetProperty("message").GetString());

        var todo = await GetSingleTodoByTitleAsync(UserAClient, "Create API todo");
        Assert.Equal("Create API todo", todo.GetProperty("title").GetString());
        Assert.Equal("created through API", todo.GetProperty("description").GetString());
        Assert.Equal("High", todo.GetProperty("priority").GetString());
        Assert.Equal("Todo", todo.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CreateTodos_WithMultipleValidItems_ShouldCreateAllTodos()
    {
        var response = await UserAClient.PostAsJsonAsync("/api/todos", new[]
        {
            TodoRequest("Bulk todo one", priority: TodoPriority.Low),
            TodoRequest("Bulk todo two", priority: TodoPriority.High)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var todosResponse = await UserAClient.GetAsync("/api/todos?search=Bulk%20todo&pageSize=10");
        var todos = await ReadJsonAsync(todosResponse);

        Assert.Equal(2, todos.GetProperty("totalItems").GetInt32());
        Assert.Contains(todos.GetProperty("items").EnumerateArray(), t => t.GetProperty("title").GetString() == "Bulk todo one");
        Assert.Contains(todos.GetProperty("items").EnumerateArray(), t => t.GetProperty("title").GetString() == "Bulk todo two");
    }

    [Fact]
    public async Task CreateTodos_WithoutPriority_ShouldDefaultToMedium()
    {
        await CreateTodoAsync(UserAClient, "Default priority todo");

        var todo = await GetSingleTodoByTitleAsync(UserAClient, "Default priority todo");

        Assert.Equal("Medium", todo.GetProperty("priority").GetString());
    }

    [Fact]
    public async Task CreateTodos_WithoutCategory_ShouldCreateTodoWithNullCategory()
    {
        var todo = await CreateTodoAsync(UserAClient, "No category todo", priority: TodoPriority.Low);

        Assert.True(todo.GetProperty("categoryId").ValueKind is System.Text.Json.JsonValueKind.Null);
        Assert.True(todo.GetProperty("categoryName").ValueKind is System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task CreateTodos_WithValidCategory_ShouldAttachCategory()
    {
        var category = await CreateCategoryAsync(UserAClient, "Work");

        var todo = await CreateTodoAsync(UserAClient, "Categorized todo", categoryId: Id(category));

        Assert.Equal(Id(category), todo.GetProperty("categoryId").GetInt32());
        Assert.Equal("Work", todo.GetProperty("categoryName").GetString());
    }

    [Fact]
    public async Task CreateTodos_WithInvalidCategory_ShouldReturnNotFound()
    {
        var response = await UserAClient.PostAsJsonAsync("/api/todos", new[]
        {
            TodoRequest("Invalid category todo", categoryId: 999999)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("CATEGORY_NOT_FOUND", ErrorCode(json));
    }

    [Fact]
    public async Task CreateTodos_WithAnotherUsersCategory_ShouldReturnNotFound()
    {
        var category = await CreateCategoryAsync(UserBClient, "User B category");

        var response = await UserAClient.PostAsJsonAsync("/api/todos", new[]
        {
            TodoRequest("Foreign category todo", categoryId: Id(category))
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("CATEGORY_NOT_FOUND", ErrorCode(json));
    }

    [Fact]
    public async Task GetTodos_ShouldReturnOnlyCurrentUsersTodos()
    {
        await CreateTodoAsync(UserAClient, "User A visible todo");
        await CreateTodoAsync(UserBClient, "User B hidden todo");

        var response = await UserAClient.GetAsync("/api/todos?pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Contains(json.GetProperty("items").EnumerateArray(), t => t.GetProperty("title").GetString() == "User A visible todo");
        Assert.DoesNotContain(json.GetProperty("items").EnumerateArray(), t => t.GetProperty("title").GetString() == "User B hidden todo");
    }

    [Fact]
    public async Task GetTodo_ByExistingId_ShouldReturnTodo()
    {
        var created = await CreateTodoAsync(UserAClient, "Get by id todo");

        var response = await UserAClient.GetAsync($"/api/todos/{Id(created)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todo = await ReadJsonAsync(response);
        Assert.Equal(Id(created), Id(todo));
        Assert.Equal("Get by id todo", todo.GetProperty("title").GetString());
    }

    [Fact]
    public async Task UpdateTodo_WithValidData_ShouldUpdateTodo()
    {
        var created = await CreateTodoAsync(UserAClient, "Original update title", priority: TodoPriority.Low);

        var response = await UserAClient.PutAsJsonAsync($"/api/todos/{Id(created)}",
            TodoRequest("Updated update title", "updated", TodoPriority.High, DateTime.UtcNow.AddDays(2)),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todo = await GetSingleTodoByTitleAsync(UserAClient, "Updated update title");
        Assert.Equal("updated", todo.GetProperty("description").GetString());
        Assert.Equal("High", todo.GetProperty("priority").GetString());
    }

    [Fact]
    public async Task UpdateTodo_WithoutPriority_ShouldReturnConflict()
    {
        var created = await CreateTodoAsync(UserAClient, "Priority required todo", priority: TodoPriority.Low);

        var response = await UserAClient.PutAsJsonAsync($"/api/todos/{Id(created)}",
            TodoRequest("Priority missing update"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("PRIORITY_REQUIRED", ErrorCode(json));
    }

    [Fact]
    public async Task UpdateTodo_NonexistentTodo_ShouldReturnNotFound()
    {
        var response = await UserAClient.PutAsJsonAsync("/api/todos/999999",
            TodoRequest("Missing todo", priority: TodoPriority.Low),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("TODO_NOT_FOUND", ErrorCode(json));
    }

    [Fact]
    public async Task DeleteTodo_WithExistingTodo_ShouldDeleteTodo()
    {
        var created = await CreateTodoAsync(UserAClient, "Delete me", priority: TodoPriority.Low);

        var response = await UserAClient.DeleteAsync($"/api/todos/{Id(created)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var getResponse = await UserAClient.GetAsync($"/api/todos/{Id(created)}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteTodo_NonexistentTodo_ShouldReturnNotFound()
    {
        var response = await UserAClient.DeleteAsync("/api/todos/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("TODO_NOT_FOUND", ErrorCode(json));
    }

    [Fact]
    public async Task UpdateStatus_TodoToInProgress_ShouldSucceed()
    {
        var created = await CreateTodoAsync(UserAClient, "Move to in progress");

        var response = await UserAClient.PatchAsJsonAsync($"/api/todos/{Id(created)}/status", new { status = TodoStatus.InProgress }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todo = await UserAClient.GetAsync($"/api/todos/{Id(created)}");
        var json = await ReadJsonAsync(todo);
        Assert.Equal("InProgress", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateStatus_DoneToTodo_ShouldReturnConflict()
    {
        var created = await CreateTodoAsync(UserAClient, "Invalid done transition");
        await UserAClient.PatchAsJsonAsync($"/api/todos/{Id(created)}/status", new { status = TodoStatus.Done }, JsonOptions);

        var response = await UserAClient.PatchAsJsonAsync($"/api/todos/{Id(created)}/status", new { status = TodoStatus.Todo }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("INVALID_TODO_STATUS_TRANSITION", ErrorCode(json));
    }

    [Fact]
    public async Task UpdateStatus_ToDone_ShouldSetCompletedAt()
    {
        var created = await CreateTodoAsync(UserAClient, "Complete todo");

        var response = await UserAClient.PatchAsJsonAsync($"/api/todos/{Id(created)}/status", new { status = TodoStatus.Done }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todoResponse = await UserAClient.GetAsync($"/api/todos/{Id(created)}");
        var todo = await ReadJsonAsync(todoResponse);
        Assert.Equal("Done", todo.GetProperty("status").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, todo.GetProperty("completedAt").ValueKind);
    }

    [Fact]
    public async Task GetTodo_WithPastDueOpenTodo_ShouldReturnOverdue()
    {
        var created = await CreateTodoAsync(UserAClient, "Overdue todo", dueDate: DateTime.UtcNow.AddDays(-1));

        Assert.True(created.GetProperty("isOverdue").GetBoolean());
        Assert.False(created.GetProperty("isCompletedLate").GetBoolean());
    }

    [Fact]
    public async Task GetTodo_CompletedAfterDueDate_ShouldReturnCompletedLate()
    {
        var created = await CreateTodoAsync(UserAClient, "Completed late todo", dueDate: DateTime.UtcNow.AddSeconds(-1));

        var response = await UserAClient.PatchAsJsonAsync($"/api/todos/{Id(created)}/status", new { status = TodoStatus.Done }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todoResponse = await UserAClient.GetAsync($"/api/todos/{Id(created)}");
        var todo = await ReadJsonAsync(todoResponse);
        Assert.False(todo.GetProperty("isOverdue").GetBoolean());
        Assert.True(todo.GetProperty("isCompletedLate").GetBoolean());
    }
}
