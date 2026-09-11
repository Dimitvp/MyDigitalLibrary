using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class WorkConfiguration : IEntityTypeConfiguration<Work>
{
    public void Configure(EntityTypeBuilder<Work> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Title).IsRequired().HasMaxLength(500);
        builder.Property(w => w.OriginalTitle).HasMaxLength(500);
        builder.Property(w => w.SeriesId);

        // SeriesPosition wraps a single decimal — a scalar conversion, not an owned type.
        builder.Property(w => w.PositionInSeries)
            .HasConversion(
                position => position == null ? (decimal?)null : position.Value,
                value => value == null ? null : new SeriesPosition(value.Value))
            .HasColumnName("series_position");

        // GenreIds/Authors are exposed only as computed IReadOnlyLists wrapping private
        // fields, so EF is pointed at the backing fields directly.
        builder.PrimitiveCollection(w => w.GenreIds)
            .HasColumnName("genre_ids")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(w => w.Authors, author =>
        {
            author.ToTable("work_authors");
            author.WithOwner().HasForeignKey(x => x.WorkId);
            author.HasKey(x => x.Id);
            author.Property(x => x.AuthorId).IsRequired();
            author.Property(x => x.Role).IsRequired();
            author.HasIndex(x => x.AuthorId);
        });
        builder.Navigation(w => w.Authors).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(w => w.SeriesId);
    }
}
