using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Knowledge;
using MakanApp.Application.Messaging;
using MakanApp.Application.Storage;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task Be15ScheduledBindingFencesAStaleFileDeletionWithSqlRowVersion()
    {
        using var s = await CreateAdvancedConversationSetupAsync();
        var file = await UploadFileAsync(s.FirstClient, "application/pdf", "scheduled-race.pdf");
        await using var staleScope = factory.Services.CreateAsyncScope();
        var staleStore = staleScope.ServiceProvider.GetRequiredService<IFileAssetStore>();
        var stale = (await staleStore.GetForUpdateAsync(file.Id, default))!;
        _ = await ScheduleP2Async(s.FirstClient, s.ConversationId, new(factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(10),
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.File, AttachmentIds = [file.Id] }));
        try
        {
            stale.MarkDeleted(factory.Clock.GetUtcNow().UtcDateTime);
            var conflict = await Assert.ThrowsAsync<StorageException>(() => staleStore.SaveChangesAsync(default));
            Assert.Equal(StorageErrorCodes.ConcurrencyConflict, conflict.Code);
            Assert.True(await factory.StoredBytesExistAsync(file.Id));
            await FileLifecycleAssertions.BoundCannotDeleteAsync(factory, s.FirstClient, file.Id);
        }
        finally { await CleanupP2SchedulesAsync(s.First.UserId); }
    }

    [Fact]
    public async Task Be15ScheduledFileIsPrivateBoundAndCannotBeDeletedOrPromotedIntoSource()
    {
        using var s = await CreateAdvancedConversationSetupAsync();
        var file = await UploadFileAsync(s.FirstClient, "application/pdf", "scheduled-private.pdf");
        var task = await ScheduleP2Async(s.FirstClient, s.ConversationId, new(factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(10),
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.File, AttachmentIds = [file.Id] }));
        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                Assert.NotNull((await db.FileAssets.AsNoTracking().SingleAsync(f => f.Id == file.Id)).RetainedAtUtc);
                // شبیه‌سازی دادهٔ قدیمی بدون marker؛ اتصال واقعی SQL همچنان باید جلوی حذف را بگیرد.
                await db.FileAssets.Where(f => f.Id == file.Id).ExecuteUpdateAsync(setters => setters.SetProperty(f => f.RetainedAtUtc, (DateTime?)null));
            }
            await FileLifecycleAssertions.BoundCannotDeleteAsync(factory, s.FirstClient, file.Id);
            using (var own = await s.FirstClient.GetAsync($"/api/v1/files/{file.Id}/content")) Assert.Equal(HttpStatusCode.OK, own.StatusCode);
            using (var hidden = await s.SecondClient.GetAsync($"/api/v1/files/{file.Id}/content")) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            using (var source = await s.FirstClient.PostAsJsonAsync("/api/v1/sources", SourceCommand(EducationalSourceScope.Personal, file.Id), JsonOptions))
                Assert.Equal(HttpStatusCode.Forbidden, source.StatusCode);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                var member = await db.ConversationParticipants.SingleAsync(p => p.ConversationId == s.ConversationId && p.UserId == s.First.UserId);
                member.Leave(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync();
            }
            using var revoked = await s.FirstClient.GetAsync($"/api/v1/files/{file.Id}/content");
            Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
            Assert.True(await factory.StoredBytesExistAsync(file.Id));
        }
        finally { await CleanupP2SchedulesAsync(s.First.UserId); }
    }

    [Fact]
    public async Task Be15SourceArchiveRevokesBothRoutesAndRangeWithoutDeletingHistoricalBytes()
    {
        using var s = await CreateOfficialScenarioAsync(false);
        await SourceContract(SubscriptionOwner.Organization(s.OrganizationId), 5);
        var file = await SourceUpload(s.Teacher, false);
        var source = await SourcePublish(s.Teacher, await SourceCreate(s.Teacher, SourceCommand(EducationalSourceScope.Class, file.Id, s.ClassId)));
        await FileLifecycleAssertions.BoundCannotDeleteAsync(factory, s.Teacher, file.Id);
        using (var before = await s.Student.GetAsync($"/api/v1/sources/{source.SourceId}/content")) Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        using var archive = await s.Teacher.PostAsJsonAsync($"/api/v1/sources/{source.SourceId}/archive", new SourceMutationCommand(source.Version, "آرشیو برای اثبات revoke"), JsonOptions);
        archive.EnsureSuccessStatusCode();
        foreach (var path in new[] { $"/api/v1/sources/{source.SourceId}", $"/api/v1/sources/{source.SourceId}/content", $"/api/v1/files/{file.Id}", $"/api/v1/files/{file.Id}/content" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Range = new(0, 5);
            using var denied = await s.Student.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            Assert.Null(denied.Content.Headers.ContentRange);
            Assert.DoesNotContain("storageKey", await denied.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }
        Assert.True(await factory.StoredBytesExistAsync(file.Id));
        await FileLifecycleAssertions.BoundCannotDeleteAsync(factory, s.Teacher, file.Id);
    }

    [Theory]
    [InlineData(SourceSafetyStatus.ReviewRequired)]
    [InlineData(SourceSafetyStatus.Rejected)]
    public async Task Be15PendingSafetyHoldCannotBeResetByAReadyFileOrVersionRetry(SourceSafetyStatus safety)
    {
        using var client = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await SourceContract(SubscriptionOwner.Individual(user.UserId), 5);
        var file = await SourceUpload(client, true);
        var source = await SourceCreate(client, SourceCommand(EducationalSourceScope.Personal, file.Id));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var revision = await db.EducationalSourceVersions.SingleAsync(v => v.SourceId == source.SourceId);
            revision.RecordFileValidation(safety); await db.SaveChangesAsync();
        }
        using var version = await client.PostAsJsonAsync($"/api/v1/sources/{source.SourceId}/versions",
            new CreateSourceVersionCommand(file.Id, "تلاش استفاده از همان فایل", null, source.Version, "آزمون Safety"), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, version.StatusCode);
        foreach (var path in new[] { $"/api/v1/sources/{source.SourceId}/content", $"/api/v1/files/{file.Id}/content" })
        {
            using var response = await client.GetAsync(path);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Conflict, HttpStatusCode.NotFound });
        }
        await FileLifecycleAssertions.BoundCannotDeleteAsync(factory, client, file.Id);
    }
}
