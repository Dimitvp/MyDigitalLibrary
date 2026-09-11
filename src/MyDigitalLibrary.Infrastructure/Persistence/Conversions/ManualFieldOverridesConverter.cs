using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyDigitalLibrary.Domain.Catalog;

namespace MyDigitalLibrary.Infrastructure.Persistence.Conversions;

/// <summary>Shared <see cref="ManualFieldOverrides"/> &lt;-&gt; jsonb conversion, used by both WorkConfiguration and EditionConfiguration.</summary>
public sealed class ManualFieldOverridesConverter()
    : ValueConverter<ManualFieldOverrides, string>(
        overrides => JsonSerializer.Serialize(overrides.Fields, (JsonSerializerOptions?)null),
        json => new ManualFieldOverrides(JsonSerializer.Deserialize<string[]>(json, (JsonSerializerOptions?)null) ?? Array.Empty<string>()))
{
    public static readonly ManualFieldOverridesConverter Instance = new();
}
