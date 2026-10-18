using MakanApp.Application.Organization;

namespace MakanApp.Application.Messaging;

public sealed class ScheduledMessageProcessor(IScheduledMessageStore schedules, IMessagingStore messages,
    IContentSafetyProvider safety, MessagingOptions options, TimeProvider clock)
{
    public const int BatchSize = 20;
    public async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        var count = 0;
        for (; count < BatchSize; count++)
        {
            // هر بار فقط یک lease؛ انتظار provider برای کار قبلی، lease بیست کار بعدی را منقضی نمی‌کند.
            var claim = await schedules.ClaimNextScheduledMessageAsync(ct); if (claim is null) break;
            // پایان‌دادن کار رهاشده هم یک سهم از batch است، نه علامت خالی‌بودن صف.
            if (claim.IsTerminal) continue;
            try
            {
                var work = await schedules.GetScheduledMessageWorkAsync(claim, ct); if (work is null) continue;
                var authority = work.Authority;
                // این یک selector سروریِ intent پایدار است، نه جعل نشست یا احراز هویت کاربر.
                var context = new AccessContext(work.UserId, work.UserId, Guid.Empty, authority.WorkspaceType, authority.OrganizationId,
                    authority.MembershipId, authority.Role, authority.SubjectOrganizationPersonId);
                var hash = MessagingService.HashSendPayload(work.ConversationId, work.Command);
                var command = work.Command with { ScheduledClaim = new ScheduledSendClaim(work.Id, claim.Token, hash) };
                var evaluated = await MessageContentSafetyGate.ApplyAsync(messages, safety, clock, work.UserId, work.ConversationId,
                    context, command, hash, ct);
                await messages.SendMessageAsync(work.UserId, work.ConversationId, context, command, evaluated.Evaluated,
                    options.MaximumTextLength, clock.GetUtcNow().UtcDateTime, ct);
            }
            catch (Exception) when (ct.IsCancellationRequested) { ct.ThrowIfCancellationRequested(); throw; }
            catch (MessagingException ex)
            {
                await schedules.FailScheduledMessageAsync(claim, ex.Code, ex.Code == MessagingErrorCodes.MessageSafetyUnavailable, ct);
            }
            catch (Exception)
            {
                await schedules.FailScheduledMessageAsync(claim, "SCHEDULED_DISPATCH_TEMPORARILY_UNAVAILABLE", true, ct);
            }
        }
        return count;
    }
}
