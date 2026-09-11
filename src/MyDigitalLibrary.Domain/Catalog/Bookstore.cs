using MyDigitalLibrary.Domain.Common;

namespace MyDigitalLibrary.Domain.Catalog;

public sealed class Bookstore : Entity
{
    public string Name { get; private set; }
    public Uri BaseUrl { get; private set; }
    public string AdapterKey { get; private set; }

    public Bookstore(string name, Uri baseUrl, string adapterKey)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(adapterKey))
            throw new ArgumentException("Adapter key is required.", nameof(adapterKey));

        Name = name;
        BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        AdapterKey = adapterKey;
    }
}
