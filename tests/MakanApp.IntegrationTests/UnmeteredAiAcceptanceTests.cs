using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Theory]
    [InlineData(AiUsageMode.Unmetered, true)]
    [InlineData(AiUsageMode.Commercial, false)]
    public async Task UnmeteredTwentyFiveDistinctRequestsWithoutSubscriptionPassActualHttpLimitAndHaveNoFinancialEffect(
        AiUsageMode configuredMode, bool commercialEnforcementEnabled)
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        using var host = factory.WithAiUsageMode(configuredMode, enforceHttpLimits: true,
            commercialEnforcementEnabled: commercialEnforcementEnabled);
        using var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = actor.Client.DefaultRequestHeaders.Authorization;
        var preflight = await PostAsync<AiQuotaPreflightDto>(client, "/api/v1/commerce/ai/simple-text/preflight", new AiQuotaPreflightCommand(CommerceContextType.Personal));
        Assert.True(preflight.Allowed); Assert.Null(preflight.Remaining); Assert.False(preflight.RequiresExplicitSelection);
        Assert.Equal(AiUsageMode.Unmetered, preflight.UsageMode);
        var operations = Enumerable.Range(0, 25).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var operation in operations)
        {
            using var result = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("پرسش کنترل‌شدهٔ نامحدود", null) { OperationId = operation }, Json);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }
        Assert.Equal(25, factory.AiService.ProviderCallCount);
        await WithDb(async db =>
        {
            var records = await db.UsageReservations.Where(r => r.ConsumerUserId == actor.UserId).ToArrayAsync();
            Assert.Equal(25, records.Length);
            Assert.All(records, r =>
            {
                Assert.True(r.IsUnmetered); Assert.False(r.IsTrial); Assert.Null(r.SubscriptionId); Assert.Null(r.PlanVersionId);
                Assert.Equal(UsageReservationStatus.Consumed, r.Status); Assert.Contains(r.OperationId, operations);
                Assert.Equal(UsageAccountingMode.Unmetered, r.AccountingMode);
            });
            var ledger = await db.UsageLedgerEntries.Where(e => e.ConsumerUserId == actor.UserId).ToArrayAsync();
            Assert.Equal(25, ledger.Count(e => e.Status == UsageReservationStatus.Consumed));
            Assert.All(ledger, e => { Assert.True(e.IsUnmetered); Assert.Null(e.SubscriptionId); Assert.Null(e.PlanVersionId); });
            Assert.False(await db.Subscriptions.AnyAsync(s => s.OwnerId == actor.UserId));
            Assert.False(await db.Orders.AnyAsync(o => o.PurchaserUserId == actor.UserId));
            Assert.False(await db.PaymentAttempts.AnyAsync(p => db.Orders.Any(o => o.Id == p.OrderId && o.PurchaserUserId == actor.UserId)));
            Assert.False(await db.Payments.AnyAsync(p => db.Orders.Any(o => o.Id == p.OrderId && o.PurchaserUserId == actor.UserId)));
        });
        // بازگشت به Commercial نباید عملیات نامحدود را مصرف Trial یا قرارداد پولی بشمارد.
        var commercial = await PostAsync<AiQuotaPreflightDto>(actor.Client, "/api/v1/commerce/ai/simple-text/preflight",
            new AiQuotaPreflightCommand(CommerceContextType.Personal, Payer: AiPayerSelection.FreeTrial));
        Assert.True(commercial.Allowed); Assert.Equal(3, commercial.Remaining);
    }

    [Theory]
    [InlineData(null)] [InlineData("Individual")] [InlineData("Organization")] [InlineData("FreeTrial")]
    public async Task UnmeteredValidPayerHasNoCommercialEffectOrReplayIdentityEffect(string? payer)
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        using var host = factory.WithAiUsageMode(AiUsageMode.Unmetered); using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = actor.Client.DefaultRequestHeaders.Authorization;
        if (payer is not null) client.DefaultRequestHeaders.Add("X-AI-Payer", payer);
        var command = new SendAiChatCommand("قصد ثابت با Payer سازگار", null) { OperationId = Guid.NewGuid() };
        using (var response = await client.PostAsJsonAsync("/api/v1/ai/chat", command, Json)) response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("X-AI-Payer"); client.DefaultRequestHeaders.Add("X-AI-Payer", "FreeTrial");
        using var replay = await client.PostAsJsonAsync("/api/v1/ai/chat", command, Json);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode); Assert.Equal(AiInvocationErrorCodes.Completed, await ProblemAsync(replay));
        Assert.Equal(1, factory.AiService.ProviderCallCount);
        await WithDb(async db => Assert.True((await db.UsageReservations.SingleAsync(r => r.OperationId == command.OperationId)).IsUnmetered));
    }

    [Fact]
    public async Task UnmeteredInvalidPayerAndOversizedMessageNeverReserveOrInvoke()
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        using var host = factory.WithAiUsageMode(AiUsageMode.Unmetered); using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = actor.Client.DefaultRequestHeaders.Authorization;
        client.DefaultRequestHeaders.Add("X-AI-Payer", "not-a-payer");
        using (var invalid = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("پرسش", null), Json)) Assert.False(invalid.IsSuccessStatusCode);
        client.DefaultRequestHeaders.Remove("X-AI-Payer");
        using (var large = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand(new string('x', 20001), null), Json)) Assert.False(large.IsSuccessStatusCode);
        Assert.Equal(0, factory.AiService.ProviderCallCount);
        await WithDb(async db => Assert.False(await db.UsageReservations.AnyAsync(r => r.ConsumerUserId == actor.UserId)));
    }

    [Fact]
    public async Task UnmeteredTwentyConcurrentDuplicatesInvokeOnceAndChangedIntentConflicts()
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        using var host = factory.WithAiUsageMode(AiUsageMode.Unmetered); using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = actor.Client.DefaultRequestHeaders.Authorization;
        var command = new SendAiChatCommand("قصد هم‌زمان کنترل‌شده", null) { OperationId = Guid.NewGuid() };
        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.PostAsJsonAsync("/api/v1/ai/chat", command, Json)));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        }
        finally { foreach (var response in responses) response.Dispose(); }
        using var conflict = await client.PostAsJsonAsync("/api/v1/ai/chat", command with { Message = "قصد متفاوت" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode); Assert.Equal(CommerceErrorCodes.UsageConflict, await ProblemAsync(conflict));
        Assert.Equal(1, factory.AiService.ProviderCallCount);
        await WithDb(async db =>
        {
            Assert.Equal(1, await db.UsageReservations.CountAsync(r => r.ConsumerUserId == actor.UserId && r.OperationId == command.OperationId));
            Assert.Equal(1, await db.UsageLedgerEntries.CountAsync(e => e.SourceOperationId == command.OperationId && e.Status == UsageReservationStatus.Consumed));
        });
    }

    [Theory]
    [InlineData(AiServiceErrorCodes.ServiceTimeout, UsageReservationStatus.Unknown)]
    [InlineData(AiServiceErrorCodes.ResponseInvalid, UsageReservationStatus.Unknown)]
    [InlineData(AiServiceErrorCodes.RateLimited, UsageReservationStatus.Released)]
    [InlineData(AiServiceErrorCodes.ServiceUnauthorized, UsageReservationStatus.Released)]
    public async Task UnmeteredFailureKeepsDurableOutcomeAndNeverRetriesProvider(string errorCode, UsageReservationStatus expected)
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        using var host = factory.WithAiUsageMode(AiUsageMode.Unmetered); using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = actor.Client.DefaultRequestHeaders.Authorization;
        factory.AiService.FailNext(new AiServiceException(errorCode, "خطای کنترل‌شده بدون محتوای خصوصی"));
        var command = new SendAiChatCommand("قصد بازیابی کنترل‌شده", null) { OperationId = Guid.NewGuid() };
        using (var failure = await client.PostAsJsonAsync("/api/v1/ai/chat", command, Json))
        {
            Assert.False(failure.IsSuccessStatusCode); Assert.Equal(errorCode, await ProblemAsync(failure));
            if (errorCode == AiServiceErrorCodes.RateLimited) Assert.Equal(HttpStatusCode.TooManyRequests, failure.StatusCode);
        }
        using var replay = await client.PostAsJsonAsync("/api/v1/ai/chat", command, Json);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal(expected == UsageReservationStatus.Unknown ? AiInvocationErrorCodes.Unknown : AiInvocationErrorCodes.Failed, await ProblemAsync(replay));
        Assert.Equal(1, factory.AiService.ProviderCallCount);
        await WithDb(async db =>
        {
            var saved = await db.UsageReservations.SingleAsync(r => r.OperationId == command.OperationId);
            Assert.True(saved.IsUnmetered); Assert.Equal(expected, saved.Status);
            Assert.False(await db.UsageLedgerEntries.AnyAsync(e => e.SourceOperationId == command.OperationId && e.Status == UsageReservationStatus.Consumed));
        });
    }

    [Fact]
    public async Task CommercialActualHttpLimiterStillRejectsTwentyFirstRequest()
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual, ai: 100));
        using var host = factory.WithAiUsageMode(AiUsageMode.Commercial, enforceHttpLimits: true); using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = actor.Client.DefaultRequestHeaders.Authorization;
        for (var index = 0; index < 20; index++)
        {
            using var accepted = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("قصد تجاری کنترل‌شده", null), Json);
            accepted.EnsureSuccessStatusCode();
        }
        using var limited = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("درخواست بیست‌ویکم", null), Json);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode); Assert.Equal(20, factory.AiService.ProviderCallCount);
    }

    [Fact]
    public async Task UnmeteredDoesNotDebitExistingCommercialQuotaAndModeChangeDoesNotReinvokeSameOperation()
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        var subscription = await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual, ai: 1));
        using var host = factory.WithAiUsageMode(AiUsageMode.Unmetered); using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = actor.Client.DefaultRequestHeaders.Authorization;
        var command = new SendAiChatCommand("قصد بدون مصرف تجاری", null) { OperationId = Guid.NewGuid() };
        using (var accepted = await client.PostAsJsonAsync("/api/v1/ai/chat", command, Json)) accepted.EnsureSuccessStatusCode();
        var summary = await GetAsync<CommercialSummaryDto>(actor.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(0, summary.Quotas.Single(q => q.Key == QuotaKey.AiTextRequests).Consumed);
        Assert.Equal(1, summary.Quotas.Single(q => q.Key == QuotaKey.AiTextRequests).Remaining);
        using var changedMode = await actor.Client.PostAsJsonAsync("/api/v1/ai/chat", command, Json);
        Assert.Equal(HttpStatusCode.Conflict, changedMode.StatusCode); Assert.Equal(1, factory.AiService.ProviderCallCount);
        await WithDb(async db => Assert.False(await db.UsageLedgerEntries.AnyAsync(e => e.SubscriptionId == subscription)));
    }

    [Fact]
    public async Task UnmeteredPersonalClassPreflightStillRequiresCurrentOwnershipOrMembership()
    {
        var owner = await ActorAsync(); var outsider = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(owner.UserId), await PlanAsync(SubscriptionOwnerType.Individual, ai: 0, hosting: true, aiEntitled: false));
        var personalClass = await ApprovedClassAsync(owner, "کلاس مجاز بدون سهمیه AI");
        using var host = factory.WithAiUsageMode(AiUsageMode.Unmetered); using var allowed = host.CreateClient(); using var denied = host.CreateClient();
        allowed.DefaultRequestHeaders.Authorization = owner.Client.DefaultRequestHeaders.Authorization;
        denied.DefaultRequestHeaders.Authorization = outsider.Client.DefaultRequestHeaders.Authorization;
        var command = new AiQuotaPreflightCommand(CommerceContextType.PersonalClass, personalClass.Id);
        var preflight = await PostAsync<AiQuotaPreflightDto>(allowed, "/api/v1/commerce/ai/simple-text/preflight", command);
        Assert.True(preflight.Allowed); Assert.Null(preflight.Remaining);
        using var rejected = await denied.PostAsJsonAsync("/api/v1/commerce/ai/simple-text/preflight", command, Json);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UnmeteredStillRejectsMissingSessionOrSuspendedAccount(bool suspend)
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        using var host = factory.WithAiUsageMode(AiUsageMode.Unmetered); using var client = host.CreateClient();
        if (suspend)
        {
            client.DefaultRequestHeaders.Authorization = actor.Client.DefaultRequestHeaders.Authorization;
            await WithDb(async db => { (await db.Users.SingleAsync(u => u.Id == actor.UserId)).Suspend(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(); });
        }
        using var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("قصد بدون مجوز", null), Json);
        Assert.False(response.IsSuccessStatusCode); Assert.Equal(0, factory.AiService.ProviderCallCount);
        await WithDb(async db => Assert.False(await db.UsageReservations.AnyAsync(r => r.ConsumerUserId == actor.UserId)));
    }
}
