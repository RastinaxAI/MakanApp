using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class PersonalClassRequestConfiguration : IEntityTypeConfiguration<PersonalClassRequest>
{
    public void Configure(EntityTypeBuilder<PersonalClassRequest> b)
    {
        b.ToTable("PersonalClassRequests", "academic", t =>
        {
            t.HasCheckConstraint("CK_PersonalClassRequests_Status", "[Status] BETWEEN 1 AND 7");
            t.HasCheckConstraint("CK_PersonalClassRequests_Capacity", "[SuggestedCapacity]>0");
            t.HasCheckConstraint("CK_PersonalClassRequests_Approval", "([Status]=5 AND [CreatedClassId] IS NOT NULL AND [FinalManagerUserId] IS NOT NULL) OR ([Status]<>5 AND [CreatedClassId] IS NULL AND [FinalManagerUserId] IS NULL)");
        });
        b.HasKey(x => x.Id); b.Property(x => x.RowVersion).IsRowVersion();
        b.Ignore(x => x.CanEdit); b.Ignore(x => x.CanCancel);
        b.Property(x => x.CreationPayloadHash).HasMaxLength(64).IsFixedLength();
        b.Property(x => x.Title).HasMaxLength(200); b.Property(x => x.Purpose).HasMaxLength(2000);
        b.Property(x => x.Description).HasMaxLength(4000); b.Property(x => x.DecisionReason).HasMaxLength(500);
        b.HasIndex(x => new { x.RequesterUserId, x.ClientOperationId }).IsUnique();
        b.HasIndex(x => new { x.Status, x.CreatedAtUtc, x.Id });
        b.HasIndex(x => x.CreatedClassId).IsUnique().HasFilter("[CreatedClassId] IS NOT NULL");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RequesterUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.SuggestedManagerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.SuggestedTeacherUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.FinalManagerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PersonalClass>().WithMany().HasForeignKey(x => x.CreatedClassId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PersonalClassJoinRequestConfiguration : IEntityTypeConfiguration<PersonalClassJoinRequest>
{
    public void Configure(EntityTypeBuilder<PersonalClassJoinRequest> b)
    {
        b.ToTable("PersonalClassJoinRequests", "academic", t =>
        {
            t.HasCheckConstraint("CK_PersonalClassJoinRequests_Status", "[Status] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_PersonalClassJoinRequests_Closure", "([Status]=1 AND [ClosedAtUtc] IS NULL AND [ClosureReason] IS NULL) OR ([Status]<>1 AND [ClosedAtUtc] IS NOT NULL AND [ClosedAtUtc]>=[CreatedAtUtc] AND [ClosureReason] IS NOT NULL)");
            t.HasCheckConstraint("CK_PersonalClassJoinRequests_Membership", "([Status]=2 AND [MembershipId] IS NOT NULL) OR ([Status]<>2 AND [MembershipId] IS NULL)");
        });
        b.HasKey(x => x.Id); b.Property(x => x.RowVersion).IsRowVersion(); b.Property(x => x.ClosureReason).HasMaxLength(500);
        b.HasIndex(x => new { x.PersonalClassId, x.UserId }).IsUnique().HasFilter("[Status]=1");
        b.HasIndex(x => new { x.UserId, x.ClientOperationId }).IsUnique();
        b.HasOne<PersonalClass>().WithMany().HasForeignKey(x => x.PersonalClassId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PersonalClassParticipant>().WithMany().HasForeignKey(x => new { x.PersonalClassId, x.UserId, x.MembershipId })
            .HasPrincipalKey(x => new { x.PersonalClassId, x.StudentUserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PersonalClassManagerTransferConfiguration : IEntityTypeConfiguration<PersonalClassManagerTransfer>
{
    public void Configure(EntityTypeBuilder<PersonalClassManagerTransfer> b)
    {
        b.ToTable("PersonalClassManagerTransfers", "academic", t =>
        {
            t.HasCheckConstraint("CK_PersonalClassManagerTransfers_Status", "[Status] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_PersonalClassManagerTransfers_Target", "[PreviousManagerUserId] IS NULL OR [PreviousManagerUserId]<>[TargetUserId]");
            t.HasCheckConstraint("CK_PersonalClassManagerTransfers_Decision", "([Status]=1 AND [DecidedAtUtc] IS NULL) OR ([Status]<>1 AND [DecidedAtUtc] IS NOT NULL AND [DecidedAtUtc]>=[CreatedAtUtc])");
        });
        b.HasKey(x => x.Id); b.Property(x => x.RowVersion).IsRowVersion(); b.Property(x => x.Reason).HasMaxLength(500);
        b.HasIndex(x => x.PersonalClassId).IsUnique().HasFilter("[Status]=1");
        b.HasIndex(x => new { x.PersonalClassId, x.ClientOperationId }).IsUnique();
        b.HasOne<PersonalClass>().WithMany().HasForeignKey(x => x.PersonalClassId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.PreviousManagerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.TargetUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
