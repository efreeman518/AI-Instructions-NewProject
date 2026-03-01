using Domain.Model;
using Domain.Shared;
using Infrastructure.Data;

namespace Test.Support;

/// <summary>
/// DbContext helpers for creating/clearing test entity data.
/// Pattern from https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.Support/TodoDbContextSupport.cs
/// </summary>
public static class TaskFlowDbContextSupport
{
    /// <summary>
    /// Remove all TodoItem records from the database.
    /// </summary>
    public static void ClearTodoItemData(this TaskFlowDbContextBase db)
    {
        db.Set<TodoItem>().RemoveRange(db.Set<TodoItem>());
    }

    /// <summary>
    /// Remove all Category records from the database.
    /// </summary>
    public static void ClearCategoryData(this TaskFlowDbContextBase db)
    {
        db.Set<Category>().RemoveRange(db.Set<Category>());
    }

    /// <summary>
    /// Remove all Tag records from the database.
    /// </summary>
    public static void ClearTagData(this TaskFlowDbContextBase db)
    {
        db.Set<Tag>().RemoveRange(db.Set<Tag>());
    }

    /// <summary>
    /// Remove all Team records from the database.
    /// </summary>
    public static void ClearTeamData(this TaskFlowDbContextBase db)
    {
        db.Set<Team>().RemoveRange(db.Set<Team>());
    }

    /// <summary>
    /// Seed TodoItem records into the database.
    /// </summary>
    public static void SeedTodoItemData(this TaskFlowDbContextBase db, Guid tenantId, int size = 10)
    {
        db.Set<TodoItem>().AddRange(TodoItemListFactory(tenantId, size));
    }

    /// <summary>
    /// Create a list of TodoItem entities for test data.
    /// </summary>
    public static List<TodoItem> TodoItemListFactory(Guid tenantId, int size = 10)
    {
        var list = new List<TodoItem>();
        for (int i = 0; i < size; i++)
        {
            var result = TodoItem.Create(tenantId, $"a-{Utility.RandomString(10)}", $"Description {i}", priority: 3);
            if (result.IsSuccess)
            {
                list.Add(result.Value!);
            }
        }
        return list;
    }

    /// <summary>
    /// Create a single TodoItem entity for testing.
    /// </summary>
    public static TodoItem TodoItemFactory(Guid tenantId, string name)
    {
        var result = TodoItem.Create(tenantId, name, "Test description", priority: 3);
        return result.Value!;
    }

    /// <summary>
    /// InMemory provider does not understand RowVersion like SQL EF Provider.
    /// </summary>
    public static void ApplyRowVersion(this TodoItem item)
    {
        item.RowVersion = DbSupport.GetRandomByteArray(16);
    }

    /// <summary>
    /// Seed Category records into the database.
    /// </summary>
    public static void SeedCategoryData(this TaskFlowDbContextBase db, Guid tenantId, int size = 3)
    {
        for (int i = 0; i < size; i++)
        {
            var result = Category.Create(tenantId, $"Category-{Utility.RandomString(8)}", $"Desc {i}", "#FF0000", i + 1);
            if (result.IsSuccess)
            {
                db.Set<Category>().Add(result.Value!);
            }
        }
    }

    /// <summary>
    /// Seed Tag records into the database.
    /// </summary>
    public static void SeedTagData(this TaskFlowDbContextBase db, int size = 3)
    {
        for (int i = 0; i < size; i++)
        {
            var result = Tag.Create($"Tag-{Utility.RandomString(8)}", $"Desc {i}");
            if (result.IsSuccess)
            {
                db.Set<Tag>().Add(result.Value!);
            }
        }
    }
}
