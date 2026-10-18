using System.Text.Json;
using MakanApp.Application.Ai;

namespace MakanApp.Infrastructure.Ai;

internal static class AiResponseGrounding
{
    // فقط شناسه‌های همان payload مجازند؛ scope یا roster منبع استنتاج شناسه نیست.
    public static void Validate(
        IAiServiceRequest request,
        IAiServiceResponse response,
        JsonSerializerOptions options)
    {
        var input = JsonSerializer.SerializeToElement(request, request.GetType(), options);
        var output = JsonSerializer.SerializeToElement(response, response.GetType(), options);
        var known = new HashSet<(string Type, string Id)>();
        foreach (var containerName in new[] { "context", "studentData", "childData", "classData", "schoolData" })
        {
            if (!input.TryGetProperty(containerName, out var data)) continue;
            AddCollection(data, "subjects", "Subject", "subjectId");
            AddCollection(data, "homeworks", "Homework", "assignmentId");
            AddCollection(data, "exams", "Exam", "examId");
            if (request is not AiTeacherActivitySuggestionsRequest)
                AddCollection(data, "grades", "Grade", "gradeId");
            if (request is AiManagerSchoolSummaryRequest)
                AddCollection(data, "classes", "Class", "classId");
            if (request is AiTeacherStudentInsightRequest or AiManagerStudentInsightRequest &&
                data.TryGetProperty("studentId", out var studentId) && ValidIdentifier(studentId))
                known.Add(("Student", studentId.GetString()!));
        }
        foreach (var (field, kind, alias) in new[]
                 { ("homework", "Homework", "assignmentId"), ("exam", "Exam", "examId") })
            if (input.TryGetProperty(field, out var record)) AddRecord(record, kind, alias);

        foreach (var source in output.GetProperty("sources").EnumerateArray())
            RequireReference(source.GetProperty("type"), source.GetProperty("id"));
        foreach (var field in new[] { "insights", "strengths", "weaknesses", "risks", "attentionAreas" })
        {
            if (!output.TryGetProperty(field, out var findings)) continue;
            foreach (var finding in findings.EnumerateArray())
                if (finding.TryGetProperty("entityId", out var entityId) && entityId.ValueKind != JsonValueKind.Null)
                {
                    if (!finding.TryGetProperty("entityType", out var kind)) throw InvalidResponse();
                    RequireReference(kind, entityId);
                }
        }
        if (request is AiParentChildSummaryRequest && output.TryGetProperty("upcomingItems", out var upcoming))
        {
            foreach (var item in upcoming.EnumerateArray())
            {
                RequireReference(item.GetProperty("type"), item.GetProperty("id"));
                var collection = item.GetProperty("type").GetString() == "Homework" ? "homeworks" : "exams";
                if (!input.GetProperty("childData").GetProperty(collection).EnumerateArray().Any(record =>
                        RecordId(record, collection == "homeworks" ? "assignmentId" : "examId") == item.GetProperty("id").GetString() &&
                        record.TryGetProperty("title", out var title) && title.GetString() == item.GetProperty("title").GetString() &&
                        (!item.TryGetProperty("date", out var date) || date.ValueKind == JsonValueKind.Null ||
                         record.TryGetProperty("date", out var suppliedDate) && date.GetString() == suppliedDate.GetString())))
                    throw InvalidResponse();
            }
        }
        return;

        void AddCollection(JsonElement data, string collection, string kind, string alias)
        {
            if (!data.TryGetProperty(collection, out var records)) return;
            foreach (var record in records.EnumerateArray()) AddRecord(record, kind, alias);
        }
        void AddRecord(JsonElement record, string kind, string alias)
        {
            if (RecordId(record, alias) is { } id) known.Add((kind, id));
        }
        void RequireReference(JsonElement kind, JsonElement id)
        {
            if (kind.ValueKind != JsonValueKind.String || !ValidIdentifier(id) ||
                !known.Contains((kind.GetString()!, id.GetString()!))) throw InvalidResponse();
        }
    }

    private static string? RecordId(JsonElement record, string alias)
    {
        if (record.ValueKind != JsonValueKind.Object) return null;
        // وجود id نامعتبر نباید با alias سالم ترمیم شود.
        if (!record.TryGetProperty("id", out var id) && !record.TryGetProperty(alias, out id)) return null;
        return ValidIdentifier(id) ? id.GetString() : null;
    }

    private static bool ValidIdentifier(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { Length: <= 128 } id &&
        !string.IsNullOrWhiteSpace(id);

    private static AiServiceException InvalidResponse() => new(
        AiServiceErrorCodes.ResponseInvalid, "ارجاع پاسخ هوش مصنوعی در زمینهٔ مجاز وجود ندارد.");
}
