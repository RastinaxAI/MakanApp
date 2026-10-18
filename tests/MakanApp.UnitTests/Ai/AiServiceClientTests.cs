using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MakanApp.Application.Ai;
using MakanApp.Infrastructure.Ai;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class AiServiceClientTests
{
    public static IEnumerable<object[]> OperationRequests =>
        AiRoleContractTests.Requests.Select(item => new object[] { item[0] })
            .Append([Request()]);

    [Theory]
    [MemberData(nameof(OperationRequests))]
    public async Task EveryOperation_UsesExactContractRouteHeadersAndResponse(IAiServiceRequest request)
    {
        string? path = null;
        var response = request switch
        {
            AiChatRequest => new { answer = new { text = "پاسخ", language = "fa" }, insights = Array.Empty<object>(), risks = Array.Empty<object>() } as object,
            AiStudentAnalysisRequest => new { summary = "خلاصه", strengths = Array.Empty<object>(), weaknesses = Array.Empty<object>(), risks = Array.Empty<object>() },
            AiStudyPlanRequest => new { summary = "خلاصه", priorities = Array.Empty<object>(), planItems = Array.Empty<object>() },
            AiHomeworkExplanationRequest => new { explanation = "توضیح", steps = Array.Empty<object>(), hints = Array.Empty<object>() },
            AiExamPreparationRequest => new { summary = "خلاصه", priorityTopics = Array.Empty<object>(), preparationSteps = Array.Empty<object>(), risks = Array.Empty<object>() },
            AiParentChildSummaryRequest => new { academicSummary = "خلاصه", progress = new { trend = "Unknown", description = "داده کافی نیست" }, attentionAreas = Array.Empty<object>(), upcomingItems = Array.Empty<object>(), risks = Array.Empty<object>() },
            AiTeacherActivitySuggestionsRequest => new { summary = "خلاصه", activities = Array.Empty<object>() },
            _ => new { summary = "خلاصه", strengths = Array.Empty<object>(), attentionAreas = Array.Empty<object>(), risks = Array.Empty<object>() }
        };
        var body = JsonSerializer.SerializeToNode(response)!.AsObject();
        body["requestId"] = request.RequestId;
        body["correlationId"] = request.CorrelationId;
        body["recommendations"] = new JsonArray();
        body["sources"] = new JsonArray();
        body["metadata"] = new JsonObject { ["modelAlias"] = "synthetic-model", ["processingTimeMs"] = 1 };
        var handler = new StubHandler(async (outgoing, cancellation) =>
        {
            path = outgoing.RequestUri!.AbsolutePath;
            Assert.Equal("Bearer", outgoing.Headers.Authorization!.Scheme);
            Assert.Equal("v1", outgoing.Headers.GetValues("X-AI-Contract-Version").Single());
            Assert.Equal("application/json", outgoing.Content!.Headers.ContentType!.MediaType);
            using var payload = JsonDocument.Parse(await outgoing.Content.ReadAsStringAsync(cancellation));
            Assert.Equal(request.CorrelationId, payload.RootElement.GetProperty("correlationId").GetString());
            Assert.Equal(request.CorrelationId, outgoing.Headers.GetValues("X-Correlation-ID").Single());
            return JsonResponse(HttpStatusCode.OK, body.ToJsonString());
        });
        var client = CreateClient(handler);
        var expected = request switch
        {
            AiChatRequest value => Await(client.ChatAsync(value, CancellationToken.None), "chat"),
            AiStudentAnalysisRequest value => Await(client.AnalyzeStudentAsync(value, CancellationToken.None), "students/analyze"),
            AiStudyPlanRequest value => Await(client.CreateStudyPlanAsync(value, CancellationToken.None), "study-plan"),
            AiHomeworkExplanationRequest value => Await(client.ExplainHomeworkAsync(value, CancellationToken.None), "homeworks/explain"),
            AiExamPreparationRequest value => Await(client.PrepareExamAsync(value, CancellationToken.None), "exams/prepare"),
            AiParentChildSummaryRequest value => Await(client.SummarizeChildAsync(value, CancellationToken.None), "parents/child-summary"),
            AiTeacherClassSummaryRequest value => Await(client.SummarizeTeacherClassAsync(value, CancellationToken.None), "teachers/class-summary"),
            AiTeacherStudentInsightRequest value => Await(client.GetTeacherStudentInsightAsync(value, CancellationToken.None), "teachers/student-insight"),
            AiTeacherActivitySuggestionsRequest value => Await(client.SuggestTeacherActivitiesAsync(value, CancellationToken.None), "teachers/activity-suggestions"),
            AiManagerSchoolSummaryRequest value => Await(client.SummarizeManagerSchoolAsync(value, CancellationToken.None), "managers/school-summary"),
            AiManagerClassInsightRequest value => Await(client.GetManagerClassInsightAsync(value, CancellationToken.None), "managers/class-insight"),
            AiManagerStudentInsightRequest value => Await(client.GetManagerStudentInsightAsync(value, CancellationToken.None), "managers/student-insight"),
            _ => throw new InvalidOperationException()
        };
        Assert.Equal("/api/v1/ai/" + await expected, path);
        Assert.Equal(1, handler.CallCount);

        static async Task<string> Await<T>(Task<T> task, string suffix) { await task; return suffix; }
    }

    [Fact]
    public async Task DefaultTimeout_AllowsCurrentQueueAndInferenceBudget()
    {
        Assert.Equal(180, new AiServiceOptions().TimeoutSeconds);
        var options = new AiServiceOptions { Enabled = true };
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, ValidResponse())));
        var client = new AiServiceClient(new HttpClient(handler), new StubCredentialProvider(), options,
            new AiServiceCircuitBreaker(options, TimeProvider.System));
        await client.ChatAsync(Request(), CancellationToken.None);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task AnalyzeStudentAsync_UsesFrozenRouteAndParsesStrictResponse()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            captured = request;
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(
                HttpStatusCode.OK,
                "{\"requestId\":\"req-role-1\",\"correlationId\":\"corr-role-1\"," +
                "\"summary\":\"خلاصه\",\"strengths\":[],\"weaknesses\":[],\"risks\":[]," +
                "\"recommendations\":[],\"sources\":[]," +
                "\"metadata\":{\"modelAlias\":\"education-fast\",\"processingTimeMs\":3}}");
        });
        var client = CreateClient(handler);
        var request = new AiStudentAnalysisRequest(
            "req-role-1",
            "corr-role-1",
            new AiActor("user-1", "Student"),
            new AiScope("org-1", "org-1", null, "student-1"),
            AiAuthorizedContext.Empty);

        var result = await client.AnalyzeStudentAsync(request, CancellationToken.None);

        Assert.Equal("خلاصه", result.Summary);
        Assert.Equal("/api/v1/ai/students/analyze", captured!.RequestUri!.AbsolutePath);
        Assert.Contains("\"studentData\"", body);
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("service-token", captured.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task ChatAsync_SendsCredentialCorrelationAndContract_AndParsesResponse()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler((request, _) =>
        {
            captured = request;
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, ValidResponse()));
        });
        var client = CreateClient(handler);

        var result = await client.ChatAsync(Request(), CancellationToken.None);

        Assert.Equal("پاسخ", result.Answer.Text);
        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("service-token", captured.Headers.Authorization?.Parameter);
        Assert.Equal("corr-1", captured.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Equal("v1", captured.Headers.GetValues("X-AI-Contract-Version").Single());
        Assert.Equal("application/json", captured.Content!.Headers.ContentType!.MediaType);
        Assert.Equal(1, handler.CallCount);
    }

    public static TheoryData<HttpStatusCode, string> ErrorMappings => new()
    {
        { HttpStatusCode.BadRequest, AiServiceErrorCodes.InvalidRequest },
        { HttpStatusCode.Unauthorized, AiServiceErrorCodes.ServiceUnauthorized },
        { HttpStatusCode.Forbidden, AiServiceErrorCodes.ServiceForbidden },
        { HttpStatusCode.UnprocessableEntity, AiServiceErrorCodes.InvalidRequest },
        { HttpStatusCode.TooManyRequests, AiServiceErrorCodes.RateLimited },
        { HttpStatusCode.BadGateway, AiServiceErrorCodes.ResponseInvalid },
        { HttpStatusCode.InternalServerError, AiServiceErrorCodes.ServiceUnavailable },
        { HttpStatusCode.ServiceUnavailable, AiServiceErrorCodes.ServiceUnavailable },
        { HttpStatusCode.GatewayTimeout, AiServiceErrorCodes.ServiceTimeout }
    };

    [Theory]
    [MemberData(nameof(ErrorMappings))]
    public async Task ChatAsync_MapsUpstreamStatusWithoutRetry(
        HttpStatusCode statusCode,
        string expectedCode)
    {
        var response = new HttpResponseMessage(statusCode);
        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                TimeSpan.FromSeconds(7));
        }
        var handler = new StubHandler((_, _) => Task.FromResult(response));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(Request(), CancellationToken.None));

        Assert.Equal(expectedCode, error.Code);
        Assert.Equal((int)statusCode, error.UpstreamStatusCode);
        Assert.Equal(1, handler.CallCount);
        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            Assert.Equal(TimeSpan.FromSeconds(7), error.RetryAfter);
        }
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"requestId\":\"different\",\"answer\":{\"text\":\"ok\",\"language\":\"fa\"},\"insights\":[],\"risks\":[],\"recommendations\":[],\"sources\":[],\"metadata\":{\"modelAlias\":\"model\",\"processingTimeMs\":1}}")]
    [InlineData("{\"requestId\":\"req-1\",\"answer\":{\"text\":\"\",\"language\":\"fa\"},\"insights\":[],\"risks\":[],\"recommendations\":[],\"sources\":[],\"metadata\":{\"modelAlias\":\"model\",\"processingTimeMs\":1}}")]
    [InlineData("{\"requestId\":\"req-1\",\"answer\":{\"text\":\"ok\",\"language\":\"fa\"},\"insights\":[{\"type\":\"Learning\",\"severity\":\"Unknown\"}],\"risks\":[],\"recommendations\":[],\"sources\":[],\"metadata\":{\"modelAlias\":\"model\",\"processingTimeMs\":1}}")]
    [InlineData("{\"requestId\":\"req-1\",\"answer\":{\"text\":\"ok\",\"language\":\"fa\"},\"insights\":[],\"risks\":[],\"recommendations\":[],\"sources\":[],\"metadata\":{\"modelAlias\":\"model\",\"processingTimeMs\":1},\"unexpected\":true}")]
    public async Task ChatAsync_RejectsMalformedOrSchemaInvalidResponse(string body)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, body)));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(Request(), CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ResponseInvalid, error.Code);
    }

    [Fact]
    public async Task ChatAsync_PreservesCallerCancellation()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(HttpStatusCode.OK, ValidResponse());
        });
        var client = CreateClient(handler);
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ChatAsync(Request(), source.Token));
    }

    [Fact]
    public async Task ChatAsync_MapsInternalTimeout()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(HttpStatusCode.OK, ValidResponse());
        });
        var client = CreateClient(handler, timeoutSeconds: 1);

        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(Request(), CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ServiceTimeout, error.Code);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ChatAsync_ConnectionRefusedIsSanitizedWithoutRetry()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("private-network-detail"));
        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            CreateClient(handler).ChatAsync(Request(), CancellationToken.None));
        Assert.Equal(AiServiceErrorCodes.ServiceUnavailable, error.Code);
        Assert.DoesNotContain("private-network-detail", error.Message);
        Assert.DoesNotContain("private-network-detail", error.ToString());
        Assert.Null(error.InnerException);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ChatAsync_RejectsNonJsonContentType()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidResponse(), Encoding.UTF8, "text/html")
        }));
        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            CreateClient(handler).ChatAsync(Request(), CancellationToken.None));
        Assert.Equal(AiServiceErrorCodes.ResponseInvalid, error.Code);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ChatAsync_TimeoutAlsoCoversResponseBody()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(StreamResponse(new DelayedStream())));
        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            CreateClient(handler, timeoutSeconds: 1).ChatAsync(Request(), CancellationToken.None));
        Assert.Equal(AiServiceErrorCodes.ServiceTimeout, error.Code);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ChatAsync_CallerCancellationDuringBodyReadIsPreserved()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(StreamResponse(new DelayedStream())));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateClient(handler).ChatAsync(Request(), cancellation.Token));
    }

    [Fact]
    public async Task ChatAsync_BrokenBodyTransportIsSanitized()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(StreamResponse(new DelayedStream(fail: true))));
        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            CreateClient(handler).ChatAsync(Request(), CancellationToken.None));
        Assert.Equal(AiServiceErrorCodes.ServiceUnavailable, error.Code);
        Assert.DoesNotContain("private-transport-detail", error.Message);
        Assert.DoesNotContain("private-transport-detail", error.ToString());
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData("missing-time")]
    [InlineData("string-time")]
    [InlineData("unknown-field")]
    [InlineData("wrong-correlation")]
    [InlineData("wrong-request")]
    [InlineData("null-source")]
    [InlineData("null-insight")]
    [InlineData("null-recommendation")]
    [InlineData("unknown-source")]
    [InlineData("numeric-severity")]
    [InlineData("wrong-case-severity")]
    public async Task ChatAsync_StrictResponseRejectsInvalidShapeAndGrounding(string mutation)
    {
        var body = JsonNode.Parse(ValidResponse())!.AsObject();
        switch (mutation)
        {
            case "missing-time": body["metadata"]!.AsObject().Remove("processingTimeMs"); break;
            case "string-time": body["metadata"]!["processingTimeMs"] = "3"; break;
            case "unknown-field": body["metadata"]!["unrecognized"] = true; break;
            case "wrong-correlation": body["correlationId"] = "other"; break;
            case "wrong-request": body["requestId"] = "other"; break;
            case "null-source": body["sources"] = new JsonArray((JsonNode?)null); break;
            case "null-insight": body["insights"] = new JsonArray((JsonNode?)null); break;
            case "null-recommendation": body["recommendations"] = new JsonArray((JsonNode?)null); break;
            case "unknown-source": body["sources"] = JsonNode.Parse("[{\"type\":\"Homework\",\"id\":\"unprovided\"}]"); break;
            case "numeric-severity": body["insights"] = JsonNode.Parse("[{\"type\":\"Learning\",\"severity\":0}]"); break;
            case "wrong-case-severity": body["insights"] = JsonNode.Parse("[{\"type\":\"Learning\",\"severity\":\"info\"}]"); break;
        }
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, body.ToJsonString())));
        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            CreateClient(handler).ChatAsync(Request(), CancellationToken.None));
        Assert.Equal(AiServiceErrorCodes.ResponseInvalid, error.Code);
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData("{\"assignmentId\":\"assignment-1\"}", "Homework", "assignment-1", true)]
    [InlineData("{\"id\":\"explicit\",\"assignmentId\":\"alias\"}", "Homework", "explicit", true)]
    [InlineData("{\"id\":\"explicit\",\"assignmentId\":\"alias\"}", "Homework", "alias", false)]
    [InlineData("{\"id\":null,\"assignmentId\":\"alias\"}", "Homework", "alias", false)]
    [InlineData("{\"id\":\"\",\"assignmentId\":\"alias\"}", "Homework", "alias", false)]
    [InlineData("{\"classId\":\"class-1\"}", "Subject", "class-1", false)]
    [InlineData("{\"subjectId\":\"subject-1\"}", "Subject", "subject-1", true)]
    [InlineData("{\"gradeId\":\"grade-1\"}", "Grade", "grade-1", true)]
    [InlineData("{\"assessmentId\":\"assessment-1\"}", "Grade", "assessment-1", false)]
    [InlineData("{\"examId\":\"exam-1\"}", "Exam", "exam-1", true)]
    public async Task ChatAsync_SourceIdsAreGroundedWithoutInventedAliasFallback(
        string record, string kind, string id, bool accepted)
    {
        IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> records =
            [JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(record)!];
        var context = new AiAuthorizedContext(
            kind == "Subject" ? records : [], kind == "Grade" ? records : [],
            kind == "Homework" ? records : [], kind == "Exam" ? records : [], []);
        var request = Request() with { Context = context };
        var body = JsonNode.Parse(ValidResponse())!;
        body["sources"] = new JsonArray(new JsonObject { ["type"] = kind, ["id"] = id });
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, body.ToJsonString())));
        var client = CreateClient(handler);
        if (accepted) await client.ChatAsync(request, CancellationToken.None);
        else
        {
            var error = await Assert.ThrowsAsync<AiServiceException>(() => client.ChatAsync(request, CancellationToken.None));
            Assert.Equal(AiServiceErrorCodes.ResponseInvalid, error.Code);
        }
    }

    [Theory]
    [InlineData("selected-student", true)]
    [InlineData("roster-student", false)]
    public async Task StudentInsight_GroundsOnlyExplicitSelectedStudent(string sourceId, bool accepted)
    {
        var request = new AiTeacherStudentInsightRequest("req-1", "corr-1", new AiActor("teacher", "Teacher"),
            new AiTeacherStudentScope("org", "org", "class", "selected-student"),
            new AiTeacherStudentData("selected-student", [], [], [], [], []));
        var response = new AiRoleInsightResponse("req-1", "corr-1", "خلاصه", [], [], [], [],
            [new AiSourceReference("Student", sourceId)], new AiResponseMetadata("synthetic", 1));
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)))));
        var client = CreateClient(handler);
        if (accepted) await client.GetTeacherStudentInsightAsync(request, CancellationToken.None);
        else
        {
            var error = await Assert.ThrowsAsync<AiServiceException>(() => client.GetTeacherStudentInsightAsync(request, CancellationToken.None));
            Assert.Equal(AiServiceErrorCodes.ResponseInvalid, error.Code);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ParentUpcomingItem_RequiresMatchingAuthorizedTitle(bool matchingTitle)
    {
        var records = new Dictionary<string, JsonElement>
        {
            ["assignmentId"] = JsonSerializer.SerializeToElement("assignment-1"),
            ["title"] = JsonSerializer.SerializeToElement("عنوان مجاز")
        };
        var request = new AiParentChildSummaryRequest("req-1", "corr-1", new AiActor("parent", "Parent"),
            new AiScope("org", "org", null, "child"), new AiAuthorizedContext([], [], [records], [], []));
        var response = new AiParentChildSummaryResponse("req-1", "corr-1", "خلاصه", new AiProgress("Unknown", "داده کافی نیست"), [],
            [new AiUpcomingItem("Homework", "assignment-1", matchingTitle ? "عنوان مجاز" : "عنوان ساختگی", null)], [], [], [],
            new AiResponseMetadata("synthetic", 1));
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)))));
        var client = CreateClient(handler);
        if (matchingTitle) await client.SummarizeChildAsync(request, CancellationToken.None);
        else
        {
            var error = await Assert.ThrowsAsync<AiServiceException>(() => client.SummarizeChildAsync(request, CancellationToken.None));
            Assert.Equal(AiServiceErrorCodes.ResponseInvalid, error.Code);
        }
    }

    [Fact]
    public async Task ChatAsync_RecoversCircuitAfterDependencyRestored()
    {
        var clock = new AdjustableClock();
        var healthy = false;
        var handler = new StubHandler((_, _) => Task.FromResult(healthy
            ? JsonResponse(HttpStatusCode.OK, ValidResponse()) : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var client = CreateClient(handler, timeProvider: clock);
        for (var index = 0; index < 3; index++)
            await Assert.ThrowsAsync<AiServiceException>(() => client.ChatAsync(Request(), CancellationToken.None));
        await Assert.ThrowsAsync<AiServiceException>(() => client.ChatAsync(Request(), CancellationToken.None));
        Assert.Equal(3, handler.CallCount);
        clock.Advance(TimeSpan.FromSeconds(31));
        healthy = true;
        await client.ChatAsync(Request(), CancellationToken.None);
        Assert.Equal(4, handler.CallCount);
    }

    [Fact]
    public async Task ChatAsync_OpensCircuitAfterConfiguredTransientFailures()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var client = CreateClient(handler, failureThreshold: 3);

        for (var index = 0; index < 3; index++)
        {
            await Assert.ThrowsAsync<AiServiceException>(() =>
                client.ChatAsync(Request(), CancellationToken.None));
        }
        var openError = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(Request(), CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ServiceUnavailable, openError.Code);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task ChatAsync_Disabled_DoesNotResolveCredentialOrCallNetwork()
    {
        var handler = new StubHandler((_, _) => throw new InvalidOperationException());
        var credential = new StubCredentialProvider { ThrowIfCalled = true };
        var client = CreateClient(handler, enabled: false, credentialProvider: credential);

        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(Request(), CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.FeatureDisabled, error.Code);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ChatAsync_CredentialFailure_IsSanitizedAndDoesNotCallNetwork()
    {
        var handler = new StubHandler((_, _) => throw new InvalidOperationException());
        var client = CreateClient(
            handler,
            credentialProvider: new StubCredentialProvider { ThrowIfCalled = true });

        var error = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(Request(), CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ServiceUnavailable, error.Code);
        Assert.DoesNotContain("Credential provider must not be called", error.Message);
        Assert.DoesNotContain("Credential provider must not be called", error.ToString());
        Assert.Null(error.InnerException);
        Assert.Equal(0, handler.CallCount);
    }

    private static AiServiceClient CreateClient(
        HttpMessageHandler handler,
        bool enabled = true,
        int timeoutSeconds = 5,
        int failureThreshold = 3,
        IAiServiceCredentialProvider? credentialProvider = null,
        TimeProvider? timeProvider = null)
    {
        var options = new AiServiceOptions
        {
            Enabled = enabled,
            BaseUrl = "http://ai.test",
            TimeoutSeconds = timeoutSeconds,
            ContractVersion = "v1",
            CircuitBreakerFailureThreshold = failureThreshold,
            CircuitBreakerBreakSeconds = 30
        };
        return new AiServiceClient(
            new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan },
            credentialProvider ?? new StubCredentialProvider(),
            options,
            new AiServiceCircuitBreaker(options, timeProvider ?? TimeProvider.System));
    }

    private static AiChatRequest Request() => new(
        "req-1",
        "corr-1",
        null,
        new AiActor("user-1", "Student"),
        new AiScope("org-1", "school-1", null, "student-1"),
        new AiMessage("پرسش"),
        AiAuthorizedContext.Empty);

    private static string ValidResponse() =>
        "{\"requestId\":\"req-1\",\"correlationId\":\"corr-1\",\"conversationId\":null," +
        "\"answer\":{\"text\":\"پاسخ\",\"language\":\"fa\"},\"insights\":[],\"risks\":[]," +
        "\"recommendations\":[],\"sources\":[],\"metadata\":{\"modelAlias\":\"education-fast\",\"processingTimeMs\":3}}";

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body) => new(statusCode)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage StreamResponse(Stream stream)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        return response;
    }

    private sealed class DelayedStream(bool fail = false) : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (fail) throw new IOException("private-transport-detail");
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }

    private sealed class AdjustableClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class StubCredentialProvider : IAiServiceCredentialProvider
    {
        public bool ThrowIfCalled { get; init; }

        public ValueTask<AiServiceCredential> GetCredentialAsync(CancellationToken cancellationToken)
        {
            if (ThrowIfCalled)
            {
                throw new InvalidOperationException("Credential provider must not be called.");
            }
            return ValueTask.FromResult(new AiServiceCredential(
                "Bearer",
                "service-token",
                DateTime.UtcNow.AddMinutes(2)));
        }
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return send(request, cancellationToken);
        }
    }
}
