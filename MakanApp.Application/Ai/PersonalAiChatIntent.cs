using MakanApp.Application.Organization;

namespace MakanApp.Application.Ai;

// intent داخلی از DTO سرویس جدا می‌ماند؛ تبدیل wire فقط با قرارداد رسمی Personal انجام می‌شود.
public sealed record PersonalAiChatIntent(string RequestId, string? CorrelationId, Guid UserId, Guid? ConversationId,
    AiScope Scope, AiMessage Message, AiAuthorizedContext Context) : IAiInvocationRequest
{
    public static PersonalAiChatIntent Create(Guid user, Guid session, AccessContext context, string correlation, SendAiChatCommand command)
    {
        AiInvocationContext.EnsurePersonal(context, user, session);
        return new(Guid.NewGuid().ToString("N"), correlation, user, command.ConversationId,
            new(null, null, null, null), new(command.Message.Trim()), AiAuthorizedContext.Empty);
    }

    public void EnsureScope(Guid user)
    {
        if (UserId != user || Scope.OrganizationId is not null || Scope.SchoolId is not null || Scope.ClassId is not null || Scope.StudentId is not null ||
            Context.Subjects.Count != 0 || Context.Grades.Count != 0 || Context.Homeworks.Count != 0 || Context.Exams.Count != 0 || Context.Activity.Count != 0)
            throw new AiServiceException(AiServiceErrorCodes.ContextNotAllowed, "intent شخصی نباید داده یا شناسهٔ سازمانی داشته باشد.");
    }
}

public static class PersonalAiChatContract
{
    public const string ActorRole = "Personal";
    // قرارداد v1 مستقر در personal-5735598ec997 این Actor را با scope تهی پشتیبانی می‌کند.
    public static bool IsSupported => true;

    public static AiChatRequest ToServiceRequest(PersonalAiChatIntent intent)
    {
        intent.EnsureScope(intent.UserId);
        return new(intent.RequestId, intent.CorrelationId, intent.ConversationId?.ToString("N"),
            new(intent.UserId.ToString("D"), ActorRole), intent.Scope, intent.Message, intent.Context);
    }
}
