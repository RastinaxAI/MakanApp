using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Knowledge;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Ai;

public sealed class AiSourceSelectionService(IAccountOperationContextResolver accounts, IAccessContextResolver contexts,
    IStudentContextBuilder students, ITeacherClassContextBuilder teachers, IAiSourceAuthorizationService authorization,
    IAiSourceSelectionStore selections, IAiScopeStore scopes) : IAiSourceSelectionService
{
    public async Task GuardAsync(Guid userId, Guid sessionId, AiSourceOperation operation,
        IReadOnlyCollection<Guid>? selectedSourceIds, Guid? operationId, object payload,
        Guid? classId, Guid? assignmentId, CancellationToken ct)
    {
        var ids = AiSourceSelection.Canonicalize(selectedSourceIds);
        if (ids.Length == 0)
        {
            if (operationId.HasValue)
            {
                if (operationId == Guid.Empty) throw AiSourceSelection.Error(AiSourceErrorCodes.Invalid);
                await accounts.PersonalAsync(userId, sessionId, ct);
                await selections.EnsureUnselectedAsync(userId, operationId.Value, ct);
            }
            return;
        }
        if (operationId is null || operationId == Guid.Empty) throw AiSourceSelection.Error(AiSourceErrorCodes.Invalid);
        var command = new AiSourcePreflightCommand(operation, ids, classId, assignmentId);
        var context = await ResolveAsync(userId, sessionId, command, ct);
        var hash = AiSourceSelection.Hash(context, payload, ids);
        var previous = await selections.FindPinnedAsync(userId, operationId.Value, hash, ct);
        var manifests = await authorization.AuthorizeAsync(context, ids, previous, ct);
        var saved = await selections.PinAsync(userId, operationId.Value, hash,
            manifests.Select(m => m.SourceVersionId).ToArray(), ct);

        // سابقهٔ انتخاب، مجوز نیست؛ درست پیش از مرز اجرا دوباره نشست، Context و ACL خوانده می‌شوند.
        var current = await ResolveAsync(userId, sessionId, command, ct);
        if (!hash.SequenceEqual(AiSourceSelection.Hash(current, payload, ids)))
            throw AiSourceSelection.Error(AiSourceErrorCodes.ContextMismatch);
        var pinned = await authorization.AuthorizeAsync(current, ids, saved.Pins, ct);
        var plan = new AuthorizedAiRetrievalPlan(pinned, saved.Replayed);
        if (!plan.RetrievalAvailable) throw AiSourceSelection.Error(AiSourceErrorCodes.RetrievalUnavailable);
    }

    public async Task<AiSourcePreflightResult> PreflightAsync(Guid userId, Guid sessionId,
        AiSourcePreflightCommand command, CancellationToken ct)
    {
        var ids = AiSourceSelection.Canonicalize(command.SelectedSourceIds);
        var context = await ResolveAsync(userId, sessionId, command, ct);
        var sources = await authorization.AuthorizeAsync(context, ids, null, ct);
        // این بررسی فقط خواندنی است؛ ناتوانی Retrieval باید پیش از Commerce مشخص شود.
        return new(true, false, ids.Length, ids.Length == 0 ? null : AiSourceErrorCodes.RetrievalUnavailable,
            ids.Length == 0 ? "UseExistingCommercePreflight" : "NotEvaluatedRetrievalUnavailable",
            sources.Select(s => s.SourceVersionId).ToArray());
    }

    private async Task<AiSourceContext> ResolveAsync(Guid userId, Guid sessionId, AiSourcePreflightCommand command, CancellationToken ct)
    {
        if (!Enum.IsDefined(command.Operation) || command.ClassId == Guid.Empty || command.AssignmentId == Guid.Empty)
            throw AiSourceSelection.Error(AiSourceErrorCodes.Invalid);
        var account = await accounts.PersonalAsync(userId, sessionId, ct);
        var selected = await contexts.ResolveAsync(userId, sessionId, ct);
        AccessContext? organization = selected.WorkspaceType == WorkspaceType.Organization ? selected : null;
        Guid? classId = null;
        switch (command.Operation)
        {
            case AiSourceOperation.Chat:
                if (command.ClassId.HasValue || command.AssignmentId.HasValue) throw AiSourceSelection.Error(AiSourceErrorCodes.Invalid);
                if (organization is not null && organization.ActiveRole is not (OrganizationRole.Student or OrganizationRole.Parent or OrganizationRole.Teacher or OrganizationRole.Manager))
                    throw AiSourceSelection.Error(AiServiceErrorCodes.ContextNotAllowed);
                if (organization?.ActiveRole == OrganizationRole.Student &&
                    await scopes.GetActiveLearnerOrganizationPersonIdAsync(organization.OrganizationId!.Value, userId, ct) is null)
                    throw AiSourceSelection.Error(AiServiceErrorCodes.ContextNotAllowed);
                break;
            case AiSourceOperation.StudyPlan:
                if (command.ClassId.HasValue || command.AssignmentId.HasValue) throw AiSourceSelection.Error(AiSourceErrorCodes.Invalid);
                await students.BuildAsync(userId, sessionId, ct);
                break;
            case AiSourceOperation.HomeworkExplain:
                if (command.AssignmentId is not Guid homework || command.ClassId.HasValue) throw AiSourceSelection.Error(AiSourceErrorCodes.Invalid);
                var built = await students.BuildHomeworkAsync(userId, sessionId, homework, ct);
                classId = built.Homework.ClassId;
                break;
            case AiSourceOperation.TeacherActivitySuggestions:
                if (command.ClassId is not Guid requested || command.AssignmentId.HasValue) throw AiSourceSelection.Error(AiSourceErrorCodes.Invalid);
                await teachers.BuildAsync(userId, sessionId, requested, ct);
                classId = requested;
                break;
        }
        return new(new SourceActor(userId, sessionId, account, organization), command.Operation, classId, command.AssignmentId);
    }
}
