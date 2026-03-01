namespace Application.Mappers;

public static class ReminderMapper
{
    public static ReminderDto ToDto(this Reminder entity) =>
        new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TodoItemId = entity.TodoItemId,
            Type = entity.Type,
            RemindAt = entity.RemindAt,
            CronExpression = entity.CronExpression,
            Message = entity.Message,
            IsActive = entity.IsActive,
            LastFiredAt = entity.LastFiredAt,
        };
}
