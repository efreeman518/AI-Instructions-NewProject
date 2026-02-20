-- ═══════════════════════════════════════════════════════════════
-- Pattern: Managed identity SQL user creation script.
-- Creates a contained database user for the Azure App Service / Container App
-- managed identity, then grants appropriate roles.
-- Run by a DBA or CI/CD pipeline after provisioning the managed identity.
--
-- Usage:
--   1. Replace <ManagedIdentityName> with the managed identity display name
--      (System-assigned MI = the app name; User-assigned MI = the MI resource name).
--   2. Connect to the TARGET database (not master) using an admin account.
--   3. Execute this script.
--
-- Convention: Run once per environment after infrastructure provisioning.
-- ═══════════════════════════════════════════════════════════════

-- Pattern: Variable — replace with your managed identity name.
DECLARE @ManagedIdentityName NVARCHAR(128) = N'<ManagedIdentityName>';
DECLARE @SQL NVARCHAR(MAX);

-- ═══════════════════════════════════════════════════════════════
-- Step 1: Create contained database user for the managed identity.
-- Pattern: Contained user — no server-level login needed.
-- The FROM EXTERNAL PROVIDER clause links to the Entra ID identity.
-- ═══════════════════════════════════════════════════════════════

-- Idempotent: check if user already exists.
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE [name] = @ManagedIdentityName)
BEGIN
    SET @SQL = N'CREATE USER [' + @ManagedIdentityName + N'] FROM EXTERNAL PROVIDER';
    EXEC sp_executesql @SQL;
    PRINT 'Created contained user: ' + @ManagedIdentityName;
END
ELSE
BEGIN
    PRINT 'User already exists: ' + @ManagedIdentityName;
END

-- ═══════════════════════════════════════════════════════════════
-- Step 2: Grant database roles.
-- Pattern: Least privilege — reader + writer + execute for app workloads.
-- Do NOT grant db_owner to application managed identities.
-- ═══════════════════════════════════════════════════════════════

-- Read access (SELECT)
SET @SQL = N'ALTER ROLE db_datareader ADD MEMBER [' + @ManagedIdentityName + N']';
EXEC sp_executesql @SQL;
PRINT 'Granted db_datareader to: ' + @ManagedIdentityName;

-- Write access (INSERT, UPDATE, DELETE)
SET @SQL = N'ALTER ROLE db_datawriter ADD MEMBER [' + @ManagedIdentityName + N']';
EXEC sp_executesql @SQL;
PRINT 'Granted db_datawriter to: ' + @ManagedIdentityName;

-- Execute stored procedures (if any)
GRANT EXECUTE TO [$(ManagedIdentityName)];
-- Note: The above uses SQLCMD variable syntax. For plain T-SQL, use dynamic SQL:
-- SET @SQL = N'GRANT EXECUTE TO [' + @ManagedIdentityName + N']';
-- EXEC sp_executesql @SQL;

PRINT 'Managed identity user setup complete for: ' + @ManagedIdentityName;
GO
