using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.ValueObjects;
using MyDigitalLibrary.Infrastructure.Persistence.Conversions;
using NpgsqlTypes;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class WorkConfiguration : IEntityTypeConfiguration<Work>
{
    public void Configure(EntityTypeBuilder<Work> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Title).IsRequired().HasMaxLength(500);
        builder.Property(w => w.OriginalTitle).HasMaxLength(500);
        builder.Property(w => w.SeriesId);

        // ManualFieldOverrides (plan 5.5) is a variable-length set of field names — stored
        // as jsonb, not an OwnsOne with fixed columns.
        builder.Property(w => w.FieldOverrides)
            .HasConversion(ManualFieldOverridesConverter.Instance)
            .HasColumnType("jsonb")
            .HasColumnName("field_overrides")
            .IsRequired();

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
            // Id is always assigned client-side (Entity's base constructor), never
            // by the database — see ProgressEntryConfiguration for why this matters:
            // without it, EF Core misjudges Added vs. Unchanged for a WorkAuthor
            // discovered via navigation fixup (AddAuthor appending to an
            // already-tracked, already-persisted Work) and emits a no-op UPDATE.
            author.Property(x => x.Id).ValueGeneratedNever();
            author.Property(x => x.AuthorId).IsRequired();
            author.Property(x => x.Role).IsRequired();
            author.HasIndex(x => x.AuthorId);
        });
        builder.Navigation(w => w.Authors).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(w => w.SeriesId);

        // Plan section 9: full-text search over Title/OriginalTitle/Description.
        // "simple" config, not a language-specific stemmer — verified against a
        // real Postgres 17 instance (2026-09-11) that it has no Bulgarian
        // dictionary (\dFd lists 29 snowball stemmers, none Bulgarian), matching
        // the plan's own hedge. A pure Domain-ignorant shadow property (Work.cs
        // itself has no SearchVector member) so Domain stays free of persistence
        // concerns; queried via EF.Property<NpgsqlTsVector>(w, "SearchVector").
        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasColumnName("search_vector")
            .IsGeneratedTsVectorColumn("simple", [nameof(Work.Title), nameof(Work.OriginalTitle), nameof(Work.Description)]);
        builder.HasIndex("SearchVector").HasMethod("GIN");
    }
}
