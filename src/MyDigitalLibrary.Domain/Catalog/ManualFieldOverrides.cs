namespace MyDigitalLibrary.Domain.Catalog;

/// <summary>
/// Tracks which enrichable fields on a <see cref="Work"/>/<see cref="Edition"/>
/// have been set by an explicit manual edit (plan section 5.5). A later
/// import/enrichment run must skip these fields so it never silently overwrites
/// a correction the user made. Immutable — "marking" a field returns a new
/// instance, matching this codebase's other value objects (e.g. <c>Isbn</c>)
/// and keeping EF Core's change-tracking snapshot comparison correct without a
/// custom value comparer.
/// </summary>
public sealed class ManualFieldOverrides : IEquatable<ManualFieldOverrides>
{
    public static readonly ManualFieldOverrides None = new([]);

    private readonly HashSet<string> _fields;

    public ManualFieldOverrides(IEnumerable<string> fields) => _fields = new HashSet<string>(fields, StringComparer.Ordinal);

    public IReadOnlyCollection<string> Fields => _fields;

    public bool IsOverridden(string field) => _fields.Contains(field);

    public ManualFieldOverrides WithOverridden(string field)
        => _fields.Contains(field) ? this : new ManualFieldOverrides([.. _fields, field]);

    public bool Equals(ManualFieldOverrides? other) => other is not null && _fields.SetEquals(other._fields);

    public override bool Equals(object? obj) => Equals(obj as ManualFieldOverrides);

    // XOR, not HashCode.Combine — must be order-independent since _fields is a HashSet
    // whose enumeration order isn't guaranteed to match for two equal instances.
    public override int GetHashCode() => _fields.Aggregate(0, (hash, field) => hash ^ field.GetHashCode());
}
