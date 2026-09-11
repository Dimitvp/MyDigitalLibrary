using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Reading;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class ReadingSessionConfiguration : IEntityTypeConfiguration<ReadingSession>
{
    public void Configure(EntityTypeBuilder<ReadingSession> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.LibraryItemId).IsRequired();
        builder.Property(s => s.Format).IsRequired();
        builder.Property(s => s.StartedOn).IsRequired();
        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.AbandonReason).HasMaxLength(1000);

        builder.HasIndex(s => new { s.UserId, s.LibraryItemId });

        // Progress is exposed only as a computed IReadOnlyList wrapping the private
        // _progress field; the relationship itself is configured from the
        // ProgressEntry side (ProgressEntryConfiguration), which is a normal
        // entity in its own table, not an owned type.
        builder.Navigation(s => s.Progress).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
