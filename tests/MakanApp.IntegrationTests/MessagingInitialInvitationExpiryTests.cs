using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData("groups")]
    [InlineData("channels")]
    public async Task InitialInvitationSevenDayExpirySurvivesCreationRetriesAndRejectsExpiredAcceptance(string resource)
    {
        var createdAt = DateTime.UtcNow;
        factory.Clock.SetUtcNow(createdAt);
        try
        {
            using var ownerClient = factory.CreateClient();
            using var targetClient = factory.CreateClient();
            var owner = await CreateMessagingUserAsync(ownerClient);
            var target = await CreateMessagingUserAsync(targetClient);
            await factory.AllowPersonalGroupInviteAsync(owner.UserId, target.UserId);
            var command = PersonalCommand("دعوت اولیهٔ هفت‌روزه", target.UserId);
            var route = $"/api/v1/conversations/{resource}";
            using var response = await ownerClient.PostAsJsonAsync(route, command, JsonOptions);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = (await response.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions))!;
            var receipt = Assert.Single(created.InitialTargets);
            Assert.Equal(ConversationInitialTargetOutcome.InvitationPending, receipt.Outcome);
            Assert.Equal(createdAt.AddDays(7), receipt.InvitationExpiresAtUtc);

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var original = await db.ConversationInvitations.AsNoTracking().SingleAsync(i => i.Id == receipt.InvitationId);
            Assert.Equal(createdAt, original.CreatedAtUtc);
            Assert.Equal(receipt.InvitationExpiresAtUtc, original.ExpiresAtUtc);

            factory.Clock.SetUtcNow(createdAt.AddHours(12));
            await AssertSameCreationReceiptAsync();

            // نشست تازه از مسیر واقعی API تست گرفته می‌شود؛ انقضای نشست با انقضای دعوت اشتباه نشود.
            factory.Clock.SetUtcNow(createdAt.AddDays(7));
            await SignInAgainAsync(ownerClient, owner);
            await SignInAgainAsync(targetClient, target);
            await AssertSameCreationReceiptAsync();
            using var expiredAccept = await targetClient.PostAsync($"/api/v1/messaging/invitations/{receipt.InvitationId}/accept", null);
            Assert.Equal(HttpStatusCode.NotFound, expiredAccept.StatusCode);
            Assert.Equal(MessagingErrorCodes.ConversationNotFound, await ReadProblemCodeAsync(expiredAccept));
            var pending = (await targetClient.GetFromJsonAsync<ConversationInvitationResult[]>("/api/v1/messaging/invitations", JsonOptions))!;
            Assert.DoesNotContain(pending, invitation => invitation.Id == receipt.InvitationId);
            Assert.False(await db.ConversationParticipants.AnyAsync(p => p.ConversationId == created.ConversationId && p.UserId == target.UserId));

            // شناسهٔ جدید در API ساخت، گفتگوی جدید می‌سازد؛ تمدید دعوت قبلی یا ارسال مجدد ضمنی نیست.
            using var newOperation = await ownerClient.PostAsJsonAsync(route, command with { ClientOperationId = Guid.NewGuid() }, JsonOptions);
            Assert.Equal(HttpStatusCode.Created, newOperation.StatusCode);
            var next = (await newOperation.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions))!;
            var nextReceipt = Assert.Single(next.InitialTargets);
            Assert.NotEqual(created.ConversationId, next.ConversationId);
            Assert.NotEqual(receipt.InvitationId, nextReceipt.InvitationId);
            Assert.Equal(createdAt.AddDays(14), nextReceipt.InvitationExpiresAtUtc);
            using var accepted = await targetClient.PostAsync($"/api/v1/messaging/invitations/{nextReceipt.InvitationId}/accept", null);
            accepted.EnsureSuccessStatusCode();

            factory.Clock.SetUtcNow(createdAt.AddDays(7).AddHours(1));
            await AssertSameCreationReceiptAsync();
            var unchanged = await db.ConversationInvitations.AsNoTracking().SingleAsync(i => i.ConversationId == created.ConversationId);
            Assert.Equal(original.Id, unchanged.Id);
            Assert.Equal(original.CreatedAtUtc, unchanged.CreatedAtUtc);
            Assert.Equal(original.ExpiresAtUtc, unchanged.ExpiresAtUtc);
            Assert.Equal(original.RowVersion, unchanged.RowVersion);
            Assert.Single(await db.ConversationInitialTargets.Where(t => t.ConversationId == created.ConversationId).ToArrayAsync());

            async Task AssertSameCreationReceiptAsync()
            {
                using var retry = await ownerClient.PostAsJsonAsync(route, command, JsonOptions);
                Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
                var replay = (await retry.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions))!;
                Assert.True(replay.AlreadyExisted);
                Assert.Equal(created.ConversationId, replay.ConversationId);
                Assert.Equal(receipt, Assert.Single(replay.InitialTargets));
            }

            async Task SignInAgainAsync(HttpClient client, MessagingUser user)
            {
                client.DefaultRequestHeaders.Authorization = null;
                using var challengeResponse = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(user.PhoneNumber), JsonOptions);
                challengeResponse.EnsureSuccessStatusCode();
                var challenge = (await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>(JsonOptions))!;
                using var verifyResponse = await client.PostAsJsonAsync("/api/v1/auth/otp/verify",
                    new VerifyOtpCommand(challenge.ChallengeId, user.PhoneNumber, factory.GetLatestOtpCode(user.PhoneNumber)), JsonOptions);
                verifyResponse.EnsureSuccessStatusCode();
                var verified = (await verifyResponse.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions))!;
                Assert.Equal(user.UserId, verified.User.Id);
                UseBearerToken(client, verified.AccessToken);
            }
        }
        finally { factory.Clock.UseSystemTime(); }
    }
}
