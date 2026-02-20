// Pattern: Mapper for time-based entity — Reminder.

using System.Linq.Expressions;
using Domain.Model.Entities;
using Application.Models.Reminder;

namespace Application.Contracts.Mappers;

public static class ReminderMapper
{
    public static ReminderDto ToDto(this Reminder entity) => new()
    {
        Id = entity.Id,
        TodoItemId = entity.TodoItemId,
        ReminderType = entity.ReminderType,
        ReminderDateUtc = entity.ReminderDateUtc,
        CronExpression = entity.CronExpression,
        Message = entity.Message,
        IsActive = entity.IsActive,
        LastFiredUtc = entity.LastFiredUtc
    };

    public static Expression<Func<Reminder, ReminderDto>> ProjectorSearch =>
        entity => new ReminderDto
        {
            Id = entity.Id,
            TodoItemId = entity.TodoItemId,
            ReminderType = entity.ReminderType,
            ReminderDateUtc = entity.ReminderDateUtc,
            CronExpression = entity.CronExpression,
            Message = entity.Message,
            IsActive = entity.IsActive,
            LastFiredUtc = entity.LastFiredUtc
        };
}
