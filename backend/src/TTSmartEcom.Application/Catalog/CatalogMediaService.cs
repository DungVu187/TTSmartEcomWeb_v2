using TTSmartEcom.Application.Abstractions.Catalog;

namespace TTSmartEcom.Application.Catalog;

public sealed class CatalogMediaService(ICatalogMediaRepository repository)
{
    public Task<bool> IsSectionImageReferencedAsync(string filename, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filename) || filename.Length > 180)
        {
            return Task.FromResult(false);
        }

        return repository.IsSectionImageReferencedAsync(filename, cancellationToken);
    }
}
