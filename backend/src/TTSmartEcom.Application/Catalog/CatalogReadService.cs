using TTSmartEcom.Application.Abstractions.Catalog;
using TTSmartEcom.Domain.Catalog;

namespace TTSmartEcom.Application.Catalog;

public sealed class CatalogReadService(ICatalogRepository repository)
{
    public Task<IReadOnlyList<BrandRecord>> ListBrandsAsync(CancellationToken cancellationToken) =>
        repository.ListBrandsAsync(cancellationToken);

    public Task<IReadOnlyList<string>> ListSectionNamesAsync(CancellationToken cancellationToken) =>
        repository.ListSectionNamesAsync(cancellationToken);

    public Task<SectionDocumentRecord?> GetSectionDocumentAsync(CancellationToken cancellationToken) =>
        repository.GetSectionDocumentAsync(cancellationToken);

    public Task<IReadOnlyList<string>?> GetSectionValuesAsync(string sectionName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        if (sectionName.Length > 200)
        {
            throw new ArgumentException("Section name is too long.", nameof(sectionName));
        }

        return repository.GetSectionValuesAsync(sectionName, cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, string?>> GetSectionImagesAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken)
    {
        if (names.Count > 100)
        {
            throw new ArgumentException("Too many section names.", nameof(names));
        }

        string[] boundedNames = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Where(name => name.Length <= 200)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return repository.GetSectionImagesAsync(boundedNames, cancellationToken);
    }

    public Task<ManageRecord?> GetManageAsync(CancellationToken cancellationToken) =>
        repository.GetManageAsync(cancellationToken);

    public Task<IReadOnlyList<ManagePolicyRecord>> GetPoliciesAsync(CancellationToken cancellationToken) =>
        repository.GetPoliciesAsync(cancellationToken);
}
