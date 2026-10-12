using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", "commerce", table =>
        {
            table.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_Payments_Currency", "[Currency] = 'IRR'");
        });
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Provider)
            .HasMaxLength(PaymentAttempt.MaximumProviderLength)
            .IsRequired();
        builder.Property(payment => payment.ProviderReference)
            .HasMaxLength(PaymentAttempt.MaximumProviderReferenceLength)
            .IsRequired();
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.Currency)
            .HasConversion(new ValueConverter<CurrencyCode, string>(
                value => CurrencyPersistence.ToDatabase(value),
                value => CurrencyPersistence.FromDatabase(value)))
            .HasColumnType("char(3)");
        builder.Property(payment => payment.PaidAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(payment => payment.OrderId)
            .IsUnique()
            .HasDatabaseName("UX_Payments_Order");
        builder.HasIndex(payment => payment.PaymentAttemptId)
            .IsUnique()
            .HasDatabaseName("UX_Payments_Attempt");
        builder.HasIndex(payment => new { payment.Provider, payment.ProviderReference })
            .IsUnique()
            .HasDatabaseName("UX_Payments_Provider_Reference");
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(payment => payment.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentAttempt>()
            .WithMany()
            .HasForeignKey(payment => payment.PaymentAttemptId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
