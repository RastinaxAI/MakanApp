using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;

namespace MakanApp.Application.Messaging;

public sealed record VerifyCommunicationAgeCommand(DateOnly VerifiedBirthDate, string ExpectedVersion, string Reason);
public sealed record CommunicationAgeResult(Guid UserId, CommunicationAgeCategory Classification,
    CommunicationAgeVerificationStatus VerificationStatus, string Version);

public interface ICommunicationAgeStore
{
    Task<CommunicationAgeResult> GetSchoolAsync(Guid actorId, Guid sessionId, AccessContext access, Guid personId, CancellationToken ct);
    Task<CommunicationAgeResult> VerifyAsync(Guid actorId, Guid sessionId, AccessContext? access, Guid targetId,
        VerifyCommunicationAgeCommand command, string correlationId, CancellationToken ct);
}

public sealed class CommunicationAgeService(ICommunicationAgeStore store, SchoolManagementAccessResolver schoolAccess, TimeProvider clock)
{
    public async Task<CommunicationAgeResult> GetSchoolAsync(Guid actorId, Guid sessionId, Guid personId, CancellationToken ct) =>
        await store.GetSchoolAsync(actorId, sessionId, await schoolAccess.ResolveAsync(actorId, sessionId, ct), personId, ct);

    public async Task<CommunicationAgeResult> VerifySchoolAsync(Guid actorId, Guid sessionId, Guid personId,
        VerifyCommunicationAgeCommand command, string correlationId, CancellationToken ct)
    {
        Validate(command);
        var access = await schoolAccess.ResolveAsync(actorId, sessionId, ct);
        return await store.VerifyAsync(actorId, sessionId, access, personId, command, correlationId, ct);
    }

    public Task<CommunicationAgeResult> ResolveConflictAsync(Guid actorId, Guid sessionId, Guid userId,
        VerifyCommunicationAgeCommand command, string correlationId, CancellationToken ct)
    {
        Validate(command);
        return store.VerifyAsync(actorId, sessionId, null, userId, command, correlationId, ct);
    }

    private void Validate(VerifyCommunicationAgeCommand command)
    {
        try { _ = CommunicationAgeVerification.Classify(command.VerifiedBirthDate, clock.GetUtcNow().UtcDateTime); }
        catch (ArgumentException) { throw new MessagingException("AGE_VERIFICATION_INVALID", "تاریخ تأییدشده معتبر نیست."); }
        SchoolOnboardingValidation.Reason(command.Reason);
        if (string.IsNullOrWhiteSpace(command.ExpectedVersion))
            throw new MessagingException("AGE_VERIFICATION_INVALID", "نسخه جاری لازم است.");
    }
}
