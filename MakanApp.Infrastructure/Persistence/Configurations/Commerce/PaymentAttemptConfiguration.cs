using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

public sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("PaymentAttempts", "commerce", table =>
        {
            table.HasCheckConstraint("CK_PaymentAttempts_Status", "[Status] BETWEEN 1 AND 6");
            table.HasCheckConstraint("CK_PaymentAttempts_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_PaymentAttempts_Currency", "[Currency] = 'IRR'");
            table.HasCheckConstraint(
                "CK_PaymentAttempts_RequestPayloadHash",
                $"DATALENGTH([RequestPayloadHash]) = {PaymentAttempt.RequestPayloadHashLength}");
            table.HasCheckConstraint(
                "CK_PaymentAttempts_Claim",
                "([Status] = 2 AND [InitiationClaimToken] IS NOT NULL AND [InitiationClaimedUntilUtc] IS NOT NULL) OR " +
                "([Status] <> 2 AND [InitiationClaimToken] IS NULL AND [InitiationClaimedUntilUtc] IS NULL)");
        });
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Provider)
            .HasMaxLength(PaymentAttempt.MaximumProviderLength)
            .IsRequired();
        builder.Property(attempt => attempt.Amount).HasPrecision(18, 2);
        builder.Property(attempt => attempt.Currency)
            .HasConversion(new ValueConverter<CurrencyCode, string>(
                value => CurrencyPersistence.ToDatabase(value),
                value => CurrencyPersistence.FromDatabase(value)))
            .HasColumnType("char(3)");
        builder.Property(attempt => attempt.RequestPayloadHash)
            .HasColumnType("binary(32)")
            .IsRequired();
        builder.Property(attempt => attempt.ProviderReference)
            .HasMaxLength(PaymentAttempt.MaximumProviderReferenceLength);
        builder.Property(attempt => attempt.PaymentUrl)
            .HasMaxLength(PaymentAttempt.MaximumPaymentUrlLength);
        builder.Property(attempt => attempt.FailureCode)
            .HasMaxLength(PaymentAttempt.MaximumFailureCodeLength);
        builder.Property(attempt => attempt.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.InitiatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.VerifiedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.CompletedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.InitiationClaimedUntilUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.RowVersion).IsRowVersion();
        builder.HasIndex(attempt => new { attempt.OrderId, attempt.ClientOperationId })
            .IsUnique()
            .HasDatabaseName("UX_PaymentAttempts_Order_ClientOperation");
        builder.HasIndex(attempt => new { attempt.Provider, attempt.ProviderReference })
            .IsUnique()
            .HasFilter("[ProviderReference] IS NOT NULL")
            .HasDatabaseName("UX_PaymentAttempts_Provider_Reference");
        builder.HasIndex(attempt => new { attempt.OrderId, attempt.Status })
            .HasDatabaseName("IX_PaymentAttempts_Order_Status");
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(attempt => attempt.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
