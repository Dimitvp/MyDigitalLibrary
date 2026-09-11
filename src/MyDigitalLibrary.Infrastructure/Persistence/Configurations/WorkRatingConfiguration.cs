using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class WorkRatingConfiguration : IEntityTypeConfiguration<WorkRating>
{
    public void Configure(EntityTypeBuilder<WorkRating> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.WorkId).IsRequired();
        builder.Property(r => r.Score).IsRequired();
        builder.Property(r => r.RatedAt).IsRequired();

        builder.HasIndex(r => new { r.UserId, r.WorkId }).IsUnique();
    }
}
