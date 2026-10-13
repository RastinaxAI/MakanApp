/*
  فقط برای SQL Server/LocalDB ایزوله است. نام‌ها باید disposable باشند.
  SourceDatabase، TargetDatabase و BackupFile باید با sqlcmd -v داده شوند.
  هرگز نام دیتابیس Development یا Production را به این متغیرها ندهید.
*/
USE [master];
GO

IF N'$(SourceDatabase)' NOT LIKE N'MakanApp_Step16RestoreSource%'
   OR N'$(TargetDatabase)' NOT LIKE N'MakanApp_Step16RestoreTarget%'
    THROW 51010, 'Only disposable STEP 16 drill database names are allowed.', 1;
GO

IF DB_ID(N'$(SourceDatabase)') IS NOT NULL OR DB_ID(N'$(TargetDatabase)') IS NOT NULL
    THROW 51011, 'Disposable drill database already exists; inspect it before retrying.', 1;
GO

DECLARE @createSource nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(N'$(SourceDatabase)') + N';';
EXEC sys.sp_executesql @createSource;
GO

DECLARE @seed nvarchar(max) =
    N'CREATE TABLE ' + QUOTENAME(N'$(SourceDatabase)') + N'.dbo.RestoreProbe ' +
    N'(Id int NOT NULL PRIMARY KEY, Marker nvarchar(100) NOT NULL);' +
    N'INSERT INTO ' + QUOTENAME(N'$(SourceDatabase)') +
    N'.dbo.RestoreProbe (Id, Marker) VALUES (1, N''STEP16_RESTORE_OK'');';
EXEC sys.sp_executesql @seed;
GO

DECLARE @backup nvarchar(max) =
    N'BACKUP DATABASE ' + QUOTENAME(N'$(SourceDatabase)') +
    N' TO DISK = N''$(BackupFile)'' WITH INIT, CHECKSUM, STATS = 10;';
EXEC sys.sp_executesql @backup;
GO

DECLARE @dataPath nvarchar(4000) = CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath'));
DECLARE @logPath nvarchar(4000) = CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultLogPath'));
DECLARE @restore nvarchar(max) =
    N'RESTORE DATABASE ' + QUOTENAME(N'$(TargetDatabase)') +
    N' FROM DISK = N''$(BackupFile)'' WITH CHECKSUM, ' +
    N'MOVE N''$(SourceDatabase)'' TO N''' + @dataPath + N'$(TargetDatabase).mdf'', ' +
    N'MOVE N''$(SourceDatabase)_log'' TO N''' + @logPath + N'$(TargetDatabase)_log.ldf'', STATS = 10;';
EXEC sys.sp_executesql @restore;
GO

DECLARE @probeCount int;
DECLARE @validate nvarchar(max) =
    N'SELECT @count = COUNT(*) FROM ' + QUOTENAME(N'$(TargetDatabase)') +
    N'.dbo.RestoreProbe WHERE Id = 1 AND Marker = N''STEP16_RESTORE_OK'';';
EXEC sys.sp_executesql @validate, N'@count int OUTPUT', @count = @probeCount OUTPUT;
IF @probeCount <> 1
    THROW 51012, 'Restored probe data is invalid.', 1;
PRINT N'STEP16_RESTORE_DRILL_SUCCEEDED';
GO

ALTER DATABASE [$(TargetDatabase)] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [$(TargetDatabase)];
ALTER DATABASE [$(SourceDatabase)] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [$(SourceDatabase)];
GO
