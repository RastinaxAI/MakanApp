using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    private static Task<ContactDeviceResult> ContactConsent(TestActor actor, Guid device, bool enabled = true) =>
        PostContactConsent(actor, new(device, "v1", "ContactsLimited", enabled));
    private static async Task<ContactDeviceResult> PostContactConsent(TestActor actor, SetContactConsentCommand command)
    {
        using var response = await actor.Client.PutAsJsonAsync("/api/v1/address-book/devices/consent", command, Json);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<ContactDeviceResult>(Json))!;
    }
    private static Task<ContactSyncResult> ContactSync(TestActor actor, Guid device, long revision, params ContactMutation[] items) =>
        PostAsync<ContactSyncResult>(actor.Client, "/api/v1/address-book/sync", new ContactSyncCommand(device, Guid.NewGuid(), revision, items));
    private static Task<ContactDeltaResult> ContactDelta(TestActor actor, Guid device, string? cursor = null, int limit = 100) =>
        GetAsync<ContactDeltaResult>(actor.Client, $"/api/v1/address-book/delta?deviceId={device}&limit={limit}&cursor={Uri.EscapeDataString(cursor ?? "")}");

    [Fact]
    public async Task ProductV6AddressBookSupportsUnregisteredMultiplePhonesEncryptedStorageAndExactReplay()
    {
        var owner = await ActorAsync(); var device = Guid.NewGuid(); var id = Guid.NewGuid();
        await ContactConsent(owner, device);
        var usersBefore = await WithDb(db => db.Users.CountAsync());
        var command = new ContactSyncCommand(device, Guid.NewGuid(), 1,
            [new ContactMutation(id, ContactMutationKind.Upsert, null, " نام ذخیره‌شده ", ["۰۹۱۲۱۲۳۴۵۶۷", "+98 912 1234567", "+989351234567"], ContactNameOrigin.Manual)]);
        var first = await PostAsync<ContactSyncResult>(owner.Client, "/api/v1/address-book/sync", command);
        var retry = await PostAsync<ContactSyncResult>(owner.Client, "/api/v1/address-book/sync", command);
        Assert.Equal(first.ServerVersion, retry.ServerVersion); Assert.Equal(first.NextCursor, retry.NextCursor);
        var delta = await ContactDelta(owner, device);
        var entry = Assert.Single(delta.Upserts);
        Assert.Equal("نام ذخیره‌شده", entry.SavedName); Assert.Equal(2, entry.Phones.Count); Assert.Null(entry.LinkedAccountUserId);
        Assert.False(entry.LinkCurrentlyVisible); Assert.Empty(delta.Tombstones);
        await WithDb(async db =>
        {
            Assert.Equal(usersBefore, await db.Users.CountAsync());
            var stored = await db.AddressBookEntries.SingleAsync(e => e.OwnerUserId == owner.UserId && e.Id == id);
            Assert.DoesNotContain("نام", stored.ProtectedContent); Assert.DoesNotContain("9121234567", stored.ProtectedContent);
            Assert.Equal(2, await db.ContactPhones.CountAsync(p => p.OwnerUserId == owner.UserId && p.EntryId == id));
            Assert.Equal(1, await db.ContactSyncOperations.CountAsync(o => o.OwnerUserId == owner.UserId));
        });
        var update = await ContactSync(owner, device, 2, new ContactMutation(id, ContactMutationKind.Upsert, entry.ServerVersion, "نام OS", ["+989121234567"]));
        Assert.Equal("Upserted", Assert.Single(update.Items).Status);
        Assert.Equal("نام ذخیره‌شده", Assert.Single((await ContactDelta(owner, device)).Upserts).SavedName);
        var logs = string.Join('\n', factory.SensitiveLogs.Snapshot());
        Assert.DoesNotContain("نام ذخیره‌شده", logs); Assert.DoesNotContain("+989121234567", logs); Assert.DoesNotContain("+989351234567", logs);
    }

    [Fact]
    public async Task ProductV6TombstonesStopOldDeviceResurrectionAndConsentRevocationDoesNotDeleteCloudData()
    {
        var owner = await ActorAsync(); var a = Guid.NewGuid(); var b = Guid.NewGuid(); var id = Guid.NewGuid();
        await ContactConsent(owner, a); await ContactConsent(owner, b);
        await ContactSync(owner, a, 1, new ContactMutation(id, ContactMutationKind.Upsert, null, "آزمون", ["+989121234567"]));
        var old = await ContactDelta(owner, b); var version = Assert.Single(old.Upserts).ServerVersion;
        var deletion = await ContactSync(owner, a, 2, new ContactMutation(id, ContactMutationKind.Delete, version));
        Assert.Equal("Deleted", Assert.Single(deletion.Items).Status);
        var changes = await ContactDelta(owner, b, old.NextCursor);
        Assert.Empty(changes.Upserts); Assert.Equal(id, Assert.Single(changes.Tombstones).EntryId);
        var stale = await ContactSync(owner, b, 1, new ContactMutation(id, ContactMutationKind.Upsert, version, "نام قدیمی", ["+989121234567"]));
        Assert.Equal("Tombstone", Assert.Single(stale.Items).Status);
        await ContactConsent(owner, b, false);
        using var denied = await owner.Client.GetAsync($"/api/v1/address-book/delta?deviceId={b}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await ContactConsent(owner, b);
        using var oldCursor = await owner.Client.GetAsync($"/api/v1/address-book/delta?deviceId={b}&cursor={Uri.EscapeDataString(changes.NextCursor)}");
        Assert.Equal(HttpStatusCode.BadRequest, oldCursor.StatusCode);
        Assert.Single((await ContactDelta(owner, b)).Tombstones);
        await WithDb(async db => Assert.Equal(0, await db.ContactPhones.CountAsync(p => p.OwnerUserId == owner.UserId)));
    }

    [Fact]
    public async Task ProductV6AddressBookHasPerItemErrorsAndCrossAccountCursorIsolation()
    {
        var owner = await ActorAsync(); var other = await ActorAsync(); var device = Guid.NewGuid();
        using var noConsent = await owner.Client.PostAsJsonAsync("/api/v1/address-book/sync",
            new ContactSyncCommand(device, Guid.NewGuid(), 1, [new ContactMutation(Guid.NewGuid(), ContactMutationKind.Upsert, null, "آزمون", ["+989121234567"])]), Json);
        Assert.Equal(HttpStatusCode.Forbidden, noConsent.StatusCode);
        await ContactConsent(owner, device); await ContactConsent(other, device);
        var first = await ContactSync(owner, device, 1,
            new ContactMutation(Guid.NewGuid(), ContactMutationKind.Upsert, null, "معتبر", ["+989121234567"]),
            new ContactMutation(Guid.NewGuid(), ContactMutationKind.Upsert, null, "ناقص", ["9121234"]));
        Assert.Equal(1, first.Items.Count(i => i.Status == "Upserted")); Assert.Equal(1, first.Items.Count(i => i.ErrorCode == AddressBookErrorCodes.Invalid));
        Assert.Empty((await ContactDelta(other, device)).Upserts);
        using var leak = await other.Client.GetAsync($"/api/v1/address-book/delta?deviceId={device}&cursor={Uri.EscapeDataString(first.NextCursor)}");
        Assert.Equal(HttpStatusCode.BadRequest, leak.StatusCode);
        using var sourceStale = await owner.Client.PostAsJsonAsync("/api/v1/address-book/sync",
            new ContactSyncCommand(device, Guid.NewGuid(), 1, [new ContactMutation(Guid.NewGuid(), ContactMutationKind.Upsert, null, "قدیمی", ["+989121234567"])]), Json);
        Assert.Equal(HttpStatusCode.Conflict, sourceStale.StatusCode);
    }

    [Fact]
    public async Task ProductV6ConcurrentContactUpdatesHaveOneWinnerAndNoDuplicatePhone()
    {
        var owner = await ActorAsync(); var a = Guid.NewGuid(); var b = Guid.NewGuid(); var id = Guid.NewGuid();
        await ContactConsent(owner, a); await ContactConsent(owner, b);
        var created = await ContactSync(owner, a, 1, new ContactMutation(id, ContactMutationKind.Upsert, null, "آزمون", ["+989121234567"]));
        var version = Assert.Single(created.Items).ServerVersion;
        var results = await Task.WhenAll(ContactSync(owner, a, 2, new ContactMutation(id, ContactMutationKind.Upsert, version, "الف", ["+989121234567"])),
            ContactSync(owner, b, 1, new ContactMutation(id, ContactMutationKind.Upsert, version, "ب", ["+989121234567"])));
        Assert.Single(results.SelectMany(r => r.Items), i => i.Status == "Upserted");
        Assert.Single(results.SelectMany(r => r.Items), i => i.ErrorCode == AddressBookErrorCodes.Conflict);
        await WithDb(async db => Assert.Equal(1, await db.ContactPhones.CountAsync(p => p.OwnerUserId == owner.UserId && p.EntryId == id)));
    }
}
