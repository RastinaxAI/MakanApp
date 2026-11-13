using MakanApp.Application.Organization;

namespace MakanApp.Application.Ai;

// این مدل intent داخلی است، نه DTO سرویس AI؛ تا تصویب قرارداد Personal هیچ role روی wire ساخته نمی‌شود.
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
    // OpenAPI زندهٔ ۲۰۲۶-۱۱-۱۳ فقط Student/Parent/Teacher/Manager را تعریف می‌کند؛ این مقدار فلگ قابل bypass نیست.
    public static bool IsSupported => false;
    public static void EnsureSupported() => throw new AiServiceException(AiServiceErrorCodes.PersonalContextContractRequired,
        "قرارداد سرویس AI هنوز actor عمومیِ حساب شخصی را پشتیبانی نمی‌کند؛ هیچ نقش جایگزین یا مصرفی ایجاد نشد.");
}
