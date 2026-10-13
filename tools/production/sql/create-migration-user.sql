:setvar DatabaseName "MakanApp"
:setvar MigrationLogin "makanapp_migration"
:setvar MigrationUser "makanapp_migration"

/*
  این principal فقط در پنجره deployment استفاده می‌شود و credential آن هرگز
  وارد تنظیمات runtime برنامه نمی‌شود.
*/
USE [$(DatabaseName)];
GO

IF SUSER_ID(N'$(MigrationLogin)') IS NULL
    THROW 51001, 'Migration login must be created by DBA before this script.', 1;
GO

IF DATABASE_PRINCIPAL_ID(N'$(MigrationUser)') IS NULL
BEGIN
    DECLARE @createUser nvarchar(max) =
        N'CREATE USER ' + QUOTENAME(N'$(MigrationUser)') +
        N' FOR LOGIN ' + QUOTENAME(N'$(MigrationLogin)') + N';';
    EXEC sys.sp_executesql @createUser;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members drm
    JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
    JOIN sys.database_principals member_principal ON member_principal.principal_id = drm.member_principal_id
    WHERE role_principal.name = N'db_ddladmin' AND member_principal.name = N'$(MigrationUser)')
    ALTER ROLE [db_ddladmin] ADD MEMBER [$(MigrationUser)];
IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members drm
    JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
    JOIN sys.database_principals member_principal ON member_principal.principal_id = drm.member_principal_id
    WHERE role_principal.name = N'db_datareader' AND member_principal.name = N'$(MigrationUser)')
    ALTER ROLE [db_datareader] ADD MEMBER [$(MigrationUser)];
IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members drm
    JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
    JOIN sys.database_principals member_principal ON member_principal.principal_id = drm.member_principal_id
    WHERE role_principal.name = N'db_datawriter' AND member_principal.name = N'$(MigrationUser)')
    ALTER ROLE [db_datawriter] ADD MEMBER [$(MigrationUser)];
GRANT CREATE SCHEMA TO [$(MigrationUser)];
GO
