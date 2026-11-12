using MakanApp.Domain.Identity;
using MakanApp.Domain.Intelligence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Ai;

public sealed class AssistantContextSelectionConfiguration : IEntityTypeConfiguration<AssistantContextSelection>
{
    public void Configure(EntityTypeBuilder<AssistantContextSelection> b)
    {
        b.ToTable("AssistantContextSelections", "intelligence", t => {
            t.HasCheckConstraint("CK_AssistantContextSelections_Kind", "[Kind] IN (1,2,3)");
            t.HasCheckConstraint("CK_AssistantContextSelections_Scope", "([Kind]=1 AND [OrganizationId] IS NULL AND [MembershipId] IS NULL AND [Role] IS NULL AND [SubjectOrganizationPersonId] IS NULL AND [ClassId] IS NULL) OR ([Kind]=2 AND [OrganizationId] IS NOT NULL AND [MembershipId] IS NOT NULL AND [Role] IS NOT NULL AND [ClassId] IS NULL) OR ([Kind]=3 AND [OrganizationId] IS NOT NULL AND [MembershipId] IS NOT NULL AND [Role] IS NOT NULL AND [ClassId] IS NOT NULL)");
        });
        b.HasKey(s => s.SessionId); b.Property(s => s.RowVersion).IsRowVersion();
        b.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserSession>().WithOne().HasForeignKey<AssistantContextSelection>(s => new { s.SessionId, s.UserId })
            .HasPrincipalKey<UserSession>(s => new { s.Id, s.UserId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MakanApp.Domain.Organization.Organization>().WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MakanApp.Domain.Organization.Membership>().WithMany().HasForeignKey(s => s.MembershipId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MakanApp.Domain.Organization.OrganizationPerson>().WithMany().HasForeignKey(s => s.SubjectOrganizationPersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MakanApp.Domain.Academic.Class>().WithMany().HasForeignKey(s => s.ClassId).OnDelete(DeleteBehavior.Restrict);
    }
}
