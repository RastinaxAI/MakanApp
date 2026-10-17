using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

// قرارداد و محدودیت محتوا برای ارسال فوری و intent زمان‌بندی‌شده یکسان است.
internal static class MessageCommandValidation
{
    public static SendMessageStoreCommand Validate(SendMessageCommand command, MessagingOptions options)
    {
        if (command.TopicId == Guid.Empty) throw new MessagingException(MessagingErrorCodes.TopicInvalid, "شناسه موضوع معتبر نیست.");
        if (command.ClientMessageId == Guid.Empty)
        {
            throw new MessagingException(MessagingErrorCodes.MessageClientIdRequired, "شناسه پایدار پیام کلاینت الزامی است.");
        }

        if (!Enum.IsDefined(command.Kind) || command.Kind is MessageKind.Poll or MessageKind.Checklist)
        {
            throw new MessagingException(MessagingErrorCodes.MessageKindInvalid, "نوع پیام معتبر نیست.");
        }

        var attachmentIds = (command.AttachmentIds ?? []).Distinct().ToArray();
        if (attachmentIds.Any(id => id == Guid.Empty) ||
            attachmentIds.Length > options.MaximumAttachmentsPerMessage)
        {
            throw new MessagingException(MessagingErrorCodes.MessageAttachmentNotAllowed, "فهرست پیوست‌های پیام معتبر نیست.");
        }

        var mentionedUserIds = (command.MentionedUserIds ?? []).Distinct().ToArray();
        if (mentionedUserIds.Any(id => id == Guid.Empty) ||
            mentionedUserIds.Length > options.MaximumMentionsPerMessage)
        {
            throw new MessagingException(MessagingErrorCodes.MentionNotAllowed, "فهرست منشن‌های پیام معتبر نیست.");
        }

        if (command.Text?.Length > options.MaximumTextLength)
        {
            throw new MessagingException(MessagingErrorCodes.MessageTooLong, "متن پیام از طول مجاز بیشتر است.");
        }

        if (command.Kind == MessageKind.Text)
        {
            if (string.IsNullOrWhiteSpace(command.Text))
            {
                throw new MessagingException(MessagingErrorCodes.MessageEmpty, "متن پیام الزامی است.");
            }

            if (attachmentIds.Length > 0)
            {
                throw new MessagingException(MessagingErrorCodes.MessageKindInvalid, "پیام متنی نمی‌تواند پیوست رسانه‌ای داشته باشد.");
            }
        }
        else if (command.Kind == MessageKind.ContactCard)
        {
            if (!command.ContactUserId.HasValue || command.ContactUserId == Guid.Empty ||
                command.Text is not null || attachmentIds.Length != 0)
            {
                throw new MessagingException(MessagingErrorCodes.MessageKindInvalid, "کارت مخاطب باید فقط شناسه کاربر معتبر داشته باشد.");
            }
        }
        else if (attachmentIds.Length == 0)
        {
            throw new MessagingException(MessagingErrorCodes.MessageAttachmentRequired, "این نوع پیام حداقل یک پیوست می‌خواهد.");
        }
        else if (command.Text is not null && string.IsNullOrWhiteSpace(command.Text))
        {
            throw new MessagingException(MessagingErrorCodes.MessageKindInvalid, "عنوان رسانه باید خالی یا دارای متن معتبر باشد.");
        }

        return new SendMessageStoreCommand(
            command.ClientMessageId,
            command.Kind,
            command.Text,
            attachmentIds,
            command.ReplyToMessageId,
            mentionedUserIds,
            ContactUserId: command.ContactUserId,
            TopicId: command.TopicId,
            IsSilent: command.IsSilent);
    }
}
