using MakanApp.Application.Organization;

namespace MakanApp.Application.Reports.Detailed;

public interface IDetailedReportService
{
    Task<AttendanceReportPage<StudentAttendanceReportItem>> GetStudentAttendanceAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken);
    Task<ReportPage<StudentAssignmentReportItem>> GetStudentAssignmentsAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken);
    Task<ReportPage<StudentExamReportItem>> GetStudentExamsAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken);
    Task<ReportPage<StudentReleasedGradeReportItem>> GetStudentGradesAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken);
    Task<AttendanceReportPage<ParentAttendanceReportItem>> GetParentAttendanceAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken);
    Task<ReportPage<ParentAssignmentReportItem>> GetParentAssignmentsAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken);
    Task<ReportPage<ParentExamReportItem>> GetParentExamsAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken);
    Task<AttendanceReportPage<TeacherAttendanceReportItem>> GetTeacherAttendanceAsync(Guid userId, Guid sessionId, Guid classId, DetailedReportQuery query, CancellationToken cancellationToken);
    Task<ReportPage<TeacherAssignmentReportItem>> GetTeacherAssignmentsAsync(Guid userId, Guid sessionId, Guid classId, DetailedReportQuery query, CancellationToken cancellationToken);
    Task<ReportPage<TeacherExamReportItem>> GetTeacherExamsAsync(Guid userId, Guid sessionId, Guid classId, DetailedReportQuery query, CancellationToken cancellationToken);
    Task<TeacherLearnerOverviewDto> GetTeacherLearnerAsync(Guid userId, Guid sessionId, Guid classId, Guid learnerId, CancellationToken cancellationToken);
    Task<ReportPage<ManagerEnrollmentReportItem>> GetManagerEnrollmentsAsync(Guid userId, Guid sessionId, ManagerEnrollmentReportQuery query, CancellationToken cancellationToken);
    Task<ReportPage<ManagerAttendanceReportItem>> GetManagerAttendanceAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken);
    Task<ReportPage<ManagerAssessmentReportItem>> GetManagerAssessmentsAsync(Guid userId, Guid sessionId, ManagerAssessmentReportQuery query, CancellationToken cancellationToken);
    Task<ManagerClassPerformanceDto> GetManagerClassAsync(Guid userId, Guid sessionId, Guid classId, CancellationToken cancellationToken);
    Task<ReportPage<ManagerTeacherWorkloadReportItem>> GetManagerTeacherWorkloadAsync(Guid userId, Guid sessionId, TeacherWorkloadReportQuery query, CancellationToken cancellationToken);
}

public interface IDetailedReportReadService
{
    Task<AttendanceReportPage<StudentAttendanceReportItem>> ReadStudentAttendanceAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken);
    Task<ReportPage<StudentAssignmentReportItem>> ReadStudentAssignmentsAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken);
    Task<ReportPage<StudentExamReportItem>> ReadStudentExamsAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken);
    Task<ReportPage<StudentReleasedGradeReportItem>> ReadStudentGradesAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken);
    Task<AttendanceReportPage<ParentAttendanceReportItem>> ReadParentAttendanceAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken);
    Task<ReportPage<ParentAssignmentReportItem>> ReadParentAssignmentsAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken);
    Task<ReportPage<ParentExamReportItem>> ReadParentExamsAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken);
    Task<bool> HasActiveTeacherAssignmentAsync(AccessContext context, Guid classId, CancellationToken cancellationToken);
    Task<AttendanceReportPage<TeacherAttendanceReportItem>> ReadTeacherAttendanceAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, ReportQuerySpec query, CancellationToken cancellationToken);
    Task<ReportPage<TeacherAssignmentReportItem>> ReadTeacherAssignmentsAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, ReportQuerySpec query, CancellationToken cancellationToken);
    Task<ReportPage<TeacherExamReportItem>> ReadTeacherExamsAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, ReportQuerySpec query, CancellationToken cancellationToken);
    Task<TeacherLearnerOverviewDto?> ReadTeacherLearnerAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, Guid learnerId, CancellationToken cancellationToken);
    Task<ReportPage<ManagerEnrollmentReportItem>> ReadManagerEnrollmentsAsync(AccessContext context, DateTime dataAsOfUtc, ManagerEnrollmentReportQuery query, ReportQuerySpec normalizedQuery, CancellationToken cancellationToken);
    Task<ReportPage<ManagerAttendanceReportItem>> ReadManagerAttendanceAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken);
    Task<ReportPage<ManagerAssessmentReportItem>> ReadManagerAssessmentsAsync(AccessContext context, DateTime dataAsOfUtc, ManagerAssessmentReportQuery query, ReportQuerySpec normalizedQuery, CancellationToken cancellationToken);
    Task<ManagerClassPerformanceDto?> ReadManagerClassAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, CancellationToken cancellationToken);
    Task<ReportPage<ManagerTeacherWorkloadReportItem>> ReadManagerTeacherWorkloadAsync(AccessContext context, DateTime dataAsOfUtc, TeacherWorkloadReportQuery query, ReportQuerySpec normalizedQuery, CancellationToken cancellationToken);
}
