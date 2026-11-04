using System.Text.Json;
using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task PlatformClassLearnersHaveNamedSchemaAndSupportLearnersWithoutAccounts()
    {
        var admin = await AdminAsync(); var org = await factory.CreateOrganizationAsync("schema learner " + Guid.NewGuid());
        var classroom = await factory.CreateAcademicClassAsync(org);
        var personId = await WithDb(async db => { var person = Person.Create("فراگیر", "بدون حساب", "نام مجاز فراگیر", factory.Clock.GetUtcNow().UtcDateTime);
            db.Persons.Add(person); await db.SaveChangesAsync(); return person.Id; });
        var learner = await factory.CreateOrganizationPersonAsync(org, personId);
        await factory.CreateEnrollmentAsync(org, classroom.ClassId, learner);
        var detail = await GetAsync<PlatformClassDetail>(admin.Client, $"/api/admin/v1/education/classes/{classroom.ClassId}");
        var row = Assert.Single(detail.ActiveLearners); Assert.Null(row.UserId); Assert.Equal(personId, row.PersonId);
        Assert.Equal("نام مجاز فراگیر", row.DisplayName); Assert.False(detail.ActiveLearnersTruncated);
        using var schema = JsonDocument.Parse(await admin.Client.GetStringAsync("/openapi/v1.json"));
        var learnerSchema = schema.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("PlatformClassDetail")
            .GetProperty("properties").GetProperty("activeLearners");
        Assert.True(learnerSchema.GetProperty("items").TryGetProperty("$ref", out var reference), learnerSchema.GetRawText());
        Assert.Equal("#/components/schemas/PlatformClassPerson", reference.GetString());
        Assert.True(schema.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("PlatformClassPerson")
            .GetProperty("properties").GetProperty("userId").GetProperty("nullable").GetBoolean());
        foreach (var path in new[] { "/api/admin/v1/personal-classes/{classId}/memberships", "/api/v1/organization/invitations",
            "/api/v1/organization/teacher-content-activities", "/api/v1/academic/exams/discovery" })
        {
            var parameters = schema.RootElement.GetProperty("paths").GetProperty(path).GetProperty("get").GetProperty("parameters").EnumerateArray().ToArray();
            var size = Assert.Single(parameters, p => p.GetProperty("name").GetString() == "pageSize").GetProperty("schema");
            Assert.Equal(1, size.GetProperty("minimum").GetInt32()); Assert.Equal(100, size.GetProperty("maximum").GetInt32());
            var page = Assert.Single(parameters, p => p.GetProperty("name").GetString() == "page").GetProperty("schema");
            Assert.Equal(1, page.GetProperty("default").GetInt32());
        }
        var filter = schema.RootElement.GetProperty("paths").GetProperty("/api/v1/organization/invitations").GetProperty("get")
            .GetProperty("parameters").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "invitationType");
        Assert.Contains("GuardianLink", filter.GetProperty("schema").GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
    }
}
