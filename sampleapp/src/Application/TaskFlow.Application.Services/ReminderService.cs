// Pattern: Auxiliary service — Reminder with scheduler integration.
// Demonstrates the ProcessDueRemindersAsync pattern used by TickerQ jobs.

using Microsoft.Extensions.Logging;
using Package.Infrastructure.Common;
using Application.Contracts.Repositories;
using Application.Contracts.Services;
using Application.Models.Reminder;

namespace Application.Services;

internal class ReminderService(
    ILogger<ReminderService> logger,
    IReminderRepositoryQuery repoQuery,
    IReminderRepositoryTrxn repoTrxn) : IReminderService
{
    public async Task<Result<IReadOnlyList<ReminderDto>>> GetByTodoItemIdAsync(
        Guid todoItemId, CancellationToken ct = default)
    {
        var reminders = await repoQuery.GetDueRemindersAsync(DateTimeOffset.MaxValue, ct);
        // Pattern: Filter in-memory for simplicity; in production, add a dedicated repo method.
        var filtered = reminders.Where(r => r.TodoItemId == todoItemId).ToList();
        return Result<IReadOnlyList<ReminderDto>>.Success(filtered);
    }

    public async Task<Result<ReminderDto>> CreateAsync(ReminderDto dto, CancellationToken ct = default)
    {
        var entity = Domain.Model.Entities.Reminder.Create(
            dto.TodoItemId, dto.ReminderType, dto.ReminderDateUtc, dto.CronExpression, dto.Message);

        repoTrxn.Add(entity);
        await repoTrxn.SaveChangesAsync(ct);

        logger.LogInformation("Reminder {Id} created for TodoItem {TodoItemId}", entity.Id, dto.TodoItemId);
        return Result<ReminderDto>.Success(Application.Contracts.Mappers.ReminderMapper.ToDto(entity));
    }

    public async Task<Result> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetByIdAsync(id, ct);
        if (entity is null) return Result.Success(); // idempotent

        entity.Deactivate();
        await repoTrxn.SaveChangesAsync(ct);

        logger.LogInformation("Reminder {Id} deactivated", id);
        return Result.Success();
    }

    /// <summary>
    /// Pattern: Scheduler-facing method — called by TickerQ recurring job.
    /// Fetches all due reminders, marks them fired, returns them for notification dispatch.
    /// This method is transactional — fetches and updates in one SaveChanges call.
    /// </summary>
    public async Task<Result<IReadOnlyList<ReminderDto>>> ProcessDueRemindersAsync(
        CancellationToken ct = default)
    {
        var dueReminders = await repoQuery.GetDueRemindersAsync(DateTimeOffset.UtcNow, ct);
        if (dueReminders.Count == 0)
            return Result<IReadOnlyList<ReminderDto>>.Success([]);

        // Pattern: Batch process — fetch tracked entities and update state.
        var processedDtos = new List<ReminderDto>();
        foreach (var dto in dueReminders)
        {
            var entity = await repoTrxn.GetByIdAsync(dto.Id, ct);
            if (entity is null) continue;

            entity.MarkFired();
            processedDtos.Add(Application.Contracts.Mappers.ReminderMapper.ToDto(entity));
        }

        await repoTrxn.SaveChangesAsync(ct);

        logger.LogInformation("Processed {Count} due reminders", processedDtos.Count);
        return Result<IReadOnlyList<ReminderDto>>.Success(processedDtos);
    }
}
