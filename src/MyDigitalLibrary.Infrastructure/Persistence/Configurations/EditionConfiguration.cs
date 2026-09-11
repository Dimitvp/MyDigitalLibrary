using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class EditionConfiguration : IEntityTypeConfiguration<Edition>
{
    public void Configure(EntityTypeBuilder<Edition> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.WorkId).IsRequired();
        builder.Property(e => e.Format).IsRequired();
        builder.Property(e => e.Publisher).HasMaxLength(300);
        builder.Property(e => e.Language).HasMaxLength(10);
        builder.Property(e => e.Translator).HasMaxLength(300);
        builder.Property(e => e.Narrator).HasMaxLength(300);
        builder.Property(e => e.CoverImageUrl).HasMaxLength(2000);

        // Isbn wraps a single normalized string — a scalar conversion, not an owned type.
        builder.Property(e => e.Isbn13)
            .HasConversion(
                isbn => isbn == null ? null : isbn.Value,
                value => value == null ? null : Isbn.TryCreate(value).Value)
            .HasColumnName("isbn13")
            .HasMaxLength(13);

        builder.Property(e => e.Duration)
            .HasConversion(
                duration => duration == null ? (TimeSpan?)null : duration.Value,
                value => value == null ? null : new AudioDuration(value.Value))
            .HasColumnName("duration");

        // Extent is a computed projection over PageCount/Duration, not its own column.
        builder.Ignore(e => e.Extent);

        builder.HasIndex(e => e.WorkId);
        builder.HasIndex(e => e.Isbn13).IsUnique().HasFilter("isbn13 IS NOT NULL");
    }
}
