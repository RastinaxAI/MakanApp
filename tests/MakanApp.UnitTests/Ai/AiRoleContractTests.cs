using System.Text.Json;
using MakanApp.Application.Ai;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class AiRoleContractTests
{
    public static TheoryData<IAiServiceRequest, string> Requests => new()
    {
        { new AiStudentAnalysisRequest("req", "corr", Actor("Student"), StudentScope(), AiAuthorizedContext.Empty), "studentData" },
        { new AiStudyPlanRequest("req", "corr", Actor("Student"), StudentScope(), AiAuthorizedContext.Empty, [], new AiStudyPreferences(null, null)), "preferences" },
        { new AiHomeworkExplanationRequest("req", "corr", Actor("Student"), StudentScope(), EmptyRecord(), "سؤال", AiAuthorizedContext.Empty), "homework" },
        { new AiExamPreparationRequest("req", "corr", Actor("Student"), StudentScope(), EmptyRecord(), AiAuthorizedContext.Empty, AiAuthorizedContext.Empty), "exam" },
        { new AiParentChildSummaryRequest("req", "corr", Actor("Parent"), StudentScope(), AiAuthorizedContext.Empty), "childData" },
        { new AiTeacherClassSummaryRequest("req", "corr", Actor("Teacher"), TeacherClassScope(), TeacherClassData(), "fa"), "classData" },
        { new AiTeacherStudentInsightRequest("req", "corr", Actor("Teacher"), TeacherStudentScope(), TeacherStudentData(), "fa"), "studentData" },
        { new AiTeacherActivitySuggestionsRequest("req", "corr", Actor("Teacher"), TeacherClassScope(), TeacherClassData(), "هدف", new AiActivityConstraints(null, [], null)), "constraints" },
        { new AiManagerSchoolSummaryRequest("req", "corr", Actor("Manager"), ManagerSchoolScope(), ManagerSchoolData(), "fa"), "schoolData" },
        { new AiManagerClassInsightRequest("req", "corr", Actor("Manager"), ManagerClassScope(), ManagerClassData(), "fa"), "classData" },
        { new AiManagerStudentInsightRequest("req", "corr", Actor("Manager"), ManagerStudentScope(), ManagerStudentData(), "fa"), "studentData" }
    };

    [Theory]
    [MemberData(nameof(Requests))]
    public void RoleRequest_SerializesAsCamelCaseMinimumContext(
        IAiServiceRequest request,
        string operationField)
    {
        var json = JsonSerializer.Serialize(request, request.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("req", root.GetProperty("requestId").GetString());
        Assert.Equal("corr", root.GetProperty("correlationId").GetString());
        Assert.True(root.TryGetProperty("actor", out _));
        Assert.True(root.TryGetProperty("scope", out var scope));
        Assert.True(root.TryGetProperty(operationField, out _));
        Assert.False(root.TryGetProperty("RequestId", out _));
        Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contact", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("conversation", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("moderation", json, StringComparison.OrdinalIgnoreCase);

        var expectedScopeFields = request switch
        {
            AiTeacherClassSummaryRequest or AiTeacherActivitySuggestionsRequest => 3,
            AiTeacherStudentInsightRequest => 4,
            AiManagerSchoolSummaryRequest => 2,
            AiManagerClassInsightRequest => 3,
            AiManagerStudentInsightRequest => 4,
            _ => (int?)null
        };
        if (expectedScopeFields.HasValue)
        {
            Assert.Equal(expectedScopeFields.Value, scope.EnumerateObject().Count());
            Assert.DoesNotContain(scope.EnumerateObject(), property =>
                property.Value.ValueKind == JsonValueKind.Null);
        }
    }

    private static AiActor Actor(string role) => new("user", role);
    private static AiScope StudentScope() => new("org", "org", null, "student");
    private static AiTeacherClassScope TeacherClassScope() => new("org", "org", "class");
    private static AiTeacherStudentScope TeacherStudentScope() => new("org", "org", "class", "student");
    private static AiManagerSchoolScope ManagerSchoolScope() => new("org", "org");
    private static AiManagerClassScope ManagerClassScope() => new("org", "org", "class");
    private static AiManagerStudentScope ManagerStudentScope() => new("org", "org", "class", "student");
    private static IReadOnlyDictionary<string, JsonElement> EmptyRecord() => new Dictionary<string, JsonElement>();
    private static AiTeacherClassData TeacherClassData() => new("class", [], [], [], [], [], [], null);
    private static AiTeacherStudentData TeacherStudentData() => new("student", [], [], [], [], []);
    private static AiManagerSchoolData ManagerSchoolData() => new("org", [], [], [], [], [], [], null);
    private static AiManagerClassData ManagerClassData() => new("class", [], [], [], [], [], [], null);
    private static AiManagerStudentData ManagerStudentData() => new("student", [], [], [], [], []);
}
