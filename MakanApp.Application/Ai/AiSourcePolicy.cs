using MakanApp.Domain.Knowledge;

namespace MakanApp.Application.Ai;

public static class AiSourcePolicy
{
    // این قواعد مکمل ACL جاری Store هستند؛ نقش یا پرداخت‌کننده به‌تنهایی مجوز داده نیست.
    public static void EnsureContext(EducationalSourceScope scope, Guid? owner, Guid? organizationId,
        Guid? classId, AiSourceContext context)
    {
        if (scope == EducationalSourceScope.Library) throw AiSourceSelection.Error(AiSourceErrorCodes.NotAllowed);
        if (scope == EducationalSourceScope.Personal && owner != context.Actor.UserId ||
            scope is EducationalSourceScope.Class or EducationalSourceScope.Organization &&
            (organizationId is null || organizationId != context.Actor.Organization?.OrganizationId))
            throw AiSourceSelection.Error(AiSourceErrorCodes.NotFound);
        if (scope == EducationalSourceScope.Class && (classId is null || classId != context.ClassId))
            throw AiSourceSelection.Error(AiSourceErrorCodes.ContextMismatch);
    }
    public static void EnsureReady(EducationalSourceStatus status, SourcePublicationStatus publication,
        bool sourceReady, SourceProcessingStatus processing)
    {
        if (status != EducationalSourceStatus.Active || publication != SourcePublicationStatus.Published || !sourceReady ||
            processing is SourceProcessingStatus.Pending or SourceProcessingStatus.Processing or SourceProcessingStatus.Failed)
            throw AiSourceSelection.Error(AiSourceErrorCodes.NotReady);
    }
}
