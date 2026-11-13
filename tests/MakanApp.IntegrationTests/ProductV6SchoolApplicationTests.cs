using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Application.Support;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    private static async Task<FileAssetResult> CaseFileAsync(TestActor actor)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("synthetic private case document"));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain"); form.Add(content, "file", "case-test.txt");
        using var response = await actor.Client.PostAsync("/api/v1/files?workspaceScope=Personal", form);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<FileAssetResult>(Json))!;
    }

    [Theory]
    [InlineData(SchoolApplicationKind.LegalOrganization)]
    [InlineData(SchoolApplicationKind.IndividualTeacher)]
    public async Task ProductV6SchoolDraftIsVersionedPrivateIdempotentAndRequiresExplicitSubmissionData(SchoolApplicationKind kind)
    {
        var admin = await AdminAsync(); var actor = await ActorAsync(); var other = await ActorAsync();
        var file = await CaseFileAsync(actor);
        var command = new CreateSchoolApplicationCommand(Guid.NewGuid(), kind, new("نام اولیه", null, null, null, null, null, null), [new(file.Id, "ApplicantProvided")]);
        var created = await PostAsync<SchoolApplicationResult>(actor.Client, "/api/v1/school-applications", command);
        var retry = await PostAsync<SchoolApplicationResult>(actor.Client, "/api/v1/school-applications", command);
        Assert.Equal(created.Id, retry.Id); Assert.Equal(1, retry.Revision); Assert.Null(retry.Data.StudentCapacity); Assert.Null(retry.Data.TeacherCapacity);
        using var hidden = await other.Client.GetAsync($"/api/v1/school-applications/{created.Id}"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var unpublished = await admin.Client.GetAsync($"/api/admin/v1/school-applications/{created.Id}"); Assert.Equal(HttpStatusCode.NotFound, unpublished.StatusCode);
        using var privateDraft = await admin.Client.GetAsync($"/api/v1/files/{file.Id}"); Assert.Equal(HttpStatusCode.NotFound, privateDraft.StatusCode);
        var update = new UpdateSchoolApplicationCommand(Guid.NewGuid(), created.Version, created.Data with { SchoolName = "نام اصلاح‌شده", StudentCapacity = 37 });
        using var response = await actor.Client.PutAsJsonAsync($"/api/v1/school-applications/{created.Id}", update, Json); response.EnsureSuccessStatusCode();
        var revised = (await response.Content.ReadFromJsonAsync<SchoolApplicationResult>(Json))!;
        Assert.Equal(2, revised.Revision); Assert.Equal(file.Id, Assert.Single(revised.Documents).FileAssetId); Assert.Null(revised.Data.TeacherCapacity);
        var old = await GetAsync<SchoolApplicationResult>(actor.Client, $"/api/v1/school-applications/{created.Id}?revision=1"); Assert.Equal("نام اولیه", old.Data.SchoolName);
        using var duplicate = await actor.Client.PutAsJsonAsync($"/api/v1/school-applications/{created.Id}", update, Json); duplicate.EnsureSuccessStatusCode();
        using var stale = await actor.Client.PutAsJsonAsync($"/api/v1/school-applications/{created.Id}", update with { ClientOperationId = Guid.NewGuid() }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var submit = await actor.Client.PostAsJsonAsync($"/api/v1/school-applications/{created.Id}/submit", new SchoolApplicationActionCommand(Guid.NewGuid(), revised.Version), Json);
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode); Assert.Equal(SchoolApplicationErrorCodes.SubmissionIncomplete, await ProblemAsync(submit));
        await WithDb(async db => {
            Assert.Equal(2, await db.SchoolApplicationRevisions.CountAsync(r => r.ApplicationId == created.Id));
            Assert.Equal(SchoolApplicationStatus.Draft, (await db.SchoolApplications.SingleAsync(a => a.Id == created.Id)).Status);
            Assert.False(await db.Memberships.AnyAsync(m => m.UserId == actor.UserId));
            Assert.False(await db.PlatformProvisioningRequests.AnyAsync(p => p.InitialManagerUserId == actor.UserId));
        });
    }

    [Fact]
    public async Task ProductV6SupportBoundFileUsesCaseAclBeforeUploaderFallbackAndRoleRevocation()
    {
        var admin = await AdminAsync(); var owner = await ActorAsync(); var other = await ActorAsync();
        var ticket = await PostAsync<SupportTicketResult>(owner.Client, "/api/v1/support/tickets",
            new CreateSupportTicketCommand(Guid.NewGuid(), "پرونده تست فایل", "General", "درخواست تست"));
        var file = await CaseFileAsync(admin);
        await PostAsync<SupportTicketResult>(admin.Client, $"/api/admin/v1/support/tickets/{ticket.Id}/messages",
            new ReplySupportTicketCommand(Guid.NewGuid(), ticket.Version, "مدرک تست کارشناس", [file.Id]));
        using var allowed = await owner.Client.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var hidden = await other.Client.GetAsync($"/api/v1/files/{file.Id}"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        await factory.ResetPlatformIamAsync();
        using var revoked = await admin.Client.GetAsync($"/api/v1/files/{file.Id}"); Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        using var retained = await owner.Client.GetAsync($"/api/v1/files/{file.Id}"); Assert.Equal(HttpStatusCode.OK, retained.StatusCode);
    }

    [Fact]
    public async Task ProductV6BioIsReturnedInExistingProfileAndOldContractPreservesIt()
    {
        var owner = await ActorAsync(); var username = "bio" + Guid.NewGuid().ToString("N")[..12];
        var command = new CompleteProfileCommand("نام", "آزمون", null, username, Bio: "  معرفی شخصی  ");
        using var patch = await owner.Client.PatchAsJsonAsync("/api/v1/me/profile", command, Json); patch.EnsureSuccessStatusCode();
        var updated = (await patch.Content.ReadFromJsonAsync<CurrentUserResult>(Json))!; Assert.Equal("معرفی شخصی", updated.Bio);
        using var oldClient = await owner.Client.PatchAsJsonAsync("/api/v1/me/profile", command with { Bio = null }, Json); oldClient.EnsureSuccessStatusCode();
        Assert.Equal("معرفی شخصی", (await oldClient.Content.ReadFromJsonAsync<CurrentUserResult>(Json))!.Bio);
        using var clear = await owner.Client.PatchAsJsonAsync("/api/v1/me/profile", command with { Bio = "" }, Json); clear.EnsureSuccessStatusCode();
        Assert.Null((await clear.Content.ReadFromJsonAsync<CurrentUserResult>(Json))!.Bio);
    }
}
