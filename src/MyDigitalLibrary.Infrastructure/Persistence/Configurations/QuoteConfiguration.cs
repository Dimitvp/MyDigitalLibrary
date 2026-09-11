using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.HasKey(q => q.Id);

        builder.Property(q => q.UserId).IsRequired();
        builder.Property(q => q.WorkId).IsRequired();
        builder.Property(q => q.Text).IsRequired();
        builder.Property(q => q.PageOrPosition).HasMaxLength(100);
        builder.Property(q => q.CreatedAt).IsRequired();

        builder.HasIndex(q => q.WorkId);
    }
}
