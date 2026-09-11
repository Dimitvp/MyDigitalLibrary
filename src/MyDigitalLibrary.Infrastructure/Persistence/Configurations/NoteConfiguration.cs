using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId).IsRequired();
        builder.Property(n => n.LibraryItemId).IsRequired();
        builder.Property(n => n.Body).IsRequired();
        builder.Property(n => n.LocationInBook).HasMaxLength(200);
        builder.Property(n => n.CreatedAt).IsRequired();

        builder.HasIndex(n => n.LibraryItemId);
    }
}
