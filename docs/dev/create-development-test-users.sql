-- فقط Development/Test؛ ابتدا در همان connection مقدار زیر را صریحاً تنظیم کنید:
-- EXEC sys.sp_set_session_context @key=N'MakanApp.DevelopmentSeedApproved', @value=N'DevelopmentOnly';
-- نام DB باید با MakanApp_DevelopmentTestUsers شروع شود؛ این اسکریپت schema یا migration ایجاد نمی‌کند.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'MakanApp[_]DevelopmentTestUsers%'
    THROW 51000, N'فقط دیتابیس اختصاصی MakanApp_DevelopmentTestUsers مجاز است.', 1;
IF ISNULL(CONVERT(nvarchar(50), SESSION_CONTEXT(N'MakanApp.DevelopmentSeedApproved')), N'') <> N'DevelopmentOnly'
    THROW 51001, N'تأیید صریح اجرای seed توسعه در همین connection لازم است.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'MakanApp.DevelopmentTestUsers.v1',
        @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
    IF @LockResult < 0 THROW 51002, N'قفل seed گرفته نشد.', 1;
    DECLARE @Now datetime2(7) = SYSUTCDATETIME();
    DECLARE @Org uniqueidentifier = 'd3800000-0000-0000-0000-000000000001';
    DECLARE @Period uniqueidentifier = 'd3810000-0000-0000-0000-000000000001';
    DECLARE @Course uniqueidentifier = 'd3820000-0000-0000-0000-000000000001';
    DECLARE @Class uniqueidentifier = 'd3830000-0000-0000-0000-000000000001';
    DECLARE @Seed table (Number int PRIMARY KEY, RoleKey nvarchar(100), Phone nvarchar(32),
        UserId uniqueidentifier, PersonId uniqueidentifier, CredentialId uniqueidentifier,
        MembershipId uniqueidentifier, OrganizationPersonId uniqueidentifier, AssignmentId uniqueidentifier);
    INSERT @Seed(Number,RoleKey,Phone) VALUES
        (1,N'Student',N'+989120000101'),(2,N'Teacher',N'+989120000102'),
        (3,N'Parent',N'+989120000103'),(4,N'Manager',N'+989120000104'),
        (5,N'PlatformOwner',N'+989120000105'),(6,N'Operations',N'+989120000106'),
        (7,N'Support',N'+989120000107'),(8,N'Finance',N'+989120000108'),
        (9,N'Content',N'+989120000109'),(10,N'SecurityAudit',N'+989120000110'),
        (11,N'AbuseReviewer',N'+989120000111');
    UPDATE @Seed SET
        UserId=CONVERT(uniqueidentifier,'d3700000-0000-0000-0000-'+RIGHT('000000000000'+CONVERT(varchar(12),Number),12)),
        PersonId=CONVERT(uniqueidentifier,'d3710000-0000-0000-0000-'+RIGHT('000000000000'+CONVERT(varchar(12),Number),12)),
        CredentialId=CONVERT(uniqueidentifier,'d3720000-0000-0000-0000-'+RIGHT('000000000000'+CONVERT(varchar(12),Number),12)),
        MembershipId=CONVERT(uniqueidentifier,'d3730000-0000-0000-0000-'+RIGHT('000000000000'+CONVERT(varchar(12),Number),12)),
        OrganizationPersonId=CONVERT(uniqueidentifier,'d3740000-0000-0000-0000-'+RIGHT('000000000000'+CONVERT(varchar(12),Number),12)),
        AssignmentId=CONVERT(uniqueidentifier,'d3750000-0000-0000-0000-'+RIGHT('000000000000'+CONVERT(varchar(12),Number),12));
    -- شماره، username یا ID برخوردکننده هرگز به حساب موجود merge/link نمی‌شوند.
    IF EXISTS (SELECT 1 FROM @Seed s JOIN [identity].UserCredentials c ON c.Kind=1 AND c.NormalizedIdentifier=s.Phone WHERE c.UserId<>s.UserId)
        THROW 51003, N'شمارهٔ seed متعلق به حساب دیگری است؛ هیچ تغییری انجام نشد.', 1;
    IF EXISTS (SELECT 1 FROM @Seed s JOIN [identity].UserCredentials c ON c.Id=s.CredentialId
        WHERE c.UserId<>s.UserId OR c.Kind<>1 OR c.NormalizedIdentifier<>s.Phone)
        THROW 51009, N'شناسهٔ Credential با دادهٔ دیگری برخورد دارد.', 1;
    IF EXISTS (SELECT 1 FROM @Seed s JOIN [identity].Users u ON u.Id=s.UserId
        WHERE u.PersonId IS NULL OR u.PersonId<>s.PersonId OR ISNULL(u.NormalizedUsername,N'')<>UPPER(N'dev.'+s.RoleKey))
        THROW 51004, N'شناسهٔ کاربر seed با دادهٔ موجود ناسازگار است.', 1;
    IF EXISTS (SELECT 1 FROM @Seed s JOIN [identity].Users u ON u.NormalizedUsername=UPPER(N'dev.'+s.RoleKey) WHERE u.Id<>s.UserId)
        THROW 51005, N'username تست با حساب دیگری برخورد دارد.', 1;
    IF EXISTS (SELECT 1 FROM @Seed s JOIN [identity].Persons p ON p.Id=s.PersonId WHERE p.FirstName<>N'Development' OR p.LastName<>s.RoleKey)
        THROW 51006, N'Person موجود متعلق به این seed نیست.', 1;
    IF EXISTS (SELECT 1 FROM @Seed s WHERE s.Number>4 AND NOT EXISTS
        (SELECT 1 FROM platform.PlatformRoles r WHERE r.[Key]=s.RoleKey))
        THROW 51007, N'کاتالوگ نقش‌های Backoffice کامل نیست؛ migrationهای فعلی را اجرا کنید.', 1;
    IF EXISTS (SELECT 1 FROM @Seed s JOIN organization.Memberships m ON m.Id=s.MembershipId
        WHERE s.Number<=4 AND (m.UserId<>s.UserId OR m.OrganizationId<>@Org))
        THROW 51010, N'Membership موجود متعلق به این seed نیست.', 1;
    IF EXISTS (SELECT 1 FROM @Seed s JOIN organization.OrganizationPersons p ON p.Id=s.OrganizationPersonId
        WHERE s.Number<=4 AND (p.PersonId<>s.PersonId OR p.OrganizationId<>@Org))
        THROW 51011, N'OrganizationPerson موجود متعلق به این seed نیست.', 1;
    IF EXISTS (SELECT 1 FROM @Seed s JOIN organization.RoleAssignments r ON r.Id=s.AssignmentId
        WHERE s.Number<=4 AND (r.MembershipId<>s.MembershipId OR r.[Role]<>s.Number))
        THROW 51012, N'انتساب نقش سازمانی موجود ناسازگار است.', 1;
    IF EXISTS (SELECT 1 FROM @Seed s JOIN platform.PlatformRoleAssignments a ON a.Id=s.AssignmentId
        JOIN platform.PlatformRoles r ON r.Id=a.RoleId WHERE s.Number>4 AND (a.UserId<>s.UserId OR r.[Key]<>s.RoleKey))
        THROW 51013, N'انتساب نقش پلتفرمی موجود ناسازگار است.', 1;
    IF EXISTS (SELECT 1 FROM academic.Classes WHERE Id=@Class AND
        (OrganizationId<>@Org OR AcademicPeriodId<>@Period OR CourseId<>@Course))
        THROW 51014, N'کلاس موجود متعلق به این seed نیست.', 1;
    INSERT [identity].Persons(Id,FirstName,LastName,DisplayName,CreatedAtUtc)
        SELECT s.PersonId,N'Development',s.RoleKey,N'Development '+s.RoleKey,@Now FROM @Seed s
        WHERE NOT EXISTS (SELECT 1 FROM [identity].Persons p WHERE p.Id=s.PersonId);
    INSERT [identity].Users(Id,PersonId,Username,NormalizedUsername,ProfileCompletedAtUtc,CreatedAtUtc,
        AccountStatus,CommunicationAgeCategory,CommunicationAgeVerificationStatus)
        SELECT s.UserId,s.PersonId,N'dev.'+s.RoleKey,UPPER(N'dev.'+s.RoleKey),@Now,@Now,1,0,0 FROM @Seed s
        WHERE NOT EXISTS (SELECT 1 FROM [identity].Users u WHERE u.Id=s.UserId);
    INSERT [identity].UserCredentials(Id,UserId,Kind,NormalizedIdentifier,VerifiedAtUtc,CreatedAtUtc)
        SELECT s.CredentialId,s.UserId,1,s.Phone,@Now,@Now FROM @Seed s
        WHERE NOT EXISTS (SELECT 1 FROM [identity].UserCredentials c WHERE c.Id=s.CredentialId);
    IF EXISTS (SELECT 1 FROM organization.Organizations WHERE Id=@Org AND Name<>N'Makan Development Test School')
        THROW 51008, N'شناسهٔ سازمان تست متعلق به دادهٔ دیگری است.', 1;
    IF NOT EXISTS (SELECT 1 FROM organization.Organizations WHERE Id=@Org)
        INSERT organization.Organizations(Id,Name,Status,CreatedAtUtc) VALUES(@Org,N'Makan Development Test School',1,@Now);
    INSERT organization.Memberships(Id,UserId,OrganizationId,Status,CreatedAtUtc,ActivatedAtUtc)
        SELECT s.MembershipId,s.UserId,@Org,1,@Now,@Now FROM @Seed s WHERE s.Number<=4
        AND NOT EXISTS (SELECT 1 FROM organization.Memberships m WHERE m.Id=s.MembershipId);
    INSERT organization.RoleAssignments(Id,MembershipId,[Role],Status,AssignedAtUtc)
        SELECT s.AssignmentId,s.MembershipId,s.Number,1,@Now FROM @Seed s WHERE s.Number<=4
        AND NOT EXISTS (SELECT 1 FROM organization.RoleAssignments r WHERE r.Id=s.AssignmentId);
    INSERT organization.OrganizationPersons(Id,OrganizationId,PersonId,Status,CreatedAtUtc,ActivatedAtUtc)
        SELECT s.OrganizationPersonId,@Org,s.PersonId,1,@Now,@Now FROM @Seed s WHERE s.Number<=4
        AND NOT EXISTS (SELECT 1 FROM organization.OrganizationPersons p WHERE p.Id=s.OrganizationPersonId);
    INSERT platform.PlatformRoleAssignments(Id,UserId,RoleId,Status,GrantedAtUtc)
        SELECT s.AssignmentId,s.UserId,r.Id,1,@Now FROM @Seed s JOIN platform.PlatformRoles r ON r.[Key]=s.RoleKey
        WHERE s.Number>4 AND NOT EXISTS (SELECT 1 FROM platform.PlatformRoleAssignments a WHERE a.Id=s.AssignmentId);
    IF NOT EXISTS (SELECT 1 FROM academic.AcademicPeriods WHERE Id=@Period)
        INSERT academic.AcademicPeriods(Id,OrganizationId,Title,StartDate,EndDate,Status,CreatedAtUtc)
        VALUES(@Period,@Org,N'Development Period',CONVERT(date,@Now),DATEADD(year,1,CONVERT(date,@Now)),1,@Now);
    IF NOT EXISTS (SELECT 1 FROM academic.Courses WHERE Id=@Course)
        INSERT academic.Courses(Id,OrganizationId,Title,Status,CreatedAtUtc) VALUES(@Course,@Org,N'Development Course',1,@Now);
    IF NOT EXISTS (SELECT 1 FROM academic.Classes WHERE Id=@Class)
        INSERT academic.Classes(Id,OrganizationId,AcademicPeriodId,CourseId,Title,Capacity,Status,CreatedAtUtc,ActivatedAtUtc)
        VALUES(@Class,@Org,@Period,@Course,N'Development Class',30,2,@Now,@Now);
    IF NOT EXISTS (SELECT 1 FROM academic.Enrollments WHERE Id='d3850000-0000-0000-0000-000000000001')
        INSERT academic.Enrollments(Id,OrganizationId,ClassId,LearnerOrganizationPersonId,Status,EnrolledAtUtc)
        VALUES('d3850000-0000-0000-0000-000000000001',@Org,@Class,'d3740000-0000-0000-0000-000000000001',1,@Now);
    IF NOT EXISTS (SELECT 1 FROM academic.TeacherAssignments WHERE Id='d3860000-0000-0000-0000-000000000001')
        INSERT academic.TeacherAssignments(Id,OrganizationId,ClassId,TeacherMembershipId,Status,AssignedAtUtc)
        VALUES('d3860000-0000-0000-0000-000000000001',@Org,@Class,'d3730000-0000-0000-0000-000000000002',1,@Now);
    IF NOT EXISTS (SELECT 1 FROM guardian.GuardianRelations WHERE Id='d3840000-0000-0000-0000-000000000001')
        INSERT guardian.GuardianRelations(Id,OrganizationId,GuardianUserId,LearnerOrganizationPersonId,Status,ValidFromUtc,CreatedAtUtc)
        VALUES('d3840000-0000-0000-0000-000000000001',@Org,'d3700000-0000-0000-0000-000000000003',
            'd3740000-0000-0000-0000-000000000001',2,@Now,@Now);
    COMMIT;
    SELECT s.RoleKey,s.UserId,s.Phone,u.AccountStatus,s.PersonId,
        CASE WHEN s.Number<=4 THEN @Org END AS OrganizationId,
        CASE WHEN s.Number<=4 THEN s.MembershipId END AS MembershipId,
        CASE WHEN s.Number<=4 THEN @Class END AS ClassId
    FROM @Seed s JOIN [identity].Users u ON u.Id=s.UserId ORDER BY s.Number;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
