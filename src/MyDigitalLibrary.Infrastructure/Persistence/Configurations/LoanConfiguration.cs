using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.UserId).IsRequired();
        builder.Property(l => l.LibraryItemId).IsRequired();
        builder.Property(l => l.BorrowerName).IsRequired().HasMaxLength(200);
        builder.Property(l => l.LentOn).IsRequired();

        builder.HasIndex(l => l.LibraryItemId);
    }
}
