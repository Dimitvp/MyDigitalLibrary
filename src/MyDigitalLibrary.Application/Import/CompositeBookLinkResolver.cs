using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Application.Import;

/// <summary>Chain-of-responsibility runner: tries each registered <see cref="IBookLinkResolver"/> in DI registration order, stopping at the first ISBN found.</summary>
public sealed class CompositeBookLinkResolver(IEnumerable<IBookLinkResolver> resolvers)
{
    public async Task<Isbn?> ResolveAsync(Uri pageUrl, CancellationToken ct)
    {
        foreach (var resolver in resolvers)
        {
            var isbn = await resolver.TryResolveAsync(pageUrl, ct);
            if (isbn is not null)
                return isbn;
        }

        return null;
    }
}
