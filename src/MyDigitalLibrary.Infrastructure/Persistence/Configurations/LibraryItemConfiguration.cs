using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class LibraryItemConfiguration : IEntityTypeConfiguration<LibraryItem>
{
    public void Configure(EntityTypeBuilder<LibraryItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.UserId).IsRequired();
        builder.Property(i => i.EditionId).IsRequired();
        builder.Property(i => i.Format).IsRequired();
        builder.Property(i => i.Status).IsRequired();
        builder.Property(i => i.PersonalNote);

        builder.OwnsOne(i => i.Acquisition, acquisition =>
        {
            acquisition.Property(a => a.AcquiredOn).HasColumnName("acquired_on").IsRequired();
            acquisition.Property(a => a.Method).HasColumnName("acquisition_method").IsRequired();
            acquisition.Property(a => a.Source).HasColumnName("acquisition_source").HasMaxLength(300);

            acquisition.OwnsOne(a => a.Price, price =>
            {
                price.Property(p => p.Amount).HasColumnName("acquisition_price_amount").HasColumnType("numeric(12,2)");
                price.Property(p => p.CurrencyCode).HasColumnName("acquisition_price_currency").HasMaxLength(3);
            });
        });
        builder.Navigation(i => i.Acquisition).IsRequired();

        builder.OwnsOne(i => i.Location, location =>
        {
            location.Property(l => l.Room).HasColumnName("location_room").HasMaxLength(200);
            location.Property(l => l.Shelf).HasColumnName("location_shelf").HasMaxLength(200);
            location.Property(l => l.Box).HasColumnName("location_box").HasMaxLength(200);
        });

        builder.HasIndex(i => new { i.UserId, i.Status, i.Format });
    }
}
