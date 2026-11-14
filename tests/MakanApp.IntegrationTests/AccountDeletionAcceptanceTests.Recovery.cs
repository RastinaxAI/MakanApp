using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Knowledge;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Knowledge;
using MakanApp.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AccountDeletionAcceptanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletionKnowledgeConfirmedDeletionOrConfirmedAbsenceCanCompleteExactlyOnce(bool absent)
    {
        var owner = await ActorAsync(); var source = await SourceAsync(owner); _knowledge.NotIngested = absent;
        var request = await RequestAsync(owner); await DrainAsync(); await DrainAsync();
        Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id)); Assert.Single(_knowledge.Deletes);
        Assert.Equal(absent ? "NotIngested" : "Deleted", await Db(db => db.AccountDeletionKnowledgePurges.Select(p => p.Outcome).SingleAsync()));
        Assert.Equal(new KnowledgeSourceVersion(source, 1), _knowledge.Deletes[0]);
    }

    [Fact]
    public async Task DeletionKnowledgeSupersededIsNotProofOfPhysicalDeletion()
    {
        var owner = await ActorAsync(); await SourceAsync(owner); _knowledge.State = "Superseded";
        var request = await RequestAsync(owner); await DrainAsync();
        Assert.Equal(AccountDeletionStatus.Processing, await Status(request.Id));
        Assert.Null(await Db(db => db.AccountDeletionKnowledgePurges.Select(p => p.CompletedAtUtc).SingleAsync()));
        Assert.Null(await Db(db => db.AccountDeletionRequests.Select(r => r.CompletedAtUtc).SingleAsync()));
        _knowledge.State = "Deleted"; _factory.Clock.SetUtcNow(Now.AddSeconds(3)); await DrainAsync();
        Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
    }

    [Fact]
    public async Task DeletionKnowledgeFailureIsBoundedAndOwnerRetryResumesWithoutRepeatingSuccessfulSteps()
    {
        var owner = await ActorAsync(); var admin = await ActorAsync(); await _factory.BootstrapPlatformOwnerAsync(admin.Id);
        await SourceAsync(owner); _knowledge.Unavailable = true; var request = await RequestAsync(owner);
        for (var i = 0; i < 10; i++) { await DrainAsync(); _factory.Clock.SetUtcNow(Now.AddMinutes(6)); }
        Assert.Equal(AccountDeletionStatus.NeedsAttention, await Status(request.Id)); Assert.Equal(10, _knowledge.Deletes.Count);
        var version = Convert.ToBase64String(await Db(db => db.AccountDeletionRequests.Where(r => r.Id == request.Id).Select(r => r.RowVersion).SingleAsync()));
        var command = new RetryAccountDeletionCommand(Guid.NewGuid(), version);
        using var forbidden = await owner.Client.PostAsJsonAsync($"/api/admin/v1/privacy/deletion-requests/{request.Id}/retry", command, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, forbidden.StatusCode);
        using var stale = await admin.Client.PostAsJsonAsync($"/api/admin/v1/privacy/deletion-requests/{request.Id}/retry", command with { ExpectedVersion = Convert.ToBase64String(new byte[8]) }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await Post<AccountDeletionStatusResult>(admin.Client, $"/api/admin/v1/privacy/deletion-requests/{request.Id}/retry", command);
        await Post<AccountDeletionStatusResult>(admin.Client, $"/api/admin/v1/privacy/deletion-requests/{request.Id}/retry", command);
        _knowledge.Unavailable = false; await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        await Post<AccountDeletionStatusResult>(admin.Client, $"/api/admin/v1/privacy/deletion-requests/{request.Id}/retry", command);
        Assert.Equal(11, _knowledge.Deletes.Count); Assert.Single(_knowledge.CommandIds.Distinct());
        await Db(async db =>
        {
            Assert.Equal(1, (await db.AccountDeletionSteps.SingleAsync(s => s.RequestId == request.Id && s.Step == AccountDeletionStepKind.FreezeAccount)).AttemptCount);
            Assert.Equal(1, await db.AdminAuditLogs.CountAsync(a => a.TargetId == request.Id.ToString() && a.Action == "privacy.DeletionResumed"));
            Assert.Equal(8, await db.AdminAuditLogs.CountAsync(a => a.TargetId == request.Id.ToString() && a.Action == "privacy.DeletionStepCompleted"));
            Assert.Equal(PlatformCatalogIds.PlatformOwnerRole, await db.PlatformRolePermissions.Where(p => p.PermissionId == PlatformCatalogIds.PrivacyRequestsManagePermission).Select(p => p.RoleId).SingleAsync());
        });
    }

    [Fact]
    public async Task DeletionCrashAfterRemoteEffectReusesCommandAndFencesOldAcknowledgement()
    {
        var owner = await ActorAsync(); await SourceAsync(owner); var request = await RequestAsync(owner);
        for (var i = 0; i < 4; i++) await OneAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _knowledge.BeforeResponse = async () => { entered.TrySetResult(); await release.Task; };
        var old = await ClaimAsync(); Assert.NotNull(old); var running = ProcessAsync(old);
        AccountDeletionClaim? replacement;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            _factory.Clock.SetUtcNow(Now.Add(MakanApp.Infrastructure.Identity.AccountDeletionProcessor.ClaimLease).AddSeconds(1));
            replacement = await ClaimAsync(); Assert.NotNull(replacement);
        }
        finally { release.TrySetResult(); }
        await running;
        Assert.Null(await Db(db => db.AccountDeletionKnowledgePurges.Select(p => p.CompletedAtUtc).SingleAsync()));
        await ProcessAsync(replacement!); await DrainAsync();
        Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id)); Assert.Equal(2, _knowledge.Deletes.Count);
        Assert.Single(_knowledge.CommandIds.Distinct()); Assert.Single(_knowledge.RemoteEffects);
        Assert.Equal(2, await Db(db => db.AccountDeletionKnowledgePurges.Select(p => p.AttemptCount).SingleAsync()));
    }

    [Fact]
    public async Task DeletionTrustedPurgeRejectsForeignSourceAndForeignOwnerBeforeAnyOutboundCall()
    {
        var owner = await ActorAsync(); var peer = await ActorAsync(); await SourceAsync(owner); var foreign = await SourceAsync(peer);
        var request = await RequestAsync(owner); var tested = false;
        _knowledge.BeforeResponse = async () =>
        {
            await using var scope = _factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanApp.Infrastructure.Persistence.MakanDbContext>();
            var row = await db.AccountDeletionRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id);
            var purge = await db.AccountDeletionKnowledgePurges.AsNoTracking().SingleAsync(p => p.RequestId == row.Id);
            var context = new AccountDeletionExecutionContext(row.Id, row.UserId, row.RequestedBySessionId, row.ClaimToken!.Value, row.PolicyVersion);
            var purger = ActivatorUtilities.CreateInstance<AccountDeletionKnowledgePurger>(scope.ServiceProvider, _knowledge);
            var code = await Assert.ThrowsAsync<AccountDeletionExecutionException>(() => purger.DeleteAsync(new(context, foreign, 1, purge.CommandRequestId), default));
            Assert.Equal(AccountDeletionErrorCodes.OwnershipConflict, code.Code);
            await Assert.ThrowsAsync<AccountDeletionExecutionException>(() => purger.DeleteAsync(new(context with { UserId = peer.Id }, purge.SourceId, 1, purge.CommandRequestId), default));
            tested = true;
        };
        await DrainAsync(); Assert.True(tested); Assert.Single(_knowledge.Deletes); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletionFailureAndAuditNeverContainRawPrivateExceptionOrFormerIdentity(bool untrustedCode)
    {
        var owner = await ActorAsync(); await SourceAsync(owner); const string privateValue = "synthetic-private-body-secret-sql-path";
        _knowledge.BeforeResponse = () => untrustedCode
            ? throw new AccountDeletionExecutionException(privateValue, true)
            : throw new InvalidOperationException(privateValue);
        var request = await RequestAsync(owner); await DrainAsync(); Assert.Equal(AccountDeletionStatus.NeedsAttention, await Status(request.Id));
        var audit = await Db(db => db.AdminAuditLogs.Where(a => a.TargetId == request.Id.ToString()).Select(a => new { a.Reason, a.BeforeSummary, a.AfterSummary }).ToArrayAsync());
        var evidence = System.Text.Json.JsonSerializer.Serialize(audit) + string.Join('\n', _factory.SensitiveLogs.Snapshot());
        Assert.DoesNotContain(privateValue, evidence); Assert.DoesNotContain(owner.Phone, evidence); Assert.DoesNotContain(owner.Username, evidence);
        Assert.Contains(AccountDeletionErrorCodes.StepFailed, evidence);
        Assert.Null(await Db(db => db.AccountDeletionRequests.Select(r => r.CompletedAtUtc).SingleAsync()));
    }

    [Fact]
    public async Task DeletionKnowledgeCheckpointSkipsSuccessfulVersionsOnRetry()
    {
        var owner = await ActorAsync(); var sourceId = await SourceAsync(owner);
        await Db(async db =>
        {
            var source = await db.EducationalSources.SingleAsync(); var first = await db.EducationalSourceVersions.SingleAsync();
            source.AdvanceVersion(Now); db.EducationalSourceVersions.Add(EducationalSourceVersion.Create(source.Id, 2, first.FileAssetId, owner.Id, "نسخهٔ دوم", null, EducationalSourceType.Document, Now));
            await db.SaveChangesAsync();
        });
        _knowledge.UnavailableVersion = 2; var request = await RequestAsync(owner); await DrainAsync();
        Assert.Equal(AccountDeletionStatus.Processing, await Status(request.Id));
        Assert.Single(_knowledge.Deletes, d => d.Version == 1); Assert.Single(_knowledge.Deletes, d => d.Version == 2);
        _knowledge.UnavailableVersion = null; _factory.Clock.SetUtcNow(Now.AddSeconds(3)); await DrainAsync();
        Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id)); Assert.Single(_knowledge.Deletes, d => d.Version == 1);
        Assert.Equal(2, _knowledge.Deletes.Count(d => d.Version == 2)); Assert.Equal(2, _knowledge.CommandIds.Distinct().Count());
        Assert.Equal(2, await Db(db => db.AccountDeletionKnowledgePurges.CountAsync(p => p.SourceId == sourceId && p.CompletedAtUtc != null)));
    }

    [Fact]
    public async Task DeletionRepeatedAbandonedClaimsReachNeedsAttentionWithoutAnInfiniteRecoveryLoop()
    {
        var owner = await ActorAsync(); var request = await RequestAsync(owner);
        for (var i = 0; i <= AccountDeletionProcessor.MaximumFailures; i++)
        { await ClaimAsync(); _factory.Clock.SetUtcNow(Now.Add(AccountDeletionProcessor.ClaimLease).AddSeconds(1)); }
        Assert.Equal(AccountDeletionStatus.NeedsAttention, await Status(request.Id)); Assert.Null(await ClaimAsync());
        Assert.Equal(AccountDeletionErrorCodes.RecoveryExhausted, await Db(db => db.AccountDeletionRequests.Select(r => r.FailureCode).SingleAsync()));
        Assert.Equal(AccountDeletionProcessor.MaximumFailures, await Db(db => db.AccountDeletionRequests.Select(r => r.ConsecutiveFailures).SingleAsync()));
        Assert.Null(await Db(db => db.Users.Where(u => u.Id == owner.Id).Select(u => u.DeletionStartedAtUtc).SingleAsync()));
    }
}
