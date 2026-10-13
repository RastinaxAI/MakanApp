using System.Text.Json;
using MakanApp.Application.Reports.Detailed;

namespace MakanApp.Application.Ai;

internal static class AiContextProjection
{
    public static IReadOnlyDictionary<string, JsonElement> Item(
        params (string Name, object? Value)[] fields)
    {
        var item = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (name, value) in fields)
        {
            if (value is not null)
            {
                item.Add(name, JsonSerializer.SerializeToElement(value));
            }
        }

        return item;
    }

    public static IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Subjects(
        IEnumerable<(Guid ClassId, string ClassTitle)> source) =>
        source
            .DistinctBy(item => item.ClassId)
            .Take(AiContextLimits.Subjects)
            .Select(item => Item(
                ("classId", item.ClassId.ToString("D")),
                ("title", Trim(item.ClassTitle, 200))))
            .ToArray();

    public static IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> StudentGrades(
        IEnumerable<StudentReleasedGradeReportItem> source) =>
        source.Take(AiContextLimits.Grades).Select(item => Item(
            ("assessmentType", item.Kind.ToString()),
            ("assessmentId", item.AssessmentId.ToString("D")),
            ("classId", item.ClassId.ToString("D")),
            ("title", Trim(item.Title, 200)),
            ("score", item.Score),
            ("maxScore", item.MaximumScore),
            ("releasedAtUtc", item.ReleasedAtUtc))).ToArray();

    public static IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> StudentAssignments(
        IEnumerable<StudentAssignmentReportItem> source) =>
        source.Take(AiContextLimits.Assignments).Select(item => Item(
            ("assignmentId", item.AssignmentId.ToString("D")),
            ("classId", item.ClassId.ToString("D")),
            ("title", Trim(item.Title, 200)),
            ("dueAtUtc", item.DueAtUtc),
            ("status", item.SubmissionStatus?.ToString()),
            ("maxScore", item.MaximumScore))).ToArray();

    public static IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> ParentAssignments(
        IEnumerable<ParentAssignmentReportItem> source) =>
        source.Take(AiContextLimits.Assignments).Select(item => Item(
            ("assignmentId", item.AssignmentId.ToString("D")),
            ("classId", item.ClassId.ToString("D")),
            ("title", Trim(item.Title, 200)),
            ("dueAtUtc", item.DueAtUtc),
            ("status", item.SubmissionStatus?.ToString()),
            ("maxScore", item.MaximumScore))).ToArray();

    public static IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> StudentExams(
        IEnumerable<StudentExamReportItem> source) =>
        source.Take(AiContextLimits.Exams).Select(item => Item(
            ("examId", item.ExamId.ToString("D")),
            ("classId", item.ClassId.ToString("D")),
            ("title", Trim(item.Title, 200)),
            ("availableFromUtc", item.AvailableFromUtc),
            ("availableUntilUtc", item.AvailableUntilUtc),
            ("status", item.LatestAttemptStatus?.ToString()),
            ("maxScore", item.MaximumScore))).ToArray();

    public static IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> ParentExams(
        IEnumerable<ParentExamReportItem> source) =>
        source.Take(AiContextLimits.Exams).Select(item => Item(
            ("examId", item.ExamId.ToString("D")),
            ("classId", item.ClassId.ToString("D")),
            ("title", Trim(item.Title, 200)),
            ("availableFromUtc", item.AvailableFromUtc),
            ("availableUntilUtc", item.AvailableUntilUtc),
            ("status", item.LatestAttemptStatus?.ToString()),
            ("maxScore", item.MaximumScore))).ToArray();

    public static IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> StudentActivity(
        IEnumerable<StudentAttendanceReportItem> source) =>
        source.Take(AiContextLimits.Activity).Select(item => Item(
            ("type", "Attendance"),
            ("classId", item.ClassId.ToString("D")),
            ("status", item.Status.ToString()),
            ("occurredAtUtc", item.StartUtc))).ToArray();

    public static IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> ParentActivity(
        IEnumerable<ParentAttendanceReportItem> source) =>
        source.Take(AiContextLimits.Activity).Select(item => Item(
            ("type", "Attendance"),
            ("classId", item.ClassId.ToString("D")),
            ("status", item.Status.ToString()),
            ("occurredAtUtc", item.StartUtc))).ToArray();

    public static IReadOnlyDictionary<string, JsonElement> Homework(AiSelectedHomework item) => Item(
        ("assignmentId", item.AssignmentId.ToString("D")),
        ("classId", item.ClassId.ToString("D")),
        ("title", Trim(item.Title, 200)),
        ("description", Trim(item.Description, AiContextLimits.FreeTextLength)),
        ("dueAtUtc", item.DueAtUtc),
        ("maxScore", item.MaxScore));

    public static IReadOnlyDictionary<string, JsonElement> Exam(AiSelectedExam item) => Item(
        ("examId", item.ExamId.ToString("D")),
        ("classId", item.ClassId.ToString("D")),
        ("title", Trim(item.Title, 200)),
        ("description", Trim(item.Description, AiContextLimits.FreeTextLength)),
        ("availableFromUtc", item.AvailableFromUtc),
        ("availableUntilUtc", item.AvailableUntilUtc),
        ("durationMinutes", item.DurationMinutes),
        ("maxScore", item.MaxScore));

    public static string? Trim(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, maximumLength)];
}

public static class AiContextLimits
{
    public const int Subjects = 10;
    public const int Assignments = 10;
    public const int Exams = 10;
    public const int Grades = 10;
    public const int Activity = 20;
    public const int ClassStudents = 30;
    public const int FreeTextLength = 2_000;
    public const int GoalCount = 10;
    public const int MaterialCount = 10;
}
