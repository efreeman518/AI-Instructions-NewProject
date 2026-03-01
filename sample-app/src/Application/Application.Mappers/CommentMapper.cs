namespace Application.Mappers;

public static class CommentMapper
{
    public static CommentDto ToDto(this Comment entity) =>
        new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TodoItemId = entity.TodoItemId,
            Text = entity.Text,
            AuthorId = entity.AuthorId,
            CreatedAt = entity.CreatedAt,
        };
}
