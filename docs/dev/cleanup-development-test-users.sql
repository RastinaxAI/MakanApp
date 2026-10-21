-- فقط fixture همین seed؛ با دادهٔ وابستهٔ جدید، FKها باعث rollback می‌شوند؛ cascade دستی برای دادهٔ کسب‌وکاری نداریم.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'MakanApp[_]DevelopmentTestUsers%'
    THROW 51100, N'پاک‌سازی فقط در دیتابیس اختصاصی DevelopmentTestUsers مجاز است.', 1;
IF ISNULL(CONVERT(nvarchar(50), SESSION_CONTEXT(N'MakanApp.DevelopmentSeedApproved')),N'')<>N'DevelopmentOnly'
    THROW 51101, N'تأیید صریح پاک‌سازی توسعه در همین connection لازم است.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'MakanApp.DevelopmentTestUsers.v1',
        @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
    IF @LockResult<0 THROW 51102, N'قفل پاک‌سازی گرفته نشد.', 1;
    DECLARE @Seed table(UserId uniqueidentifier PRIMARY KEY,PersonId uniqueidentifier,Phone nvarchar(32));
    INSERT @Seed
        SELECT CONVERT(uniqueidentifier,'d3700000-0000-0000-0000-'+RIGHT('000000000000'+CONVERT(varchar(12),n),12)),
            CONVERT(uniqueidentifier,'d3710000-0000-0000-0000-'+RIGHT('000000000000'+CONVERT(varchar(12),n),12)),
            N'+989120000'+RIGHT('000'+CONVERT(nvarchar(3),100+n),3)
        FROM (VALUES(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11)) numbers(n);
    IF EXISTS(SELECT 1 FROM @Seed s JOIN [identity].Users u ON u.Id=s.UserId
        WHERE u.PersonId IS NULL OR u.PersonId<>s.PersonId OR u.Username NOT LIKE N'dev.%')
        THROW 51103, N'شناسه‌های seed با دادهٔ فعلی سازگار نیستند؛ پاک‌سازی متوقف شد.', 1;
    IF EXISTS(SELECT 1 FROM @Seed s JOIN [identity].UserCredentials c ON c.Kind=1 AND c.NormalizedIdentifier=s.Phone WHERE c.UserId<>s.UserId)
        THROW 51104, N'شماره به حساب دیگری تعلق دارد؛ پاک‌سازی متوقف شد.', 1;
    DELETE r FROM [identity].RefreshTokens r JOIN [identity].UserSessions t ON t.Id=r.SessionId JOIN @Seed s ON s.UserId=t.UserId;
    DELETE t FROM [identity].UserSessions t JOIN @Seed s ON s.UserId=t.UserId;
    DELETE c FROM [identity].OtpChallenges c JOIN @Seed s ON s.Phone=c.NormalizedPhoneNumber;
    DELETE a FROM platform.PlatformRoleAssignments a JOIN @Seed s ON s.UserId=a.UserId;
    DELETE guardian.GuardianRelations WHERE Id='d3840000-0000-0000-0000-000000000001';
    DELETE academic.TeacherAssignments WHERE Id='d3860000-0000-0000-0000-000000000001';
    DELETE academic.Enrollments WHERE Id='d3850000-0000-0000-0000-000000000001';
    DELETE academic.Classes WHERE Id='d3830000-0000-0000-0000-000000000001';
    DELETE academic.Courses WHERE Id='d3820000-0000-0000-0000-000000000001';
    DELETE academic.AcademicPeriods WHERE Id='d3810000-0000-0000-0000-000000000001';
    DELETE r FROM organization.RoleAssignments r JOIN organization.Memberships m ON m.Id=r.MembershipId
        JOIN @Seed s ON s.UserId=m.UserId WHERE m.OrganizationId='d3800000-0000-0000-0000-000000000001';
    DELETE m FROM organization.Memberships m JOIN @Seed s ON s.UserId=m.UserId
        WHERE m.OrganizationId='d3800000-0000-0000-0000-000000000001';
    DELETE p FROM organization.OrganizationPersons p JOIN @Seed s ON s.PersonId=p.PersonId
        WHERE p.OrganizationId='d3800000-0000-0000-0000-000000000001';
    DELETE organization.Organizations WHERE Id='d3800000-0000-0000-0000-000000000001';
    DELETE c FROM [identity].UserCredentials c JOIN @Seed s ON s.UserId=c.UserId;
    DELETE u FROM [identity].Users u JOIN @Seed s ON s.UserId=u.Id;
    DELETE p FROM [identity].Persons p JOIN @Seed s ON s.PersonId=p.Id;
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
