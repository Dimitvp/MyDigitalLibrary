using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.HasKey(j => j.Id);

        builder.Property(j => j.UserId).IsRequired();
        builder.Property(j => j.Kind).IsRequired();
        builder.Property(j => j.Status).IsRequired();
        builder.Property(j => j.SourceFileName).HasMaxLength(500);
        builder.Property(j => j.StartedAt).IsRequired();

        builder.OwnsOne(j => j.Stats, stats =>
        {
            stats.Property(s => s.TotalRows).HasColumnName("stats_total_rows").IsRequired();
            stats.Property(s => s.SucceededRows).HasColumnName("stats_succeeded_rows").IsRequired();
            stats.Property(s => s.FailedRows).HasColumnName("stats_failed_rows").IsRequired();
        });
        builder.Navigation(j => j.Stats).IsRequired();

        builder.HasIndex(j => new { j.UserId, j.Status });
    }
}
