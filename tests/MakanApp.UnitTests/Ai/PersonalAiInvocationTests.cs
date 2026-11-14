using System.Text.Json;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using MakanApp.Application.Ai;
using MakanApp.Application.Commerce;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class PersonalAiInvocationTests
{
    private static AccessContext Context()
    {
        var user = Guid.NewGuid();
        return new(user, user, Guid.NewGuid(), WorkspaceType.Personal, null, null, null, null);
    }

    private static PersonalAiChatIntent Intent(AccessContext context) => PersonalAiChatIntent.Create(context.UserId,
        context.SessionId, context, "synthetic-correlation", new("  پرسش شخصی  ", null));

    [Fact]
    public void PersonalIntentHasOnlyAccountIdentityAndEmptyScopeWithoutWireRole()
    {
        var context = Context(); var intent = Intent(context);
        intent.EnsureScope(context.UserId);
        Assert.Equal(context.UserId, intent.UserId);
        Assert.Equal("پرسش شخصی", intent.Message.Text);
        Assert.Equal(new AiScope(null, null, null, null), intent.Scope);
        Assert.Equal(AiAuthorizedContext.Empty, intent.Context);
        var properties = JsonSerializer.SerializeToElement(intent).EnumerateObject().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("Actor", properties); Assert.DoesNotContain("Role", properties);
        Assert.False((object)intent is IAiServiceRequest);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("session")]
    [InlineData("user")]
    [InlineData("organization")]
    [InlineData("membership")]
    [InlineData("role")]
    [InlineData("subject")]
    [InlineData("class")]
    public void PersonalContextRejectsForeignIdentityAndEveryOrganizationSelector(string mutation)
    {
        var original = Context();
        var context = mutation switch
        {
            "actor" => original with { ActorId = Guid.NewGuid() },
            "session" => original with { SessionId = Guid.NewGuid() },
            "user" => original with { UserId = Guid.NewGuid() },
            "organization" => original with { OrganizationId = Guid.NewGuid() },
            "membership" => original with { MembershipId = Guid.NewGuid() },
            "role" => original with { ActiveRole = OrganizationRole.Teacher },
            "subject" => original with { SubjectOrganizationPersonId = Guid.NewGuid() },
            _ => original with { AssistantClassId = Guid.NewGuid() }
        };
        Assert.Equal(AiServiceErrorCodes.ContextNotAllowed, Assert.Throws<AiServiceException>(() =>
            PersonalAiChatIntent.Create(original.UserId, original.SessionId, context, "test", new("test", null))).Code);
    }

    [Fact]
    public void IntentCannotSmuggleSchoolOrStudentScope()
    {
        var context = Context(); var intent = Intent(context);
        foreach (var scope in new[] { new AiScope("school", null, null, null), new(null, "school", null, null),
                     new(null, null, "class", null), new(null, null, null, "student") })
            Assert.Throws<AiServiceException>(() => (intent with { Scope = scope }).EnsureScope(context.UserId));
        Assert.Throws<AiServiceException>(() => intent.EnsureScope(Guid.NewGuid()));
    }

    [Fact]
    public void PersonalPayerDefaultsToIndividualAndNeverAllowsOrganization()
    {
        var context = Context();
        var resolved = AiInvocationContext.CommerceContext(context, null);
        Assert.Equal(new AiQuotaPreflightCommand(CommerceContextType.Personal, context.UserId, AiPayerSelection.Individual), resolved);
        Assert.Equal(AiPayerSelection.FreeTrial, AiInvocationContext.CommerceContext(context, AiPayerSelection.FreeTrial).Payer);
        Assert.Equal(CommerceErrorCodes.ContextNotAllowed, Assert.Throws<CommerceException>(() =>
            AiInvocationContext.CommerceContext(context, AiPayerSelection.Organization)).Code);
        Assert.Throws<AiServiceException>(() => AiInvocationContext.CommerceContext(context, (AiPayerSelection)999));
    }

    [Fact]
    public void FingerprintIgnoresTransportButBindsAccountConversationAndMessage()
    {
        var context = Context(); var intent = Intent(context);
        var hash = AiInvocationExecutor.Fingerprint(intent, context);
        Assert.Equal(hash, AiInvocationExecutor.Fingerprint(intent with { RequestId = "new", CorrelationId = "other" },
            context with { SessionId = Guid.NewGuid() }));
        Assert.NotEqual(hash, AiInvocationExecutor.Fingerprint(intent with { UserId = Guid.NewGuid() }, context));
        Assert.NotEqual(hash, AiInvocationExecutor.Fingerprint(intent with { ConversationId = Guid.NewGuid() }, context));
        Assert.NotEqual(hash, AiInvocationExecutor.Fingerprint(intent with { Message = new("new intent") }, context));
    }

    [Fact]
    public void SupportedPersonalContractMapsOnlyOfficialActorAndEmptyScope()
    {
        var context = Context(); var intent = Intent(context);
        Assert.True(PersonalAiChatContract.IsSupported);
        var request = PersonalAiChatContract.ToServiceRequest(intent);
        Assert.Equal(new AiActor(context.UserId.ToString("D"), "Personal"), request.Actor);
        Assert.Equal(new AiScope(null, null, null, null), request.Scope);
        Assert.Equal(intent.RequestId, request.RequestId); Assert.Equal(intent.CorrelationId, request.CorrelationId);
        Assert.Equal("fa", request.Message.Language); Assert.Equal(AiAuthorizedContext.Empty, request.Context);
        Assert.Throws<AiServiceException>(() => PersonalAiChatContract.ToServiceRequest(intent with { Scope = new("foreign", null, null, null) }));
    }

    [Fact]
    public void ExecutionMetricsContainOnlyBoundedContextPayerOperationAndResultLabels()
    {
        var collected = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        { if (instrument.Meter.Name == "MakanApp.AiService" && instrument.Name == "makan.ai.execution.results") current.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            var values = tags.ToArray();
            if (values.Any(t => t.Key == "payerKind" && Equals(t.Value, "FreeTrial"))) collected.Enqueue(values);
        });
        listener.Start();
        using (var measurement = AiExecutionTelemetry.Begin("knowledge_rag", WorkspaceType.Personal, AiPayerSelection.FreeTrial)) measurement.Succeeded();
        var result = Assert.Single(collected);
        Assert.Equal(new[] { "operation", "contextKind", "payerKind", "result" }, result.Select(t => t.Key));
        Assert.Equal(new object?[] { "knowledge_rag", "Personal", "FreeTrial", "success" }, result.Select(t => t.Value));
        var failed = AiExecutionTelemetry.Begin("متن خصوصی که نباید tag شود", WorkspaceType.Personal, AiPayerSelection.FreeTrial);
        failed.Dispose(); failed.Dispose();
        Assert.Equal(2, collected.Count);
        var failure = collected.Last();
        Assert.Equal(new object?[] { "role_specific", "Personal", "FreeTrial", "failure" }, failure.Select(t => t.Value));
    }
}
