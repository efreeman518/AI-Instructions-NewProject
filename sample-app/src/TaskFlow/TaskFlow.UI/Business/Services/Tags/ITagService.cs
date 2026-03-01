namespace TaskFlow.UI.Business.Services.Tags;

/// <summary>
/// UI-layer service interface for tags — calls Gateway API.
/// </summary>
public interface ITagService
{
    ValueTask<IImmutableList<TagSummary>> GetAll(CancellationToken ct);
    ValueTask<TagSummary?> GetById(Guid id, CancellationToken ct);
    ValueTask<TagSummary?> Create(TagSummary item, CancellationToken ct);
    ValueTask<TagSummary?> Update(TagSummary item, CancellationToken ct);
    ValueTask<bool> Delete(Guid id, CancellationToken ct);
}
