using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class ReadingGoalConfiguration : IEntityTypeConfiguration<ReadingGoal>
{
    public void Configure(EntityTypeBuilder<ReadingGoal> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.UserId).IsRequired();
        builder.Property(g => g.Year).IsRequired();

        builder.HasIndex(g => new { g.UserId, g.Year }).IsUnique();
    }
}
