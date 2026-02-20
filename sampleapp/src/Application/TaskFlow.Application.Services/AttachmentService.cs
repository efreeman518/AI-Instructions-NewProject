// Pattern: File upload service — Attachment with polymorphic entity support.
// Demonstrates blob storage integration and EntityType discriminator pattern.

using Microsoft.Extensions.Logging;
using Package.Infrastructure.Common;
using Package.Infrastructure.Domain.Contracts;
using Application.Contracts.Mappers;
using Application.Contracts.Repositories;
using Application.Contracts.Services;
using Application.Models.Attachment;
using Domain.Model.Entities;
using Domain.Model.Enums;
using Domain.Shared;

namespace Application.Services;

internal class AttachmentService(
    ILogger<AttachmentService> logger,
    IRequestContext<string, Guid?> requestContext,
    IAttachmentRepositoryQuery repoQuery,
    IAttachmentRepositoryTrxn repoTrxn) : IAttachmentService
{
    private Guid? CallerTenantId => requestContext.TenantId;
    private string CallerUserId => requestContext.UserId ?? "system";

    public async Task<Result<IReadOnlyList<AttachmentDto>>> GetByEntityAsync(
        Guid entityId, EntityType entityType, CancellationToken ct = default)
    {
        // Pattern: Polymorphic query — EntityId + EntityType discriminator.
        var attachments = await repoQuery.GetByEntityAsync(entityId, entityType, ct);
        return Result<IReadOnlyList<AttachmentDto>>.Success(attachments);
    }

    /// <summary>
    /// Pattern: File upload — accepts stream metadata, creates blob reference + DB record.
    /// In production, the blob upload would be delegated to an Infrastructure.Storage service.
    /// Here we demonstrate the service-level orchestration pattern.
    /// </summary>
    public async Task<Result<AttachmentDto>> UploadAsync(
        Guid entityId, EntityType entityType, string fileName,
        string contentType, Stream content, CancellationToken ct = default)
    {
        // Pattern: Generate blob URI — in production, upload stream to Azure Blob Storage.
        // The infrastructure layer would return the actual URI after upload.
        var blobUri = $"https://storage.blob.core.windows.net/attachments/{Guid.NewGuid()}/{fileName}";

        var entity = Attachment.Create(
            tenantId: CallerTenantId ?? Guid.Empty,
            entityId: entityId,
            entityType: entityType,
            fileName: fileName,
            contentType: contentType,
            fileSizeBytes: content.Length,
            blobUri: blobUri,
            uploadedBy: CallerUserId);

        repoTrxn.Add(entity);
        await repoTrxn.SaveChangesAsync(ct);

        logger.LogInformation("Attachment {Id} uploaded for {EntityType}:{EntityId}",
            entity.Id, entityType, entityId);

        return Result<AttachmentDto>.Success(entity.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetByIdAsync(id, ct);
        if (entity is null) return Result.Success(); // idempotent

        // Pattern: Tenant boundary check.
        if (CallerTenantId.HasValue && entity.TenantId != CallerTenantId)
            return Result.Forbidden("Access denied.");

        // Pattern: In production, also delete the blob from Azure Blob Storage here.
        repoTrxn.Delete(entity);
        await repoTrxn.SaveChangesAsync(ct);

        logger.LogInformation("Attachment {Id} deleted", id);
        return Result.Success();
    }
}
