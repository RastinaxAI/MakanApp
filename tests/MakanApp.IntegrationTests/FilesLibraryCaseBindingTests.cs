using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Knowledge;
using MakanApp.Application.Organization;
using MakanApp.Application.Support;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task Be15SchoolReviewerPermissionRevocationImmediatelyBlocksOldFileAndRange()
    {
        var reviewer = await AdminAsync(); var owner = await ActorAsync();
        var application = await SchoolDraftAsync(owner, SchoolApplicationKind.LegalOrganization);
        application = await SchoolAction(owner, application, "submit");
        var fileId = Assert.Single(application.Documents).FileAssetId;
        using (var allowed = await reviewer.Client.GetAsync($"/api/v1/files/{fileId}/content")) Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        await FileLifecycleAssertions.BoundCannotDeleteAsync(factory, owner.Client, fileId);
        await factory.ResetPlatformIamAsync();
        foreach (var suffix in new[] { "", "/content" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/files/{fileId}{suffix}");
            request.Headers.Range = new(0, 3);
            using var denied = await reviewer.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            Assert.Null(denied.Content.Headers.ContentRange);
        }
        using var retained = await owner.Client.GetAsync($"/api/v1/files/{fileId}/content");
        Assert.Equal(HttpStatusCode.OK, retained.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Be15PrivateCaseBindingWithoutRetentionStillPreventsDeletionAndSourcePromotion(bool school)
    {
        var actor = await ActorAsync(); var other = await ActorAsync();
        var file = await CaseFileAsync(actor);
        if (school)
            _ = await PostAsync<SchoolApplicationResult>(actor.Client, "/api/v1/school-applications",
                new CreateSchoolApplicationCommand(Guid.NewGuid(), SchoolApplicationKind.LegalOrganization,
                    new("پرونده آزمایشی", null, null, null, null, null, null), [new(file.Id, "ApplicantProvided")]));
        else
            _ = await PostAsync<SupportTicketResult>(actor.Client, "/api/v1/support/tickets",
                new CreateSupportTicketCommand(Guid.NewGuid(), "درخواست آزمایشی فایل", "General", "متن ساختگی", [file.Id]));
        await WithDb(async db => await db.FileAssets.Where(f => f.Id == file.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(f => f.RetainedAtUtc, (DateTime?)null)));
        await FileLifecycleAssertions.BoundCannotDeleteAsync(factory, actor.Client, file.Id);
        using (var own = await actor.Client.GetAsync($"/api/v1/files/{file.Id}/content")) Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        foreach (var suffix in new[] { "", "/content" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/files/{file.Id}{suffix}");
            request.Headers.Range = new(0, 8);
            using var denied = await other.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            Assert.Null(denied.Content.Headers.ContentRange);
        }
        using var promotion = await actor.Client.PostAsJsonAsync("/api/v1/sources",
            new CreateEducationalSourceCommand(Guid.NewGuid(), EducationalSourceScope.Personal, null,
                "تلاش مصرف مجدد", null, file.Id, "آزمون جلوگیری از اتصال دوباره"), Json);
        Assert.Equal(HttpStatusCode.Forbidden, promotion.StatusCode);
    }
}
