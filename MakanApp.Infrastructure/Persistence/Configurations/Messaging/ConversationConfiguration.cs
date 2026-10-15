using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationEntity = MakanApp.Domain.Organization.Organization;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations", "messaging", table =>
        {
            table.HasCheckConstraint("CK_Conversations_AcademicClass", "[AcademicClassId] IS NULL OR ([ManagementPolicy]=2 AND [Type]=2 AND [Scope]=2 AND [OrganizationId] IS NOT NULL)");
            table.HasCheckConstraint("CK_Conversations_Type", "[Type] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_Conversations_Scope", "[Scope] IN (1, 2)");
            table.HasCheckConstraint("CK_Conversations_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint("CK_Conversations_ManagementPolicy", "[ManagementPolicy] IN (0, 1, 2)");
            table.HasCheckConstraint("CK_Conversations_Visibility", "[Visibility] IN (1, 2)");
            table.HasCheckConstraint("CK_Conversations_GroupHistoryPolicy", "[GroupHistoryPolicy] IN (1, 2)");
            table.HasCheckConstraint("CK_Conversations_MemberCapabilities", "[MemberCapabilities] BETWEEN 0 AND 7");
            table.HasCheckConstraint(
                "CK_Conversations_ScopeOrganization",
                "([Scope] = 1 AND [OrganizationId] IS NULL) OR ([Scope] = 2 AND [OrganizationId] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Conversations_DirectPair",
                "[Type] <> 1 OR ([DirectUserLowId] IS NOT NULL AND [DirectUserHighId] IS NOT NULL AND [DirectUserLowId] <> [DirectUserHighId])");
            table.HasCheckConstraint(
                "CK_Conversations_SavedShape",
                "([Type] = 4 AND [Scope] = 1 AND [OrganizationId] IS NULL AND [SavedOwnerUserId] IS NOT NULL AND [DirectUserLowId] IS NULL AND [DirectUserHighId] IS NULL AND [ManagementPolicy] = 0 AND [Title] IS NULL) OR ([Type] <> 4 AND [SavedOwnerUserId] IS NULL)");
            table.HasCheckConstraint(
                "CK_Conversations_ManagedShape",
                "([Type] IN (1, 4) AND [ManagementPolicy] = 0 AND [Title] IS NULL) OR ([Type] IN (2, 3) AND [ManagementPolicy] IN (1, 2) AND LEN(LTRIM(RTRIM([Title]))) > 0 AND [DirectUserLowId] IS NULL AND [DirectUserHighId] IS NULL)");
            table.HasCheckConstraint(
                "CK_Conversations_UserManagedCreation",
                "([ManagementPolicy] <> 1) OR ([CreatedByUserId] IS NOT NULL AND [ClientOperationId] IS NOT NULL AND [CreationPayloadHash] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Conversations_SystemManagedAcademic",
                "([ManagementPolicy] <> 2) OR ([Scope] = 2 AND [CreatedByUserId] IS NULL AND [ClientOperationId] IS NULL AND [CreationPayloadHash] IS NULL)");
            table.HasCheckConstraint("CK_Conversations_NextMessageSequence", "[NextMessageSequence] > 0");
            table.HasCheckConstraint("CK_Conversations_NextChangeSequence", "[NextChangeSequence] > 0");
            table.HasCheckConstraint(
                "CK_Conversations_BehaviorShape",
                "([Type] IN (1, 4) AND [Visibility] = 1 AND [GroupHistoryPolicy] = 1 AND [MemberCapabilities] = 0) OR " +
                "([Type] = 2) OR " +
                "([Type] = 3 AND [GroupHistoryPolicy] = 1 AND [MemberCapabilities] = 0)");
            table.HasCheckConstraint(
                "CK_Conversations_SystemManagedVisibility",
                "[ManagementPolicy] <> 2 OR [Visibility] = 1");
        });
        builder.HasKey(conversation => conversation.Id);
        builder.HasIndex(c => c.OrganizationId);
        builder.HasIndex(c => new { c.OrganizationId, c.AcademicClassId }).IsUnique().HasFilter("[AcademicClassId] IS NOT NULL");
        builder.HasOne<MakanApp.Domain.Academic.Class>().WithMany()
            .HasForeignKey(c => new { c.OrganizationId, c.AcademicClassId })
            .HasPrincipalKey(c => new { c.OrganizationId, c.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Property(conversation => conversation.Title)
            .HasMaxLength(Conversation.TitleMaximumLength);
        builder.Property(conversation => conversation.Description)
            .HasMaxLength(Conversation.DescriptionMaximumLength);
        builder.Property(conversation => conversation.CreationPayloadHash)
            .HasMaxLength(Conversation.CreationPayloadHashLength)
            .IsFixedLength();
        builder.Property(conversation => conversation.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(conversation => conversation.ArchivedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(conversation => conversation.RowVersion).IsRowVersion();

        builder.HasIndex(conversation => new
        {
            conversation.Scope,
            conversation.OrganizationId,
            conversation.DirectUserLowId,
            conversation.DirectUserHighId
        })
            .IsUnique()
            .HasFilter("[Type] = 1")
            .HasDatabaseName("UX_Conversations_Direct_Scope_Organization_Pair");
        builder.HasIndex(conversation => conversation.SavedOwnerUserId)
            .IsUnique()
            .HasFilter("[Type] = 4")
            .HasDatabaseName("UX_Conversations_Saved_Owner");
        builder.HasIndex(conversation => new
        {
            conversation.CreatedByUserId,
            conversation.ClientOperationId
        })
            .IsUnique()
            .HasFilter("[ManagementPolicy] = 1")
            .HasDatabaseName("UX_Conversations_Creator_ClientOperationId");
        builder.HasIndex(conversation => new
        {
            conversation.Scope,
            conversation.OrganizationId,
            conversation.Visibility,
            conversation.Type,
            conversation.Status,
            conversation.CreatedAtUtc,
            conversation.Id
        }).HasDatabaseName("IX_Conversations_PublicDiscovery");

        builder.HasOne<OrganizationEntity>()
            .WithMany()
            .HasForeignKey(conversation => conversation.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(conversation => conversation.DirectUserLowId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(conversation => conversation.SavedOwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(conversation => conversation.DirectUserHighId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(conversation => conversation.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
