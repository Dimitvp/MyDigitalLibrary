using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Library;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class ShelfConfiguration : IEntityTypeConfiguration<Shelf>
{
    public void Configure(EntityTypeBuilder<Shelf> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.IsSystem).IsRequired();

        builder.OwnsMany(s => s.Items, item =>
        {
            item.ToTable("shelf_items");
            item.WithOwner().HasForeignKey(x => x.ShelfId);
            item.HasKey(x => x.Id);
            // Id is always assigned client-side (Entity's base constructor), never
            // by the database — see ProgressEntryConfiguration for why this matters:
            // without it, EF Core misjudges Added vs. Unchanged for a ShelfItem
            // discovered via navigation fixup (AddItem appending to an
            // already-tracked, already-loaded Shelf.Items) and emits a no-op UPDATE.
            item.Property(x => x.Id).ValueGeneratedNever();
            item.Property(x => x.LibraryItemId).IsRequired();
            item.Property(x => x.AddedOn).IsRequired();
            item.Property(x => x.SortOrder).IsRequired();

            item.HasIndex(x => new { x.ShelfId, x.LibraryItemId }).IsUnique();
        });
        builder.Navigation(s => s.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => new { s.UserId, s.Name }).IsUnique();
    }
}
