-- ═══════════════════════════════════════════════════════════════
-- Pattern: Index maintenance script — rebuild or reorganize based on fragmentation.
-- Scheduled as a periodic maintenance job (e.g., weekly via SQL Agent or Scheduler).
-- Adaptive: reorganize if fragmentation 10-30%, rebuild if >30%.
-- Convention: Maintenance scripts go in Scripts/Maintenance/.
-- ═══════════════════════════════════════════════════════════════

SET NOCOUNT ON;

DECLARE @SchemaName NVARCHAR(128);
DECLARE @TableName  NVARCHAR(128);
DECLARE @IndexName  NVARCHAR(128);
DECLARE @Frag       FLOAT;
DECLARE @SQL        NVARCHAR(MAX);

PRINT '══════════════════════════════════════════════════';
PRINT 'Index Maintenance — ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
PRINT '══════════════════════════════════════════════════';

-- Pattern: Cursor over fragmented indexes — skip heap (index_id = 0).
DECLARE index_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT
    s.[name]   AS SchemaName,
    t.[name]   AS TableName,
    i.[name]   AS IndexName,
    ips.avg_fragmentation_in_percent AS Fragmentation
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') AS ips
INNER JOIN sys.tables  AS t ON t.[object_id] = ips.[object_id]
INNER JOIN sys.schemas AS s ON s.[schema_id] = t.[schema_id]
INNER JOIN sys.indexes AS i ON i.[object_id] = ips.[object_id] AND i.index_id = ips.index_id
WHERE ips.avg_fragmentation_in_percent > 10
  AND ips.index_id > 0           -- skip heaps
  AND ips.page_count > 1000      -- skip small indexes
ORDER BY ips.avg_fragmentation_in_percent DESC;

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO @SchemaName, @TableName, @IndexName, @Frag;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Pattern: Adaptive threshold — REORGANIZE for moderate, REBUILD for heavy.
    IF @Frag > 30
    BEGIN
        SET @SQL = 'ALTER INDEX [' + @IndexName + '] ON [' + @SchemaName + '].[' + @TableName + '] REBUILD WITH (ONLINE = ON)';
        PRINT 'REBUILD: ' + @SchemaName + '.' + @TableName + '.' + @IndexName + ' (frag: ' + CAST(@Frag AS VARCHAR(10)) + '%)';
    END
    ELSE
    BEGIN
        SET @SQL = 'ALTER INDEX [' + @IndexName + '] ON [' + @SchemaName + '].[' + @TableName + '] REORGANIZE';
        PRINT 'REORGANIZE: ' + @SchemaName + '.' + @TableName + '.' + @IndexName + ' (frag: ' + CAST(@Frag AS VARCHAR(10)) + '%)';
    END

    BEGIN TRY
        EXEC sp_executesql @SQL;
    END TRY
    BEGIN CATCH
        PRINT 'ERROR on ' + @IndexName + ': ' + ERROR_MESSAGE();
    END CATCH

    FETCH NEXT FROM index_cursor INTO @SchemaName, @TableName, @IndexName, @Frag;
END

CLOSE index_cursor;
DEALLOCATE index_cursor;

PRINT 'Index maintenance complete.';
GO
