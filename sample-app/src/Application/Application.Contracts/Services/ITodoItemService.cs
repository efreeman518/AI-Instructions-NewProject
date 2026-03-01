namespace Application.Contracts.Services;

public interface ITodoItemService
{
    Task<PagedResponse<TodoItemDto>> SearchAsync(SearchRequest<TodoItemSearchFilter> request, CancellationToken cancellationToken = default);
    Task<Result<TodoItemDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TodoItemDto>> CreateAsync(TodoItemDto dto, CancellationToken cancellationToken = default);
    Task<Result<TodoItemDto>> UpdateAsync(TodoItemDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // State machine actions
    Task<Result<TodoItemDto>> StartAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TodoItemDto>> CompleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TodoItemDto>> BlockAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TodoItemDto>> UnblockAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TodoItemDto>> CancelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TodoItemDto>> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TodoItemDto>> RestoreAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TodoItemDto>> ReopenAsync(Guid id, CancellationToken cancellationToken = default);

    // Assignment
    Task<Result<TodoItemDto>> AssignAsync(Guid id, Guid? assignedToId, CancellationToken cancellationToken = default);

    // Comments
    Task<Result<CommentDto>> AddCommentAsync(Guid todoItemId, CommentDto comment, CancellationToken cancellationToken = default);
}
