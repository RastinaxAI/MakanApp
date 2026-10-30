using MakanApp.Domain.Assessment;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Assessment;

public sealed record ExamQuestionAttachmentPolicyCommand(bool AttachmentsAllowed, bool TextRequired,
    IReadOnlyCollection<string>? AllowedContentTypes, int? MaximumCount, long? MaximumTotalBytes);

public sealed record ExamQuestionAttachmentPolicyDto(bool AttachmentsAllowed, bool TextRequired,
    IReadOnlyCollection<string> AllowedContentTypes, int? MaximumCount, long? MaximumTotalBytes)
{
    public static ExamQuestionAttachmentPolicyDto From(QuestionVersion question) => new(
        question.AttachmentsAllowed, question.AnswerTextRequired, question.AttachmentPolicy?.AllowedContentTypes ?? [],
        question.MaximumAnswerAttachmentCount, question.MaximumAnswerAttachmentBytes);
}

public sealed record AttachExamAnswerFileCommand(Guid FileAssetId, Guid ClientOperationId, long WriteLeaseVersion, int? ExpectedRevisionNumber);
public sealed record ChangeExamAnswerAttachmentCommand(Guid ClientOperationId, long WriteLeaseVersion, int? ExpectedRevisionNumber);

public sealed record AcademicAnswerAttachmentDto(Guid FileAssetId, string OriginalFileName, string ContentType,
    long SizeBytes, FileAssetStatus FileStatus, bool CanDownload, string? DownloadPath)
{
    public static AcademicAnswerAttachmentDto From(FileAsset file) => new(file.Id, file.OriginalFileName,
        file.ContentType, file.SizeBytes, file.Status, file.Status == FileAssetStatus.Ready,
        file.Status == FileAssetStatus.Ready ? $"/api/v1/files/{file.Id}/content" : null);
}

public sealed record ExamAnswerAttachmentRecord(AnswerRevisionAttachment Attachment, FileAsset FileAsset);
