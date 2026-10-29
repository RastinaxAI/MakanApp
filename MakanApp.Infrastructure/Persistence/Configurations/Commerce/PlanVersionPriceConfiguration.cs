using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

public sealed class PlanVersionPriceConfiguration : IEntityTypeConfiguration<PlanVersionPrice>
{
    public void Configure(EntityTypeBuilder<PlanVersionPrice> builder)
    {
        builder.ToTable("PlanVersionPrices", "commerce", table =>
        {
            table.HasCheckConstraint("CK_PlanVersionPrices_Amount", "[Amount] >= 0");
            table.HasCheckConstraint("CK_PlanVersionPrices_Currency", "[Currency] = 'IRR'");
        });
        builder.HasKey(price => price.PlanVersionId);
        builder.Property(price => price.Amount).HasPrecision(18, 2);
        builder.Property(price => price.Currency)
            .HasConversion(new ValueConverter<CurrencyCode, string>(
                value => CurrencyPersistence.ToDatabase(value),
                value => CurrencyPersistence.FromDatabase(value)))
            .HasColumnType("char(3)");
        builder.Property(price => price.RowVersion).IsRowVersion();
        builder.HasOne<PlanVersion>()
            .WithOne(version => version.Price)
            .HasForeignKey<PlanVersionPrice>(price => price.PlanVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
