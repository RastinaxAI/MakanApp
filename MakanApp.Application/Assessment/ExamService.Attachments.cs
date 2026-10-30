using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public sealed partial class ExamService
{
    private ExamAnswerAttachmentPolicy? ToAttachmentPolicy(ExamQuestionAttachmentPolicyCommand? command)
    {
        if (command is null) return null;
        if (!command.AttachmentsAllowed)
        {
            if (command.TextRequired || command.MaximumCount.HasValue || command.MaximumTotalBytes.HasValue || command.AllowedContentTypes?.Count > 0)
                throw new ArgumentException("سیاست غیرفعال نباید محدودیت فعال داشته باشد.");
            return null;
        }
        if (!command.MaximumCount.HasValue || !command.MaximumTotalBytes.HasValue || command.AllowedContentTypes is null ||
            command.AllowedContentTypes.Any(type => !_storageOptions.AllowedContentTypes.Contains(type)))
            throw new ArgumentException("سقف و نوع‌های مجاز سؤال باید صریح و سازگار با Storage باشند.");
        return new(command.TextRequired, command.MaximumCount.Value, command.MaximumTotalBytes.Value, command.AllowedContentTypes);
    }
}
