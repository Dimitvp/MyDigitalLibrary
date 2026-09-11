using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Catalog;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class BookstoreConfiguration : IEntityTypeConfiguration<Bookstore>
{
    public void Configure(EntityTypeBuilder<Bookstore> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name).IsRequired().HasMaxLength(200);
        builder.Property(b => b.AdapterKey).IsRequired().HasMaxLength(100);

        builder.Property(b => b.BaseUrl)
            .HasConversion(url => url.ToString(), value => new Uri(value))
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(b => b.AdapterKey).IsUnique();
    }
}
