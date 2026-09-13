using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class FollowedBookSourceConfiguration : IEntityTypeConfiguration<FollowedBookSource>
{
    public void Configure(EntityTypeBuilder<FollowedBookSource> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Url).IsRequired().HasMaxLength(2000);
        builder.Property(s => s.Category).HasMaxLength(100);
        builder.Property(s => s.Notes).HasMaxLength(2000);
        builder.Property(s => s.AddedOn).IsRequired();

        builder.HasIndex(s => new { s.UserId, s.Url }).IsUnique();
    }
}
