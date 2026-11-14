using MakanApp.Application.Identity;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Knowledge;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class AccountDeletionStateTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
    private static AccountDeletionRequest Request() => AccountDeletionRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);

    [Theory]
    [InlineData(false, null, true)]
    [InlineData(true, null, false)]
    [InlineData(true, -1, false)]
    [InlineData(false, -1, false)]
    [InlineData(true, 0, true)]
    [InlineData(true, 72, true)]
    public void ExecutionRequiresExplicitValidGrace(bool enabled, int? hours, bool valid) =>
        Assert.Equal(valid, new AccountDeletionOptions { ExecutionEnabled = enabled, GracePeriodHours = hours }.IsValid());

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(48)]
    public void SchedulingUsesExplicitGraceWithoutAHiddenLegalDefault(int hours)
    {
        var options = new AccountDeletionOptions { ExecutionEnabled = true, GracePeriodHours = hours };
        Assert.Equal(Now.AddHours(hours), options.ScheduleAt(Now));
        Assert.Throws<AccountDeletionExecutionException>(() => new AccountDeletionOptions().ScheduleAt(Now));
    }

    [Fact]
    public void ClaimHonorsDeadlineLeaseAndFencesThePreviousWorker()
    {
        var request = Request(); request.Schedule(Now.AddHours(3)); var token = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => request.Claim(token, Now, TimeSpan.FromMinutes(5)));
        request.Claim(token, Now.AddHours(3), TimeSpan.FromMinutes(5));
        Assert.Throws<InvalidOperationException>(() => request.Claim(Guid.NewGuid(), Now.AddHours(3), TimeSpan.FromMinutes(5)));
        Assert.Throws<AccountDeletionClaimLostException>(() => request.EnsureClaim(token, Now.AddHours(4)));
        var replacement = Guid.NewGuid(); request.Claim(replacement, Now.AddHours(4), TimeSpan.FromMinutes(5));
        Assert.Throws<AccountDeletionClaimLostException>(() => request.EnsureClaim(token, Now.AddHours(4)));
        request.EnsureClaim(replacement, Now.AddHours(4)); Assert.Equal(2, request.AttemptCount); Assert.Equal(1, request.ConsecutiveFailures);
    }

    [Fact]
    public void CompletionRequiresEveryDistinctDurableCheckpoint()
    {
        var request = Request(); request.Schedule(Now); var token = Guid.NewGuid(); request.Claim(token, Now, TimeSpan.FromMinutes(5));
        request.MarkStarted(token, Now);
        Assert.Throws<InvalidOperationException>(() => request.Complete(token, Now, []));
        var steps = Enum.GetValues<AccountDeletionStepKind>().Select(k => AccountDeletionStep.Create(request.Id, k)).ToArray();
        foreach (var step in steps) { step.Start(Now); step.Complete(Now); }
        Assert.Throws<InvalidOperationException>(() => request.Complete(token, Now, steps.Take(7).Append(steps[0]).ToArray()));
        request.Complete(token, Now, steps); Assert.Equal(AccountDeletionStatus.Completed, request.Status); Assert.Null(request.ClaimToken);
        Assert.Throws<InvalidOperationException>(() => steps[0].Start(Now));
    }

    [Fact]
    public void BoundedRecoveryNeedsAttentionAndResumePreservesOriginalDeadline()
    {
        var request = Request(); request.Schedule(Now); var token = Guid.NewGuid();
        request.Claim(token, Now, TimeSpan.FromMinutes(5)); request.Fail(token, AccountDeletionStepKind.DeletePersonalKnowledge, "ACCOUNT_DELETION_KNOWLEDGE_UNAVAILABLE", true, Now, 1);
        Assert.Equal(AccountDeletionStatus.NeedsAttention, request.Status); Assert.Null(request.ClaimToken);
        request.Resume(Now.AddHours(3)); Assert.Equal(Now, request.ScheduledExecutionAtUtc); Assert.Equal(AccountDeletionStatus.Scheduled, request.Status);
        Assert.Equal(0, request.ConsecutiveFailures); Assert.Throws<InvalidOperationException>(() => request.Resume(Now.AddHours(4)));
    }

    [Fact]
    public void AnonymizationRetainsRootIdsAndIsIrreversibleAndIdempotent()
    {
        var user = User.Create(Now); var person = Person.Create("نام قبلی", "خانوادگی", "نمایش قبلی", Now);
        person.SetDateOfBirth(new DateOnly(2000, 1, 1), Now); user.SetProfile(person.Id, "previous_name", "PREVIOUS_NAME", Now);
        user.SetBio("معرفی خصوصی", Now); user.SetProfilePhoto(Guid.NewGuid(), Now);
        Assert.Throws<InvalidOperationException>(() => user.Anonymize(Now));
        user.BeginAccountDeletion(Now); user.Anonymize(Now); person.Anonymize(Now);
        Assert.Equal(person.Id, user.PersonId); Assert.False(user.IsProfileComplete); Assert.Null(user.Bio); Assert.Null(user.ProfilePhotoFileAssetId);
        Assert.StartsWith(User.DeletedUsernamePrefix, user.Username); Assert.InRange(user.Username!.Length, 1, 32);
        Assert.DoesNotMatch("^[A-Za-z0-9._]+$", user.Username); Assert.Equal(user.Username.ToUpperInvariant(), user.NormalizedUsername);
        Assert.Equal(Person.DeletedDisplayName, person.DisplayName); Assert.Null(person.DateOfBirth);
        var name = user.Username; user.Anonymize(Now.AddHours(1)); Assert.Equal(name, user.Username); Assert.Equal(Now, user.AnonymizedAtUtc);
        Assert.Throws<InvalidOperationException>(() => user.Reactivate(Now.AddHours(1)));
        var other = User.Create(Now); other.BeginAccountDeletion(Now); other.Anonymize(Now); Assert.NotEqual(user.Username, other.Username);
    }

    [Theory]
    [InlineData(EducationalSourceScope.Organization)]
    [InlineData(EducationalSourceScope.Class)]
    [InlineData(EducationalSourceScope.Library)]
    public void KnowledgeDeletionNeverUsesCreatedByAsOwnership(EducationalSourceScope scope)
    {
        var owner = Guid.NewGuid(); var source = EducationalSource.Create(scope, owner,
            scope is EducationalSourceScope.Class or EducationalSourceScope.Organization ? Guid.NewGuid() : null,
            scope == EducationalSourceScope.Class ? Guid.NewGuid() : null, Guid.NewGuid(), new string('a', 64), Now);
        var version = EducationalSourceVersion.Create(source.Id, 1, Guid.NewGuid(), owner, "منبع ساختگی", null, EducationalSourceType.Document, Now);
        Assert.Throws<InvalidOperationException>(() => AccountDeletionKnowledgePurge.Create(Guid.NewGuid(), source, version, owner));
    }

    [Fact]
    public void PersonalKnowledgeReceiptHasAStableCommandIdAndAcceptsOnlyDefinitiveDeletion()
    {
        var owner = Guid.NewGuid(); var source = EducationalSource.Create(EducationalSourceScope.Personal, owner, null, null, Guid.NewGuid(), new string('a', 64), Now);
        var version = EducationalSourceVersion.Create(source.Id, 1, Guid.NewGuid(), owner, "منبع", null, EducationalSourceType.Document, Now);
        Assert.Throws<InvalidOperationException>(() => AccountDeletionKnowledgePurge.Create(Guid.NewGuid(), source, version, Guid.NewGuid()));
        var purge = AccountDeletionKnowledgePurge.Create(Guid.NewGuid(), source, version, owner); var command = purge.CommandRequestId;
        purge.Attempt(Now); purge.Attempt(Now.AddSeconds(1)); Assert.Equal(command, purge.CommandRequestId);
        Assert.Throws<InvalidOperationException>(() => purge.Complete("Superseded", Now));
        purge.Complete("Deleted", Now); purge.Complete("Deleted", Now.AddMinutes(1)); Assert.Equal(Now, purge.CompletedAtUtc);
        Assert.Throws<InvalidOperationException>(() => purge.Attempt(Now.AddMinutes(1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PhoneChangeHistoryIsDetachedAndSanitizedWithoutFabricatingACredential(bool completed)
    {
        var user = Guid.NewGuid(); var credential = UserCredential.CreateMobile(user, "+989390000001", Now);
        var request = PhoneChangeRequest.Prepare(user, Guid.NewGuid(), credential, Guid.NewGuid(), "+989390000002", Now, TimeSpan.FromMinutes(2), 5);
        request.Ready(new byte[32], new byte[16], Now); if (completed) request.Complete(Now);
        request.DetachForAccountDeletion(); request.DetachForAccountDeletion();
        Assert.Null(request.CredentialId); Assert.Equal("account-deleted", request.TargetPhone);
        Assert.Equal(completed ? PhoneChangeState.Completed : PhoneChangeState.Superseded, request.State);
    }
}
