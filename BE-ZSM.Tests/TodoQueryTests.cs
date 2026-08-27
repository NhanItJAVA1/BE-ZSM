using System.Net;
using BE_ZSM.Enums;

namespace BE_ZSM.Tests;

public sealed class TodoQueryTests : IntegrationTestBase
{
    [Fact]
    public async Task GetTodos_SearchByTitleOrDescription_ShouldReturnMatchingTodos()
    {
        await CreateTodoAsync(UserAClient, "alpha title", "plain description");
        await CreateTodoAsync(UserAClient, "Plain title", "contains alpha keyword");
        await CreateTodoAsync(UserAClient, "Beta title", "different");

        var response = await UserAClient.GetAsync("/api/todos?search=alpha&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal(2, json.GetProperty("totalItems").GetInt32());
        Assert.All(json.GetProperty("items").EnumerateArray(), todo =>
            Assert.Contains("alpha", $"{todo.GetProperty("title").GetString()} {todo.GetProperty("description").GetString()}",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetTodos_FilterByStatus_ShouldReturnOnlyMatchingStatus()
    {
        var todo = await CreateTodoAsync(UserAClient, "Done filtered todo");
        await CreateTodoAsync(UserAClient, "Todo filtered todo");
        await UserAClient.PatchAsJsonAsync($"/api/todos/{Id(todo)}/status", new { status = TodoStatus.Done }, JsonOptions);

        var response = await UserAClient.GetAsync("/api/todos?status=Done");

        var json = await ReadJsonAsync(response);
        Assert.Equal(1, json.GetProperty("totalItems").GetInt32());
        Assert.Equal("Done filtered todo", json.GetProperty("items")[0].GetProperty("title").GetString());
        Assert.Equal("Done", json.GetProperty("items")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetTodos_FilterByPriority_ShouldReturnOnlyMatchingPriority()
    {
        await CreateTodoAsync(UserAClient, "Low priority filtered", priority: TodoPriority.Low);
        await CreateTodoAsync(UserAClient, "High priority filtered", priority: TodoPriority.High);

        var response = await UserAClient.GetAsync("/api/todos?priority=High");

        var json = await ReadJsonAsync(response);
        Assert.Equal(1, json.GetProperty("totalItems").GetInt32());
        Assert.Equal("High priority filtered", json.GetProperty("items")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetTodos_FilterByCategoryId_ShouldReturnOnlyMatchingCategory()
    {
        var category = await CreateCategoryAsync(UserAClient, "Filter category");
        await CreateTodoAsync(UserAClient, "In category", categoryId: Id(category));
        await CreateTodoAsync(UserAClient, "Outside category");

        var response = await UserAClient.GetAsync($"/api/todos?categoryId={Id(category)}");

        var json = await ReadJsonAsync(response);
        Assert.Equal(1, json.GetProperty("totalItems").GetInt32());
        Assert.Equal("In category", json.GetProperty("items")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetTodos_FilterByIsOverdue_ShouldReturnOnlyOverdueOpenTodos()
    {
        await CreateTodoAsync(UserAClient, "Overdue filtered", dueDate: DateTime.UtcNow.AddDays(-1));
        await CreateTodoAsync(UserAClient, "Future filtered", dueDate: DateTime.UtcNow.AddDays(1));

        var response = await UserAClient.GetAsync("/api/todos?isOverdue=true");

        var json = await ReadJsonAsync(response);
        Assert.Equal(1, json.GetProperty("totalItems").GetInt32());
        Assert.Equal("Overdue filtered", json.GetProperty("items")[0].GetProperty("title").GetString());
        Assert.True(json.GetProperty("items")[0].GetProperty("isOverdue").GetBoolean());
    }

    [Fact]
    public async Task GetTodos_SortByTitleAscending_ShouldReturnSortedResults()
    {
        await CreateTodoAsync(UserAClient, "Charlie sort");
        await CreateTodoAsync(UserAClient, "Alpha sort");
        await CreateTodoAsync(UserAClient, "Bravo sort");

        var response = await UserAClient.GetAsync("/api/todos?sortBy=title&isDescending=false&pageSize=10");

        var json = await ReadJsonAsync(response);
        var titles = json.GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString())
            .ToList();

        Assert.Equal(new[] { "Alpha sort", "Bravo sort", "Charlie sort" }, titles);
    }

    [Fact]
    public async Task GetTodos_Pagination_ShouldReturnRequestedPage()
    {
        await CreateTodoAsync(UserAClient, "Paging C");
        await CreateTodoAsync(UserAClient, "Paging A");
        await CreateTodoAsync(UserAClient, "Paging B");

        var response = await UserAClient.GetAsync("/api/todos?sortBy=title&page=2&pageSize=1");

        var json = await ReadJsonAsync(response);
        Assert.Equal(2, json.GetProperty("page").GetInt32());
        Assert.Equal(1, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, json.GetProperty("totalItems").GetInt32());
        Assert.Equal(3, json.GetProperty("totalPages").GetInt32());
        Assert.Equal("Paging B", json.GetProperty("items")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetTodos_PageAndPageSizeBoundaries_ShouldNormalizeValues()
    {
        await CreateTodoAsync(UserAClient, "Boundary todo");

        var lowResponse = await UserAClient.GetAsync("/api/todos?page=0&pageSize=0");
        var highResponse = await UserAClient.GetAsync("/api/todos?pageSize=101");

        var low = await ReadJsonAsync(lowResponse);
        var high = await ReadJsonAsync(highResponse);

        Assert.Equal(1, low.GetProperty("page").GetInt32());
        Assert.Equal(10, low.GetProperty("pageSize").GetInt32());
        Assert.Equal(100, high.GetProperty("pageSize").GetInt32());
    }
}
