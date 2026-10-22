-- فقط خواندنی؛ قبل از seed در همان پنجرهٔ SSMS اجرا کنید.
SET NOCOUNT ON;
SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName,
       DB_NAME() AS DatabaseName,
       CONVERT(int,SERVERPROPERTY('IsLocalDB')) AS IsLocalDB;
SELECT expected.ColumnName,
       CASE WHEN COL_LENGTH(N'identity.Users',expected.ColumnName) IS NULL THEN 0 ELSE 1 END AS ExistsInDatabase,
       expected.IntroducedByMigration
FROM (VALUES
    (N'AccountStatus',N'20261013055902_AddPlatformOrganizationUserOperations'),
    (N'CommunicationAgeCategory',N'20261004175932_AddDirectMessagingFoundation'),
    (N'CommunicationAgeVerificationStatus',N'20261015091336_EnforceApprovedMessengerChildSafety')
) expected(ColumnName,IntroducedByMigration);
IF OBJECT_ID(N'dbo.__EFMigrationsHistory',N'U') IS NOT NULL
    EXEC sys.sp_executesql N'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;';
ELSE
    SELECT N'جدول تاریخچهٔ EF موجود نیست؛ قبل از seed، مقصد و migrationها را بررسی کنید.' AS Diagnostic;
-- فقدان ستون با حذف آن از INSERT یا ALTER TABLE دستی رفع نشود؛ schema باید با کد هم‌نسخه باشد.
