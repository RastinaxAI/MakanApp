using System.Text.Json;
using System.Text.Json.Nodes;
using MakanApp.Application.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class SubmissionPolicyContractTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Theory]
    [InlineData("dueAtUtc")]
    [InlineData("allowLateSubmission")]
    [InlineData("expectedAssignmentRowVersion")]
    [InlineData("expectedVersionRowVersion")]
    public void EveryPolicyAndConcurrencyFieldMustBeExplicit(string missingField)
    {
        var document = Request(); document.Remove(missingField);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateAssignmentSubmissionPolicyCommand>(document.ToJsonString(), Options));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("maxScore")]
    [InlineData("maxAttempts")]
    [InlineData("attachmentFileIds")]
    public void PublishedContentAndGradingFieldsAreRejectedByThePolicyContract(string field)
    {
        var document = Request(); document[field] = "ورودی خارج از قرارداد";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateAssignmentSubmissionPolicyCommand>(document.ToJsonString(), Options));
    }

    [Fact]
    public void ExplicitFalseIsDistinctFromAnOmittedLatePermission()
    {
        var command = JsonSerializer.Deserialize<UpdateAssignmentSubmissionPolicyCommand>(Request().ToJsonString(), Options)!;
        Assert.False(command.AllowLateSubmission);
        Assert.Equal(DateTimeKind.Utc, command.DueAtUtc.Kind);
    }

    private static JsonObject Request() => JsonSerializer.SerializeToNode(new UpdateAssignmentSubmissionPolicyCommand(
        new DateTime(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc), false,
        Convert.ToBase64String(new byte[8]), Convert.ToBase64String(new byte[8])), Options)!.AsObject();
}
