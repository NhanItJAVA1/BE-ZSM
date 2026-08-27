using System.Net;
using BE_ZSM.Enums;

namespace BE_ZSM.Tests;

public sealed class TodoCategoryApiTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateCategory_WithValidData_ShouldReturnSuccess()
    {
        var response = await UserAClient.PostAsJsonAsync("/api/todo-categories", new { name = "Personal" }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("Category created successfully", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetCategories_ShouldReturnOnlyCurrentUsersCategories()
    {
        await CreateCategoryAsync(UserAClient, "User A category");
        await CreateCategoryAsync(UserBClient, "User B category");

        var response = await UserAClient.GetAsync("/api/todo-categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = await ReadJsonAsync(response);
        Assert.Contains(categories.EnumerateArray(), c => c.GetProperty("name").GetString() == "User A category");
        Assert.DoesNotContain(categories.EnumerateArray(), c => c.GetProperty("name").GetString() == "User B category");
    }

    [Fact]
    public async Task GetCategory_ByExistingId_ShouldReturnCategory()
    {
        var category = await CreateCategoryAsync(UserAClient, "Get category");

        var response = await UserAClient.GetAsync($"/api/todo-categories/{Id(category)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal(Id(category), Id(json));
        Assert.Equal("Get category", json.GetProperty("name").GetString());
    }

    [Fact]
    public async Task UpdateCategory_WithValidData_ShouldUpdateCategory()
    {
        var category = await CreateCategoryAsync(UserAClient, "Before category update");

        var response = await UserAClient.PutAsJsonAsync($"/api/todo-categories/{Id(category)}", new { name = "After category update" }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var getResponse = await UserAClient.GetAsync($"/api/todo-categories/{Id(category)}");
        var json = await ReadJsonAsync(getResponse);
        Assert.Equal("After category update", json.GetProperty("name").GetString());
    }

    [Fact]
    public async Task DeleteCategory_WithExistingCategory_ShouldDeleteCategory()
    {
        var category = await CreateCategoryAsync(UserAClient, "Delete category");

        var response = await UserAClient.DeleteAsync($"/api/todo-categories/{Id(category)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var getResponse = await UserAClient.GetAsync($"/api/todo-categories/{Id(category)}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task CategoryEndpoints_WithoutJwt_ShouldReturnUnauthorized()
    {
        using var anonymousClient = Factory.CreateClient();

        var getResponse = await anonymousClient.GetAsync("/api/todo-categories");
        var postResponse = await anonymousClient.PostAsJsonAsync("/api/todo-categories", new { name = "Anonymous" }, JsonOptions);
        var putResponse = await anonymousClient.PutAsJsonAsync("/api/todo-categories/1", new { name = "Anonymous" }, JsonOptions);
        var deleteResponse = await anonymousClient.DeleteAsync("/api/todo-categories/1");

        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, putResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task GetCategory_AnotherUsersCategory_ShouldReturnNotFound()
    {
        var category = await CreateCategoryAsync(UserBClient, "Foreign category");

        var response = await UserAClient.GetAsync($"/api/todo-categories/{Id(category)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("CATEGORY_NOT_FOUND", ErrorCode(json));
    }

    [Fact]
    public async Task UpdateCategory_NonexistentCategory_ShouldReturnNotFound()
    {
        var response = await UserAClient.PutAsJsonAsync("/api/todo-categories/999999", new { name = "Missing" }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("CATEGORY_NOT_FOUND", ErrorCode(json));
    }

    [Fact]
    public async Task UpdateCategory_AnotherUsersCategory_ShouldReturnNotFound()
    {
        var category = await CreateCategoryAsync(UserBClient, "Foreign update category");

        var response = await UserAClient.PutAsJsonAsync($"/api/todo-categories/{Id(category)}", new { name = "Illegal update" }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("CATEGORY_NOT_FOUND", ErrorCode(json));
    }

    [Fact]
    public async Task DeleteCategory_NonexistentCategory_ShouldReturnNotFound()
    {
        var response = await UserAClient.DeleteAsync("/api/todo-categories/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("CATEGORY_NOT_FOUND", ErrorCode(json));
    }

    [Fact]
    public async Task DeleteCategory_AnotherUsersCategory_ShouldReturnNotFound()
    {
        var category = await CreateCategoryAsync(UserBClient, "Foreign delete category");

        var response = await UserAClient.DeleteAsync($"/api/todo-categories/{Id(category)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("CATEGORY_NOT_FOUND", ErrorCode(json));
    }

    [Fact]
    public async Task DeleteCategory_WithDeleteTodosFalse_ShouldKeepTodosAndClearCategoryId()
    {
        var category = await CreateCategoryAsync(UserAClient, "Clear category");
        var todo = await CreateTodoAsync(UserAClient, "Clear category todo", priority: TodoPriority.Low, categoryId: Id(category));

        var response = await UserAClient.DeleteAsync($"/api/todo-categories/{Id(category)}?deleteTodos=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todoResponse = await UserAClient.GetAsync($"/api/todos/{Id(todo)}");
        Assert.Equal(HttpStatusCode.OK, todoResponse.StatusCode);
        var updatedTodo = await ReadJsonAsync(todoResponse);
        Assert.True(updatedTodo.GetProperty("categoryId").ValueKind is System.Text.Json.JsonValueKind.Null);
        Assert.True(updatedTodo.GetProperty("categoryName").ValueKind is System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task DeleteCategory_WithDeleteTodosTrue_ShouldDeleteAssociatedTodos()
    {
        var category = await CreateCategoryAsync(UserAClient, "Delete todos category");
        var todo = await CreateTodoAsync(UserAClient, "Deleted with category todo", priority: TodoPriority.High, categoryId: Id(category));

        var response = await UserAClient.DeleteAsync($"/api/todo-categories/{Id(category)}?deleteTodos=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todoResponse = await UserAClient.GetAsync($"/api/todos/{Id(todo)}");
        Assert.Equal(HttpStatusCode.NotFound, todoResponse.StatusCode);
    }
}
