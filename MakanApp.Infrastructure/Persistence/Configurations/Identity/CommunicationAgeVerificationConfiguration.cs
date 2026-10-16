using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Identity;

public sealed class CommunicationAgeVerificationConfiguration : IEntityTypeConfiguration<CommunicationAgeVerification>
{
    public void Configure(EntityTypeBuilder<CommunicationAgeVerification> builder)
    {
        builder.ToTable("CommunicationAgeVerifications", "identity", table =>
        {
            table.HasCheckConstraint("CK_CommunicationAgeVerifications_Categories", "[PreviousCategory] IN (0,1,2) AND [ProposedCategory] IN (1,2)");
            table.HasCheckConstraint("CK_CommunicationAgeVerifications_Status", "[Status] IN (1,2)");
            table.HasCheckConstraint("CK_CommunicationAgeVerifications_Source", "([Source]=1 AND [OrganizationId] IS NOT NULL) OR ([Source] IN (2,3) AND [OrganizationId] IS NULL)");
        });
        builder.HasKey(v => v.Id);
        builder.Property(v => v.VerifiedBirthDate).HasColumnType("date");
        builder.Property(v => v.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(v => v.Reason).HasMaxLength(500);
        builder.HasIndex(v => new { v.UserId, v.CreatedAtUtc, v.Id });
        builder.HasOne<User>().WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(v => v.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MakanApp.Domain.Organization.Organization>().WithMany().HasForeignKey(v => v.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
