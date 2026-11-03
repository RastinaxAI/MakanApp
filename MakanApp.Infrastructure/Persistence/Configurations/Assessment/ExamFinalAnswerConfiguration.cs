using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class ExamFinalAnswerConfiguration : IEntityTypeConfiguration<ExamFinalAnswer>
{
    public void Configure(EntityTypeBuilder<ExamFinalAnswer> builder)
    {
        builder.ToTable("ExamFinalAnswers", "assessment");
        builder.HasKey(finalAnswer => new
        {
            finalAnswer.ContextKind, finalAnswer.ContextId,
            finalAnswer.ExamAttemptId,
            finalAnswer.ExamAttemptQuestionId
        });

        builder.HasOne<ExamAttempt>()
            .WithMany()
            .HasForeignKey(finalAnswer => new
            {
                finalAnswer.ContextKind, finalAnswer.ContextId,
                finalAnswer.ExamAttemptId
            })
            .HasPrincipalKey(attempt => new { attempt.ContextKind, attempt.ContextId, attempt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExamAttemptQuestion>()
            .WithMany()
            .HasForeignKey(finalAnswer => new
            {
                finalAnswer.ContextKind, finalAnswer.ContextId,
                finalAnswer.ExamAttemptId,
                finalAnswer.ExamAttemptQuestionId,
                finalAnswer.QuestionVersionId
            })
            .HasPrincipalKey(question => new
            {
                question.ContextKind, question.ContextId,
                question.ExamAttemptId,
                Id = question.Id,
                question.QuestionVersionId
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnswerRevision>()
            .WithMany()
            .HasForeignKey(finalAnswer => new
            {
                finalAnswer.ContextKind, finalAnswer.ContextId,
                finalAnswer.ExamAttemptId,
                finalAnswer.ExamAttemptQuestionId,
                finalAnswer.AnswerRevisionId
            })
            .HasPrincipalKey(revision => new
            {
                revision.ContextKind, revision.ContextId,
                revision.ExamAttemptId,
                revision.ExamAttemptQuestionId,
                Id = revision.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureScope();
    }
}
