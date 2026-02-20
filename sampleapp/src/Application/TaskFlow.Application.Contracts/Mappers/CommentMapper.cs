// Pattern: Child entity mapper — separate static class.
// Comment is append-only so no ToEntity() needed (created via parent's AddComment method).

using System.Linq.Expressions;
using Domain.Model.Entities;
using Application.Models.Comment;

namespace Application.Contracts.Mappers;

public static class CommentMapper
{
    public static CommentDto ToDto(this Comment entity) => new()
    {
        Id = entity.Id,
        TodoItemId = entity.TodoItemId,
        Text = entity.Text,
        AuthorId = entity.AuthorId,
        CreatedAt = entity.CreatedAt
    };

    /// <summary>Projector for comment list within a TodoItem detail view.</summary>
    public static Expression<Func<Comment, CommentDto>> ProjectorSearch => entity => new CommentDto
    {
        Id = entity.Id,
        TodoItemId = entity.TodoItemId,
        Text = entity.Text,
        AuthorId = entity.AuthorId,
        CreatedAt = entity.CreatedAt
    };
}
