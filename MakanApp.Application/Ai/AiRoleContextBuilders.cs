using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Dashboards;
using MakanApp.Application.Reports.Detailed;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Ai;

public sealed class StudentContextBuilder(
    IAccessContextResolver accessContextResolver,
    IAiScopeStore scopeStore,
    IDetailedReportReadService reports,
    TimeProvider timeProvider) : IStudentContextBuilder
{
    public async Task<AiStudentContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await AiContextAuthorization.ResolveAsync(
            accessContextResolver,
            userId,
            sessionId,
            OrganizationRole.Student,
            cancellationToken);
        var learnerId = await scopeStore.GetActiveLearnerOrganizationPersonIdAsync(
            context.OrganizationId!.Value,
            context.UserId,
            cancellationToken) ?? throw AiContextAuthorization.NotAllowed();
        var learnerClasses = await scopeStore.GetActiveLearnerClassesAsync(
            context.OrganizationId.Value,
            learnerId,
            AiContextLimits.Subjects,
            cancellationToken);
        if (learnerClasses.Count == 0)
        {
            throw AiContextAuthorization.NotAllowed();
        }
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var assignments = await reports.ReadStudentAssignmentsAsync(
            context,
            now,
            AiReportQueries.Assignments(),
            cancellationToken);
        var exams = await reports.ReadStudentExamsAsync(
            context,
            now,
            AiReportQueries.Exams(),
            cancellationToken);
        var grades = await reports.ReadStudentGradesAsync(
            context,
            now,
            AiReportQueries.Grades(),
            cancellationToken);
        var attendance = await reports.ReadStudentAttendanceAsync(
            context,
            now,
            AiReportQueries.Attendance(now),
            cancellationToken);
        var subjects = assignments.Items
            .Select(item => (item.ClassId, item.ClassTitle))
            .Concat(exams.Items.Select(item => (item.ClassId, item.ClassTitle)))
            .Concat(learnerClasses.Select(item => (item.ClassId, item.ClassTitle)));

        return new AiStudentContext(
            AiContextAuthorization.Actor(context),
            AiContextAuthorization.Scope(context, learnerId),
            new AiAuthorizedContext(
                AiContextProjection.Subjects(subjects),
                AiContextProjection.StudentGrades(grades.Items),
                AiContextProjection.StudentAssignments(assignments.Items),
                AiContextProjection.StudentExams(exams.Items),
                AiContextProjection.StudentActivity(attendance.Items)));
    }

    public async Task<(AiStudentContext Context, AiSelectedHomework Homework)> BuildHomeworkAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        if (assignmentId == Guid.Empty)
        {
            throw AiContextAuthorization.InvalidRequest();
        }

        var context = await BuildAsync(userId, sessionId, cancellationToken);
        var learnerId = Guid.Parse(context.Scope.StudentId!);
        var homework = await scopeStore.GetPublishedHomeworkAsync(
            Guid.Parse(context.Scope.OrganizationId!),
            learnerId,
            assignmentId,
            cancellationToken) ?? throw AiContextAuthorization.NotAllowed();
        return (context with
        {
            Scope = context.Scope with { ClassId = homework.ClassId.ToString("D") }
        }, homework);
    }

    public async Task<(AiStudentContext Context, AiSelectedExam Exam)> BuildExamAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken)
    {
        if (examId == Guid.Empty)
        {
            throw AiContextAuthorization.InvalidRequest();
        }

        var context = await BuildAsync(userId, sessionId, cancellationToken);
        var learnerId = Guid.Parse(context.Scope.StudentId!);
        var exam = await scopeStore.GetPublishedExamAsync(
            Guid.Parse(context.Scope.OrganizationId!),
            learnerId,
            examId,
            cancellationToken) ?? throw AiContextAuthorization.NotAllowed();
        return (context with
        {
            Scope = context.Scope with { ClassId = exam.ClassId.ToString("D") }
        }, exam);
    }
}

public sealed class ParentChildContextBuilder(
    IAccessContextResolver accessContextResolver,
    IDetailedReportReadService reports,
    TimeProvider timeProvider) : IParentChildContextBuilder
{
    public async Task<AiParentChildContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await AiContextAuthorization.ResolveAsync(
            accessContextResolver,
            userId,
            sessionId,
            OrganizationRole.Parent,
            cancellationToken);
        var learnerId = context.SubjectOrganizationPersonId
            ?? throw AiContextAuthorization.NotAllowed();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var assignments = await reports.ReadParentAssignmentsAsync(
            context,
            now,
            AiReportQueries.Assignments(),
            cancellationToken);
        var exams = await reports.ReadParentExamsAsync(
            context,
            now,
            AiReportQueries.Exams(),
            cancellationToken);
        var attendance = await reports.ReadParentAttendanceAsync(
            context,
            now,
            AiReportQueries.Attendance(now),
            cancellationToken);
        var subjects = assignments.Items
            .Select(item => (item.ClassId, item.ClassTitle))
            .Concat(exams.Items.Select(item => (item.ClassId, item.ClassTitle)));

        return new AiParentChildContext(
            AiContextAuthorization.Actor(context),
            AiContextAuthorization.Scope(context, learnerId),
            new AiAuthorizedContext(
                AiContextProjection.Subjects(subjects),
                ParentGradeItems(assignments.Items, exams.Items),
                AiContextProjection.ParentAssignments(assignments.Items),
                AiContextProjection.ParentExams(exams.Items),
                AiContextProjection.ParentActivity(attendance.Items)));
    }

    internal static IReadOnlyCollection<IReadOnlyDictionary<string, System.Text.Json.JsonElement>> ParentGradeItems(
        IEnumerable<ParentAssignmentReportItem> assignments,
        IEnumerable<ParentExamReportItem> exams)
    {
        var assignmentGrades = assignments
            .Where(item => item.ReleasedScore.HasValue && item.ReleasedAtUtc.HasValue)
            .Select(item => AiContextProjection.Item(
                ("assessmentType", "Assignment"),
                ("assessmentId", item.AssignmentId.ToString("D")),
                ("classId", item.ClassId.ToString("D")),
                ("title", AiContextProjection.Trim(item.Title, 200)),
                ("score", item.ReleasedScore),
                ("maxScore", item.MaximumScore),
                ("releasedAtUtc", item.ReleasedAtUtc)));
        var examGrades = exams
            .Where(item => item.ReleasedScore.HasValue && item.ReleasedAtUtc.HasValue)
            .Select(item => AiContextProjection.Item(
                ("assessmentType", "Exam"),
                ("assessmentId", item.ExamId.ToString("D")),
                ("classId", item.ClassId.ToString("D")),
                ("title", AiContextProjection.Trim(item.Title, 200)),
                ("score", item.ReleasedScore),
                ("maxScore", item.MaximumScore),
                ("releasedAtUtc", item.ReleasedAtUtc)));
        return assignmentGrades.Concat(examGrades).Take(AiContextLimits.Grades).ToArray();
    }
}

public sealed class TeacherClassContextBuilder(
    IAccessContextResolver accessContextResolver,
    IDetailedReportReadService reports,
    TimeProvider timeProvider) : ITeacherClassContextBuilder
{
    public async Task<AiTeacherClassContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CancellationToken cancellationToken)
    {
        var context = await AiContextAuthorization.ResolveAsync(
            accessContextResolver,
            userId,
            sessionId,
            OrganizationRole.Teacher,
            cancellationToken);
        if (classId == Guid.Empty ||
            !await reports.HasActiveTeacherAssignmentAsync(context, classId, cancellationToken))
        {
            throw AiContextAuthorization.NotAllowed();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var assignments = await reports.ReadTeacherAssignmentsAsync(
            context, now, classId, AiReportQueries.TeacherAssignments(), cancellationToken);
        var exams = await reports.ReadTeacherExamsAsync(
            context, now, classId, AiReportQueries.TeacherExams(), cancellationToken);
        var attendance = await reports.ReadTeacherAttendanceAsync(
            context, now, classId, AiReportQueries.Attendance(now), cancellationToken);
        var students = assignments.Items.Select(item => item.LearnerOrganizationPersonId)
            .Concat(exams.Items.Select(item => item.LearnerOrganizationPersonId))
            .Concat(attendance.Items.Select(item => item.LearnerOrganizationPersonId))
            .Distinct()
            .Take(AiContextLimits.ClassStudents)
            .Select(id => AiContextProjection.Item(("studentId", id.ToString("D"))))
            .ToArray();
        var assignmentRows = assignments.Items.ToArray();
        var submittedCount = assignmentRows.Count(item => item.SubmissionStatus.HasValue);
        var recordedAttendance = attendance.Summary.RecordedCount;
        var attendanceRate = recordedAttendance == 0
            ? (double?)null
            : (attendance.Summary.PresentCount + attendance.Summary.LateCount) /
              (double)recordedAttendance;
        var completionRate = assignmentRows.Length == 0
            ? (double?)null
            : submittedCount / (double)assignmentRows.Length;

        return new AiTeacherClassContext(
            AiContextAuthorization.Actor(context),
            AiContextAuthorization.Scope(context, classId: classId),
            new AiTeacherClassData(
                classId.ToString("D"),
                [AiContextProjection.Item(("classId", classId.ToString("D")))],
                TeacherReleasedGrades(assignments.Items, exams.Items),
                TeacherAssignments(assignments.Items),
                TeacherExams(exams.Items),
                TeacherAttendance(attendance.Items),
                students,
                new AiTeacherAggregateMetrics(null, null, completionRate, attendanceRate, "Unknown")));
    }

    private static IReadOnlyCollection<IReadOnlyDictionary<string, System.Text.Json.JsonElement>> TeacherReleasedGrades(
        IEnumerable<TeacherAssignmentReportItem> assignments,
        IEnumerable<TeacherExamReportItem> exams)
    {
        var assignmentGrades = assignments
            .Where(item => item.IsReleased && item.CurrentScore.HasValue)
            .Select(item => AiContextProjection.Item(
                ("assessmentType", "Assignment"),
                ("assessmentId", item.AssignmentId.ToString("D")),
                ("studentId", item.LearnerOrganizationPersonId.ToString("D")),
                ("score", item.CurrentScore),
                ("maxScore", item.MaximumScore)));
        var examGrades = exams
            .Where(item => item.IsReleased && item.CurrentScore.HasValue)
            .Select(item => AiContextProjection.Item(
                ("assessmentType", "Exam"),
                ("assessmentId", item.ExamId.ToString("D")),
                ("studentId", item.LearnerOrganizationPersonId.ToString("D")),
                ("score", item.CurrentScore),
                ("maxScore", item.MaximumScore)));
        return assignmentGrades.Concat(examGrades).Take(AiContextLimits.Grades).ToArray();
    }

    private static IReadOnlyCollection<IReadOnlyDictionary<string, System.Text.Json.JsonElement>> TeacherAssignments(
        IEnumerable<TeacherAssignmentReportItem> source) =>
        source.DistinctBy(item => item.AssignmentId).Take(AiContextLimits.Assignments)
            .Select(item => AiContextProjection.Item(
                ("assignmentId", item.AssignmentId.ToString("D")),
                ("title", AiContextProjection.Trim(item.Title, 200)),
                ("dueAtUtc", item.DueAtUtc),
                ("maxScore", item.MaximumScore))).ToArray();

    private static IReadOnlyCollection<IReadOnlyDictionary<string, System.Text.Json.JsonElement>> TeacherExams(
        IEnumerable<TeacherExamReportItem> source) =>
        source.DistinctBy(item => item.ExamId).Take(AiContextLimits.Exams)
            .Select(item => AiContextProjection.Item(
                ("examId", item.ExamId.ToString("D")),
                ("title", AiContextProjection.Trim(item.Title, 200)),
                ("availableUntilUtc", item.AvailableUntilUtc),
                ("maxScore", item.MaximumScore))).ToArray();

    private static IReadOnlyCollection<IReadOnlyDictionary<string, System.Text.Json.JsonElement>> TeacherAttendance(
        IEnumerable<TeacherAttendanceReportItem> source) =>
        source.Take(AiContextLimits.Activity).Select(item => AiContextProjection.Item(
            ("type", "Attendance"),
            ("sessionId", item.SessionId.ToString("D")),
            ("studentId", item.LearnerOrganizationPersonId.ToString("D")),
            ("status", item.Status.ToString()),
            ("occurredAtUtc", item.StartUtc))).ToArray();
}

public sealed class TeacherStudentContextBuilder(
    IAccessContextResolver accessContextResolver,
    IAiScopeStore scopeStore,
    IDetailedReportReadService reports,
    TimeProvider timeProvider) : ITeacherStudentContextBuilder
{
    public async Task<AiTeacherStudentContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var context = await AiContextAuthorization.ResolveAsync(
            accessContextResolver, userId, sessionId, OrganizationRole.Teacher, cancellationToken);
        if (classId == Guid.Empty || studentId == Guid.Empty ||
            !await reports.HasActiveTeacherAssignmentAsync(context, classId, cancellationToken))
        {
            throw AiContextAuthorization.NotAllowed();
        }


        _ = await scopeStore.GetActiveLearnerClassAsync(
            context.OrganizationId!.Value,
            studentId,
            classId,
            cancellationToken) ?? throw AiContextAuthorization.NotAllowed();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var learner = await reports.ReadTeacherLearnerAsync(
            context, now, classId, studentId, cancellationToken)
            ?? throw AiContextAuthorization.NotAllowed();
        var learnerContext = context with { SubjectOrganizationPersonId = studentId };
        var assignments = await reports.ReadParentAssignmentsAsync(
            learnerContext, now, AiReportQueries.Assignments(classId), cancellationToken);
        var exams = await reports.ReadParentExamsAsync(
            learnerContext, now, AiReportQueries.Exams(classId), cancellationToken);
        var attendance = await reports.ReadParentAttendanceAsync(
            learnerContext, now, AiReportQueries.Attendance(now, classId), cancellationToken);

        return new AiTeacherStudentContext(
            AiContextAuthorization.Actor(context),
            AiContextAuthorization.Scope(context, studentId, classId),
            new AiTeacherStudentData(
                studentId.ToString("D"),
                [AiContextProjection.Item(
                    ("classId", classId.ToString("D")),
                    ("title", AiContextProjection.Trim(learner.ClassTitle, 200)))],
                ParentChildContextBuilder.ParentGradeItems(assignments.Items, exams.Items),
                AiContextProjection.ParentAssignments(assignments.Items),
                AiContextProjection.ParentExams(exams.Items),
                AiContextProjection.ParentActivity(attendance.Items)));
    }
}

public sealed class ManagerSchoolContextBuilder(
    IAccessContextResolver accessContextResolver,
    IDashboardReadService dashboards,
    TimeProvider timeProvider) : IManagerSchoolContextBuilder
{
    public async Task<AiManagerSchoolContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await AiContextAuthorization.ResolveAsync(
            accessContextResolver, userId, sessionId, OrganizationRole.Manager, cancellationToken);
        var dashboard = await dashboards.ReadManagerAsync(
            context,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        var activity = dashboard.UpcomingSessions.Take(AiContextLimits.Activity)
            .Select(item => AiContextProjection.Item(
                ("type", "UpcomingSession"),
                ("classId", item.ClassId.ToString("D")),
                ("title", AiContextProjection.Trim(item.Title, 200)),
                ("occurredAtUtc", item.StartUtc)))
            .ToArray();
        var organizationId = context.OrganizationId!.Value;

        return new AiManagerSchoolContext(
            AiContextAuthorization.Actor(context),
            AiContextAuthorization.Scope(context),
            new AiManagerSchoolData(
                organizationId.ToString("D"),
                [], [], [], [], activity, [],
                new AiAggregateMetrics(
                    null, null, null, null, "Unknown",
                    dashboard.OperationalCounts.EnrolledLearnerCount,
                    dashboard.OperationalCounts.ActiveClassCount)));
    }
}

public sealed class ManagerClassContextBuilder(
    IAccessContextResolver accessContextResolver,
    IDetailedReportReadService reports,
    TimeProvider timeProvider) : IManagerClassContextBuilder
{
    public async Task<AiManagerClassContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CancellationToken cancellationToken)
    {
        var context = await AiContextAuthorization.ResolveAsync(
            accessContextResolver, userId, sessionId, OrganizationRole.Manager, cancellationToken);
        if (classId == Guid.Empty)
        {
            throw AiContextAuthorization.InvalidRequest();
        }

        var performance = await reports.ReadManagerClassAsync(
            context,
            timeProvider.GetUtcNow().UtcDateTime,
            classId,
            cancellationToken) ?? throw AiContextAuthorization.NotAllowed();
        var averages = new[]
            {
                performance.AssignmentAveragePercentage,
                performance.ExamAveragePercentage
            }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        var average = averages.Length == 0 ? (double?)null : (double)averages.Average();

        return new AiManagerClassContext(
            AiContextAuthorization.Actor(context),
            AiContextAuthorization.Scope(context, classId: classId),
            new AiManagerClassData(
                classId.ToString("D"),
                [AiContextProjection.Item(
                    ("classId", classId.ToString("D")),
                    ("title", AiContextProjection.Trim(performance.ClassTitle, 200)),
                    ("courseTitle", AiContextProjection.Trim(performance.CourseTitle, 200)))],
                [], [], [], [], [],
                new AiAggregateMetrics(
                    average,
                    "0-100 normalized percentage",
                    null,
                    null,
                    "Unknown",
                    performance.ActiveLearnerCount)));
    }
}

public sealed class ManagerStudentContextBuilder(
    IAccessContextResolver accessContextResolver,
    IAiScopeStore scopeStore,
    IDetailedReportReadService reports,
    TimeProvider timeProvider) : IManagerStudentContextBuilder
{
    public async Task<AiManagerStudentContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var context = await AiContextAuthorization.ResolveAsync(
            accessContextResolver, userId, sessionId, OrganizationRole.Manager, cancellationToken);
        if (classId == Guid.Empty || studentId == Guid.Empty)
        {
            throw AiContextAuthorization.InvalidRequest();
        }

        var learnerClass = await scopeStore.GetActiveLearnerClassAsync(
            context.OrganizationId!.Value,
            studentId,
            classId,
            cancellationToken) ?? throw AiContextAuthorization.NotAllowed();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var learnerContext = context with { SubjectOrganizationPersonId = studentId };
        var assignments = await reports.ReadParentAssignmentsAsync(
            learnerContext, now, AiReportQueries.Assignments(classId), cancellationToken);
        var exams = await reports.ReadParentExamsAsync(
            learnerContext, now, AiReportQueries.Exams(classId), cancellationToken);
        var attendance = await reports.ReadParentAttendanceAsync(
            learnerContext, now, AiReportQueries.Attendance(now, classId), cancellationToken);

        return new AiManagerStudentContext(
            AiContextAuthorization.Actor(context),
            AiContextAuthorization.Scope(context, studentId, classId),
            new AiManagerStudentData(
                studentId.ToString("D"),
                [AiContextProjection.Item(
                    ("classId", learnerClass.ClassId.ToString("D")),
                    ("title", AiContextProjection.Trim(learnerClass.ClassTitle, 200)),
                    ("courseTitle", AiContextProjection.Trim(learnerClass.CourseTitle, 200)))],
                ParentChildContextBuilder.ParentGradeItems(assignments.Items, exams.Items),
                AiContextProjection.ParentAssignments(assignments.Items),
                AiContextProjection.ParentExams(exams.Items),
                AiContextProjection.ParentActivity(attendance.Items)));
    }
}

internal static class AiContextAuthorization
{
    public static async Task<AccessContext> ResolveAsync(
        IAccessContextResolver resolver,
        Guid userId,
        Guid sessionId,
        OrganizationRole role,
        CancellationToken cancellationToken)
    {
        var context = await resolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            !context.MembershipId.HasValue ||
            context.ActiveRole != role)
        {
            throw NotAllowed();
        }

        return context;
    }

    public static AiActor Actor(AccessContext context) =>
        new(context.UserId.ToString("D"), context.ActiveRole!.Value.ToString());

    public static AiScope Scope(
        AccessContext context,
        Guid? studentId = null,
        Guid? classId = null) =>
        new(
            context.OrganizationId!.Value.ToString("D"),
            context.OrganizationId.Value.ToString("D"),
            classId?.ToString("D"),
            studentId?.ToString("D"));

    public static AiServiceException NotAllowed() => new(
        AiServiceErrorCodes.ContextNotAllowed,
        "بافت آموزشی در محدوده دسترسی فعلی مجاز نیست.");

    public static AiServiceException InvalidRequest() => new(
        AiServiceErrorCodes.InvalidRequest,
        "درخواست هوش مصنوعی معتبر نیست.");
}

internal static class AiReportQueries
{
    public static ReportQuerySpec Assignments(Guid? classId = null) =>
        new(1, AiContextLimits.Assignments, null, null, classId, "duedesc");

    public static ReportQuerySpec Exams(Guid? classId = null) =>
        new(1, AiContextLimits.Exams, null, null, classId, "availabledesc");

    public static ReportQuerySpec Grades() =>
        new(1, AiContextLimits.Grades, null, null, null, "releaseddesc");

    public static ReportQuerySpec Attendance(DateTime now, Guid? classId = null) =>
        new(1, AiContextLimits.Activity, now.AddDays(-90), now, classId, "occurreddesc");

    public static ReportQuerySpec TeacherAssignments() =>
        new(1, AiContextLimits.ClassStudents, null, null, null, "duedesc");

    public static ReportQuerySpec TeacherExams() =>
        new(1, AiContextLimits.ClassStudents, null, null, null, "availabledesc");
}
