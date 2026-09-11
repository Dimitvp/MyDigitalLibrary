using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Reading;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

/// <summary>
/// ProgressEntry is a normal entity in its own table, not an owned type — EF
/// Core does not support discriminator-based inheritance for owned types, so
/// the polymorphic ProgressPoint is flattened by the domain itself into private
/// fields (_kind/_pageValue/_percentValue/_positionTicks), mapped here directly
/// by field name. EF can read/write private fields via reflection with no
/// InternalsVisibleTo needed.
/// </summary>
public sealed class ProgressEntryConfiguration : IEntityTypeConfiguration<ProgressEntry>
{
    public void Configure(EntityTypeBuilder<ProgressEntry> builder)
    {
        builder.ToTable("reading_progress", t => t.HasCheckConstraint(
            "ck_reading_progress_single_value",
            """
            (kind = 'page' AND page_value IS NOT NULL AND percent_value IS NULL AND position_ticks IS NULL)
            OR (kind = 'percent' AND percent_value IS NOT NULL AND page_value IS NULL AND position_ticks IS NULL)
            OR (kind = 'timestamp' AND position_ticks IS NOT NULL AND page_value IS NULL AND percent_value IS NULL)
            """));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ReadingSessionId).IsRequired();
        builder.Property(e => e.RecordedAt).IsRequired();

        builder.Property<string>("_kind").HasColumnName("kind").HasMaxLength(20).IsRequired();
        builder.Property<int?>("_pageValue").HasColumnName("page_value");
        builder.Property<decimal?>("_percentValue").HasColumnName("percent_value").HasPrecision(5, 2);
        builder.Property<long?>("_positionTicks").HasColumnName("position_ticks");

        // Computed purely from the four fields above — not its own column.
        builder.Ignore(e => e.Point);

        builder.HasOne<ReadingSession>()
            .WithMany(s => s.Progress)
            .HasForeignKey(e => e.ReadingSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
