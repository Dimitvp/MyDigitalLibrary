using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class WishlistEntryConfiguration : IEntityTypeConfiguration<WishlistEntry>
{
    public void Configure(EntityTypeBuilder<WishlistEntry> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.UserId).IsRequired();
        builder.Property(w => w.WorkId).IsRequired();
        builder.Property(w => w.DesiredFormat).IsRequired();
        builder.Property(w => w.Priority).IsRequired();
        builder.Property(w => w.AddedOn).IsRequired();
        builder.Property(w => w.Note);
        builder.Property(w => w.IsOutOfStock).IsRequired();

        builder.OwnsOne(w => w.MaxPrice, price =>
        {
            price.Property(p => p.Amount).HasColumnName("max_price_amount").HasColumnType("numeric(12,2)");
            price.Property(p => p.CurrencyCode).HasColumnName("max_price_currency").HasMaxLength(3);
        });

        builder.HasIndex(w => new { w.UserId, w.IsFulfilled });
    }
}
