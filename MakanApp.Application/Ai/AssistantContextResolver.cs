using MakanApp.Application.Academic;
using MakanApp.Application.Guardian;
using MakanApp.Application.Organization;
using MakanApp.Domain.Intelligence;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Ai;

public static class AssistantContextErrorCodes
{
    public const string Conflict = "ASSISTANT_CONTEXT_CONFLICT";
}
public sealed record SelectAssistantContextCommand(AssistantDataContextKind Kind, Guid ClientOperationId,
    Guid? MembershipId = null, OrganizationRole? Role = null, Guid? SubjectOrganizationPersonId = null, Guid? ClassId = null, string? ExpectedVersion = null);
public sealed record AssistantContextResult(AssistantDataContextKind Kind, Guid? OrganizationId, Guid? MembershipId, OrganizationRole? Role,
    Guid? SubjectOrganizationPersonId, Guid? ClassId, DateTime SelectedAtUtc, string Version, bool UpstreamContextSupported, string? Limitation);
public interface IAssistantContextStore
{
    Task<AssistantContextSelection?> GetAsync(Guid user, Guid session, CancellationToken ct);
    Task<AssistantContextSelection> SaveAsync(Guid user, Guid session, SelectAssistantContextCommand command, AccessContext context,
        DateTime now, bool onlyIfAbsent, CancellationToken ct);
}

// زمینهٔ Assistant انتخاب مستقلی است؛ هیچ فیلد Workspace در UserSession نوشته نمی‌شود.
public sealed class AssistantContextResolver(IAccessContextResolver schoolContext, IAccountOperationContextResolver accounts,
    IAssistantContextStore store, IOrganizationStore organizations, IGuardianStore guardians, ISchoolReadStore school, TimeProvider clock) : IAccessContextResolver
{
    public async Task<AccessContext> ResolveAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        await accounts.PersonalAsync(userId, sessionId, ct);
        var selection = await store.GetAsync(userId, sessionId, ct);
        if (selection is null)
        {
            // برای سازگاری کلاینت قدیمی، زمینهٔ اولین اجرا یک‌بار ثبت می‌شود؛ Role switch آن را تغییر نمی‌دهد.
            var initial = await schoolContext.ResolveAsync(userId, sessionId, ct);
            var command = new SelectAssistantContextCommand(initial.OrganizationId.HasValue ? AssistantDataContextKind.Organization : AssistantDataContextKind.Personal,
                Guid.NewGuid(), initial.MembershipId, initial.ActiveRole, initial.SubjectOrganizationPersonId);
            selection = await store.SaveAsync(userId, sessionId, command, initial, clock.GetUtcNow().UtcDateTime, true, ct);
        }
        return await AuthorizeAsync(userId, sessionId, ToCommand(selection), ct);
    }
    public async Task<AssistantContextResult?> ReadAsync(Guid user, Guid session, CancellationToken ct)
    {
        await accounts.PersonalAsync(user, session, ct);
        var saved = await store.GetAsync(user, session, ct);
        if (saved is null) return null;
        await AuthorizeAsync(user, session, ToCommand(saved), ct); return Map(saved);
    }
    public async Task<AssistantContextResult> SelectAsync(Guid user, Guid session, SelectAssistantContextCommand command, CancellationToken ct)
    {
        var context = await AuthorizeAsync(user, session, command, ct);
        var saved = await store.SaveAsync(user, session, command, context, clock.GetUtcNow().UtcDateTime, false, ct);
        await AuthorizeAsync(user, session, ToCommand(saved), ct); return Map(saved);
    }
    private async Task<AccessContext> AuthorizeAsync(Guid user, Guid session, SelectAssistantContextCommand command, CancellationToken ct)
    {
        var account = await accounts.PersonalAsync(user, session, ct);
        if (!Enum.IsDefined(command.Kind) || command.ClientOperationId == Guid.Empty || command.MembershipId == Guid.Empty || command.ClassId == Guid.Empty ||
            command.SubjectOrganizationPersonId == Guid.Empty || command.Role.HasValue && !Enum.IsDefined(command.Role.Value) ||
            (command.Kind == AssistantDataContextKind.OrganizationClass) != command.ClassId.HasValue) throw AiContextAuthorization.InvalidRequest();
        if (command.Kind == AssistantDataContextKind.Personal)
        {
            if (command.MembershipId.HasValue || command.Role.HasValue || command.SubjectOrganizationPersonId.HasValue) throw AiContextAuthorization.InvalidRequest();
            return account;
        }
        if (command.MembershipId is not Guid membership || command.Role is not OrganizationRole role) throw AiContextAuthorization.NotAllowed();
        var workspace = await organizations.GetActiveWorkspaceRecordAsync(user, membership, role, ct);
        if (workspace is null || !workspace.IsEffective) throw AiContextAuthorization.NotAllowed();
        var context = new AccessContext(user, user, session, WorkspaceType.Organization, workspace.OrganizationId, membership, role, null)
            { AssistantClassId = command.ClassId };
        if (command.SubjectOrganizationPersonId is Guid child)
        {
            if (role != OrganizationRole.Parent || await guardians.GetAuthorizedChildAsync(user, workspace.OrganizationId, child, clock.GetUtcNow().UtcDateTime, ct) is null)
                throw AiContextAuthorization.NotAllowed();
            context = context with { SubjectOrganizationPersonId = child };
        }
        if (command.ClassId is Guid classId && !await school.CanManageClassAsync(context, classId, ct) && !await school.CanStudyClassAsync(context, classId, ct))
            throw AiContextAuthorization.NotAllowed();
        return context;
    }
    private static SelectAssistantContextCommand ToCommand(AssistantContextSelection s) => new(s.Kind, s.LastClientOperationId, s.MembershipId, s.Role, s.SubjectOrganizationPersonId, s.ClassId);
    private static AssistantContextResult Map(AssistantContextSelection s) => new(s.Kind, s.OrganizationId, s.MembershipId, s.Role, s.SubjectOrganizationPersonId,
        s.ClassId, s.SelectedAtUtc, Convert.ToBase64String(s.RowVersion), s.Kind != AssistantDataContextKind.Personal || PersonalAiChatContract.IsSupported,
        s.Kind == AssistantDataContextKind.Personal ? AiServiceErrorCodes.PersonalContextContractRequired : null);
}
