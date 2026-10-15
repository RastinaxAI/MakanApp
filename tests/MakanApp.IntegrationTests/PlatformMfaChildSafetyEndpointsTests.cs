using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Messaging;
using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformMfaEndpointsTests
{
    private static readonly JsonSerializerOptions PolicyMfaJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    [Fact]
    public async Task AgeConflictResolutionUsesRealSessionMfaAndExpiredStepUpCannotResolve()
    {
        using var ownerClient = _host.CreateClient(); _ = await OwnerAsync(ownerClient);
        using var targetClient = _host.CreateClient(); var target = await AuthenticateAsync(targetClient);
        await factory.MarkLegacyAgeConflictForMfaTestAsync(target.User.Id);
        var state = await factory.ReadAgeStateAsync(target.User.Id);
        var command = new VerifyCommunicationAgeCommand(new DateOnly(2015, 1, 1), state.Version, "حل شاهد متعارض با MFA واقعی");
        var route = $"/api/admin/v1/users/{target.User.Id}/communication-age/resolve";
        await ErrorAsync(await ownerClient.PostAsJsonAsync(route, command), HttpStatusCode.Forbidden, PlatformErrorCodes.MfaRequired);
        var setup = await StartAsync(ownerClient); await ConfirmAsync(ownerClient, setup);
        factory.Clock.SetUtcNow(Now.AddMinutes(11));
        await ErrorAsync(await ownerClient.PostAsJsonAsync(route, command), HttpStatusCode.Forbidden, PlatformErrorCodes.StepUpRequired);
        await PostAsync(ownerClient, "/api/admin/v1/mfa/verify", new VerifyPlatformMfaCommand(Code(setup)));
        using var resolved = await ownerClient.PostAsJsonAsync(route, command);
        resolved.EnsureSuccessStatusCode();
        Assert.Equal(CommunicationAgeVerificationStatus.Verified, (await factory.ReadAgeStateAsync(target.User.Id)).VerificationStatus);
        Assert.Equal(CommunicationAgeCategory.Minor, (await factory.ReadAgeStateAsync(target.User.Id)).Classification);
    }

    [Fact]
    public async Task EscalatedEvidenceUsesExistingRealTotpFreshnessAndOrdinaryCaseReadDoesNotRequireFreshness()
    {
        using var ownerClient = _host.CreateClient(); _ = await OwnerAsync(ownerClient);
        using var senderClient = _host.CreateClient(); var sender = await AuthenticateAsync(senderClient);
        using var reporterClient = _host.CreateClient(); var reporter = await AuthenticateAsync(reporterClient);
        var org = await factory.CreateOrganizationAsync($"MFA case {Guid.NewGuid():N}");
        var conversationId = await factory.CreateSystemManagedConversationAsync(org, sender.User.Id, ConversationType.Group);
        await factory.AddConversationParticipantAsync(conversationId, reporter.User.Id);
        var messageId = await factory.CreateCommittedMessageAsync(conversationId, sender.User.Id, "شاهد محدود پرونده");
        var caseId = await factory.CreatePlatformSafetyCaseAsync(conversationId, messageId, reporter.User.Id, sender.User.Id);
        var setup = await StartAsync(ownerClient); await ConfirmAsync(ownerClient, setup);
        var route = $"/api/admin/v1/moderation/cases/{caseId}";
        var detail = (await ownerClient.GetFromJsonAsync<PlatformModerationCaseDetail>(route, PolicyMfaJson))!;
        detail = await PostCaseAsync(ownerClient, route + "/start-review", new PlatformModerationVersionCommand(detail.Case.Version, "شروع بررسی"));
        detail = await PostCaseAsync(ownerClient, route + "/actions", new PlatformModerationActionCommand(detail.Case.Version, "ارجاع صریح", ModerationActionType.Escalated));
        detail = await PostCaseAsync(ownerClient, route + "/start-review", new PlatformModerationVersionCommand(detail.Case.Version, "پذیرش ارجاع"));
        factory.Clock.SetUtcNow(Now.AddMinutes(11));
        using var ordinaryRead = await ownerClient.GetAsync(route); Assert.Equal(HttpStatusCode.OK, ordinaryRead.StatusCode);
        var command = new PlatformModerationVersionCommand(detail.Case.Version, "گسترش با دلیل");
        await ErrorAsync(await ownerClient.PostAsJsonAsync(route + "/expanded-context", command), HttpStatusCode.Forbidden, PlatformErrorCodes.StepUpRequired);
        await PostAsync(ownerClient, "/api/admin/v1/mfa/verify", new VerifyPlatformMfaCommand(Code(setup)));
        using var expanded = await ownerClient.PostAsJsonAsync(route + "/expanded-context", command);
        expanded.EnsureSuccessStatusCode();
    }

    private static async Task<PlatformModerationCaseDetail> PostCaseAsync(HttpClient client, string route, object command)
    {
        using var response = await client.PostAsJsonAsync(route, command);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PlatformModerationCaseDetail>(PolicyMfaJson))!;
    }
}
