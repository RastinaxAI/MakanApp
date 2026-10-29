using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Academic;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed partial class CommercialSubscriptionEndpointsTests(MakanAppWebApplicationFactory factory, ITestOutputHelper output) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = MakeJson();
    private static int _phone = 8_400_000;
    private readonly List<HttpClient> _clients = [];
    private int _requestCount;

    [Fact]
    public async Task IndividualPurchaseWithoutOrganizationActivatesExactlyOnceAndCreatesNoRole()
    {
        var actor = await ActorAsync();
        var versionId = await PlanAsync(SubscriptionOwnerType.Individual, ai: 10, hosting: true);
        var command = new CreateOrderCommand(versionId, Guid.NewGuid());
        var quote = await PostAsync<CommercialQuoteDto>(actor.Client, "/api/v1/commerce/personal/quote", command);
        Assert.Equal(100m, quote.PriceToman); Assert.False(quote.AutoRenew);
        var created = await PostAsync<CreateOrderResult>(actor.Client, "/api/v1/commerce/personal/orders", command);
        var replay = await PostAsync<CreateOrderResult>(actor.Client, "/api/v1/commerce/personal/orders", command);
        Assert.Equal(created.Order.OrderId, replay.Order.OrderId); Assert.True(replay.Replayed);
        Assert.Equal(1000m, created.Order.Amount);
        var attempt = await PayAsync(actor, created.Order.OrderId);
        await VerifyAsync(attempt.PaymentAttemptId); await VerifyAsync(attempt.PaymentAttemptId);
        await factory.DrainNotificationOutboxAsync();
        await factory.DrainNotificationOutboxAsync();
        var summary = await GetAsync<CommercialSummaryDto>(actor.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(SubscriptionDisplayState.Active, summary.State); Assert.Equal(actor.UserId, summary.OwnerId);
        Assert.True(summary.CommercialControlEnabled); Assert.NotNull(summary.PeriodEndsAtUtc);
        Assert.Equal(1, await factory.CountSubscriptionsForOrderAsync(created.Order.OrderId));
        Assert.Equal(1, await factory.CountPaymentsAsync(created.Order.OrderId));
        await WithDb(async db => Assert.False(await db.Memberships.AnyAsync(m => m.UserId == actor.UserId)));
        using var noRole = await actor.Client.GetAsync("/api/v1/commerce/organization/subscription");
        Assert.Equal(HttpStatusCode.Forbidden, noRole.StatusCode);
    }

    [Fact]
    public async Task PersonalClassParticipationNeedsHostContractButNoStudentPurchaseAndNeverExposesHostFinance()
    {
        var host = await ActorAsync(); var student = await ActorAsync(); var outsider = await ActorAsync();
        var versionId = await PlanAsync(SubscriptionOwnerType.Individual, ai: 10, hosting: true);
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), versionId);
        var classroom = await PostAsync<PersonalClassDto>(host.Client, "/api/v1/personal-classes", new CreatePersonalClassCommand("کلاس مستقل آزمون", Guid.NewGuid()));
        var invitation = await PostAsync<PersonalClassInvitationDto>(host.Client, $"/api/v1/personal-classes/{classroom.Id}/invitations",
            new InvitePersonalClassStudentCommand(student.UserId, Guid.NewGuid()));
        using var denied = await outsider.Client.PostAsync($"/api/v1/personal-classes/invitations/{invitation.Id}/accept", null);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var joined = await PostAsync<PersonalClassDto>(student.Client, $"/api/v1/personal-classes/invitations/{invitation.Id}/accept", null);
        Assert.False(joined.IsHost); Assert.False(joined.CanCreateEducationalContent);
        Assert.Equal(classroom.Id, (await GetAsync<PersonalClassDto>(student.Client, $"/api/v1/personal-classes/{classroom.Id}")).Id);
        var summary = await GetAsync<CommercialSummaryDto>(student.Client, $"/api/v1/commerce/personal/subscription?userId={host.UserId}");
        Assert.Equal(student.UserId, summary.OwnerId); Assert.Null(summary.SubscriptionId);
        var trial = new AiQuotaPreflightCommand(CommerceContextType.PersonalClass, classroom.Id, AiPayerSelection.FreeTrial);
        var before = await GetAsync<CommercialSummaryDto>(host.Client, "/api/v1/commerce/personal/subscription");
        var reservation = await ReserveAsync(student, trial, Guid.NewGuid()); await FinishAsync(student, reservation, UsageReservationStatus.Consumed);
        var after = await GetAsync<CommercialSummaryDto>(host.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(before.Quotas.Single(q => q.Key == QuotaKey.AiTextRequests).Remaining, after.Quotas.Single(q => q.Key == QuotaKey.AiTextRequests).Remaining);
        await WithDb(async db => Assert.False(await db.Memberships.AnyAsync(m => m.UserId == host.UserId || m.UserId == student.UserId)));
    }

    [Fact]
    public async Task HostLearnerCapacityIsUniqueAcrossClassesAndConcurrentLastSeatHasOneWinner()
    {
        var host = await ActorAsync(); var first = await ActorAsync(); var second = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true, learners: 1));
        var a = await PostAsync<PersonalClassDto>(host.Client, "/api/v1/personal-classes", new CreatePersonalClassCommand("کلاس یک", Guid.NewGuid()));
        var b = await PostAsync<PersonalClassDto>(host.Client, "/api/v1/personal-classes", new CreatePersonalClassCommand("کلاس دو", Guid.NewGuid()));
        var ia = await InviteAsync(host, first, a.Id); var ib = await InviteAsync(host, second, b.Id);
        var results = await Task.WhenAll(first.Client.PostAsync($"/api/v1/personal-classes/invitations/{ia.Id}/accept", null),
            second.Client.PostAsync($"/api/v1/personal-classes/invitations/{ib.Id}/accept", null));
        try
        {
            Assert.Single(results, r => r.IsSuccessStatusCode); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
            var winner = results[0].IsSuccessStatusCode ? first : second;
            var destination = results[0].IsSuccessStatusCode ? b.Id : a.Id;
            var invitation = await InviteAsync(host, winner, destination);
            await PostAsync<PersonalClassDto>(winner.Client, $"/api/v1/personal-classes/invitations/{invitation.Id}/accept", null);
            var summary = await GetAsync<CommercialSummaryDto>(host.Client, "/api/v1/commerce/personal/subscription");
            Assert.Equal(1, summary.Quotas.Single(q => q.Key == QuotaKey.ActiveLearners).Consumed);
        }
        finally { foreach (var result in results) result.Dispose(); }
    }

    [Fact]
    public async Task TrialCountsThreeSuccessfulRequestsAcrossContextsAndRetryOnlyOnce()
    {
        var actor = await ActorAsync(); var context = new AiQuotaPreflightCommand(CommerceContextType.Personal, Payer: AiPayerSelection.FreeTrial);
        var operation = Guid.NewGuid(); var first = await ReserveAsync(actor, context, operation);
        await FinishAsync(actor, first, UsageReservationStatus.Consumed);
        var anotherDevice = await AnotherDeviceAsync(actor); Assert.NotEqual(actor.SessionId, anotherDevice.SessionId);
        var replay = await ReserveAsync(anotherDevice, context, operation); Assert.True(replay.Replayed);
        await FinishAsync(anotherDevice, replay, UsageReservationStatus.Consumed);
        for (var i = 0; i < 2; i++) await FinishAsync(actor, await ReserveAsync(actor, context, Guid.NewGuid()), UsageReservationStatus.Consumed);
        var error = await Assert.ThrowsAsync<CommerceException>(() => ReserveAsync(actor, context, Guid.NewGuid()));
        Assert.Equal(CommerceErrorCodes.QuotaExceeded, error.Code);
        var preflight = await PostAsync<AiQuotaPreflightDto>(actor.Client, "/api/v1/commerce/ai/simple-text/preflight", context);
        Assert.False(preflight.Allowed); Assert.Equal(0, preflight.Remaining);
        var changed = await Assert.ThrowsAsync<CommerceException>(() => ReserveAsync(actor, context, operation, RandomNumberGenerator.GetBytes(32)));
        Assert.Equal(CommerceErrorCodes.UsageConflict, changed.Code);
    }

    [Fact]
    public async Task ConcurrentLastAiUnitReservesOnceAndUnknownStaysInOriginalDayUntilLateFinalization()
    {
        var actor = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual, ai: 1));
        var context = new AiQuotaPreflightCommand(CommerceContextType.Personal, Payer: AiPayerSelection.Individual);
        var outcomes = await Task.WhenAll(TryReserveAsync(actor, context), TryReserveAsync(actor, context));
        Assert.Single(outcomes, r => r is null); Assert.Single(outcomes, r => r == CommerceErrorCodes.QuotaExceeded);
        factory.Clock.SetUtcNow(new DateTime(2026, 10, 29, 20, 29, 30, DateTimeKind.Utc));
        var trialActor = await ActorAsync(); var trial = new AiQuotaPreflightCommand(CommerceContextType.Personal, Payer: AiPayerSelection.FreeTrial);
        var reserved = await ReserveAsync(trialActor, trial, Guid.NewGuid());
        await FinishAsync(trialActor, reserved, UsageReservationStatus.Unknown);
        var before = await PostAsync<AiQuotaPreflightDto>(trialActor.Client, "/api/v1/commerce/ai/simple-text/preflight", trial);
        Assert.Equal(2, before.Remaining);
        factory.Clock.SetUtcNow(new DateTime(2026, 10, 29, 20, 30, 30, DateTimeKind.Utc));
        var late = await FinishAsync(trialActor, reserved, UsageReservationStatus.Consumed);
        Assert.Equal(reserved.PeriodStartsAtUtc, late.PeriodStartsAtUtc);
        var next = await PostAsync<AiQuotaPreflightDto>(trialActor.Client, "/api/v1/commerce/ai/simple-text/preflight", trial);
        Assert.Equal(3, next.Remaining);
    }

    [Fact]
    public async Task OrganizationPayerNeverFallsBackAndPersonalPayerNeverGrantsOrganizationAuthorization()
    {
        var member = await ActorAsync(); var outsider = await ActorAsync();
        var organizationId = await factory.CreateOrganizationAsync("سازمان سهمیه " + Guid.NewGuid().ToString("N"));
        var membership = await factory.CreateMembershipAsync(member.UserId, organizationId, OrganizationRole.Manager);
        await SelectAsync(member, membership.MembershipId, OrganizationRole.Manager);
        await SubscribeAsync(SubscriptionOwner.Organization(organizationId), await PlanAsync(SubscriptionOwnerType.Organization, ai: 0));
        await SubscribeAsync(SubscriptionOwner.Individual(member.UserId), await PlanAsync(SubscriptionOwnerType.Individual, ai: 5));
        var orgContext = new AiQuotaPreflightCommand(CommerceContextType.Organization, organizationId);
        var denied = await PostAsync<AiQuotaPreflightDto>(member.Client, "/api/v1/commerce/ai/simple-text/preflight", orgContext);
        Assert.False(denied.Allowed); Assert.Equal(AiPayerSelection.Organization, denied.Payer); Assert.Equal(CommerceErrorCodes.QuotaExceeded, denied.ErrorCode);
        var personal = orgContext with { Payer = AiPayerSelection.Individual };
        var chosen = await PostAsync<AiQuotaPreflightDto>(member.Client, "/api/v1/commerce/ai/simple-text/preflight", personal);
        Assert.True(chosen.Allowed); Assert.Equal(member.UserId, chosen.OwnerId); Assert.False(chosen.AuthorizesEducationalData);
        await FinishAsync(member, await ReserveAsync(member, personal, Guid.NewGuid()), UsageReservationStatus.Consumed);
        var summary = await GetAsync<CommercialSummaryDto>(member.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(1, summary.Quotas.Single(q => q.Key == QuotaKey.AiTextRequests).Consumed);
        using var forbidden = await outsider.Client.PostAsJsonAsync("/api/v1/commerce/ai/simple-text/preflight", personal, Json);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var other = await factory.CreateOrganizationAsync("سازمان دیگر " + Guid.NewGuid().ToString("N"));
        var own = await GetAsync<CommercialSummaryDto>(member.Client, $"/api/v1/commerce/organization/subscription?organizationId={other}");
        Assert.Equal(organizationId, own.OwnerId);
    }
}
