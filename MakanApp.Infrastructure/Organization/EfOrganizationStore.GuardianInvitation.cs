using MakanApp.Application.Academic;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Organization;

public sealed partial class EfOrganizationStore
{
    public async Task CompleteGuardianInvitationAsync(Invitation invitation, Guid userId, Guid sessionId, DateTime nowUtc, bool replayed, CancellationToken ct)
    {
        var user = await dbContext.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={userId}").SingleOrDefaultAsync(ct);
        if (user is null) throw new IdentityException(IdentityErrorCodes.AuthRequired, "نشست معتبر لازم است.");
        await dbContext.Entry(user).ReloadAsync(ct);
        var session = await dbContext.UserSessions.FromSqlInterpolated($"SELECT * FROM [identity].[UserSessions] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={sessionId} AND [UserId]={userId}").SingleOrDefaultAsync(ct);
        if (session is not null) await dbContext.Entry(session).ReloadAsync(ct);
        if (user.AccountStatus != UserAccountStatus.Active || session is null || !session.IsActive(nowUtc))
            throw new IdentityException(IdentityErrorCodes.AuthRequired, "نشست معتبر لازم است.");
        if (!user.IsProfileComplete) throw new OrganizationException(SchoolOnboardingErrorCodes.AccountProfileRequired, "ابتدا پروفایل حساب را تکمیل کنید.");
        var learnerId = invitation.GuardianLearnerOrganizationPersonId!.Value;
        if (replayed)
        {
            // بازپخش رسید، رابطه یا نقش پایان‌یافته را دوباره فعال نمی‌کند.
            if (!await dbContext.GuardianRelations.AnyAsync(g => g.Id == invitation.AcceptedGuardianRelationId &&
                g.OrganizationId == invitation.OrganizationId && g.GuardianUserId == userId && g.LearnerOrganizationPersonId == learnerId, ct))
                throw new OrganizationException(SchoolErrorCodes.GuardianInvitationConflict, "رسید رابطهٔ پذیرفته‌شده معتبر نیست.");
            return;
        }
        // دعوت جدید راه دورزدن تعلیق جاری عضویت نیست؛ بازفعال‌سازی، workflow مستقل مدیریت است.
        if (await dbContext.Memberships.AnyAsync(m => m.OrganizationId == invitation.OrganizationId && m.UserId == userId &&
            m.Status == MembershipStatus.Suspended && m.EndedAtUtc == null, ct))
            throw new OrganizationException(OrganizationErrorCodes.MembershipNotActive, "ابتدا وضعیت عضویت تعلیق‌شده باید از مسیر مجاز مدیریت تعیین شود.");
        var learner = await dbContext.OrganizationPersons.FromSqlInterpolated($"SELECT * FROM [organization].[OrganizationPersons] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId]={invitation.OrganizationId} AND [Id]={learnerId}").SingleOrDefaultAsync(ct);
        if (learner is null || !learner.IsActive) throw new OrganizationException(SchoolErrorCodes.GuardianTargetUnavailable, "فراگیر دعوت در دسترس نیست.");
        var relation = await dbContext.GuardianRelations.Where(g => g.OrganizationId == invitation.OrganizationId && g.GuardianUserId == userId &&
            g.LearnerOrganizationPersonId == learnerId && (g.Status == GuardianRelationStatus.Pending || g.Status == GuardianRelationStatus.Active) && g.EndedAtUtc == null)
            .OrderByDescending(g => g.Status == GuardianRelationStatus.Active).ThenBy(g => g.Id).FirstOrDefaultAsync(ct);
        if (relation is null)
        {
            relation = GuardianRelation.CreatePending(invitation.OrganizationId, userId, learnerId, nowUtc);
            dbContext.GuardianRelations.Add(relation);
        }
        if (relation.Status == GuardianRelationStatus.Pending) relation.Activate(nowUtc);
        invitation.ConfirmGuardianRelation(relation.Id);
        dbContext.AdminAuditLogs.Add(AdminAuditLog.Create(userId, user.PersonId, sessionId, "organization.guardian.invitation.accept",
            "Invitation", invitation.Id.ToString("D"), invitation.OrganizationId, "تأیید صریح والد در نشست احرازشده", nowUtc,
            $"guardian-{Guid.NewGuid():N}", AdminAuditResult.Succeeded, null, $"learner={learnerId:D};relation={relation.Id:D}"));
    }
}
