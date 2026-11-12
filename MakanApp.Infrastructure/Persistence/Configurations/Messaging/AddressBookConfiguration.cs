using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class AddressBookEntryConfiguration : IEntityTypeConfiguration<AddressBookEntry>
{
    public void Configure(EntityTypeBuilder<AddressBookEntry> b)
    {
        b.ToTable("AddressBookEntries", "messaging", t => {
            t.HasCheckConstraint("CK_AddressBookEntries_Version", "[ServerVersion] > 0");
            t.HasCheckConstraint("CK_AddressBookEntries_Tombstone", "[DeletedAtUtc] IS NULL OR ([ProtectedContent] = N'' AND [LinkedAccountUserId] IS NULL)");
        });
        b.HasKey(e => new { e.OwnerUserId, e.Id });
        b.Property(e => e.ProtectedContent).HasMaxLength(16384).IsRequired();
        b.Property(e => e.RowVersion).IsRowVersion();
        b.HasIndex(e => new { e.OwnerUserId, e.ServerVersion }).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.LinkedAccountUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ContactPhoneConfiguration : IEntityTypeConfiguration<ContactPhone>
{
    public void Configure(EntityTypeBuilder<ContactPhone> b)
    {
        b.ToTable("ContactPhones", "messaging");
        b.HasKey(e => new { e.OwnerUserId, e.EntryId, e.PhoneIndex });
        b.Property(e => e.PhoneIndex).HasMaxLength(64).IsUnicode(false);
        b.HasOne<AddressBookEntry>().WithMany().HasForeignKey(e => new { e.OwnerUserId, e.EntryId }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.OwnerUserId, e.PhoneIndex });
    }
}
public sealed class ContactSyncDeviceConfiguration : IEntityTypeConfiguration<ContactSyncDevice>
{
    public void Configure(EntityTypeBuilder<ContactSyncDevice> b)
    {
        b.ToTable("ContactSyncDevices", "messaging", t => t.HasCheckConstraint("CK_ContactSyncDevices_Revision", "[LastSourceRevision] >= 0"));
        b.HasKey(e => new { e.OwnerUserId, e.DeviceId });
        b.Property(e => e.ConsentVersion).HasMaxLength(64); b.Property(e => e.Scope).HasMaxLength(64);
        b.Property(e => e.RowVersion).IsRowVersion();
        b.HasOne<User>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ContactSyncStateConfiguration : IEntityTypeConfiguration<ContactSyncState>
{
    public void Configure(EntityTypeBuilder<ContactSyncState> b)
    {
        b.ToTable("ContactSyncStates", "messaging", t => t.HasCheckConstraint("CK_ContactSyncStates_Version", "[ServerVersion] >= 0"));
        b.HasKey(e => e.OwnerUserId);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ContactDeviceBindingConfiguration : IEntityTypeConfiguration<ContactDeviceBinding>
{
    public void Configure(EntityTypeBuilder<ContactDeviceBinding> b)
    {
        b.ToTable("ContactDeviceBindings", "messaging", t => t.HasCheckConstraint("CK_ContactDeviceBindings_Versions", "[SourceRevision] > 0 AND [ServerVersion] > 0"));
        b.HasKey(e => new { e.OwnerUserId, e.DeviceId, e.EntryId });
        b.HasOne<AddressBookEntry>().WithMany().HasForeignKey(e => new { e.OwnerUserId, e.EntryId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ContactSyncDevice>().WithMany().HasForeignKey(e => new { e.OwnerUserId, e.DeviceId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ContactSyncOperationConfiguration : IEntityTypeConfiguration<ContactSyncOperation>
{
    public void Configure(EntityTypeBuilder<ContactSyncOperation> b)
    {
        b.ToTable("ContactSyncOperations", "messaging");
        b.HasKey(e => new { e.OwnerUserId, e.DeviceId, e.ClientOperationId });
        b.Property(e => e.RequestFingerprint).HasMaxLength(64).IsUnicode(false);
        b.Property(e => e.Receipt).HasMaxLength(65536);
        b.HasOne<ContactSyncDevice>().WithMany().HasForeignKey(e => new { e.OwnerUserId, e.DeviceId }).OnDelete(DeleteBehavior.Restrict);
    }
}
