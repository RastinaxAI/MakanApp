using MakanApp.Api.Authentication;
using MakanApp.Application.Reports.Detailed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/reports")]
public sealed class ReportsController(IDetailedReportService reportService) : ControllerBase
{
    [HttpGet("student/attendance")]
    public async Task<ActionResult<AttendanceReportPage<StudentAttendanceReportItem>>> GetStudentAttendance([FromQuery] DetailedReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetStudentAttendanceAsync(UserId(), SessionId(), query, cancellationToken));

    [HttpGet("student/assignments")]
    public async Task<ActionResult<ReportPage<StudentAssignmentReportItem>>> GetStudentAssignments([FromQuery] DetailedReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetStudentAssignmentsAsync(UserId(), SessionId(), query, cancellationToken));

    [HttpGet("student/exams")]
    public async Task<ActionResult<ReportPage<StudentExamReportItem>>> GetStudentExams([FromQuery] DetailedReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetStudentExamsAsync(UserId(), SessionId(), query, cancellationToken));

    [HttpGet("student/grades")]
    public async Task<ActionResult<ReportPage<StudentReleasedGradeReportItem>>> GetStudentGrades([FromQuery] DetailedReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetStudentGradesAsync(UserId(), SessionId(), query, cancellationToken));

    [HttpGet("parent/attendance")]
    public async Task<ActionResult<AttendanceReportPage<ParentAttendanceReportItem>>> GetParentAttendance([FromQuery] DetailedReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetParentAttendanceAsync(UserId(), SessionId(), query, cancellationToken));

    [HttpGet("parent/assignments")]
    public async Task<ActionResult<ReportPage<ParentAssignmentReportItem>>> GetParentAssignments([FromQuery] DetailedReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetParentAssignmentsAsync(UserId(), SessionId(), query, cancellationToken));

    [HttpGet("parent/exams")]
    public async Task<ActionResult<ReportPage<ParentExamReportItem>>> GetParentExams([FromQuery] DetailedReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetParentExamsAsync(UserId(), SessionId(), query, cancellationToken));

    [HttpGet("teacher/classes/{classId:guid}/attendance")]
    public async Task<ActionResult<AttendanceReportPage<TeacherAttendanceReportItem>>> GetTeacherAttendance(Guid classId, [FromQuery] DetailedReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetTeacherAttendanceAsync(UserId(), SessionId(), classId, query, cancellationToken));

    [HttpGet("teacher/classes/{classId:guid}/assignments")]
    public async Task<ActionResult<ReportPage<TeacherAssignmentReportItem>>> GetTeacherAssignments(Guid classId, [FromQuery] DetailedReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetTeacherAssignmentsAsync(UserId(), SessionId(), classId, query, cancellationToken));

    [HttpGet("teacher/classes/{classId:guid}/exams")]
    public async Task<ActionResult<ReportPage<TeacherExamReportItem>>> GetTeacherExams(Guid classId, [FromQuery] DetailedReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetTeacherExamsAsync(UserId(), SessionId(), classId, query, cancellationToken));

    [HttpGet("teacher/classes/{classId:guid}/learners/{learnerId:guid}")]
    public async Task<ActionResult<TeacherLearnerOverviewDto>> GetTeacherLearner(Guid classId, Guid learnerId, CancellationToken cancellationToken) => Ok(await reportService.GetTeacherLearnerAsync(UserId(), SessionId(), classId, learnerId, cancellationToken));

    [HttpGet("manager/enrollments")]
    public async Task<ActionResult<ReportPage<ManagerEnrollmentReportItem>>> GetManagerEnrollments([FromQuery] ManagerEnrollmentReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetManagerEnrollmentsAsync(UserId(), SessionId(), query, cancellationToken));

    [HttpGet("manager/attendance")]
    public async Task<ActionResult<ReportPage<ManagerAttendanceReportItem>>> GetManagerAttendance([FromQuery] DetailedReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetManagerAttendanceAsync(UserId(), SessionId(), query, cancellationToken));

    [HttpGet("manager/assessments")]
    public async Task<ActionResult<ReportPage<ManagerAssessmentReportItem>>> GetManagerAssessments([FromQuery] ManagerAssessmentReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetManagerAssessmentsAsync(UserId(), SessionId(), query, cancellationToken));

    [HttpGet("manager/classes/{classId:guid}")]
    public async Task<ActionResult<ManagerClassPerformanceDto>> GetManagerClass(Guid classId, CancellationToken cancellationToken) => Ok(await reportService.GetManagerClassAsync(UserId(), SessionId(), classId, cancellationToken));

    [HttpGet("manager/teacher-workload")]
    public async Task<ActionResult<ReportPage<ManagerTeacherWorkloadReportItem>>> GetManagerTeacherWorkload([FromQuery] TeacherWorkloadReportQuery query, CancellationToken cancellationToken) => Ok(await reportService.GetManagerTeacherWorkloadAsync(UserId(), SessionId(), query, cancellationToken));

    private Guid UserId() => AuthenticatedSession.GetUserId(User);
    private Guid SessionId() => AuthenticatedSession.GetSessionId(User);
}
