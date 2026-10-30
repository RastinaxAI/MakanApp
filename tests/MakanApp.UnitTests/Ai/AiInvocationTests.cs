using MakanApp.Application.Ai;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Ai;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class AiInvocationTests
{
    [Fact] public void ConflictingOrEmptyOperationIdsAreRejected()
    {
        var id = Guid.NewGuid(); Assert.Equal(id, AiInvocationOptions.Resolve(new(id), id).OperationId);
        Assert.Throws<AiServiceException>(() => AiInvocationOptions.Resolve(new(id), Guid.NewGuid()));
        Assert.Throws<AiServiceException>(() => AiInvocationOptions.Resolve(new(Guid.Empty)));
        Assert.Throws<AiServiceException>(() => AiInvocationOptions.Resolve(null, Guid.Empty));
        Assert.Throws<AiServiceException>(() => AiInvocationOptions.Resolve(new(null, (AiPayerSelection)999)));
    }
    [Fact] public void LegacyCallGetsNewIdentityWithoutInventingIndividualPayer()
    {
        var one = AiInvocationOptions.Resolve(null); var two = AiInvocationOptions.Resolve(null);
        Assert.NotNull(one.OperationId); Assert.NotEqual(one.OperationId, two.OperationId); Assert.Null(one.Payer);
    }
    [Fact] public void LogicalHashIgnoresTransportAndChangingServerReportsButBindsIntentAndScope()
    {
        var user = Guid.NewGuid(); var org = Guid.NewGuid(); var member = Guid.NewGuid();
        var c = new AccessContext(user, user, Guid.NewGuid(), WorkspaceType.Organization, org, member, OrganizationRole.Manager, null);
        var request = new AiChatRequest("one", "corr", null, new(user.ToString("D"),"Manager"),
            new(org.ToString("D"),org.ToString("D"),null,null), new("پرسش"), AiAuthorizedContext.Empty);
        var hash = AiInvocationExecutor.Fingerprint(request, c);
        Assert.Equal(hash, AiInvocationExecutor.Fingerprint(request with { RequestId = "another", CorrelationId = "new-correlation" }, c with { SessionId = Guid.NewGuid() }));
        Assert.NotEqual(hash, AiInvocationExecutor.Fingerprint(request with { Message = new("قصد تازه") }, c));
        Assert.NotEqual(hash, AiInvocationExecutor.Fingerprint(request, c with { MembershipId = Guid.NewGuid() }));
        Assert.NotEqual(hash, AiInvocationExecutor.Fingerprint(request with { Scope = request.Scope with { ClassId = Guid.NewGuid().ToString("D") } }, c));
        Assert.DoesNotContain("selectedSourceIds", System.Text.Json.JsonSerializer.Serialize(request));
    }
    [Fact] public void CurrentContextComparisonIsStructuralAndFailClosedOnPrivateDataChange()
    {
        AiInvocationContext.EnsureSame(new { Values = new[] { 1, 2 } }, new { Values = new[] { 1, 2 } });
        Assert.Throws<AiServiceException>(() => AiInvocationContext.EnsureSame(new { Values = new[] { 1, 2 } }, new { Values = new[] { 1, 3 } }));
    }
    [Fact] public void DisabledRuntimeIsRejectedBeforeCommerce()
    {
        var guard = new AiRuntimeAvailability(new AiServiceOptions { Enabled = false });
        Assert.Equal(AiServiceErrorCodes.FeatureDisabled, Assert.Throws<AiServiceException>(guard.EnsureEnabled).Code);
    }
}
