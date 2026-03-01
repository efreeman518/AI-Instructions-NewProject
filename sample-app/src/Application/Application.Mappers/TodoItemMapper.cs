namespace Application.Mappers;

public static class TodoItemMapper
{
    public static TodoItemDto ToDto(this TodoItem entity) =>
        new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Title = entity.Title,
            Description = entity.Description,
            Priority = entity.Priority,
            Status = entity.Status,
            EstimatedHours = entity.EstimatedHours,
            ActualHours = entity.ActualHours,
            StartDate = entity.Schedule?.StartDate,
            DueDate = entity.Schedule?.DueDate,
            ParentId = entity.ParentId,
            CategoryId = entity.CategoryId,
            AssignedToId = entity.AssignedToId,
            TeamId = entity.TeamId,
            Category = entity.Category?.ToDto(),
            AssignedTo = entity.AssignedTo?.ToDto(),
            Comments = entity.Comments?.Select(c => c.ToDto()).ToList() ?? [],
            Reminders = entity.Reminders?.Select(r => r.ToDto()).ToList() ?? [],
            Tags = entity.Tags?.Select(t => t.ToDto()).ToList() ?? [],
            Attachments = entity.Attachments?.Select(a => a.ToDto()).ToList() ?? [],
        };
}
