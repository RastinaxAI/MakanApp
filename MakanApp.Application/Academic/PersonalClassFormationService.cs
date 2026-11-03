using MakanApp.Application.Commerce;
using MakanApp.Application.Platform;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Platform;

namespace MakanApp.Application.Academic;

public sealed class PersonalClassFormationService(CommercialActorAccess actors, IPlatformPermissionResolver permissions,
    IPersonalClassFormationStore store, TimeProvider clock)
{
    public async Task<PersonalClassRequestDto> CreateAsync(PersonalClassActor actor, CreatePersonalClassRequestCommand command, CancellationToken ct)
    {
        await Authorize(actor, false, false, ct);
        Validate(command.Details);
        if (command.ClientOperationId == Guid.Empty) throw Invalid();
        return await store.CreateAsync(actor, command, Now, ct);
    }
    public async Task<PersonalClassPage<PersonalClassRequestDto>> ListAsync(PersonalClassActor actor, bool platform,
        PersonalClassRequestStatus? status, int page, int pageSize, CancellationToken ct)
    {
        await Authorize(actor, platform, false, ct); ValidatePage(page, pageSize);
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw Invalid();
        return await store.ListAsync(actor.UserId, platform, status, page, pageSize, ct);
    }
    public async Task<PersonalClassRequestDto> GetAsync(PersonalClassActor actor, Guid id, bool platform, CancellationToken ct)
    { await Authorize(actor, platform, false, ct); return await store.GetAsync(actor.UserId, id, platform, ct); }
    public async Task<PersonalClassRequestDto> EditAsync(PersonalClassActor actor, Guid id, EditPersonalClassRequestCommand command, CancellationToken ct)
    { await Authorize(actor, false, false, ct); Validate(command.Details); ValidateVersion(command.ExpectedVersion); return await store.EditAsync(actor, id, command, Now, ct); }
    public async Task<PersonalClassRequestDto> TransitionAsync(PersonalClassActor actor, Guid id, PersonalClassVersionCommand command, bool submit, CancellationToken ct)
    { await Authorize(actor, false, false, ct); ValidateVersion(command.ExpectedVersion); return await store.TransitionAsync(actor, id, command, submit, Now, ct); }
    public async Task<PersonalClassRequestDto> ReviewAsync(PersonalClassActor actor, Guid id, ReviewPersonalClassRequestCommand command, CancellationToken ct)
    {
        await Authorize(actor, true, true, ct); ValidateVersion(command.ExpectedVersion); ValidateReason(command.Reason);
        if (command.Decision is not (PersonalClassRequestStatus.UnderReview or PersonalClassRequestStatus.NeedsChanges or PersonalClassRequestStatus.Rejected or PersonalClassRequestStatus.Approved) ||
            command.FinalManagerUserId == Guid.Empty || command.Decision != PersonalClassRequestStatus.Approved && command.FinalManagerUserId.HasValue) throw Invalid();
        return await store.ReviewAsync(actor, id, command, Now, ct);
    }
    public async Task<PersonalClassPage<PersonalClassDecisionDto>> HistoryAsync(PersonalClassActor actor, Guid id, bool platform, int page, int pageSize, CancellationToken ct)
    { await Authorize(actor, platform, false, ct); ValidatePage(page, pageSize); return await store.HistoryAsync(actor.UserId, id, platform, page, pageSize, ct); }
    private async Task Authorize(PersonalClassActor actor, bool platform, bool review, CancellationToken ct)
    {
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        if (platform && !await permissions.HasPermissionAsync(actor.UserId, review ? PlatformPermissionKeys.PersonalClassesReview : PlatformPermissionKeys.PersonalClassesRead, ct))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "مجوز جاری بررسی کلاس مستقل لازم است.");
    }
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static void Validate(PersonalClassRequestInput input)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(input.Purpose) || input.Purpose.Trim().Length > 2000 || input.Description?.Trim().Length > 4000 ||
            input.SuggestedCapacity <= 0 || input.SuggestedManagerUserId == Guid.Empty || input.SuggestedTeacherUserId == Guid.Empty) throw Invalid();
    }
    internal static void ValidatePage(int page, int size) { if (page < 1 || page > 1000000 || size is < 1 or > 100) throw Invalid(); }
    internal static void ValidateReason(string reason) { if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) throw Invalid(); }
    internal static void ValidateVersion(string version)
    {
        try { if (string.IsNullOrWhiteSpace(version) || Convert.FromBase64String(version).Length != 8) throw Invalid(); }
        catch (FormatException) { throw Invalid(); }
    }
    internal static AcademicException Invalid() => new(AcademicErrorCodes.PersonalClassRequestInvalid, "اطلاعات درخواست کلاس مستقل معتبر نیست.");
}
