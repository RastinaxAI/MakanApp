-- فقط دادهٔ fixture تأییدشدهٔ توسعه؛ این فایل API تغییر شمارهٔ کاربران واقعی نیست.
-- در همین connection، تأیید Seed، نام DB و تأیید مستقل تغییر شماره لازم‌اند؛ راهنما را بخوانید.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'MakanApp' AND DB_NAME() NOT LIKE N'MakanApp[_]DevelopmentTestUsers[_]%'
    THROW 51200, N'تغییر شماره فقط در مقصد توسعهٔ تأییدشده مجاز است.', 1;
IF ISNULL(CONVERT(nvarchar(50),SESSION_CONTEXT(N'MakanApp.DevelopmentSeedApproved')),N'') <> N'DevelopmentOnly'
    THROW 51201, N'تأیید صریح توسعه در همین connection لازم است.', 1;
IF ISNULL(CONVERT(sysname,SESSION_CONTEXT(N'MakanApp.DevelopmentSeedDatabase')),N'') <> DB_NAME()
    THROW 51202, N'نام دیتابیس مقصد باید در همین connection تأیید شود.', 1;
IF ISNULL(CONVERT(nvarchar(50),SESSION_CONTEXT(N'MakanApp.DevelopmentSeedPhoneUpdateApproved')),N'') <> N'IranianFixturePhones'
    THROW 51203, N'تأیید مستقل تغییر شمارهٔ fixture در همین connection لازم است.', 1;
IF @@TRANCOUNT <> 0
    THROW 51209, N'این عملیات باید در connection بدون تراکنش باز اجرا شود.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'MakanApp.DevelopmentTestUsers.v1',
        @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
    IF @LockResult < 0 THROW 51207, N'قفل تغییر شمارهٔ fixture گرفته نشد.', 1;

    DECLARE @Expected table(Number int PRIMARY KEY, RoleKey nvarchar(100), UserId uniqueidentifier,
        PersonId uniqueidentifier, CredentialId uniqueidentifier, AssignmentId uniqueidentifier,
        MembershipId uniqueidentifier, PreviousPhone nvarchar(32), IranianPhone nvarchar(32));
    INSERT @Expected(Number,RoleKey) VALUES
        (1,N'Student'),(2,N'Teacher'),(3,N'Parent'),(4,N'Manager'),(5,N'PlatformOwner'),
        (6,N'Operations'),(7,N'Support'),(8,N'Finance'),(9,N'Content'),(10,N'SecurityAudit'),(11,N'AbuseReviewer');
    UPDATE @Expected SET
        UserId=CONVERT(uniqueidentifier,'d3700000-0000-0000-0000-'+RIGHT('000000000000'+CONVERT(varchar(12),Number),12)),
        PersonId=CONVERT(uniqueidentifier,'d3710000-0000-0000-0000-'+RIGHT('000000000000'+CONVERT(varchar(12),Number),12)),
        CredentialId=CONVERT(uniqueidentifier,'d3720000-0000-0000-0000-'+RIGHT('000000000000'+CONVERT(varchar(12),Number),12)),
        MembershipId=CONVERT(uniqueidentifier,'d3730000-0000-0000-0000-'+RIGHT('000000000000'+CONVERT(varchar(12),Number),12)),
        AssignmentId=CONVERT(uniqueidentifier,'d3750000-0000-0000-0000-'+RIGHT('000000000000'+CONVERT(varchar(12),Number),12)),
        PreviousPhone=N'+1202555'+RIGHT('0000'+CONVERT(nvarchar(4),100+Number),4),
        IranianPhone=N'+989120000'+RIGHT('000'+CONVERT(nvarchar(3),100+Number),3);

    IF (SELECT COUNT(*) FROM @Expected e
        JOIN [identity].Users u WITH (UPDLOCK,HOLDLOCK) ON u.Id=e.UserId AND u.PersonId=e.PersonId
            AND u.Username=N'dev.'+e.RoleKey AND u.NormalizedUsername=UPPER(N'dev.'+e.RoleKey)
        JOIN [identity].Persons p WITH (UPDLOCK,HOLDLOCK) ON p.Id=e.PersonId AND p.FirstName=N'Development' AND p.LastName=e.RoleKey
        JOIN [identity].UserCredentials c WITH (UPDLOCK,HOLDLOCK) ON c.Id=e.CredentialId AND c.UserId=e.UserId AND c.Kind=1) <> 11
        THROW 51204, N'شناسه یا مالکیت یکی از حساب‌های fixture ناسازگار است؛ هیچ تغییری مجاز نیست.', 1;
    IF EXISTS (SELECT 1 FROM @Expected e JOIN [identity].UserCredentials c WITH (UPDLOCK,HOLDLOCK) ON c.Id=e.CredentialId
        WHERE c.NormalizedIdentifier NOT IN (e.PreviousPhone,e.IranianPhone))
        THROW 51205, N'شمارهٔ فعلی یکی از fixtureها با مقدار قبلی یا ایرانی سازگار نیست.', 1;
    IF EXISTS (SELECT 1 FROM @Expected e JOIN [identity].UserCredentials c WITH (UPDLOCK,HOLDLOCK)
        ON c.Kind=1 AND c.NormalizedIdentifier=e.IranianPhone WHERE c.Id<>e.CredentialId)
        THROW 51206, N'شمارهٔ ایرانی متعلق به Credential دیگری است؛ هیچ حسابی merge نمی‌شود.', 1;
    IF EXISTS (SELECT 1 FROM @Expected e WHERE e.Number<=4 AND NOT EXISTS
        (SELECT 1 FROM organization.Memberships m WITH (UPDLOCK,HOLDLOCK)
         JOIN organization.RoleAssignments r WITH (UPDLOCK,HOLDLOCK) ON r.MembershipId=m.Id
         WHERE m.Id=e.MembershipId AND m.UserId=e.UserId AND m.OrganizationId='d3800000-0000-0000-0000-000000000001'
             AND r.Id=e.AssignmentId AND r.[Role]=e.Number))
        OR EXISTS (SELECT 1 FROM @Expected e WHERE e.Number>4 AND NOT EXISTS
        (SELECT 1 FROM platform.PlatformRoleAssignments a WITH (UPDLOCK,HOLDLOCK)
         JOIN platform.PlatformRoles r WITH (HOLDLOCK) ON r.Id=a.RoleId
         WHERE a.Id=e.AssignmentId AND a.UserId=e.UserId AND r.[Key]=e.RoleKey))
        THROW 51208, N'انتساب یکی از نقش‌های fixture با حساب مورد انتظار هم‌خوان نیست.', 1;

    DECLARE @Changed int;
    UPDATE c SET NormalizedIdentifier=e.IranianPhone
    FROM [identity].UserCredentials c JOIN @Expected e ON e.CredentialId=c.Id
    WHERE c.NormalizedIdentifier=e.PreviousPhone;
    SET @Changed=@@ROWCOUNT;
    COMMIT;

    SELECT @Changed AS UpdatedCredentials;
    SELECT e.RoleKey,e.UserId,e.IranianPhone AS PhoneNumber FROM @Expected e ORDER BY e.Number;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;
