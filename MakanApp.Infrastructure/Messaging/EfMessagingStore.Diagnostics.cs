using System.Diagnostics;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    private async Task LogDirectPolicyDenialAsync(CommunicationEligibilityFacts facts, CancellationToken ct)
    {
        string category;
        if (!facts.TargetAccountAvailable)
        {
            var target = await dbContext.Users.AsNoTracking().Where(u => u.Id == facts.TargetUserId)
                .Select(u => new { u.AccountStatus, u.PersonId, u.Username, u.ProfileCompletedAtUtc }).SingleOrDefaultAsync(ct);
            category = target is null ? "NotFound" : target.AccountStatus != UserAccountStatus.Active ? "Suspended" : "ProfileIncomplete";
        }
        else if (facts.Scope == ConversationScope.Personal && facts.AccessContext.WorkspaceType != WorkspaceType.Personal)
            category = "ContextMismatch";
        else if ((facts.ActorAgeCategory != CommunicationAgeCategory.Adult || facts.TargetAgeCategory != CommunicationAgeCategory.Adult) && !facts.HasAuthorizedSafetyRelationship)
            category = "AgeRestricted";
        else if (facts.Scope == ConversationScope.Personal && !facts.TargetAllowsPersonalDirect)
            category = "PrivacyDenied";
        else category = "RelationshipRequired";
        LogDirectDenial(category);
    }

    private void LogDirectDenial(string category) => logger.LogInformation(
        "شروع Direct طبق سیاست جاری رد شد. Category={DenialCategory}; Code={ErrorCode}; TraceId={TraceId}",
        category, MessagingErrorCodes.DirectRecipientNotAvailable, Activity.Current?.TraceId.ToString());
}
