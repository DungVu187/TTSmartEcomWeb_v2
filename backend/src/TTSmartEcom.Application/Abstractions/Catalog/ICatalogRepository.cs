using TTSmartEcom.Domain.Catalog;

namespace TTSmartEcom.Application.Abstractions.Catalog;

public interface ICatalogRepository
{
    Task<IReadOnlyList<BrandRecord>> ListBrandsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListSectionNamesAsync(CancellationToken cancellationToken);

    Task<SectionDocumentRecord?> GetSectionDocumentAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>?> GetSectionValuesAsync(string sectionName, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string?>> GetSectionImagesAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken);

    Task<ManageRecord?> GetManageAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ManagePolicyRecord>> GetPoliciesAsync(CancellationToken cancellationToken);
}

public interface ICatalogWriteRepository
{

    Task<CatalogMutationResult<BrandRecord>> CreateBrandAsync(string name, CancellationToken cancellationToken);

    Task<CatalogMutationResult<BrandRecord>> DeleteBrandAsync(string id, CancellationToken cancellationToken);

    Task<ChipValuesRecord?> GetChipValuesAsync(CancellationToken cancellationToken);

    Task<CatalogMutationResult<ChipValuesRecord>> AddChipValueAsync(string type, string value, CancellationToken cancellationToken);

    Task<CatalogMutationResult<ChipValuesRecord>> RemoveChipValueAsync(string type, string value, CancellationToken cancellationToken);

    Task<CatalogMutationResult<SectionDocumentRecord>> CreateSectionAsync(string name, CancellationToken cancellationToken);

    Task<CatalogMutationResult<SectionDocumentRecord>> RenameSectionAsync(string oldName, string newName, CancellationToken cancellationToken);

    Task<CatalogMutationResult<SectionDocumentRecord>> DeleteSectionAsync(string name, CancellationToken cancellationToken);

    Task<CatalogMutationResult<SectionDocumentRecord>> AddSectionValueAsync(string name, string value, CancellationToken cancellationToken);

    Task<CatalogMutationResult<SectionDocumentRecord>> UpdateSectionValueAsync(
        string name,
        string oldValue,
        string newValue,
        string? imageUrl,
        CancellationToken cancellationToken);

    Task<CatalogMutationResult<SectionDocumentRecord>> DeleteSectionValueAsync(string name, string value, CancellationToken cancellationToken);
}

public interface ICatalogMediaRepository
{
    Task<bool> IsSectionImageReferencedAsync(string filename, CancellationToken cancellationToken);
}

public enum CatalogMutationStatus
{
    Success,
    NotFound,
    Conflict,
    Invalid,
}

public sealed record CatalogMutationResult<T>(CatalogMutationStatus Status, T? Value = default, string? Message = null);
