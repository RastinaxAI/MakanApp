using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendAcademicEngineToPersonalClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicActivityPublications_AssignmentVersions_OrganizationId_AssignmentVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropForeignKey(
                name: "FK_AcademicActivityPublications_ExamVersions_OrganizationId_ExamVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropForeignKey(
                name: "FK_AnswerRevisionAttachments_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisionAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AnswerRevisions_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_SupersedesAnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_AnswerRevisions_ExamAttemptQuestions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_AnswerRevisions_ExamAttempts_OrganizationId_ExamAttemptId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_AnswerRevisions_QuestionOptions_OrganizationId_QuestionVersionId_SelectedOptionId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentRecipients_AssignmentVersions_OrganizationId_ClassId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentVersions_Assignments_OrganizationId_ClassId_AssignmentId",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationRevisions_EvaluationRevisions_OrganizationId_SubmissionAttemptId_SupersedesEvaluationRevisionId",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationRevisions_SubmissionAttempts_OrganizationId_SubmissionAttemptId",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttemptQuestions_AnswerRevisions_OrganizationId_ExamAttemptId_Id_CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttemptQuestions_ExamAttempts_OrganizationId_ExamVersionId_ExamAttemptId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttemptQuestions_QuestionVersions_OrganizationId_ExamVersionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttempts_ExamVersions_OrganizationId_ExamId_ExamVersionId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttempts_Exams_OrganizationId_ClassId_ExamId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamFinalAnswers_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamFinalAnswers_ExamAttemptQuestions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamFinalAnswers_ExamAttempts_OrganizationId_ExamAttemptId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamGradeReleases_ExamGradeRevisions_OrganizationId_ExamAttemptId_ExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamGradeRevisions_ExamAttempts_OrganizationId_ExamAttemptId",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamGradeRevisions_ExamGradeRevisions_OrganizationId_ExamAttemptId_SupersedesExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestionGrades_ExamAttemptQuestions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestionGrades_ExamGradeRevisions_OrganizationId_ExamAttemptId_ExamGradeRevisionId",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamVersions_Exams_OrganizationId_ExamId",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_GradeReleases_EvaluationRevisions_OrganizationId_SubmissionAttemptId_EvaluationRevisionId",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionOptions_QuestionVersions_OrganizationId_QuestionVersionId",
                schema: "assessment",
                table: "QuestionOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionVersions_ExamVersions_OrganizationId_ExamVersionId",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAttachments_SubmissionAttempts_OrganizationId_SubmissionAttemptId",
                schema: "assessment",
                table: "SubmissionAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAttempts_AssignmentRecipients_OrganizationId_AssignmentId_AssignmentVersionId_EnrollmentId_AssignmentRecipientId",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAttempts_AssignmentVersions_OrganizationId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_SubmissionAttempts_Organization_Id",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionAttempts_Assignment_Status_SubmittedAtUtc",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionAttempts_OrganizationId_AssignmentId_AssignmentVersionId_EnrollmentId_AssignmentRecipientId",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropIndex(
                name: "UX_SubmissionAttempts_OneDraft",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropIndex(
                name: "UX_SubmissionAttempts_Recipient_Version_Number",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropIndex(
                name: "UX_SubmissionAttachments_Attempt_FileAsset",
                schema: "assessment",
                table: "SubmissionAttachments");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_QuestionVersions_Organization_ExamVersion_Id",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_QuestionVersions_OrganizationId_Id",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropIndex(
                name: "UX_QuestionVersions_Organization_ExamVersion_Order",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_QuestionOptions_Organization_QuestionVersion_Id",
                schema: "assessment",
                table: "QuestionOptions");

            migrationBuilder.DropIndex(
                name: "UX_QuestionOptions_Organization_Question_Order",
                schema: "assessment",
                table: "QuestionOptions");

            migrationBuilder.DropIndex(
                name: "IX_GradeReleases_Attempt_ReleasedAtUtc",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropIndex(
                name: "IX_GradeReleases_OrganizationId_SubmissionAttemptId_EvaluationRevisionId",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropIndex(
                name: "UX_GradeReleases_Organization_EvaluationRevision",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamVersions_Organization_Exam_Id",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamVersions_OrganizationId_Id",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropIndex(
                name: "UX_ExamVersions_Organization_Exam_ActiveDraft",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropIndex(
                name: "UX_ExamVersions_Organization_Exam_VersionNumber",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Exams_Organization_Class_Id",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Exams_OrganizationId_Id",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_Organization_Class_Status",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_ExamQuestionGrades_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropIndex(
                name: "UX_ExamQuestionGrades_Revision_AttemptQuestion",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamQuestionGrades_ReviewState",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamGradeRevisions_Organization_Attempt_Id",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropIndex(
                name: "IX_ExamGradeRevisions_OrganizationId_ExamAttemptId_SupersedesExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropIndex(
                name: "UX_ExamGradeRevisions_Attempt_RevisionNumber",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropIndex(
                name: "UX_ExamGradeRevisions_OneMutable",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropIndex(
                name: "UX_ExamGradeRevisions_OneReleased",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropIndex(
                name: "IX_ExamGradeReleases_Attempt_ReleasedAtUtc",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropIndex(
                name: "IX_ExamGradeReleases_OrganizationId_ExamAttemptId_ExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropIndex(
                name: "UX_ExamGradeReleases_Attempt_ClientOperation",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropIndex(
                name: "UX_ExamGradeReleases_Organization_Revision",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ExamFinalAnswers",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropIndex(
                name: "IX_ExamFinalAnswers_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropIndex(
                name: "IX_ExamFinalAnswers_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamAttempts_Organization_ExamVersion_Id",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamAttempts_Organization_Id",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttempts_OrganizationId_ClassId_ExamId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttempts_OrganizationId_ExamId_ExamVersionId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "UX_ExamAttempts_Organization_Enrollment_ClientOperation",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "UX_ExamAttempts_Organization_Exam_Enrollment_Active",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "UX_ExamAttempts_Organization_Exam_Enrollment_Number",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_FinalizationState",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_FinalizedBeforeDeadline",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamAttemptQuestions_Organization_Attempt_Id_QuestionVersion",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttemptQuestions_OrganizationId_ExamAttemptId_Id_CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttemptQuestions_OrganizationId_ExamVersionId_ExamAttemptId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttemptQuestions_OrganizationId_ExamVersionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropIndex(
                name: "UX_ExamAttemptQuestions_Organization_Attempt_DisplayOrder",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropIndex(
                name: "UX_ExamAttemptQuestions_Organization_Attempt_Question",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_EvaluationRevisions_Organization_Attempt_Id",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropIndex(
                name: "IX_EvaluationRevisions_OrganizationId_SubmissionAttemptId_SupersedesEvaluationRevisionId",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropIndex(
                name: "UX_EvaluationRevisions_Attempt_RevisionNumber",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropIndex(
                name: "UX_EvaluationRevisions_OneCurrentStatus",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AssignmentVersions_OrganizationId_Id",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_AssignmentVersions_Organization_Assignment_Id",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_AssignmentVersions_Organization_Class_Assignment_Id",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropIndex(
                name: "UX_AssignmentVersions_Organization_Assignment_VersionNumber",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Assignments_OrganizationId_ClassId_Id",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_Organization_Class_Status_UpdatedAt",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_AssignmentRecipients_SubmissionScope_Id",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentRecipients_OrganizationId_ClassId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropIndex(
                name: "UX_AssignmentRecipients_Organization_Version_Enrollment",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_AnswerRevisions_Organization_Attempt_Question_Id",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropIndex(
                name: "IX_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropIndex(
                name: "IX_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_SupersedesAnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropIndex(
                name: "IX_AnswerRevisions_OrganizationId_QuestionVersionId_SelectedOptionId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropIndex(
                name: "UX_AnswerRevisions_Organization_Attempt_ClientOperation",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropIndex(
                name: "UX_AnswerRevisions_Organization_AttemptQuestion_Revision",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropIndex(
                name: "IX_AnswerRevisionAttachments_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisionAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AnswerRevisionAttachments_OrganizationId_ExamAttemptId_FileAssetId",
                schema: "assessment",
                table: "AnswerRevisionAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AcademicActivityPublications_OrganizationId_AssignmentVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropIndex(
                name: "IX_AcademicActivityPublications_OrganizationId_ExamVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "SubmissionAttempts",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "EnrollmentId",
                schema: "assessment",
                table: "SubmissionAttempts",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "SubmissionAttempts", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "SubmissionAttempts", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(name: "ParticipantId", schema: "assessment", table: "SubmissionAttempts", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "SubmissionAttempts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassMembershipId",
                schema: "assessment",
                table: "SubmissionAttempts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "SubmissionAttachments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "SubmissionAttachments", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "SubmissionAttachments", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "SubmissionAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "QuestionVersions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "QuestionVersions", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "QuestionVersions", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "QuestionVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "QuestionOptions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "QuestionOptions", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "QuestionOptions", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "QuestionOptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ReleasedByMembershipId",
                schema: "assessment",
                table: "GradeReleases",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "GradeReleases",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "GradeReleases", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "GradeReleases", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "GradeReleases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReleasedByPersonalMembershipId",
                schema: "assessment",
                table: "GradeReleases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "storage",
                table: "FileAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassMembershipId",
                schema: "storage",
                table: "FileAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamVersions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "assessment",
                table: "ExamVersions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamVersions", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamVersions", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "Exams",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "assessment",
                table: "Exams",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "Exams", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "Exams", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "Exams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "Exams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamQuestionGrades", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamQuestionGrades", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamGradeRevisions", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamGradeRevisions", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ReleasedByMembershipId",
                schema: "assessment",
                table: "ExamGradeReleases",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamGradeReleases",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamGradeReleases", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamGradeReleases", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamGradeReleases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReleasedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamGradeReleases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamFinalAnswers", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamFinalAnswers", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamAttempts",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "EnrollmentId",
                schema: "assessment",
                table: "ExamAttempts",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "AutoFinalizedAtUtc",
                schema: "assessment",
                table: "ExamAttempts",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamAttempts", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamAttempts", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(name: "ParticipantId", schema: "assessment", table: "ExamAttempts", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamAttempts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassMembershipId",
                schema: "assessment",
                table: "ExamAttempts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamAttemptQuestions", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamAttemptQuestions", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "EvaluationRevisions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "assessment",
                table: "EvaluationRevisions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "EvaluationRevisions", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "EvaluationRevisions", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "EvaluationRevisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "EvaluationRevisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "AssignmentVersions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "assessment",
                table: "AssignmentVersions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "AssignmentVersions", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "AssignmentVersions", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "AssignmentVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "AssignmentVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "Assignments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "assessment",
                table: "Assignments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "Assignments", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "Assignments", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "Assignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "Assignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "AssignmentRecipients",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "EnrollmentId",
                schema: "assessment",
                table: "AssignmentRecipients",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "AssignmentRecipients", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "AssignmentRecipients", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(name: "ParticipantId", schema: "assessment", table: "AssignmentRecipients", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "AssignmentRecipients",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassMembershipId",
                schema: "assessment",
                table: "AssignmentRecipients",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "AnswerRevisions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "AnswerRevisions", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "AnswerRevisions", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "AnswerRevisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "assessment", table: "AnswerRevisionAttachments", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "assessment", table: "AnswerRevisionAttachments", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(name: "ContextId", schema: "messaging", table: "AcademicActivityPublications", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<int>(name: "ContextKind", schema: "messaging", table: "AcademicActivityPublications", type: "int", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                type: "uniqueidentifier",
                nullable: true);

            // نوع و شناسهٔ زمینه فقط از رابطهٔ سازمانی موجود منتقل می‌شوند؛ مقدار حدسی و Default وجود ندارد.
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[SubmissionAttempts] SET [ContextKind] = 1, [ContextId] = [OrganizationId], [ParticipantId] = [EnrollmentId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[SubmissionAttachments] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[QuestionVersions] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[QuestionOptions] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[GradeReleases] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[ExamVersions] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[Exams] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[ExamQuestionGrades] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[ExamGradeRevisions] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[ExamGradeReleases] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[ExamFinalAnswers] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[ExamAttempts] SET [ContextKind] = 1, [ContextId] = [OrganizationId], [ParticipantId] = [EnrollmentId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[ExamAttemptQuestions] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[EvaluationRevisions] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[AssignmentVersions] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[Assignments] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[AssignmentRecipients] SET [ContextKind] = 1, [ContextId] = [OrganizationId], [ParticipantId] = [EnrollmentId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[AnswerRevisions] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [assessment].[AnswerRevisionAttachments] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.Sql("EXEC(N'UPDATE [messaging].[AcademicActivityPublications] SET [ContextKind] = 1, [ContextId] = [OrganizationId]');");
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "SubmissionAttempts", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "SubmissionAttempts", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ParticipantId", schema: "assessment", table: "SubmissionAttempts", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "SubmissionAttachments", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "SubmissionAttachments", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "QuestionVersions", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "QuestionVersions", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "QuestionOptions", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "QuestionOptions", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "GradeReleases", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "GradeReleases", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamVersions", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamVersions", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "Exams", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "Exams", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamQuestionGrades", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamQuestionGrades", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamGradeRevisions", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamGradeRevisions", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamGradeReleases", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamGradeReleases", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamFinalAnswers", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamFinalAnswers", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamAttempts", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamAttempts", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ParticipantId", schema: "assessment", table: "ExamAttempts", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "ExamAttemptQuestions", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "ExamAttemptQuestions", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "EvaluationRevisions", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "EvaluationRevisions", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "AssignmentVersions", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "AssignmentVersions", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "Assignments", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "Assignments", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "AssignmentRecipients", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "AssignmentRecipients", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ParticipantId", schema: "assessment", table: "AssignmentRecipients", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "AnswerRevisions", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "AnswerRevisions", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "assessment", table: "AnswerRevisionAttachments", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "assessment", table: "AnswerRevisionAttachments", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "ContextId", schema: "messaging", table: "AcademicActivityPublications", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "ContextKind", schema: "messaging", table: "AcademicActivityPublications", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_SubmissionAttempts_Organization_Id",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "ContextKind", "ContextId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_QuestionVersions_Organization_ExamVersion_Id",
                schema: "assessment",
                table: "QuestionVersions",
                columns: new[] { "ContextKind", "ContextId", "ExamVersionId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_QuestionVersions_OrganizationId_Id",
                schema: "assessment",
                table: "QuestionVersions",
                columns: new[] { "ContextKind", "ContextId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_QuestionOptions_Organization_QuestionVersion_Id",
                schema: "assessment",
                table: "QuestionOptions",
                columns: new[] { "ContextKind", "ContextId", "QuestionVersionId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PersonalClassParticipants_PersonalClassId_Id",
                schema: "academic",
                table: "PersonalClassParticipants",
                columns: new[] { "PersonalClassId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamVersions_Organization_Exam_Id",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "ContextKind", "ContextId", "ExamId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamVersions_OrganizationId_Id",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "ContextKind", "ContextId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Exams_Organization_Class_Id",
                schema: "assessment",
                table: "Exams",
                columns: new[] { "ContextKind", "ContextId", "ClassId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Exams_OrganizationId_Id",
                schema: "assessment",
                table: "Exams",
                columns: new[] { "ContextKind", "ContextId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamGradeRevisions_Organization_Attempt_Id",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExamFinalAnswers",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamAttempts_Organization_ExamVersion_Id",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "ContextKind", "ContextId", "ExamVersionId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamAttempts_Organization_Id",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "ContextKind", "ContextId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamAttemptQuestions_Organization_Attempt_Id_QuestionVersion",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "Id", "QuestionVersionId" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_EvaluationRevisions_Organization_Attempt_Id",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "ContextKind", "ContextId", "SubmissionAttemptId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AssignmentVersions_ContextKind_ContextId_Id",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "ContextKind", "ContextId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_AssignmentVersions_Organization_Assignment_Id",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "ContextKind", "ContextId", "AssignmentId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_AssignmentVersions_Organization_Class_Assignment_Id",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "ContextKind", "ContextId", "ClassId", "AssignmentId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Assignments_OrganizationId_ClassId_Id",
                schema: "assessment",
                table: "Assignments",
                columns: new[] { "ContextKind", "ContextId", "ClassId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_AssignmentRecipients_SubmissionScope_Id",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "ContextKind", "ContextId", "AssignmentId", "AssignmentVersionId", "ParticipantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_AnswerRevisions_Organization_Attempt_Question_Id",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" });

            migrationBuilder.CreateTable(
                name: "AssignmentVersionAttachments",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PersonalClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContextKind = table.Column<int>(type: "int", nullable: false),
                    ContextId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentVersionAttachments", x => x.Id);
                    table.CheckConstraint("CK_AssignmentVersionAttachments_AcademicScope", "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");
                    table.ForeignKey(
                        name: "FK_AssignmentVersionAttachments_AssignmentVersions_ContextKind_ContextId_AssignmentId_AssignmentVersionId",
                        columns: x => new { x.ContextKind, x.ContextId, x.AssignmentId, x.AssignmentVersionId },
                        principalSchema: "assessment",
                        principalTable: "AssignmentVersions",
                        principalColumns: new[] { "ContextKind", "ContextId", "AssignmentId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignmentVersionAttachments_FileAssets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalSchema: "storage",
                        principalTable: "FileAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentVersionAttachments_PersonalClasses_PersonalClassId",
                        column: x => x.PersonalClassId,
                        principalSchema: "academic",
                        principalTable: "PersonalClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonalClassAcademicGrants",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonalClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Capabilities = table.Column<int>(type: "int", nullable: false),
                    ParentGrantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GrantedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalClassAcademicGrants", x => x.Id);
                    table.UniqueConstraint("AK_PersonalClassAcademicGrants_PersonalClassId_Id", x => new { x.PersonalClassId, x.Id });
                    table.CheckConstraint("CK_PersonalClassAcademicGrants_Capabilities", "[Capabilities] BETWEEN 0 AND 7 AND ([Capabilities] <> 0 OR [RevokedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_PersonalClassAcademicGrants_Parent", "[ParentGrantId] IS NULL OR [ParentGrantId] <> [Id]");
                    table.CheckConstraint("CK_PersonalClassAcademicGrants_Time", "[RevokedAtUtc] IS NULL OR [RevokedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_PersonalClassAcademicGrants_PersonalClassAcademicGrants_PersonalClassId_ParentGrantId",
                        columns: x => new { x.PersonalClassId, x.ParentGrantId },
                        principalSchema: "academic",
                        principalTable: "PersonalClassAcademicGrants",
                        principalColumns: new[] { "PersonalClassId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassAcademicGrants_PersonalClassParticipants_PersonalClassId_MembershipId",
                        columns: x => new { x.PersonalClassId, x.MembershipId },
                        principalSchema: "academic",
                        principalTable: "PersonalClassParticipants",
                        principalColumns: new[] { "PersonalClassId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassAcademicGrants_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformPermissions",
                columns: new[] { "Id", "Description", "Key" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000029"), "اعطا و لغو مجوز آموزشی صریح مدیر کلاس مستقل", "platform.personal_classes.academic_permissions.manage" });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformRolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000029"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttempts_Assignment_Status_SubmittedAtUtc",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "ContextKind", "ContextId", "AssignmentId", "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttempts_ContextKind_ContextId_AssignmentId_AssignmentVersionId_ParticipantId_AssignmentRecipientId",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "ContextKind", "ContextId", "AssignmentId", "AssignmentVersionId", "ParticipantId", "AssignmentRecipientId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttempts_PersonalClassId_PersonalClassMembershipId",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "PersonalClassId", "PersonalClassMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_SubmissionAttempts_OneDraft",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "ContextKind", "ContextId", "AssignmentRecipientId", "AssignmentVersionId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_SubmissionAttempts_Recipient_Version_Number",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "ContextKind", "ContextId", "AssignmentRecipientId", "AssignmentVersionId", "AttemptNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubmissionAttempts_AcademicScope",
                schema: "assessment",
                table: "SubmissionAttempts",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubmissionAttempts_Participation",
                schema: "assessment",
                table: "SubmissionAttempts",
                sql: "([ContextKind] = 1 AND [EnrollmentId] IS NOT NULL AND [ParticipantId] = [EnrollmentId] AND [PersonalClassMembershipId] IS NULL) OR ([ContextKind] = 2 AND [EnrollmentId] IS NULL AND [PersonalClassMembershipId] IS NOT NULL AND [ParticipantId] = [PersonalClassMembershipId])");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttachments_PersonalClassId",
                schema: "assessment",
                table: "SubmissionAttachments",
                column: "PersonalClassId");

            migrationBuilder.CreateIndex(
                name: "UX_SubmissionAttachments_Attempt_FileAsset",
                schema: "assessment",
                table: "SubmissionAttachments",
                columns: new[] { "ContextKind", "ContextId", "SubmissionAttemptId", "FileAssetId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubmissionAttachments_AcademicScope",
                schema: "assessment",
                table: "SubmissionAttachments",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionVersions_PersonalClassId",
                schema: "assessment",
                table: "QuestionVersions",
                column: "PersonalClassId");

            migrationBuilder.CreateIndex(
                name: "UX_QuestionVersions_Organization_ExamVersion_Order",
                schema: "assessment",
                table: "QuestionVersions",
                columns: new[] { "ContextKind", "ContextId", "ExamVersionId", "Order" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_QuestionVersions_AcademicScope",
                schema: "assessment",
                table: "QuestionVersions",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_PersonalClassId",
                schema: "assessment",
                table: "QuestionOptions",
                column: "PersonalClassId");

            migrationBuilder.CreateIndex(
                name: "UX_QuestionOptions_Organization_Question_Order",
                schema: "assessment",
                table: "QuestionOptions",
                columns: new[] { "ContextKind", "ContextId", "QuestionVersionId", "Order" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_QuestionOptions_AcademicScope",
                schema: "assessment",
                table: "QuestionOptions",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.CreateIndex(
                name: "IX_GradeReleases_Attempt_ReleasedAtUtc",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "ContextKind", "ContextId", "SubmissionAttemptId", "ReleasedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GradeReleases_ContextKind_ContextId_SubmissionAttemptId_EvaluationRevisionId",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "ContextKind", "ContextId", "SubmissionAttemptId", "EvaluationRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_GradeReleases_PersonalClassId_ReleasedByPersonalMembershipId",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "PersonalClassId", "ReleasedByPersonalMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_GradeReleases_Organization_EvaluationRevision",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "ContextKind", "ContextId", "EvaluationRevisionId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_GradeReleases_AcademicScope",
                schema: "assessment",
                table: "GradeReleases",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GradeReleases_ReleasedByActor",
                schema: "assessment",
                table: "GradeReleases",
                sql: "([ContextKind] = 1 AND [ReleasedByMembershipId] IS NOT NULL AND [ReleasedByPersonalMembershipId] IS NULL) OR ([ContextKind] = 2 AND [ReleasedByMembershipId] IS NULL AND [ReleasedByPersonalMembershipId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_FileAssets_PersonalClassId_PersonalClassMembershipId",
                schema: "storage",
                table: "FileAssets",
                columns: new[] { "PersonalClassId", "PersonalClassMembershipId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_FileAssets_PersonalClassBinding",
                schema: "storage",
                table: "FileAssets",
                sql: "([PersonalClassId] IS NULL AND [PersonalClassMembershipId] IS NULL) OR ([OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [PersonalClassMembershipId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ExamVersions_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "PersonalClassId", "CreatedByPersonalMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamVersions_Organization_Exam_ActiveDraft",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "ContextKind", "ContextId", "ExamId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_ExamVersions_Organization_Exam_VersionNumber",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "ContextKind", "ContextId", "ExamId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamVersions_AcademicScope",
                schema: "assessment",
                table: "ExamVersions",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamVersions_CreatedByActor",
                schema: "assessment",
                table: "ExamVersions",
                sql: "([ContextKind] = 1 AND [CreatedByMembershipId] IS NOT NULL AND [CreatedByPersonalMembershipId] IS NULL) OR ([ContextKind] = 2 AND [CreatedByMembershipId] IS NULL AND [CreatedByPersonalMembershipId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Organization_Class_Status",
                schema: "assessment",
                table: "Exams",
                columns: new[] { "ContextKind", "ContextId", "ClassId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Exams_OrganizationId_ClassId",
                schema: "assessment",
                table: "Exams",
                columns: new[] { "OrganizationId", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_Exams_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "Exams",
                columns: new[] { "PersonalClassId", "CreatedByPersonalMembershipId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Exams_AcademicScope",
                schema: "assessment",
                table: "Exams",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Exams_CreatedByActor",
                schema: "assessment",
                table: "Exams",
                sql: "([ContextKind] = 1 AND [CreatedByMembershipId] IS NOT NULL AND [CreatedByPersonalMembershipId] IS NULL) OR ([ContextKind] = 2 AND [CreatedByMembershipId] IS NULL AND [CreatedByPersonalMembershipId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Exams_PersonalClass",
                schema: "assessment",
                table: "Exams",
                sql: "[ContextKind] <> 2 OR [ClassId] = [PersonalClassId]");

            migrationBuilder.CreateIndex(
                name: "IX_ExamQuestionGrades_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamQuestionGrades_PersonalClassId_ReviewedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                columns: new[] { "PersonalClassId", "ReviewedByPersonalMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamQuestionGrades_Revision_AttemptQuestion",
                schema: "assessment",
                table: "ExamQuestionGrades",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamGradeRevisionId", "ExamAttemptQuestionId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamQuestionGrades_AcademicScope",
                schema: "assessment",
                table: "ExamQuestionGrades",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamQuestionGrades_ReviewedByActor",
                schema: "assessment",
                table: "ExamQuestionGrades",
                sql: "([ContextKind] = 1 AND [ReviewedByMembershipId] IS NOT NULL AND [ReviewedByPersonalMembershipId] IS NULL) OR ([ContextKind] = 2 AND [ReviewedByMembershipId] IS NULL AND [ReviewedByPersonalMembershipId] IS NOT NULL) OR ([ReviewedByMembershipId] IS NULL AND [ReviewedByPersonalMembershipId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamQuestionGrades_ReviewState",
                schema: "assessment",
                table: "ExamQuestionGrades",
                sql: "([IsReviewed] = 0 AND COALESCE([ReviewedByMembershipId], [ReviewedByPersonalMembershipId]) IS NULL AND [ReviewedAtUtc] IS NULL) OR ([IsReviewed] = 1 AND COALESCE([ReviewedByMembershipId], [ReviewedByPersonalMembershipId]) IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ExamGradeRevisions_ContextKind_ContextId_ExamAttemptId_SupersedesExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "SupersedesExamGradeRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamGradeRevisions_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "PersonalClassId", "CreatedByPersonalMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeRevisions_Attempt_RevisionNumber",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeRevisions_OneMutable",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId" },
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeRevisions_OneReleased",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "Status" },
                unique: true,
                filter: "[Status] = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamGradeRevisions_AcademicScope",
                schema: "assessment",
                table: "ExamGradeRevisions",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamGradeRevisions_CreatedByActor",
                schema: "assessment",
                table: "ExamGradeRevisions",
                sql: "([ContextKind] = 1 AND [CreatedByMembershipId] IS NOT NULL AND [CreatedByPersonalMembershipId] IS NULL) OR ([ContextKind] = 2 AND [CreatedByMembershipId] IS NULL AND [CreatedByPersonalMembershipId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ExamGradeReleases_Attempt_ReleasedAtUtc",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ReleasedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamGradeReleases_ContextKind_ContextId_ExamAttemptId_ExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamGradeRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamGradeReleases_PersonalClassId_ReleasedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "PersonalClassId", "ReleasedByPersonalMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeReleases_Attempt_ClientOperation",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeReleases_Organization_Revision",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "ContextKind", "ContextId", "ExamGradeRevisionId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamGradeReleases_AcademicScope",
                schema: "assessment",
                table: "ExamGradeReleases",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamGradeReleases_ReleasedByActor",
                schema: "assessment",
                table: "ExamGradeReleases",
                sql: "([ContextKind] = 1 AND [ReleasedByMembershipId] IS NOT NULL AND [ReleasedByPersonalMembershipId] IS NULL) OR ([ContextKind] = 2 AND [ReleasedByMembershipId] IS NULL AND [ReleasedByPersonalMembershipId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ExamFinalAnswers_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "AnswerRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamFinalAnswers_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamFinalAnswers_PersonalClassId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                column: "PersonalClassId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamFinalAnswers_AcademicScope",
                schema: "assessment",
                table: "ExamFinalAnswers",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_ContextKind_ContextId_ClassId_ExamId",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "ContextKind", "ContextId", "ClassId", "ExamId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_ContextKind_ContextId_ExamId_ExamVersionId",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "ContextKind", "ContextId", "ExamId", "ExamVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_ContextKind_Status_EffectiveDeadlineUtc",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "ContextKind", "Status", "EffectiveDeadlineUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_PersonalClassId_PersonalClassMembershipId",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "PersonalClassId", "PersonalClassMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttempts_Organization_Enrollment_ClientOperation",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "ContextKind", "ContextId", "ParticipantId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttempts_Organization_Exam_Enrollment_Active",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "ContextKind", "ContextId", "ExamId", "ParticipantId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttempts_Organization_Exam_Enrollment_Number",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "ContextKind", "ContextId", "ExamId", "ParticipantId", "AttemptNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_AcademicScope",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_FinalizationState",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "([Status] = 3 AND [FinalizedAtUtc] IS NOT NULL AND [FinalizedAnswerSetVersion] IS NOT NULL AND (([AutoFinalizedAtUtc] IS NULL AND [FinalizeClientOperationId] IS NOT NULL AND [FinalizeRequestHash] IS NOT NULL AND [FinalizedBySessionId] IS NOT NULL) OR ([AutoFinalizedAtUtc] IS NOT NULL AND [ContextKind] = 2 AND [AutoFinalizedAtUtc] >= [EffectiveDeadlineUtc] AND [FinalizedAtUtc] = [EffectiveDeadlineUtc] AND [FinalizeClientOperationId] IS NULL AND [FinalizeRequestHash] IS NULL AND [FinalizedBySessionId] IS NULL))) OR ([Status] <> 3 AND [FinalizedAtUtc] IS NULL AND [AutoFinalizedAtUtc] IS NULL AND [FinalizedAnswerSetVersion] IS NULL AND [FinalizeClientOperationId] IS NULL AND [FinalizeRequestHash] IS NULL AND [FinalizedBySessionId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_FinalizedBeforeDeadline",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "[FinalizedAtUtc] IS NULL OR ([AutoFinalizedAtUtc] IS NULL AND [FinalizedAtUtc] < [EffectiveDeadlineUtc]) OR ([AutoFinalizedAtUtc] IS NOT NULL AND [FinalizedAtUtc] = [EffectiveDeadlineUtc])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_Participation",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "([ContextKind] = 1 AND [EnrollmentId] IS NOT NULL AND [ParticipantId] = [EnrollmentId] AND [PersonalClassMembershipId] IS NULL) OR ([ContextKind] = 2 AND [EnrollmentId] IS NULL AND [PersonalClassMembershipId] IS NOT NULL AND [ParticipantId] = [PersonalClassMembershipId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_PersonalClass",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "[ContextKind] <> 2 OR [ClassId] = [PersonalClassId]");

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttemptQuestions_ContextKind_ContextId_ExamAttemptId_Id_CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "Id", "CurrentAnswerRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttemptQuestions_ContextKind_ContextId_ExamVersionId_ExamAttemptId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "ContextKind", "ContextId", "ExamVersionId", "ExamAttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttemptQuestions_ContextKind_ContextId_ExamVersionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "ContextKind", "ContextId", "ExamVersionId", "QuestionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttemptQuestions_PersonalClassId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                column: "PersonalClassId");

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttemptQuestions_Organization_Attempt_DisplayOrder",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttemptQuestions_Organization_Attempt_Question",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "QuestionVersionId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttemptQuestions_AcademicScope",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRevisions_ContextKind_ContextId_SubmissionAttemptId_SupersedesEvaluationRevisionId",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "ContextKind", "ContextId", "SubmissionAttemptId", "SupersedesEvaluationRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRevisions_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "PersonalClassId", "CreatedByPersonalMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_EvaluationRevisions_Attempt_RevisionNumber",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "ContextKind", "ContextId", "SubmissionAttemptId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EvaluationRevisions_OneCurrentStatus",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "ContextKind", "ContextId", "SubmissionAttemptId", "Status" },
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EvaluationRevisions_AcademicScope",
                schema: "assessment",
                table: "EvaluationRevisions",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EvaluationRevisions_CreatedByActor",
                schema: "assessment",
                table: "EvaluationRevisions",
                sql: "([ContextKind] = 1 AND [CreatedByMembershipId] IS NOT NULL AND [CreatedByPersonalMembershipId] IS NULL) OR ([ContextKind] = 2 AND [CreatedByMembershipId] IS NULL AND [CreatedByPersonalMembershipId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentVersions_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "PersonalClassId", "CreatedByPersonalMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_AssignmentVersions_Organization_Assignment_VersionNumber",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "ContextKind", "ContextId", "AssignmentId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentVersions_AcademicScope",
                schema: "assessment",
                table: "AssignmentVersions",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentVersions_CreatedByActor",
                schema: "assessment",
                table: "AssignmentVersions",
                sql: "([ContextKind] = 1 AND [CreatedByMembershipId] IS NOT NULL AND [CreatedByPersonalMembershipId] IS NULL) OR ([ContextKind] = 2 AND [CreatedByMembershipId] IS NULL AND [CreatedByPersonalMembershipId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentVersions_PersonalClass",
                schema: "assessment",
                table: "AssignmentVersions",
                sql: "[ContextKind] <> 2 OR [ClassId] = [PersonalClassId]");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_Organization_Class_Status_UpdatedAt",
                schema: "assessment",
                table: "Assignments",
                columns: new[] { "ContextKind", "ContextId", "ClassId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_OrganizationId_ClassId",
                schema: "assessment",
                table: "Assignments",
                columns: new[] { "OrganizationId", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "Assignments",
                columns: new[] { "PersonalClassId", "CreatedByPersonalMembershipId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assignments_AcademicScope",
                schema: "assessment",
                table: "Assignments",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assignments_CreatedByActor",
                schema: "assessment",
                table: "Assignments",
                sql: "([ContextKind] = 1 AND [CreatedByMembershipId] IS NOT NULL AND [CreatedByPersonalMembershipId] IS NULL) OR ([ContextKind] = 2 AND [CreatedByMembershipId] IS NULL AND [CreatedByPersonalMembershipId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assignments_PersonalClass",
                schema: "assessment",
                table: "Assignments",
                sql: "[ContextKind] <> 2 OR [ClassId] = [PersonalClassId]");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRecipients_ContextKind_ContextId_ClassId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "ContextKind", "ContextId", "ClassId", "AssignmentId", "AssignmentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRecipients_PersonalClassId_PersonalClassMembershipId",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "PersonalClassId", "PersonalClassMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_AssignmentRecipients_Organization_Version_Enrollment",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "ContextKind", "ContextId", "AssignmentVersionId", "ParticipantId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentRecipients_AcademicScope",
                schema: "assessment",
                table: "AssignmentRecipients",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentRecipients_Participation",
                schema: "assessment",
                table: "AssignmentRecipients",
                sql: "([ContextKind] = 1 AND [EnrollmentId] IS NOT NULL AND [ParticipantId] = [EnrollmentId] AND [PersonalClassMembershipId] IS NULL) OR ([ContextKind] = 2 AND [EnrollmentId] IS NULL AND [PersonalClassMembershipId] IS NOT NULL AND [ParticipantId] = [PersonalClassMembershipId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentRecipients_PersonalClass",
                schema: "assessment",
                table: "AssignmentRecipients",
                sql: "[ContextKind] <> 2 OR [ClassId] = [PersonalClassId]");

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_SupersedesAnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "SupersedesAnswerRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisions_ContextKind_ContextId_QuestionVersionId_SelectedOptionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "ContextKind", "ContextId", "QuestionVersionId", "SelectedOptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisions_PersonalClassId",
                schema: "assessment",
                table: "AnswerRevisions",
                column: "PersonalClassId");

            migrationBuilder.CreateIndex(
                name: "UX_AnswerRevisions_Organization_Attempt_ClientOperation",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AnswerRevisions_Organization_AttemptQuestion_Revision",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "RevisionNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AnswerRevisions_AcademicScope",
                schema: "assessment",
                table: "AnswerRevisions",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisionAttachments_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "AnswerRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisionAttachments_ContextKind_ContextId_ExamAttemptId_FileAssetId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "FileAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisionAttachments_PersonalClassId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                column: "PersonalClassId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AnswerRevisionAttachments_AcademicScope",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_ContextKind_ContextId_AssignmentVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "ContextKind", "ContextId", "AssignmentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_ContextKind_ContextId_ExamVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "ContextKind", "ContextId", "ExamVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_PersonalClassId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                column: "PersonalClassId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AcademicActivityPublications_AcademicScope",
                schema: "messaging",
                table: "AcademicActivityPublications",
                sql: "([ContextKind] = 1 AND [OrganizationId] IS NOT NULL AND [ContextId] = [OrganizationId] AND [PersonalClassId] IS NULL) OR ([ContextKind] = 2 AND [OrganizationId] IS NULL AND [PersonalClassId] IS NOT NULL AND [ContextId] = [PersonalClassId])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AcademicActivityPublications_PersonalClass",
                schema: "messaging",
                table: "AcademicActivityPublications",
                sql: "[ContextKind] <> 2 OR [ClassId] = [PersonalClassId]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AcademicActivityPublications_PersonalKind",
                schema: "messaging",
                table: "AcademicActivityPublications",
                sql: "[ContextKind] <> 2 OR [Kind] IN (2,3)");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentVersionAttachments_AssignmentVersionId_FileAssetId",
                schema: "assessment",
                table: "AssignmentVersionAttachments",
                columns: new[] { "AssignmentVersionId", "FileAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentVersionAttachments_ContextKind_ContextId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "AssignmentVersionAttachments",
                columns: new[] { "ContextKind", "ContextId", "AssignmentId", "AssignmentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentVersionAttachments_FileAssetId",
                schema: "assessment",
                table: "AssignmentVersionAttachments",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentVersionAttachments_PersonalClassId",
                schema: "assessment",
                table: "AssignmentVersionAttachments",
                column: "PersonalClassId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassAcademicGrants_GrantedByUserId_ClientOperationId",
                schema: "academic",
                table: "PersonalClassAcademicGrants",
                columns: new[] { "GrantedByUserId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassAcademicGrants_PersonalClassId_MembershipId",
                schema: "academic",
                table: "PersonalClassAcademicGrants",
                columns: new[] { "PersonalClassId", "MembershipId" },
                unique: true,
                filter: "[RevokedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassAcademicGrants_PersonalClassId_ParentGrantId",
                schema: "academic",
                table: "PersonalClassAcademicGrants",
                columns: new[] { "PersonalClassId", "ParentGrantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicActivityPublications_AssignmentVersions_ContextKind_ContextId_AssignmentVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "ContextKind", "ContextId", "AssignmentVersionId" },
                principalSchema: "assessment",
                principalTable: "AssignmentVersions",
                principalColumns: new[] { "ContextKind", "ContextId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicActivityPublications_ExamVersions_ContextKind_ContextId_ExamVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "ContextKind", "ContextId", "ExamVersionId" },
                principalSchema: "assessment",
                principalTable: "ExamVersions",
                principalColumns: new[] { "ContextKind", "ContextId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicActivityPublications_PersonalClasses_PersonalClassId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnswerRevisionAttachments_AnswerRevisions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "AnswerRevisionId" },
                principalSchema: "assessment",
                principalTable: "AnswerRevisions",
                principalColumns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnswerRevisionAttachments_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnswerRevisions_AnswerRevisions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_SupersedesAnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "SupersedesAnswerRevisionId" },
                principalSchema: "assessment",
                principalTable: "AnswerRevisions",
                principalColumns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnswerRevisions_ExamAttemptQuestions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" },
                principalSchema: "assessment",
                principalTable: "ExamAttemptQuestions",
                principalColumns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "Id", "QuestionVersionId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnswerRevisions_ExamAttempts_ContextKind_ContextId_ExamAttemptId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId" },
                principalSchema: "assessment",
                principalTable: "ExamAttempts",
                principalColumns: new[] { "ContextKind", "ContextId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnswerRevisions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "AnswerRevisions",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnswerRevisions_QuestionOptions_ContextKind_ContextId_QuestionVersionId_SelectedOptionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "ContextKind", "ContextId", "QuestionVersionId", "SelectedOptionId" },
                principalSchema: "assessment",
                principalTable: "QuestionOptions",
                principalColumns: new[] { "ContextKind", "ContextId", "QuestionVersionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentRecipients_AssignmentVersions_ContextKind_ContextId_ClassId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "ContextKind", "ContextId", "ClassId", "AssignmentId", "AssignmentVersionId" },
                principalSchema: "assessment",
                principalTable: "AssignmentVersions",
                principalColumns: new[] { "ContextKind", "ContextId", "ClassId", "AssignmentId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentRecipients_PersonalClassParticipants_PersonalClassId_PersonalClassMembershipId",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "PersonalClassId", "PersonalClassMembershipId" },
                principalSchema: "academic",
                principalTable: "PersonalClassParticipants",
                principalColumns: new[] { "PersonalClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentRecipients_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "AssignmentRecipients",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_PersonalClassParticipants_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "Assignments",
                columns: new[] { "PersonalClassId", "CreatedByPersonalMembershipId" },
                principalSchema: "academic",
                principalTable: "PersonalClassParticipants",
                principalColumns: new[] { "PersonalClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "Assignments",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentVersions_Assignments_ContextKind_ContextId_ClassId_AssignmentId",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "ContextKind", "ContextId", "ClassId", "AssignmentId" },
                principalSchema: "assessment",
                principalTable: "Assignments",
                principalColumns: new[] { "ContextKind", "ContextId", "ClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentVersions_PersonalClassParticipants_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "PersonalClassId", "CreatedByPersonalMembershipId" },
                principalSchema: "academic",
                principalTable: "PersonalClassParticipants",
                principalColumns: new[] { "PersonalClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentVersions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "AssignmentVersions",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationRevisions_EvaluationRevisions_ContextKind_ContextId_SubmissionAttemptId_SupersedesEvaluationRevisionId",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "ContextKind", "ContextId", "SubmissionAttemptId", "SupersedesEvaluationRevisionId" },
                principalSchema: "assessment",
                principalTable: "EvaluationRevisions",
                principalColumns: new[] { "ContextKind", "ContextId", "SubmissionAttemptId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationRevisions_PersonalClassParticipants_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "PersonalClassId", "CreatedByPersonalMembershipId" },
                principalSchema: "academic",
                principalTable: "PersonalClassParticipants",
                principalColumns: new[] { "PersonalClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationRevisions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "EvaluationRevisions",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationRevisions_SubmissionAttempts_ContextKind_ContextId_SubmissionAttemptId",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "ContextKind", "ContextId", "SubmissionAttemptId" },
                principalSchema: "assessment",
                principalTable: "SubmissionAttempts",
                principalColumns: new[] { "ContextKind", "ContextId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttemptQuestions_AnswerRevisions_ContextKind_ContextId_ExamAttemptId_Id_CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "Id", "CurrentAnswerRevisionId" },
                principalSchema: "assessment",
                principalTable: "AnswerRevisions",
                principalColumns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttemptQuestions_ExamAttempts_ContextKind_ContextId_ExamVersionId_ExamAttemptId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "ContextKind", "ContextId", "ExamVersionId", "ExamAttemptId" },
                principalSchema: "assessment",
                principalTable: "ExamAttempts",
                principalColumns: new[] { "ContextKind", "ContextId", "ExamVersionId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttemptQuestions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttemptQuestions_QuestionVersions_ContextKind_ContextId_ExamVersionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "ContextKind", "ContextId", "ExamVersionId", "QuestionVersionId" },
                principalSchema: "assessment",
                principalTable: "QuestionVersions",
                principalColumns: new[] { "ContextKind", "ContextId", "ExamVersionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttempts_ExamVersions_ContextKind_ContextId_ExamId_ExamVersionId",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "ContextKind", "ContextId", "ExamId", "ExamVersionId" },
                principalSchema: "assessment",
                principalTable: "ExamVersions",
                principalColumns: new[] { "ContextKind", "ContextId", "ExamId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttempts_Exams_ContextKind_ContextId_ClassId_ExamId",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "ContextKind", "ContextId", "ClassId", "ExamId" },
                principalSchema: "assessment",
                principalTable: "Exams",
                principalColumns: new[] { "ContextKind", "ContextId", "ClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttempts_PersonalClassParticipants_PersonalClassId_PersonalClassMembershipId",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "PersonalClassId", "PersonalClassMembershipId" },
                principalSchema: "academic",
                principalTable: "PersonalClassParticipants",
                principalColumns: new[] { "PersonalClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttempts_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamAttempts",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamFinalAnswers_AnswerRevisions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "AnswerRevisionId" },
                principalSchema: "assessment",
                principalTable: "AnswerRevisions",
                principalColumns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamFinalAnswers_ExamAttemptQuestions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" },
                principalSchema: "assessment",
                principalTable: "ExamAttemptQuestions",
                principalColumns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "Id", "QuestionVersionId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamFinalAnswers_ExamAttempts_ContextKind_ContextId_ExamAttemptId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId" },
                principalSchema: "assessment",
                principalTable: "ExamAttempts",
                principalColumns: new[] { "ContextKind", "ContextId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamFinalAnswers_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamGradeReleases_ExamGradeRevisions_ContextKind_ContextId_ExamAttemptId_ExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamGradeRevisionId" },
                principalSchema: "assessment",
                principalTable: "ExamGradeRevisions",
                principalColumns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamGradeReleases_PersonalClassParticipants_PersonalClassId_ReleasedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "PersonalClassId", "ReleasedByPersonalMembershipId" },
                principalSchema: "academic",
                principalTable: "PersonalClassParticipants",
                principalColumns: new[] { "PersonalClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamGradeReleases_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamGradeReleases",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamGradeRevisions_ExamAttempts_ContextKind_ContextId_ExamAttemptId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId" },
                principalSchema: "assessment",
                principalTable: "ExamAttempts",
                principalColumns: new[] { "ContextKind", "ContextId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamGradeRevisions_ExamGradeRevisions_ContextKind_ContextId_ExamAttemptId_SupersedesExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "SupersedesExamGradeRevisionId" },
                principalSchema: "assessment",
                principalTable: "ExamGradeRevisions",
                principalColumns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamGradeRevisions_PersonalClassParticipants_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "PersonalClassId", "CreatedByPersonalMembershipId" },
                principalSchema: "academic",
                principalTable: "PersonalClassParticipants",
                principalColumns: new[] { "PersonalClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamGradeRevisions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestionGrades_ExamAttemptQuestions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" },
                principalSchema: "assessment",
                principalTable: "ExamAttemptQuestions",
                principalColumns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "Id", "QuestionVersionId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestionGrades_ExamGradeRevisions_ContextKind_ContextId_ExamAttemptId_ExamGradeRevisionId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                columns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "ExamGradeRevisionId" },
                principalSchema: "assessment",
                principalTable: "ExamGradeRevisions",
                principalColumns: new[] { "ContextKind", "ContextId", "ExamAttemptId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestionGrades_PersonalClassParticipants_PersonalClassId_ReviewedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                columns: new[] { "PersonalClassId", "ReviewedByPersonalMembershipId" },
                principalSchema: "academic",
                principalTable: "PersonalClassParticipants",
                principalColumns: new[] { "PersonalClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestionGrades_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_PersonalClassParticipants_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "Exams",
                columns: new[] { "PersonalClassId", "CreatedByPersonalMembershipId" },
                principalSchema: "academic",
                principalTable: "PersonalClassParticipants",
                principalColumns: new[] { "PersonalClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "Exams",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamVersions_Exams_ContextKind_ContextId_ExamId",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "ContextKind", "ContextId", "ExamId" },
                principalSchema: "assessment",
                principalTable: "Exams",
                principalColumns: new[] { "ContextKind", "ContextId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamVersions_PersonalClassParticipants_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "PersonalClassId", "CreatedByPersonalMembershipId" },
                principalSchema: "academic",
                principalTable: "PersonalClassParticipants",
                principalColumns: new[] { "PersonalClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamVersions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamVersions",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FileAssets_PersonalClassParticipants_PersonalClassId_PersonalClassMembershipId",
                schema: "storage",
                table: "FileAssets",
                columns: new[] { "PersonalClassId", "PersonalClassMembershipId" },
                principalSchema: "academic",
                principalTable: "PersonalClassParticipants",
                principalColumns: new[] { "PersonalClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GradeReleases_EvaluationRevisions_ContextKind_ContextId_SubmissionAttemptId_EvaluationRevisionId",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "ContextKind", "ContextId", "SubmissionAttemptId", "EvaluationRevisionId" },
                principalSchema: "assessment",
                principalTable: "EvaluationRevisions",
                principalColumns: new[] { "ContextKind", "ContextId", "SubmissionAttemptId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GradeReleases_PersonalClassParticipants_PersonalClassId_ReleasedByPersonalMembershipId",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "PersonalClassId", "ReleasedByPersonalMembershipId" },
                principalSchema: "academic",
                principalTable: "PersonalClassParticipants",
                principalColumns: new[] { "PersonalClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GradeReleases_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "GradeReleases",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionOptions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "QuestionOptions",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionOptions_QuestionVersions_ContextKind_ContextId_QuestionVersionId",
                schema: "assessment",
                table: "QuestionOptions",
                columns: new[] { "ContextKind", "ContextId", "QuestionVersionId" },
                principalSchema: "assessment",
                principalTable: "QuestionVersions",
                principalColumns: new[] { "ContextKind", "ContextId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionVersions_ExamVersions_ContextKind_ContextId_ExamVersionId",
                schema: "assessment",
                table: "QuestionVersions",
                columns: new[] { "ContextKind", "ContextId", "ExamVersionId" },
                principalSchema: "assessment",
                principalTable: "ExamVersions",
                principalColumns: new[] { "ContextKind", "ContextId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionVersions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "QuestionVersions",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAttachments_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "SubmissionAttachments",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAttachments_SubmissionAttempts_ContextKind_ContextId_SubmissionAttemptId",
                schema: "assessment",
                table: "SubmissionAttachments",
                columns: new[] { "ContextKind", "ContextId", "SubmissionAttemptId" },
                principalSchema: "assessment",
                principalTable: "SubmissionAttempts",
                principalColumns: new[] { "ContextKind", "ContextId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAttempts_AssignmentRecipients_ContextKind_ContextId_AssignmentId_AssignmentVersionId_ParticipantId_AssignmentRecip~",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "ContextKind", "ContextId", "AssignmentId", "AssignmentVersionId", "ParticipantId", "AssignmentRecipientId" },
                principalSchema: "assessment",
                principalTable: "AssignmentRecipients",
                principalColumns: new[] { "ContextKind", "ContextId", "AssignmentId", "AssignmentVersionId", "ParticipantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAttempts_AssignmentVersions_ContextKind_ContextId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "ContextKind", "ContextId", "AssignmentId", "AssignmentVersionId" },
                principalSchema: "assessment",
                principalTable: "AssignmentVersions",
                principalColumns: new[] { "ContextKind", "ContextId", "AssignmentId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAttempts_PersonalClassParticipants_PersonalClassId_PersonalClassMembershipId",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "PersonalClassId", "PersonalClassMembershipId" },
                principalSchema: "academic",
                principalTable: "PersonalClassParticipants",
                principalColumns: new[] { "PersonalClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAttempts_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "SubmissionAttempts",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // بازگشت پس از ثبت دادهٔ قابلیت جدید، به بررسی مستقل نیاز دارد و خودکار مخرب نیست.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[SubmissionAttempts] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[SubmissionAttachments] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[QuestionVersions] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[QuestionOptions] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[GradeReleases] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[ExamVersions] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[Exams] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[ExamQuestionGrades] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[ExamGradeRevisions] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[ExamGradeReleases] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[ExamFinalAnswers] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[ExamAttempts] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[ExamAttemptQuestions] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[EvaluationRevisions] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[AssignmentVersions] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[Assignments] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[AssignmentRecipients] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[AnswerRevisions] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[AnswerRevisionAttachments] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [messaging].[AcademicActivityPublications] WHERE [ContextKind] <> 1) THROW 51000, 'Personal academic data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [academic].[PersonalClassAcademicGrants]) THROW 51000, 'Academic feature data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [assessment].[AssignmentVersionAttachments]) THROW 51000, 'Academic feature data prevents automatic rollback.', 1;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [storage].[FileAssets] WHERE [PersonalClassId] IS NOT NULL) THROW 51000, 'Personal academic files prevent automatic rollback.', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_AcademicActivityPublications_AssignmentVersions_ContextKind_ContextId_AssignmentVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropForeignKey(
                name: "FK_AcademicActivityPublications_ExamVersions_ContextKind_ContextId_ExamVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropForeignKey(
                name: "FK_AcademicActivityPublications_PersonalClasses_PersonalClassId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropForeignKey(
                name: "FK_AnswerRevisionAttachments_AnswerRevisions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisionAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AnswerRevisionAttachments_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "AnswerRevisionAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_AnswerRevisions_AnswerRevisions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_SupersedesAnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_AnswerRevisions_ExamAttemptQuestions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_AnswerRevisions_ExamAttempts_ContextKind_ContextId_ExamAttemptId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_AnswerRevisions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_AnswerRevisions_QuestionOptions_ContextKind_ContextId_QuestionVersionId_SelectedOptionId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentRecipients_AssignmentVersions_ContextKind_ContextId_ClassId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentRecipients_PersonalClassParticipants_PersonalClassId_PersonalClassMembershipId",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentRecipients_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_PersonalClassParticipants_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentVersions_Assignments_ContextKind_ContextId_ClassId_AssignmentId",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentVersions_PersonalClassParticipants_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentVersions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationRevisions_EvaluationRevisions_ContextKind_ContextId_SubmissionAttemptId_SupersedesEvaluationRevisionId",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationRevisions_PersonalClassParticipants_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationRevisions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_EvaluationRevisions_SubmissionAttempts_ContextKind_ContextId_SubmissionAttemptId",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttemptQuestions_AnswerRevisions_ContextKind_ContextId_ExamAttemptId_Id_CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttemptQuestions_ExamAttempts_ContextKind_ContextId_ExamVersionId_ExamAttemptId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttemptQuestions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttemptQuestions_QuestionVersions_ContextKind_ContextId_ExamVersionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttempts_ExamVersions_ContextKind_ContextId_ExamId_ExamVersionId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttempts_Exams_ContextKind_ContextId_ClassId_ExamId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttempts_PersonalClassParticipants_PersonalClassId_PersonalClassMembershipId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttempts_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamFinalAnswers_AnswerRevisions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamFinalAnswers_ExamAttemptQuestions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamFinalAnswers_ExamAttempts_ContextKind_ContextId_ExamAttemptId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamFinalAnswers_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamGradeReleases_ExamGradeRevisions_ContextKind_ContextId_ExamAttemptId_ExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamGradeReleases_PersonalClassParticipants_PersonalClassId_ReleasedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamGradeReleases_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamGradeRevisions_ExamAttempts_ContextKind_ContextId_ExamAttemptId",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamGradeRevisions_ExamGradeRevisions_ContextKind_ContextId_ExamAttemptId_SupersedesExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamGradeRevisions_PersonalClassParticipants_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamGradeRevisions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestionGrades_ExamAttemptQuestions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestionGrades_ExamGradeRevisions_ContextKind_ContextId_ExamAttemptId_ExamGradeRevisionId",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestionGrades_PersonalClassParticipants_PersonalClassId_ReviewedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestionGrades_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropForeignKey(
                name: "FK_Exams_PersonalClassParticipants_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropForeignKey(
                name: "FK_Exams_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamVersions_Exams_ContextKind_ContextId_ExamId",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamVersions_PersonalClassParticipants_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamVersions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_FileAssets_PersonalClassParticipants_PersonalClassId_PersonalClassMembershipId",
                schema: "storage",
                table: "FileAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_GradeReleases_EvaluationRevisions_ContextKind_ContextId_SubmissionAttemptId_EvaluationRevisionId",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropForeignKey(
                name: "FK_GradeReleases_PersonalClassParticipants_PersonalClassId_ReleasedByPersonalMembershipId",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropForeignKey(
                name: "FK_GradeReleases_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionOptions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "QuestionOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionOptions_QuestionVersions_ContextKind_ContextId_QuestionVersionId",
                schema: "assessment",
                table: "QuestionOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionVersions_ExamVersions_ContextKind_ContextId_ExamVersionId",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionVersions_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAttachments_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "SubmissionAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAttachments_SubmissionAttempts_ContextKind_ContextId_SubmissionAttemptId",
                schema: "assessment",
                table: "SubmissionAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAttempts_AssignmentRecipients_ContextKind_ContextId_AssignmentId_AssignmentVersionId_ParticipantId_AssignmentRecip~",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAttempts_AssignmentVersions_ContextKind_ContextId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAttempts_PersonalClassParticipants_PersonalClassId_PersonalClassMembershipId",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAttempts_PersonalClasses_PersonalClassId",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropTable(
                name: "AssignmentVersionAttachments",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "PersonalClassAcademicGrants",
                schema: "academic");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_SubmissionAttempts_Organization_Id",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionAttempts_Assignment_Status_SubmittedAtUtc",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionAttempts_ContextKind_ContextId_AssignmentId_AssignmentVersionId_ParticipantId_AssignmentRecipientId",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionAttempts_PersonalClassId_PersonalClassMembershipId",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropIndex(
                name: "UX_SubmissionAttempts_OneDraft",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropIndex(
                name: "UX_SubmissionAttempts_Recipient_Version_Number",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SubmissionAttempts_AcademicScope",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SubmissionAttempts_Participation",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionAttachments_PersonalClassId",
                schema: "assessment",
                table: "SubmissionAttachments");

            migrationBuilder.DropIndex(
                name: "UX_SubmissionAttachments_Attempt_FileAsset",
                schema: "assessment",
                table: "SubmissionAttachments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SubmissionAttachments_AcademicScope",
                schema: "assessment",
                table: "SubmissionAttachments");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_QuestionVersions_Organization_ExamVersion_Id",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_QuestionVersions_OrganizationId_Id",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropIndex(
                name: "IX_QuestionVersions_PersonalClassId",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropIndex(
                name: "UX_QuestionVersions_Organization_ExamVersion_Order",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QuestionVersions_AcademicScope",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_QuestionOptions_Organization_QuestionVersion_Id",
                schema: "assessment",
                table: "QuestionOptions");

            migrationBuilder.DropIndex(
                name: "IX_QuestionOptions_PersonalClassId",
                schema: "assessment",
                table: "QuestionOptions");

            migrationBuilder.DropIndex(
                name: "UX_QuestionOptions_Organization_Question_Order",
                schema: "assessment",
                table: "QuestionOptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QuestionOptions_AcademicScope",
                schema: "assessment",
                table: "QuestionOptions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PersonalClassParticipants_PersonalClassId_Id",
                schema: "academic",
                table: "PersonalClassParticipants");

            migrationBuilder.DropIndex(
                name: "IX_GradeReleases_Attempt_ReleasedAtUtc",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropIndex(
                name: "IX_GradeReleases_ContextKind_ContextId_SubmissionAttemptId_EvaluationRevisionId",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropIndex(
                name: "IX_GradeReleases_PersonalClassId_ReleasedByPersonalMembershipId",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropIndex(
                name: "UX_GradeReleases_Organization_EvaluationRevision",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GradeReleases_AcademicScope",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GradeReleases_ReleasedByActor",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropIndex(
                name: "IX_FileAssets_PersonalClassId_PersonalClassMembershipId",
                schema: "storage",
                table: "FileAssets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FileAssets_PersonalClassBinding",
                schema: "storage",
                table: "FileAssets");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamVersions_Organization_Exam_Id",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamVersions_OrganizationId_Id",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropIndex(
                name: "IX_ExamVersions_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropIndex(
                name: "UX_ExamVersions_Organization_Exam_ActiveDraft",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropIndex(
                name: "UX_ExamVersions_Organization_Exam_VersionNumber",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamVersions_AcademicScope",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamVersions_CreatedByActor",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Exams_Organization_Class_Id",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Exams_OrganizationId_Id",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_Organization_Class_Status",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_OrganizationId_ClassId",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Exams_AcademicScope",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Exams_CreatedByActor",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Exams_PersonalClass",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_ExamQuestionGrades_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropIndex(
                name: "IX_ExamQuestionGrades_PersonalClassId_ReviewedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropIndex(
                name: "UX_ExamQuestionGrades_Revision_AttemptQuestion",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamQuestionGrades_AcademicScope",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamQuestionGrades_ReviewedByActor",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamQuestionGrades_ReviewState",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamGradeRevisions_Organization_Attempt_Id",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropIndex(
                name: "IX_ExamGradeRevisions_ContextKind_ContextId_ExamAttemptId_SupersedesExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropIndex(
                name: "IX_ExamGradeRevisions_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropIndex(
                name: "UX_ExamGradeRevisions_Attempt_RevisionNumber",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropIndex(
                name: "UX_ExamGradeRevisions_OneMutable",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropIndex(
                name: "UX_ExamGradeRevisions_OneReleased",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamGradeRevisions_AcademicScope",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamGradeRevisions_CreatedByActor",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropIndex(
                name: "IX_ExamGradeReleases_Attempt_ReleasedAtUtc",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropIndex(
                name: "IX_ExamGradeReleases_ContextKind_ContextId_ExamAttemptId_ExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropIndex(
                name: "IX_ExamGradeReleases_PersonalClassId_ReleasedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropIndex(
                name: "UX_ExamGradeReleases_Attempt_ClientOperation",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropIndex(
                name: "UX_ExamGradeReleases_Organization_Revision",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamGradeReleases_AcademicScope",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamGradeReleases_ReleasedByActor",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ExamFinalAnswers",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropIndex(
                name: "IX_ExamFinalAnswers_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropIndex(
                name: "IX_ExamFinalAnswers_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropIndex(
                name: "IX_ExamFinalAnswers_PersonalClassId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamFinalAnswers_AcademicScope",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamAttempts_Organization_ExamVersion_Id",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamAttempts_Organization_Id",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttempts_ContextKind_ContextId_ClassId_ExamId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttempts_ContextKind_ContextId_ExamId_ExamVersionId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttempts_ContextKind_Status_EffectiveDeadlineUtc",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttempts_PersonalClassId_PersonalClassMembershipId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "UX_ExamAttempts_Organization_Enrollment_ClientOperation",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "UX_ExamAttempts_Organization_Exam_Enrollment_Active",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "UX_ExamAttempts_Organization_Exam_Enrollment_Number",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_AcademicScope",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_FinalizationState",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_FinalizedBeforeDeadline",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_Participation",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_PersonalClass",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamAttemptQuestions_Organization_Attempt_Id_QuestionVersion",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttemptQuestions_ContextKind_ContextId_ExamAttemptId_Id_CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttemptQuestions_ContextKind_ContextId_ExamVersionId_ExamAttemptId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttemptQuestions_ContextKind_ContextId_ExamVersionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttemptQuestions_PersonalClassId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropIndex(
                name: "UX_ExamAttemptQuestions_Organization_Attempt_DisplayOrder",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropIndex(
                name: "UX_ExamAttemptQuestions_Organization_Attempt_Question",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttemptQuestions_AcademicScope",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_EvaluationRevisions_Organization_Attempt_Id",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropIndex(
                name: "IX_EvaluationRevisions_ContextKind_ContextId_SubmissionAttemptId_SupersedesEvaluationRevisionId",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropIndex(
                name: "IX_EvaluationRevisions_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropIndex(
                name: "UX_EvaluationRevisions_Attempt_RevisionNumber",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropIndex(
                name: "UX_EvaluationRevisions_OneCurrentStatus",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EvaluationRevisions_AcademicScope",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EvaluationRevisions_CreatedByActor",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AssignmentVersions_ContextKind_ContextId_Id",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_AssignmentVersions_Organization_Assignment_Id",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_AssignmentVersions_Organization_Class_Assignment_Id",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentVersions_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropIndex(
                name: "UX_AssignmentVersions_Organization_Assignment_VersionNumber",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentVersions_AcademicScope",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentVersions_CreatedByActor",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentVersions_PersonalClass",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Assignments_OrganizationId_ClassId_Id",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_Organization_Class_Status_UpdatedAt",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_OrganizationId_ClassId",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_PersonalClassId_CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assignments_AcademicScope",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assignments_CreatedByActor",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assignments_PersonalClass",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_AssignmentRecipients_SubmissionScope_Id",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentRecipients_ContextKind_ContextId_ClassId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentRecipients_PersonalClassId_PersonalClassMembershipId",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropIndex(
                name: "UX_AssignmentRecipients_Organization_Version_Enrollment",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentRecipients_AcademicScope",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentRecipients_Participation",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentRecipients_PersonalClass",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_AnswerRevisions_Organization_Attempt_Question_Id",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropIndex(
                name: "IX_AnswerRevisions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropIndex(
                name: "IX_AnswerRevisions_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_SupersedesAnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropIndex(
                name: "IX_AnswerRevisions_ContextKind_ContextId_QuestionVersionId_SelectedOptionId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropIndex(
                name: "IX_AnswerRevisions_PersonalClassId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropIndex(
                name: "UX_AnswerRevisions_Organization_Attempt_ClientOperation",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropIndex(
                name: "UX_AnswerRevisions_Organization_AttemptQuestion_Revision",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AnswerRevisions_AcademicScope",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropIndex(
                name: "IX_AnswerRevisionAttachments_ContextKind_ContextId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisionAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AnswerRevisionAttachments_ContextKind_ContextId_ExamAttemptId_FileAssetId",
                schema: "assessment",
                table: "AnswerRevisionAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AnswerRevisionAttachments_PersonalClassId",
                schema: "assessment",
                table: "AnswerRevisionAttachments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AnswerRevisionAttachments_AcademicScope",
                schema: "assessment",
                table: "AnswerRevisionAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AcademicActivityPublications_ContextKind_ContextId_AssignmentVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropIndex(
                name: "IX_AcademicActivityPublications_ContextKind_ContextId_ExamVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropIndex(
                name: "IX_AcademicActivityPublications_PersonalClassId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AcademicActivityPublications_AcademicScope",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AcademicActivityPublications_PersonalClass",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AcademicActivityPublications_PersonalKind",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000029"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000029"));

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropColumn(
                name: "ParticipantId",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropColumn(
                name: "PersonalClassMembershipId",
                schema: "assessment",
                table: "SubmissionAttempts");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "SubmissionAttachments");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "SubmissionAttachments");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "SubmissionAttachments");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "QuestionOptions");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "QuestionOptions");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "QuestionOptions");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropColumn(
                name: "ReleasedByPersonalMembershipId",
                schema: "assessment",
                table: "GradeReleases");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "storage",
                table: "FileAssets");

            migrationBuilder.DropColumn(
                name: "PersonalClassMembershipId",
                schema: "storage",
                table: "FileAssets");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropColumn(
                name: "CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamVersions");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropColumn(
                name: "ReviewedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamQuestionGrades");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropColumn(
                name: "CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamGradeRevisions");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropColumn(
                name: "ReleasedByPersonalMembershipId",
                schema: "assessment",
                table: "ExamGradeReleases");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamFinalAnswers");

            migrationBuilder.DropColumn(
                name: "AutoFinalizedAtUtc",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "ParticipantId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "PersonalClassMembershipId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropColumn(
                name: "CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "EvaluationRevisions");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropColumn(
                name: "CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "CreatedByPersonalMembershipId",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropColumn(
                name: "ParticipantId",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropColumn(
                name: "PersonalClassMembershipId",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "AnswerRevisions");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "assessment",
                table: "AnswerRevisionAttachments");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "assessment",
                table: "AnswerRevisionAttachments");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "assessment",
                table: "AnswerRevisionAttachments");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropColumn(
                name: "ContextKind",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "SubmissionAttempts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EnrollmentId",
                schema: "assessment",
                table: "SubmissionAttempts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "SubmissionAttachments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "QuestionVersions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "QuestionOptions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ReleasedByMembershipId",
                schema: "assessment",
                table: "GradeReleases",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "GradeReleases",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamVersions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "assessment",
                table: "ExamVersions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "Exams",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "assessment",
                table: "Exams",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ReleasedByMembershipId",
                schema: "assessment",
                table: "ExamGradeReleases",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamGradeReleases",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamAttempts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EnrollmentId",
                schema: "assessment",
                table: "ExamAttempts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "EvaluationRevisions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "assessment",
                table: "EvaluationRevisions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "AssignmentVersions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "assessment",
                table: "AssignmentVersions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "Assignments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "assessment",
                table: "Assignments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "AssignmentRecipients",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EnrollmentId",
                schema: "assessment",
                table: "AssignmentRecipients",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "AnswerRevisions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_SubmissionAttempts_Organization_Id",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_QuestionVersions_Organization_ExamVersion_Id",
                schema: "assessment",
                table: "QuestionVersions",
                columns: new[] { "OrganizationId", "ExamVersionId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_QuestionVersions_OrganizationId_Id",
                schema: "assessment",
                table: "QuestionVersions",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_QuestionOptions_Organization_QuestionVersion_Id",
                schema: "assessment",
                table: "QuestionOptions",
                columns: new[] { "OrganizationId", "QuestionVersionId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamVersions_Organization_Exam_Id",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "OrganizationId", "ExamId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamVersions_OrganizationId_Id",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Exams_Organization_Class_Id",
                schema: "assessment",
                table: "Exams",
                columns: new[] { "OrganizationId", "ClassId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Exams_OrganizationId_Id",
                schema: "assessment",
                table: "Exams",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamGradeRevisions_Organization_Attempt_Id",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "Id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExamFinalAnswers",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamAttempts_Organization_ExamVersion_Id",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "ExamVersionId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamAttempts_Organization_Id",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamAttemptQuestions_Organization_Attempt_Id_QuestionVersion",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "Id", "QuestionVersionId" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_EvaluationRevisions_Organization_Attempt_Id",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AssignmentVersions_OrganizationId_Id",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_AssignmentVersions_Organization_Assignment_Id",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "OrganizationId", "AssignmentId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_AssignmentVersions_Organization_Class_Assignment_Id",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "OrganizationId", "ClassId", "AssignmentId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Assignments_OrganizationId_ClassId_Id",
                schema: "assessment",
                table: "Assignments",
                columns: new[] { "OrganizationId", "ClassId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_AssignmentRecipients_SubmissionScope_Id",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "OrganizationId", "AssignmentId", "AssignmentVersionId", "EnrollmentId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_AnswerRevisions_Organization_Attempt_Question_Id",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttempts_Assignment_Status_SubmittedAtUtc",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "OrganizationId", "AssignmentId", "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttempts_OrganizationId_AssignmentId_AssignmentVersionId_EnrollmentId_AssignmentRecipientId",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "OrganizationId", "AssignmentId", "AssignmentVersionId", "EnrollmentId", "AssignmentRecipientId" });

            migrationBuilder.CreateIndex(
                name: "UX_SubmissionAttempts_OneDraft",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "OrganizationId", "AssignmentRecipientId", "AssignmentVersionId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_SubmissionAttempts_Recipient_Version_Number",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "OrganizationId", "AssignmentRecipientId", "AssignmentVersionId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SubmissionAttachments_Attempt_FileAsset",
                schema: "assessment",
                table: "SubmissionAttachments",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "FileAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_QuestionVersions_Organization_ExamVersion_Order",
                schema: "assessment",
                table: "QuestionVersions",
                columns: new[] { "OrganizationId", "ExamVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_QuestionOptions_Organization_Question_Order",
                schema: "assessment",
                table: "QuestionOptions",
                columns: new[] { "OrganizationId", "QuestionVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeReleases_Attempt_ReleasedAtUtc",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "ReleasedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GradeReleases_OrganizationId_SubmissionAttemptId_EvaluationRevisionId",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "EvaluationRevisionId" });

            migrationBuilder.CreateIndex(
                name: "UX_GradeReleases_Organization_EvaluationRevision",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "OrganizationId", "EvaluationRevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamVersions_Organization_Exam_ActiveDraft",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "OrganizationId", "ExamId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_ExamVersions_Organization_Exam_VersionNumber",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "OrganizationId", "ExamId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Organization_Class_Status",
                schema: "assessment",
                table: "Exams",
                columns: new[] { "OrganizationId", "ClassId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamQuestionGrades_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamQuestionGrades_Revision_AttemptQuestion",
                schema: "assessment",
                table: "ExamQuestionGrades",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamGradeRevisionId", "ExamAttemptQuestionId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamQuestionGrades_ReviewState",
                schema: "assessment",
                table: "ExamQuestionGrades",
                sql: "([IsReviewed] = 0 AND [ReviewedByMembershipId] IS NULL AND [ReviewedAtUtc] IS NULL) OR ([IsReviewed] = 1 AND [ReviewedByMembershipId] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ExamGradeRevisions_OrganizationId_ExamAttemptId_SupersedesExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "SupersedesExamGradeRevisionId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeRevisions_Attempt_RevisionNumber",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeRevisions_OneMutable",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId" },
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeRevisions_OneReleased",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "Status" },
                unique: true,
                filter: "[Status] = 3");

            migrationBuilder.CreateIndex(
                name: "IX_ExamGradeReleases_Attempt_ReleasedAtUtc",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ReleasedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamGradeReleases_OrganizationId_ExamAttemptId_ExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamGradeRevisionId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeReleases_Attempt_ClientOperation",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeReleases_Organization_Revision",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "OrganizationId", "ExamGradeRevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamFinalAnswers_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "AnswerRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamFinalAnswers_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_OrganizationId_ClassId_ExamId",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "ClassId", "ExamId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_OrganizationId_ExamId_ExamVersionId",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "ExamId", "ExamVersionId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttempts_Organization_Enrollment_ClientOperation",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "EnrollmentId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttempts_Organization_Exam_Enrollment_Active",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "ExamId", "EnrollmentId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttempts_Organization_Exam_Enrollment_Number",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "ExamId", "EnrollmentId", "AttemptNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_FinalizationState",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "([Status] = 3 AND [FinalizedAtUtc] IS NOT NULL AND [FinalizedAnswerSetVersion] IS NOT NULL AND [FinalizeClientOperationId] IS NOT NULL AND [FinalizeRequestHash] IS NOT NULL AND [FinalizedBySessionId] IS NOT NULL) OR ([Status] <> 3 AND [FinalizedAtUtc] IS NULL AND [FinalizedAnswerSetVersion] IS NULL AND [FinalizeClientOperationId] IS NULL AND [FinalizeRequestHash] IS NULL AND [FinalizedBySessionId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_FinalizedBeforeDeadline",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "[FinalizedAtUtc] IS NULL OR [FinalizedAtUtc] < [EffectiveDeadlineUtc]");

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttemptQuestions_OrganizationId_ExamAttemptId_Id_CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "Id", "CurrentAnswerRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttemptQuestions_OrganizationId_ExamVersionId_ExamAttemptId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamVersionId", "ExamAttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttemptQuestions_OrganizationId_ExamVersionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamVersionId", "QuestionVersionId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttemptQuestions_Organization_Attempt_DisplayOrder",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttemptQuestions_Organization_Attempt_Question",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "QuestionVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRevisions_OrganizationId_SubmissionAttemptId_SupersedesEvaluationRevisionId",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "SupersedesEvaluationRevisionId" });

            migrationBuilder.CreateIndex(
                name: "UX_EvaluationRevisions_Attempt_RevisionNumber",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EvaluationRevisions_OneCurrentStatus",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "Status" },
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "UX_AssignmentVersions_Organization_Assignment_VersionNumber",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "OrganizationId", "AssignmentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_Organization_Class_Status_UpdatedAt",
                schema: "assessment",
                table: "Assignments",
                columns: new[] { "OrganizationId", "ClassId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRecipients_OrganizationId_ClassId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "OrganizationId", "ClassId", "AssignmentId", "AssignmentVersionId" });

            migrationBuilder.CreateIndex(
                name: "UX_AssignmentRecipients_Organization_Version_Enrollment",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "OrganizationId", "AssignmentVersionId", "EnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_SupersedesAnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "SupersedesAnswerRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisions_OrganizationId_QuestionVersionId_SelectedOptionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "QuestionVersionId", "SelectedOptionId" });

            migrationBuilder.CreateIndex(
                name: "UX_AnswerRevisions_Organization_Attempt_ClientOperation",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AnswerRevisions_Organization_AttemptQuestion_Revision",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisionAttachments_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "AnswerRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisionAttachments_OrganizationId_ExamAttemptId_FileAssetId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                columns: new[] { "OrganizationId", "ExamAttemptId", "FileAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_OrganizationId_AssignmentVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "OrganizationId", "AssignmentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_OrganizationId_ExamVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "OrganizationId", "ExamVersionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicActivityPublications_AssignmentVersions_OrganizationId_AssignmentVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "OrganizationId", "AssignmentVersionId" },
                principalSchema: "assessment",
                principalTable: "AssignmentVersions",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicActivityPublications_ExamVersions_OrganizationId_ExamVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "OrganizationId", "ExamVersionId" },
                principalSchema: "assessment",
                principalTable: "ExamVersions",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnswerRevisionAttachments_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "AnswerRevisionId" },
                principalSchema: "assessment",
                principalTable: "AnswerRevisions",
                principalColumns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnswerRevisions_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_SupersedesAnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "SupersedesAnswerRevisionId" },
                principalSchema: "assessment",
                principalTable: "AnswerRevisions",
                principalColumns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnswerRevisions_ExamAttemptQuestions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" },
                principalSchema: "assessment",
                principalTable: "ExamAttemptQuestions",
                principalColumns: new[] { "OrganizationId", "ExamAttemptId", "Id", "QuestionVersionId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnswerRevisions_ExamAttempts_OrganizationId_ExamAttemptId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId" },
                principalSchema: "assessment",
                principalTable: "ExamAttempts",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnswerRevisions_QuestionOptions_OrganizationId_QuestionVersionId_SelectedOptionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "QuestionVersionId", "SelectedOptionId" },
                principalSchema: "assessment",
                principalTable: "QuestionOptions",
                principalColumns: new[] { "OrganizationId", "QuestionVersionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentRecipients_AssignmentVersions_OrganizationId_ClassId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "OrganizationId", "ClassId", "AssignmentId", "AssignmentVersionId" },
                principalSchema: "assessment",
                principalTable: "AssignmentVersions",
                principalColumns: new[] { "OrganizationId", "ClassId", "AssignmentId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentVersions_Assignments_OrganizationId_ClassId_AssignmentId",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "OrganizationId", "ClassId", "AssignmentId" },
                principalSchema: "assessment",
                principalTable: "Assignments",
                principalColumns: new[] { "OrganizationId", "ClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationRevisions_EvaluationRevisions_OrganizationId_SubmissionAttemptId_SupersedesEvaluationRevisionId",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "SupersedesEvaluationRevisionId" },
                principalSchema: "assessment",
                principalTable: "EvaluationRevisions",
                principalColumns: new[] { "OrganizationId", "SubmissionAttemptId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaluationRevisions_SubmissionAttempts_OrganizationId_SubmissionAttemptId",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "OrganizationId", "SubmissionAttemptId" },
                principalSchema: "assessment",
                principalTable: "SubmissionAttempts",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttemptQuestions_AnswerRevisions_OrganizationId_ExamAttemptId_Id_CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "Id", "CurrentAnswerRevisionId" },
                principalSchema: "assessment",
                principalTable: "AnswerRevisions",
                principalColumns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttemptQuestions_ExamAttempts_OrganizationId_ExamVersionId_ExamAttemptId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamVersionId", "ExamAttemptId" },
                principalSchema: "assessment",
                principalTable: "ExamAttempts",
                principalColumns: new[] { "OrganizationId", "ExamVersionId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttemptQuestions_QuestionVersions_OrganizationId_ExamVersionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamVersionId", "QuestionVersionId" },
                principalSchema: "assessment",
                principalTable: "QuestionVersions",
                principalColumns: new[] { "OrganizationId", "ExamVersionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttempts_ExamVersions_OrganizationId_ExamId_ExamVersionId",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "ExamId", "ExamVersionId" },
                principalSchema: "assessment",
                principalTable: "ExamVersions",
                principalColumns: new[] { "OrganizationId", "ExamId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttempts_Exams_OrganizationId_ClassId_ExamId",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "ClassId", "ExamId" },
                principalSchema: "assessment",
                principalTable: "Exams",
                principalColumns: new[] { "OrganizationId", "ClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamFinalAnswers_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "AnswerRevisionId" },
                principalSchema: "assessment",
                principalTable: "AnswerRevisions",
                principalColumns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamFinalAnswers_ExamAttemptQuestions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" },
                principalSchema: "assessment",
                principalTable: "ExamAttemptQuestions",
                principalColumns: new[] { "OrganizationId", "ExamAttemptId", "Id", "QuestionVersionId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamFinalAnswers_ExamAttempts_OrganizationId_ExamAttemptId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "OrganizationId", "ExamAttemptId" },
                principalSchema: "assessment",
                principalTable: "ExamAttempts",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamGradeReleases_ExamGradeRevisions_OrganizationId_ExamAttemptId_ExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamGradeRevisionId" },
                principalSchema: "assessment",
                principalTable: "ExamGradeRevisions",
                principalColumns: new[] { "OrganizationId", "ExamAttemptId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamGradeRevisions_ExamAttempts_OrganizationId_ExamAttemptId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId" },
                principalSchema: "assessment",
                principalTable: "ExamAttempts",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamGradeRevisions_ExamGradeRevisions_OrganizationId_ExamAttemptId_SupersedesExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "SupersedesExamGradeRevisionId" },
                principalSchema: "assessment",
                principalTable: "ExamGradeRevisions",
                principalColumns: new[] { "OrganizationId", "ExamAttemptId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestionGrades_ExamAttemptQuestions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" },
                principalSchema: "assessment",
                principalTable: "ExamAttemptQuestions",
                principalColumns: new[] { "OrganizationId", "ExamAttemptId", "Id", "QuestionVersionId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestionGrades_ExamGradeRevisions_OrganizationId_ExamAttemptId_ExamGradeRevisionId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamGradeRevisionId" },
                principalSchema: "assessment",
                principalTable: "ExamGradeRevisions",
                principalColumns: new[] { "OrganizationId", "ExamAttemptId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamVersions_Exams_OrganizationId_ExamId",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "OrganizationId", "ExamId" },
                principalSchema: "assessment",
                principalTable: "Exams",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GradeReleases_EvaluationRevisions_OrganizationId_SubmissionAttemptId_EvaluationRevisionId",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "EvaluationRevisionId" },
                principalSchema: "assessment",
                principalTable: "EvaluationRevisions",
                principalColumns: new[] { "OrganizationId", "SubmissionAttemptId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionOptions_QuestionVersions_OrganizationId_QuestionVersionId",
                schema: "assessment",
                table: "QuestionOptions",
                columns: new[] { "OrganizationId", "QuestionVersionId" },
                principalSchema: "assessment",
                principalTable: "QuestionVersions",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionVersions_ExamVersions_OrganizationId_ExamVersionId",
                schema: "assessment",
                table: "QuestionVersions",
                columns: new[] { "OrganizationId", "ExamVersionId" },
                principalSchema: "assessment",
                principalTable: "ExamVersions",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAttachments_SubmissionAttempts_OrganizationId_SubmissionAttemptId",
                schema: "assessment",
                table: "SubmissionAttachments",
                columns: new[] { "OrganizationId", "SubmissionAttemptId" },
                principalSchema: "assessment",
                principalTable: "SubmissionAttempts",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAttempts_AssignmentRecipients_OrganizationId_AssignmentId_AssignmentVersionId_EnrollmentId_AssignmentRecipientId",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "OrganizationId", "AssignmentId", "AssignmentVersionId", "EnrollmentId", "AssignmentRecipientId" },
                principalSchema: "assessment",
                principalTable: "AssignmentRecipients",
                principalColumns: new[] { "OrganizationId", "AssignmentId", "AssignmentVersionId", "EnrollmentId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAttempts_AssignmentVersions_OrganizationId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "OrganizationId", "AssignmentId", "AssignmentVersionId" },
                principalSchema: "assessment",
                principalTable: "AssignmentVersions",
                principalColumns: new[] { "OrganizationId", "AssignmentId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
