-- ═══════════════════════════════════════════════════════════════
-- Pattern: Seed script — idempotent insertion of default Categories.
-- Run after initial migration to populate lookup data.
-- Uses MERGE for idempotent behavior (safe to run multiple times).
-- Convention: Seed scripts go in Scripts/Seed/ and are run by CI/CD pipeline.
-- ═══════════════════════════════════════════════════════════════

SET NOCOUNT ON;

PRINT 'Seeding default Categories...';

-- Pattern: MERGE for idempotent seed — inserts only if not present.
MERGE INTO [dbo].[Category] AS target
USING (VALUES
    ('00000000-0000-0000-0000-000000000001', NULL, 'Bug',         'Defects and issues to fix',      1, 'system', SYSDATETIMEOFFSET()),
    ('00000000-0000-0000-0000-000000000002', NULL, 'Feature',     'New feature requests',           1, 'system', SYSDATETIMEOFFSET()),
    ('00000000-0000-0000-0000-000000000003', NULL, 'Enhancement', 'Improvements to existing features', 1, 'system', SYSDATETIMEOFFSET()),
    ('00000000-0000-0000-0000-000000000004', NULL, 'Chore',       'Maintenance and housekeeping',   1, 'system', SYSDATETIMEOFFSET()),
    ('00000000-0000-0000-0000-000000000005', NULL, 'Research',    'Spikes and investigation tasks',  1, 'system', SYSDATETIMEOFFSET())
) AS source (Id, TenantId, [Name], [Description], IsActive, CreatedBy, CreatedDate)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, TenantId, [Name], [Description], IsActive, CreatedBy, CreatedDate)
    VALUES (source.Id, source.TenantId, source.[Name], source.[Description],
            source.IsActive, source.CreatedBy, source.CreatedDate);

PRINT 'Category seed complete. Rows affected: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
GO
