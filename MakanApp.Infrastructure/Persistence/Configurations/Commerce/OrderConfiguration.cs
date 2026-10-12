using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", "commerce", table =>
        {
            table.HasCheckConstraint("CK_Orders_Status", "[Status] BETWEEN 1 AND 3");
            table.HasCheckConstraint("CK_Orders_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_Orders_Currency", "[Currency] = 'IRR'");
            table.HasCheckConstraint(
                "CK_Orders_RequestPayloadHash",
                $"DATALENGTH([RequestPayloadHash]) = {Order.RequestPayloadHashLength}");
            table.HasCheckConstraint(
                "CK_Orders_Lifecycle",
                "([Status] = 1 AND [PaidAtUtc] IS NULL AND [CancelledAtUtc] IS NULL) OR " +
                "([Status] = 2 AND [PaidAtUtc] IS NOT NULL AND [CancelledAtUtc] IS NULL) OR " +
                "([Status] = 3 AND [PaidAtUtc] IS NULL AND [CancelledAtUtc] IS NOT NULL)");
        });
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Amount).HasPrecision(18, 2);
        builder.Property(order => order.Currency)
            .HasConversion(new ValueConverter<CurrencyCode, string>(
                value => CurrencyPersistence.ToDatabase(value),
                value => CurrencyPersistence.FromDatabase(value)))
            .HasColumnType("char(3)");
        builder.Property(order => order.RequestPayloadHash)
            .HasColumnType("binary(32)")
            .IsRequired();
        builder.Property(order => order.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(order => order.PaidAtUtc).HasColumnType("datetime2(7)");
        builder.Property(order => order.CancelledAtUtc).HasColumnType("datetime2(7)");
        builder.Property(order => order.RowVersion).IsRowVersion();
        builder.HasIndex(order => new { order.PurchaserUserId, order.ClientOperationId })
            .IsUnique()
            .HasDatabaseName("UX_Orders_Purchaser_ClientOperation");
        builder.HasIndex(order => new { order.OrganizationId, order.Status, order.CreatedAtUtc })
            .HasDatabaseName("IX_Orders_Organization_Status_CreatedAt");
        builder.HasIndex(order => new { order.PurchaserUserId, order.CreatedAtUtc })
            .HasDatabaseName("IX_Orders_Purchaser_CreatedAt");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(order => order.PurchaserUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(order => order.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlanVersion>()
            .WithMany()
            .HasForeignKey(order => order.PlanVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
