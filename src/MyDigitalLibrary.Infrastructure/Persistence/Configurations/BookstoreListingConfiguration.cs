using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Catalog;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class BookstoreListingConfiguration : IEntityTypeConfiguration<BookstoreListing>
{
    public void Configure(EntityTypeBuilder<BookstoreListing> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.EditionId).IsRequired();
        builder.Property(l => l.BookstoreId).IsRequired();
        builder.Property(l => l.Availability).IsRequired();
        builder.Property(l => l.ConsecutiveFailures).IsRequired();

        builder.Property(l => l.Url)
            .HasConversion(url => url.ToString(), value => new Uri(value))
            .HasMaxLength(1000)
            .IsRequired();

        builder.OwnsOne(l => l.Price, price =>
        {
            price.Property(p => p.Amount).HasColumnName("price_amount").HasColumnType("numeric(12,2)");
            price.Property(p => p.CurrencyCode).HasColumnName("price_currency").HasMaxLength(3);
        });

        builder.OwnsMany(l => l.PriceHistory, history =>
        {
            history.ToTable("bookstore_listing_price_history");
            history.WithOwner().HasForeignKey(x => x.ListingId);
            history.HasKey(x => x.Id);
            history.Property(x => x.ObservedAt).IsRequired();

            history.OwnsOne(x => x.Price, price =>
            {
                price.Property(p => p.Amount).HasColumnName("price_amount").HasColumnType("numeric(12,2)").IsRequired();
                price.Property(p => p.CurrencyCode).HasColumnName("price_currency").HasMaxLength(3).IsRequired();
            });

            history.Navigation(x => x.Price).IsRequired();
        });
        builder.Navigation(l => l.PriceHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(l => l.EditionId);
        builder.HasIndex(l => l.LastCheckedAt);
    }
}
