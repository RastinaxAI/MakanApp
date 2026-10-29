using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Guardian;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformAuthorizationRegressionTests
{
    [Fact]
    public async Task SchoolConcurrentLessonCreationAndPublicationHaveOneEffect()
    {
        var org = await factory.CreateOrganizationAsync($"Lesson race {Guid.NewGuid():N}");
        var actor = await SchoolActorAsync(org, OrganizationRole.Manager); using var client = actor.Client;
        var k = await factory.CreateAcademicClassAsync(org); var op = Guid.NewGuid();
        var responses = await Task.WhenAll(SchoolDraftAsync(client, k.ClassId, op), SchoolDraftAsync(client, k.ClassId, op));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        var results = new List<ClassLessonResult>();
        foreach (var response in responses) results.Add(await SchoolReadAsync<ClassLessonResult>(response, response.StatusCode));
        Assert.Equal(results[0].Id, results[1].Id);
        var publication = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync(
            $"/api/v1/academic/lessons/{results[0].Id}/publish", new PublishClassLessonCommand(results[0].Version))));
        foreach (var response in publication) Assert.Equal(2, (await SchoolReadAsync<ClassLessonResult>(response)).RevisionNumber);
        await using var scope = _host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.ClassLessons.CountAsync(x => x.OrganizationId == org));
    }

    [Fact]
    public async Task SchoolConcurrentParentConsentCreatesOneRelationMembershipAndRole()
    {
        var org = await factory.CreateOrganizationAsync($"Consent race {Guid.NewGuid():N}");
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var m = manager.Client;
        using var parent = _host.CreateClient(); var account = await CreateP0PersonalAccountAsync(parent, null);
        using var privacy = await parent.PutAsJsonAsync("/api/v1/messaging/privacy", new UpdateMessagingPrivacyCommand(
            PrivacyAudience.Nobody, PrivacyAudience.Nobody, PrivacyAudience.Nobody, PrivacyAudience.Nobody, null), PolicyJson); privacy.EnsureSuccessStatusCode();
        var child = (await factory.CreateOrganizationPersonsAsync(org, 1)).Single();
        var invitation = await SchoolInviteAsync(manager, child, account.Username);
        var results = await Task.WhenAll(SchoolAcceptAsync(parent, invitation.Id), SchoolAcceptAsync(parent, invitation.Id));
        Assert.Equal(results[0].GuardianRelationId, results[1].GuardianRelationId);
        Assert.Single(results, x => x.AlreadyAccepted);
        await using var scope = _host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.GuardianRelations.CountAsync(x => x.OrganizationId == org));
        Assert.Equal(1, await db.Memberships.CountAsync(x => x.OrganizationId == org && x.UserId == account.UserId));
        Assert.Equal(1, await db.RoleAssignments.CountAsync(x => x.MembershipId == results[0].MembershipId && x.Role == OrganizationRole.Parent));
        Assert.Equal(1, await db.UserCredentials.CountAsync(x => x.UserId == account.UserId));
        var privacyState = await db.MessagingPrivacySettings.SingleAsync(x => x.UserId == account.UserId);
        Assert.Equal(PrivacyAudience.Nobody, privacyState.PhoneNumberDiscoveryAudience);
        Assert.Equal(PrivacyAudience.Nobody, privacyState.DirectMessagesAudience);
    }

    [Fact]
    public async Task SchoolGuardianConsentDoesNotReactivateSuspendedMembershipOrInactiveLearner()
    {
        var org = await factory.CreateOrganizationAsync($"Suspended parent {Guid.NewGuid():N}");
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); var p = await SchoolActorAsync(org, OrganizationRole.Parent);
        using var m = manager.Client; using var parent = p.Client;
        var child = (await factory.CreateOrganizationPersonsAsync(org, 1)).Single();
        var invitation = await SchoolInviteAsync(manager, child, p.Username);
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            (await db.Memberships.SingleAsync(x => x.Id == p.MembershipId)).Suspend(); await db.SaveChangesAsync();
        }
        await ErrorAsync(await parent.PostAsJsonAsync($"/api/v1/invitations/{invitation.Id}/accept", new AcceptInvitationCommand(ConfirmGuardianRelation: true)), HttpStatusCode.Forbidden, OrganizationErrorCodes.MembershipNotActive);
        await using var finalScope = _host.Services.CreateAsyncScope(); var finalDb = finalScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(InvitationStatus.Pending, (await finalDb.Invitations.SingleAsync(x => x.Id == invitation.Id)).Status);
        Assert.Empty(await finalDb.GuardianRelations.Where(x => x.OrganizationId == org).ToArrayAsync());
        Assert.Equal(1, await finalDb.Memberships.CountAsync(x => x.OrganizationId == org && x.UserId == p.UserId));
    }

    [Fact]
    public async Task SchoolGuardianInactiveLearnerAndCrossOrganizationInviteAreDenied()
    {
        var org = await factory.CreateOrganizationAsync($"Child state {Guid.NewGuid():N}");
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var m = manager.Client;
        using var parent = _host.CreateClient(); var account = await CreateP0PersonalAccountAsync(parent, null);
        var child = (await factory.CreateOrganizationPersonsAsync(org, 1)).Single();
        var invitation = await SchoolInviteAsync(manager, child, account.Username);
        await factory.EndOrganizationPersonAsync(child);
        using var denied = await parent.PostAsJsonAsync($"/api/v1/invitations/{invitation.Id}/accept", new AcceptInvitationCommand(ConfirmGuardianRelation: true));
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var otherOrg = await factory.CreateOrganizationAsync($"Foreign child {Guid.NewGuid():N}");
        var foreign = (await factory.CreateOrganizationPersonsAsync(otherOrg, 1)).Single();
        await ErrorAsync(await m.PostAsJsonAsync($"/api/v1/organization/people/{foreign}/guardian-invitations", new CreateSchoolGuardianInvitationCommand(Guid.NewGuid(), account.Username, null, Now.AddDays(1), "دعوت غیرمجاز")), HttpStatusCode.NotFound, SchoolErrorCodes.GuardianTargetUnavailable);
        await using var scope = _host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Empty(await db.Memberships.Where(x => x.UserId == account.UserId).ToArrayAsync());
        Assert.Empty(await db.GuardianRelations.Where(x => x.OrganizationId == org).ToArrayAsync());
    }

    [Fact]
    public async Task SchoolLessonRejectsForeignPersonalUnreadyAndOtherUploaderFilesAndRetainsReplacementOrder()
    {
        var org = await factory.CreateOrganizationAsync($"File binding {Guid.NewGuid():N}");
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var m = manager.Client;
        var other = await SchoolActorAsync(org, OrganizationRole.Teacher); using var t = other.Client;
        var k = await factory.CreateAcademicClassAsync(org);
        var own = await SchoolUploadAsync(m); var second = await SchoolUploadAsync(m); var otherFile = await SchoolUploadAsync(t);
        var forbidden = new List<Guid> { otherFile.Id };
        Guid unreadyId;
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            foreach (var location in new Guid?[] { null, await factory.CreateOrganizationAsync($"File foreign {Guid.NewGuid():N}"), org })
            {
                var file = FileAsset.CreatePending(Guid.NewGuid(), location, manager.UserId, "fixture.txt", Guid.NewGuid().ToString("N"), "text/plain", Now, Now.AddHours(1));
                if (location != org) file.MarkReady(5, new string('a', 64), Now);
                db.FileAssets.Add(file); forbidden.Add(file.Id);
            }
            await db.SaveChangesAsync(); unreadyId = forbidden.Last();
        }
        foreach (var id in forbidden)
        {
            using var response = await SchoolDraftAsync(m, k.ClassId, Guid.NewGuid(), [id]);
            Assert.Equal(id == unreadyId ? HttpStatusCode.Conflict : HttpStatusCode.NotFound, response.StatusCode);
        }
        var draft = await SchoolReadAsync<ClassLessonResult>(await SchoolDraftAsync(m, k.ClassId, Guid.NewGuid(), [own.Id, second.Id]), HttpStatusCode.Created);
        var changed = await SchoolReadAsync<ClassLessonResult>(await m.PatchAsJsonAsync($"/api/v1/academic/lessons/{draft.Id}", new UpdateClassLessonCommand(draft.Title, draft.Body, 0, [second.Id], draft.Version)));
        Assert.Equal(second.Id, Assert.Single(changed.Attachments).FileAssetId);
        await using var finalScope = _host.Services.CreateAsyncScope(); var finalDb = finalScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(FileAssetStatus.Ready, (await finalDb.FileAssets.SingleAsync(x => x.Id == own.Id)).Status);
    }

    [Fact]
    public async Task SchoolOpenApiDocumentsExamplesVersionsPaginationAndMandatoryScheduleRange()
    {
        using var client = _host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var directory = paths.GetProperty("/api/v1/organization/members").GetProperty("get");
        Assert.True(directory.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").TryGetProperty("example", out var page));
        Assert.Equal(50, page.GetProperty("pageSize").GetInt32()); Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        var publish = paths.GetProperty("/api/v1/academic/lessons/{lessonId}/publish").GetProperty("post");
        Assert.Contains("expectedVersion", publish.GetProperty("requestBody").ToString(), StringComparison.Ordinal);
        Assert.True(publish.GetProperty("responses").TryGetProperty("412", out _));
        var parameters = paths.GetProperty("/api/v1/academic/schedule").GetProperty("get").GetProperty("parameters").EnumerateArray().ToArray();
        foreach (var name in new[] { "fromUtc", "toUtc" }) Assert.True(parameters.Single(x => x.GetProperty("name").GetString() == name).GetProperty("required").GetBoolean());
        var parent = paths.GetProperty("/api/v1/organization/people/{learnerId}/guardian-invitations").GetProperty("post");
        Assert.Contains("username", parent.GetProperty("requestBody").ToString(), StringComparison.Ordinal);
    }
}
