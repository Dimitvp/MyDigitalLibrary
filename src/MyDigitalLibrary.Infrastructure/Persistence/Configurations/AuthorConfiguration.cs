using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyDigitalLibrary.Domain.Catalog;

namespace MyDigitalLibrary.Infrastructure.Persistence.Configurations;

public sealed class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.FullName).IsRequired().HasMaxLength(300);
        builder.Property(a => a.SortName).IsRequired().HasMaxLength(300);
        builder.Property(a => a.Bio);

        // A plain dictionary, not a sequence — stored as a JSON column rather than
        // EF's array-oriented primitive collections. SetExternalId mutates the
        // dictionary in place, so a value comparer is required for the change
        // tracker to notice edits (dictionaries compare by reference otherwise).
        var externalIds = builder.Property<Dictionary<string, string>>("_externalIds")
            .HasColumnName("external_ids")
            .HasColumnType("jsonb")
            .HasConversion(
                map => JsonSerializer.Serialize(map, JsonSerializerOptions.Default),
                json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonSerializerOptions.Default) ?? new());

        externalIds.Metadata.SetValueComparer(new ValueComparer<Dictionary<string, string>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            d => d.Aggregate(0, (hash, kvp) => HashCode.Combine(hash, kvp.Key, kvp.Value)),
            d => new Dictionary<string, string>(d)));

        builder.HasIndex(a => a.SortName);
    }
}
