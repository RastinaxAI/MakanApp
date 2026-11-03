using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MakanApp.Api.Errors;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task PersonalClassContractOpenApiHasResolvedReferencesUniqueIdsAndActualSecurity()
    {
        using var client = factory.CreateClient();
        using var document = await ReadContractAsync(client, "fa-IR");
        using var english = await ReadContractAsync(client, "en-US");
        Assert.Equal(document.RootElement.GetProperty("paths").EnumerateObject().Count(), english.RootElement.GetProperty("paths").EnumerateObject().Count());
        var root = document.RootElement;
        var bearer = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("Opaque", bearer.GetProperty("bearerFormat").GetString());
        var operations = root.GetProperty("paths").EnumerateObject().SelectMany(p => p.Value.EnumerateObject()
            .Where(m => new[] { "get", "post", "put", "patch", "delete", "head", "options" }.Contains(m.Name))
            .Select(m => (Path: p.Name, Method: m.Name, Operation: m.Value))).ToArray();
        Assert.True(operations.Length >= 426);
        var ids = operations.Select(o => o.Operation.GetProperty("operationId").GetString()).ToArray();
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(ids.Length, ids.Distinct().Count());
        foreach (var node in JsonObjects(root))
            if (node.TryGetProperty("$ref", out var reference)) _ = ResolveReference(root, reference.GetString()!);
        foreach (var path in new[] { "/api/v1/auth/otp/challenges", "/api/v1/auth/otp/verify", "/api/v1/auth/refresh" })
            Assert.Empty(Assert.Single(root.GetProperty("paths").GetProperty(path).GetProperty("post").GetProperty("security").EnumerateArray()).EnumerateObject());
        foreach (var path in new[] { "/api/v1/personal-classes/page", "/api/v1/conversations", "/api/v1/commerce/personal/subscription" })
            Assert.True(root.GetProperty("paths").GetProperty(path).GetProperty("get").GetProperty("security")[0].TryGetProperty("Bearer", out _));
        var admin = root.GetProperty("paths").GetProperty("/api/admin/v1/personal-class-requests/{id}/review").GetProperty("post");
        Assert.Contains(admin.GetProperty("x-platform-permissions").EnumerateArray(), x => x.GetString() == MakanApp.Domain.Platform.PlatformPermissionKeys.PersonalClassesReview);
        var formationErrors = root.GetProperty("paths").GetProperty("/api/v1/personal-class-requests").GetProperty("post").GetProperty("responses").GetProperty("400").GetProperty("x-error-codes");
        Assert.Contains(formationErrors.EnumerateArray(), x => x.GetString() == AcademicErrorCodes.PersonalClassRequestInvalid);
        var academicErrors = root.GetProperty("paths").GetProperty("/api/v1/academic/personal-classes/{classId}/assignments").GetProperty("post").GetProperty("responses").GetProperty("400").GetProperty("x-error-codes");
        Assert.Contains(academicErrors.EnumerateArray(), x => x.GetString() == AssessmentErrorCodes.AssignmentMaxScoreInvalid);
        Assert.All(operations.Where(o => o.Path.Contains("personal-class") || o.Path.StartsWith("/api/v1/academic/")), o =>
        {
            Assert.False(string.IsNullOrWhiteSpace(o.Operation.GetProperty("summary").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(o.Operation.GetProperty("description").GetString()));
        });
        foreach (var path in new[] { "/api/v1/sources/{id}/content", "/api/admin/v1/moderation/cases/{caseId}/evidence/files/{fileAssetId}" })
        {
            var response = root.GetProperty("paths").GetProperty(path).GetProperty("get").GetProperty("responses");
            Assert.Equal("binary", response.GetProperty("200").GetProperty("content").GetProperty("application/octet-stream").GetProperty("schema").GetProperty("format").GetString());
            Assert.True(response.TryGetProperty("206", out _)); Assert.True(response.TryGetProperty("416", out _));
        }
        var deletion = root.GetProperty("paths").GetProperty("/api/v1/personal-classes/{id}/memberships/{memberId}").GetProperty("delete").GetProperty("responses");
        Assert.True(deletion.TryGetProperty("204", out _)); Assert.False(deletion.TryGetProperty("200", out _));
        var input = root.GetProperty("components").GetProperty("schemas").GetProperty("PersonalClassRequestInput").GetProperty("properties");
        Assert.Equal(200, input.GetProperty("title").GetProperty("x-normalized-max-length").GetInt32());
        Assert.Matches(input.GetProperty("title").GetProperty("pattern").GetString()!, "  " + new string('t', 200) + "  ");
        Assert.DoesNotMatch(input.GetProperty("title").GetProperty("pattern").GetString()!, new string('t', 201));
        Assert.Equal(1, input.GetProperty("suggestedCapacity").GetProperty("minimum").GetInt32());
        Assert.True(root.GetProperty("components").GetProperty("schemas").GetProperty("MakanProblemDetails").GetProperty("properties").TryGetProperty("code", out _));
        var form = root.GetProperty("paths").GetProperty("/api/v1/files").GetProperty("post").GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema");
        Assert.Contains(form.GetProperty("required").EnumerateArray(), x => x.GetString() == "File");
        Assert.True(form.GetProperty("properties").GetProperty("File").GetProperty("x-max-file-size-bytes").GetInt64() > 0);
    }

    [Fact]
    public async Task PersonalClassContractAuthenticationValidationAndConflictErrorsKeepTypedCorrelation()
    {
        using var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Accept.ParseAdd("image/png");
        using var unauthorized = await anonymous.GetAsync("/api/v1/personal-classes/page");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("AUTH_REQUIRED", await ProblemAsync(unauthorized));
        var actor = await ActorAsync();
        actor.Client.DefaultRequestHeaders.Accept.ParseAdd("image/png");
        using var forbidden = await actor.Client.GetAsync("/api/admin/v1/personal-class-requests");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode); Assert.Equal(ApiErrorCodes.Forbidden, await ProblemAsync(forbidden));
        actor.Client.DefaultRequestHeaders.Accept.Clear();
        using var malformed = await actor.Client.PostAsync("/api/v1/personal-class-requests", new StringContent("{", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode); Assert.Equal(ApiErrorCodes.InvalidRequest, await ProblemAsync(malformed));
        using var json = JsonDocument.Parse(await malformed.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("correlationId").GetString()));
        Assert.True(json.RootElement.TryGetProperty("traceId", out _)); Assert.True(json.RootElement.TryGetProperty("errors", out _));
        var command = new CreatePersonalClassRequestCommand(Guid.NewGuid(), new("طرح معتبر", "هدف معتبر", null, 1, null, null));
        var request = await PostAsync<PersonalClassRequestDto>(actor.Client, "/api/v1/personal-class-requests", command);
        Assert.Equal(request.Id, (await PostAsync<PersonalClassRequestDto>(actor.Client, "/api/v1/personal-class-requests", command)).Id);
        using var changed = await actor.Client.PostAsJsonAsync("/api/v1/personal-class-requests", command with { Details = command.Details with { Title = "قصد متفاوت" } }, Json);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        await AcademicWriteAsync<PersonalClassRequestDto>(actor.Client, HttpMethod.Put, $"/api/v1/personal-class-requests/{request.Id}",
            new EditPersonalClassRequestCommand(request.Version, command.Details with { Purpose = "هدف تازه" }));
        using var stale = await actor.Client.PutAsJsonAsync($"/api/v1/personal-class-requests/{request.Id}", new EditPersonalClassRequestCommand(request.Version, command.Details), Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(AcademicErrorCodes.PersonalClassVersionConflict, await ProblemAsync(stale));
    }

    [Fact]
    public async Task PersonalClassContractInputBoundariesMatchNormalizedDomainRules()
    {
        var actor = await ActorAsync();
        var route = "/api/v1/personal-class-requests";
        var valid = new PersonalClassRequestInput(new string('t', 200), new string('p', 2000), new string('d', 4000), 1, null, null);
        var accepted = await PostAsync<PersonalClassRequestDto>(actor.Client, route, new CreatePersonalClassRequestCommand(Guid.NewGuid(), valid));
        Assert.Equal(200, accepted.Details.Title.Length);
        foreach (var bad in new[] { valid with { Title = "" }, valid with { Title = "   " }, valid with { Title = new string('x', 201) },
                     valid with { Purpose = new string('x', 2001) }, valid with { Description = new string('x', 4001) }, valid with { SuggestedCapacity = 0 } })
        {
            using var denied = await actor.Client.PostAsJsonAsync(route, new CreatePersonalClassRequestCommand(Guid.NewGuid(), bad), Json);
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }
        var s = await PersonalAcademicAsync();
        var draft = AssignmentDraft() with { Title = new string('t', 200), Description = new string('d', 10000), MaxScore = .01m };
        _ = await PostAsync<AssignmentResult>(s.Manager.Client, $"/api/v1/academic/personal-classes/{s.Class.Id}/assignments", draft);
        foreach (var bad in new[] { draft with { Title = new string('x', 201) }, draft with { Description = new string('x', 10001) },
                     draft with { MaxScore = 0 }, draft with { MaxAttempts = 0 }, draft with { DueAtUtc = factory.Clock.GetUtcNow().UtcDateTime },
                     draft with { AttachmentFileIds = Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToArray() } })
        {
            using var denied = await s.Manager.Client.PostAsJsonAsync($"/api/v1/academic/personal-classes/{s.Class.Id}/assignments", bad, Json);
            Assert.Equal(bad.AttachmentFileIds?.Count > 20 ? HttpStatusCode.Forbidden : HttpStatusCode.BadRequest, denied.StatusCode);
        }
        foreach (var size in new[] { 0, 101 })
        {
            using var denied = await s.Manager.Client.GetAsync($"/api/v1/personal-classes/{s.Class.Id}/memberships?pageSize={size}");
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }
    }

    [Fact]
    public async Task PersonalClassContractCorePagesHaveStableTiesCountsAndCurrentActorScope()
    {
        var s = await PersonalAcademicAsync();
        var route = $"/api/v1/personal-classes/{s.Class.Id}";
        await AssertCorePagesAsync(s.Manager.Client, route + "/memberships", 3);
        await AssertCorePagesAsync(s.Manager.Client, "/api/v1/personal-classes/invitations/page", 2);
        await AssertCorePagesAsync(s.Learner.Client, "/api/v1/personal-classes/invitations/page", 1);
        await AssertCorePagesAsync(s.Learner.Client, route + "/memberships", 1);
        await AssertCorePagesAsync(s.Manager.Client, "/api/v1/personal-classes/page", 1);
        _ = await PostAsync<PersonalClassRequestDto>(s.Manager.Client, "/api/v1/personal-class-requests",
            new CreatePersonalClassRequestCommand(Guid.NewGuid(), new("درخواست دوم", "آزمون ترتیب پایدار", null, 1, null, null)));
        await AssertCorePagesAsync(s.Manager.Client, "/api/v1/personal-class-requests", 2);
        var current = await GetAsync<PersonalClassDto>(s.Manager.Client, route);
        await AcademicWriteAsync<PersonalClassDto>(s.Manager.Client, HttpMethod.Put, route + "/settings",
            new ConfigurePersonalClassCommand(current.Version, 10, true, "پذیرش صریح درخواست"));
        var first = await ActorAsync(); var second = await ActorAsync();
        foreach (var actor in new[] { first, second })
            _ = await PostAsync<PersonalClassJoinDto>(actor.Client, route + "/join-requests", new CreatePersonalClassJoinCommand(Guid.NewGuid()));
        await AssertCorePagesAsync(s.Manager.Client, "/api/v1/personal-classes/join-requests?classId=" + s.Class.Id, 2);
        await AssertCorePagesAsync(first.Client, "/api/v1/personal-classes/join-requests", 1);
        await AssertCorePagesAsync(first.Client, "/api/v1/personal-classes/page", 0);
        using var denied = await first.Client.GetAsync(route + "/memberships");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    private static async Task AssertCorePagesAsync(HttpClient client, string route, int total)
    {
        var ids = new List<Guid>();
        var separator = route.Contains('?') ? "&" : "?";
        for (var page = 1; page <= total + 1; page++)
        {
            var url = route + separator + $"page={page}&pageSize=1";
            using var value = JsonDocument.Parse(await client.GetStringAsync(url));
            var root = value.RootElement;
            Assert.Equal(total, root.GetProperty("totalCount").GetInt32());
            Assert.Equal(page, root.GetProperty("page").GetInt32());
            var rows = root.GetProperty("items").EnumerateArray().ToArray();
            if (page > total) Assert.Empty(rows);
            else
            {
                var id = Assert.Single(rows).GetProperty("id").GetGuid(); ids.Add(id);
                using var repeated = JsonDocument.Parse(await client.GetStringAsync(url));
                Assert.Equal(id, repeated.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
            }
        }
        Assert.Equal(total, ids.Distinct().Count());
    }

    [Fact]
    public async Task PersonalClassContractPagedQueuesAndHistoryRemainScopedAndAuditIsPrivateIdempotent()
    {
        var s = await PersonalAcademicAsync(preserveReviewAudit: true);
        var correlation = "contract-audit-" + Guid.NewGuid().ToString("N");
        s.Manager.Client.DefaultRequestHeaders.Add("X-Correlation-ID", correlation);
        var assignment = await PersonalAssignmentAsync(s);
        var attempts = new List<SubmissionAttemptResult>();
        foreach (var learner in new[] { s.Learner, s.Peer })
        {
            var draft = await PostAsync<SubmissionAttemptResult>(learner.Client, $"/api/v1/academic/assignments/{assignment.Id}/attempts", null);
            draft = await AcademicWriteAsync<SubmissionAttemptResult>(learner.Client, HttpMethod.Patch, $"/api/v1/academic/submission-attempts/{draft.Id}/draft", new SaveSubmissionDraftCommand("PRIVATE-ANSWER-CONTRACT", draft.RowVersion));
            await PostAsync<SubmissionReceipt>(learner.Client, $"/api/v1/academic/submission-attempts/{draft.Id}/submit", new FinalSubmitAssignmentCommand(draft.RowVersion));
            attempts.Add(draft);
        }
        var pageRoute = $"/api/v1/academic/evaluations/queue/page?classKind=PersonalClass&classId={s.Class.Id}&pageSize=1";
        var first = await GetAsync<AcademicPage<EvaluationQueueItemResult>>(s.Manager.Client, pageRoute);
        var second = await GetAsync<AcademicPage<EvaluationQueueItemResult>>(s.Manager.Client, pageRoute + "&page=2");
        Assert.Equal(2, first.TotalCount); Assert.Single(first.Items); Assert.Single(second.Items);
        Assert.NotEqual(first.Items.Single().SubmissionAttemptId, second.Items.Single().SubmissionAttemptId);
        using var denied = await s.Learner.Client.GetAsync(pageRoute); Assert.False(denied.IsSuccessStatusCode);
        var submitted = await GetAsync<AcademicPage<SubmissionAttemptResult>>(s.Manager.Client, $"/api/v1/academic/assignments/{assignment.Id}/submitted-attempts/page?pageSize=1");
        Assert.Equal(2, submitted.TotalCount); Assert.Single(submitted.Items);
        using var legacy = JsonDocument.Parse(await s.Manager.Client.GetStringAsync($"/api/v1/academic/assignments/{assignment.Id}/submitted-attempts"));
        Assert.Equal(JsonValueKind.Array, legacy.RootElement.ValueKind);
        var own = await GetAsync<AcademicPage<SubmissionAttemptResult>>(s.Learner.Client, $"/api/v1/academic/assignments/{assignment.Id}/attempts/me/page?pageSize=1");
        Assert.Equal(1, own.TotalCount); Assert.Equal(attempts[0].Id, own.Items.Single().Id);
        var managerHistory = await GetAsync<PersonalClassPage<PersonalClassHistoryItem>>(s.Manager.Client, $"/api/v1/personal-classes/{s.Class.Id}/history?pageSize=100");
        Assert.Contains(managerHistory.Items, x => x.Action == "personal_class.academic.AssignmentPublished");
        Assert.DoesNotContain(managerHistory.Items, x => x.ResourceType == "PersonalClassRequest");
        Assert.All(managerHistory.Items, x => { Assert.Null(x.ActorUserId); Assert.Null(x.CorrelationId); });
        var platformHistory = await GetAsync<PersonalClassPage<PersonalClassHistoryItem>>(s.Admin.Client, $"/api/admin/v1/personal-classes/{s.Class.Id}/history?pageSize=100");
        await WithDb(async db =>
        {
            var formationId = await db.PersonalClasses.Where(c => c.Id == s.Class.Id).Select(c => c.FormationRequestId).SingleAsync();
            Assert.NotNull(formationId);
            Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.TargetType == "PersonalClassRequest" && a.TargetId == formationId.Value.ToString() && a.Action == "personal_class.request.Approved"));
        });
        Assert.Contains(platformHistory.Items, x => x.Action == "personal_class.request.Approved");
        using var learnerHistory = await s.Learner.Client.GetAsync($"/api/v1/personal-classes/{s.Class.Id}/history"); Assert.Equal(HttpStatusCode.NotFound, learnerHistory.StatusCode);
        await WithDb(async db =>
        {
            var audit = Assert.Single(await db.AdminAuditLogs.Where(a => a.TargetId == assignment.Id.ToString() && a.Action == "personal_class.academic.AssignmentPublished").ToArrayAsync());
            Assert.Equal(correlation, audit.CorrelationId); Assert.Equal(s.Manager.SessionId, audit.ActorSessionId);
            Assert.DoesNotContain("PRIVATE-", audit.AfterSummary ?? "");
            Assert.Equal(2, await db.AdminAuditLogs.CountAsync(a => a.TargetId == s.Class.Id.ToString() && a.Action == "personal_class.MembershipAccepted"));
        });
        await EndAcademicMemberAsync(s, s.Learner);
        using var revoked = await s.Learner.Client.GetAsync($"/api/v1/academic/assignments/{assignment.Id}/attempts/me/page");
        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
    }

    private static IEnumerable<JsonElement> JsonObjects(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            yield return value;
            foreach (var property in value.EnumerateObject()) foreach (var node in JsonObjects(property.Value)) yield return node;
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) foreach (var node in JsonObjects(item)) yield return node;
    }
    private static async Task<JsonDocument> ReadContractAsync(HttpClient client, string culture)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
            return JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }
    private static JsonElement ResolveReference(JsonElement root, string reference)
    {
        Assert.StartsWith("#/", reference);
        var value = root;
        foreach (var part in reference[2..].Split('/')) value = value.GetProperty(part.Replace("~1", "/").Replace("~0", "~"));
        return value;
    }
}
