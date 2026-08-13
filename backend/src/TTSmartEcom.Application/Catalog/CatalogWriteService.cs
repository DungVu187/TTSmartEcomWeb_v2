using TTSmartEcom.Application.Abstractions.Catalog;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Domain.Catalog;

namespace TTSmartEcom.Application.Catalog;

public sealed class CatalogWriteService(
    ICatalogWriteRepository repository,
    ICatalogRepository reads,
    ActivityLogWriteService activityLogs)
{
    private static readonly HashSet<string> ChipFields = new(StringComparer.Ordinal)
    {
        "Color", "Shapes", "Frames", "ButtonCount",
    };

    public Task<CatalogMutationResult<BrandRecord>> CreateBrandAsync(
        string? name, CancellationToken cancellationToken) =>
        CreateBrandAsync(name, null, cancellationToken);

    public async Task<CatalogMutationResult<BrandRecord>> CreateBrandAsync(
        string? name, string? actorName, CancellationToken cancellationToken)
    {
        string value = Normalize(name, 120);
        if (value.Length == 0)
            return new CatalogMutationResult<BrandRecord>(CatalogMutationStatus.Invalid, Message: "Thiếu tên thương hiệu");
        bool duplicate = CanAudit(actorName) && (await reads.ListBrandsAsync(cancellationToken))
            .Any(brand => NormalizeKey(brand.Brand) == NormalizeKey(value));
        CatalogMutationResult<BrandRecord> result = await repository.CreateBrandAsync(value, cancellationToken);
        if (!duplicate && CanAudit(actorName) && result is { Status: CatalogMutationStatus.Success, Value: not null })
            await activityLogs.TryAppendAsync(ActivityLogEntries.CreateBrand(actorName!, result.Value.Brand), cancellationToken);
        return result;
    }

    public Task<CatalogMutationResult<BrandRecord>> DeleteBrandAsync(
        string id, CancellationToken cancellationToken) =>
        DeleteBrandAsync(id, null, cancellationToken);

    public async Task<CatalogMutationResult<BrandRecord>> DeleteBrandAsync(
        string id, string? actorName, CancellationToken cancellationToken)
    {
        if (!IsIdentifier(id))
            return new CatalogMutationResult<BrandRecord>(CatalogMutationStatus.Invalid, Message: "Mã thương hiệu không hợp lệ");
        CatalogMutationResult<BrandRecord> result = await repository.DeleteBrandAsync(id, cancellationToken);
        if (CanAudit(actorName) && result is { Status: CatalogMutationStatus.Success, Value: not null })
            await activityLogs.TryAppendAsync(ActivityLogEntries.DeleteBrand(actorName!, result.Value.Brand), cancellationToken);
        return result;
    }

    public Task<ChipValuesRecord?> GetChipValuesAsync(CancellationToken cancellationToken) =>
        repository.GetChipValuesAsync(cancellationToken);

    public Task<CatalogMutationResult<ChipValuesRecord>> AddChipValueAsync(
        string? type, string? value, CancellationToken cancellationToken)
    {
        string field = Normalize(type, 30);
        string item = Normalize(value, 200);
        return !ChipFields.Contains(field) || item.Length == 0
            ? Task.FromResult(new CatalogMutationResult<ChipValuesRecord>(CatalogMutationStatus.Invalid, Message: "Thuộc tính chip không hợp lệ"))
            : repository.AddChipValueAsync(field, item, cancellationToken);
    }

    public Task<CatalogMutationResult<ChipValuesRecord>> RemoveChipValueAsync(
        string? type, string? value, CancellationToken cancellationToken)
    {
        string field = Normalize(type, 30);
        string item = Normalize(value, 200);
        return !ChipFields.Contains(field) || item.Length == 0
            ? Task.FromResult(new CatalogMutationResult<ChipValuesRecord>(CatalogMutationStatus.Invalid, Message: "Cần có type và value để xóa."))
            : repository.RemoveChipValueAsync(field, item, cancellationToken);
    }

    public Task<CatalogMutationResult<SectionDocumentRecord>> CreateSectionAsync(
        string? name, CancellationToken cancellationToken) =>
        CreateSectionAsync(name, null, cancellationToken);

    public async Task<CatalogMutationResult<SectionDocumentRecord>> CreateSectionAsync(
        string? name, string? actorName, CancellationToken cancellationToken)
    {
        string value = Normalize(name, 200);
        if (value.Length == 0) return await InvalidSection("Tên section không hợp lệ");
        CatalogMutationResult<SectionDocumentRecord> result = await repository.CreateSectionAsync(value, cancellationToken);
        if (CanAudit(actorName) && result.Status == CatalogMutationStatus.Success)
            await activityLogs.TryAppendAsync(ActivityLogEntries.CreateSection(actorName!, value), cancellationToken);
        return result;
    }

    public Task<CatalogMutationResult<SectionDocumentRecord>> RenameSectionAsync(
        string oldName, string? newName, CancellationToken cancellationToken) =>
        RenameSectionAsync(oldName, newName, null, cancellationToken);

    public async Task<CatalogMutationResult<SectionDocumentRecord>> RenameSectionAsync(
        string oldName, string? newName, string? actorName, CancellationToken cancellationToken)
    {
        string current = Normalize(oldName, 200);
        string replacement = Normalize(newName, 200);
        if (current.Length == 0 || replacement.Length == 0) return await InvalidSection("Tên section không hợp lệ");
        CatalogMutationResult<SectionDocumentRecord> result =
            await repository.RenameSectionAsync(current, replacement, cancellationToken);
        if (CanAudit(actorName) && result.Status == CatalogMutationStatus.Success)
            await activityLogs.TryAppendAsync(ActivityLogEntries.UpdateSection(actorName!, current, replacement), cancellationToken);
        return result;
    }

    public Task<CatalogMutationResult<SectionDocumentRecord>> DeleteSectionAsync(
        string name, CancellationToken cancellationToken) =>
        DeleteSectionAsync(name, null, cancellationToken);

    public async Task<CatalogMutationResult<SectionDocumentRecord>> DeleteSectionAsync(
        string name, string? actorName, CancellationToken cancellationToken)
    {
        string value = Normalize(name, 200);
        if (value.Length == 0) return await InvalidSection("Tên section không hợp lệ");
        CatalogMutationResult<SectionDocumentRecord> result = await repository.DeleteSectionAsync(value, cancellationToken);
        if (CanAudit(actorName) && result.Status == CatalogMutationStatus.Success)
            await activityLogs.TryAppendAsync(ActivityLogEntries.DeleteSection(actorName!, value), cancellationToken);
        return result;
    }

    public Task<CatalogMutationResult<SectionDocumentRecord>> AddSectionValueAsync(
        string name, string? value, CancellationToken cancellationToken) =>
        AddSectionValueAsync(name, value, null, cancellationToken);

    public async Task<CatalogMutationResult<SectionDocumentRecord>> AddSectionValueAsync(
        string name, string? value, string? actorName, CancellationToken cancellationToken)
    {
        string section = Normalize(name, 200);
        string item = Normalize(value, 200);
        if (section.Length == 0 || item.Length == 0) return await InvalidSection("Giá trị phân loại không hợp lệ");
        CatalogMutationResult<SectionDocumentRecord> result = await repository.AddSectionValueAsync(section, item, cancellationToken);
        if (CanAudit(actorName) && result.Status == CatalogMutationStatus.Success)
            await activityLogs.TryAppendAsync(ActivityLogEntries.CreateSectionValue(actorName!, section, item), cancellationToken);
        return result;
    }

    public Task<CatalogMutationResult<SectionDocumentRecord>> UpdateSectionValueAsync(
        string name, string? oldValue, string? newValue, string? imageUrl, CancellationToken cancellationToken) =>
        UpdateSectionValueAsync(name, oldValue, newValue, imageUrl, null, cancellationToken);

    public async Task<CatalogMutationResult<SectionDocumentRecord>> UpdateSectionValueAsync(
        string name, string? oldValue, string? newValue, string? imageUrl,
        string? actorName, CancellationToken cancellationToken)
    {
        string section = Normalize(name, 200);
        string oldItem = Normalize(oldValue, 200);
        string newItem = Normalize(newValue, 200);
        if (section.Length == 0 || oldItem.Length == 0 || newItem.Length == 0 || !SafeAssetUrl(imageUrl))
            return await InvalidSection("Giá trị phân loại không hợp lệ");
        string? oldImage = CanAudit(actorName)
            ? (await reads.GetSectionDocumentAsync(cancellationToken))?.Sections.FirstOrDefault(item =>
                string.Equals(item.Name, section, StringComparison.Ordinal))?.ImageUrl
            : null;
        string? normalizedImage = imageUrl?.Trim();
        CatalogMutationResult<SectionDocumentRecord> result = await repository.UpdateSectionValueAsync(
            section, oldItem, newItem, normalizedImage, cancellationToken);
        if (CanAudit(actorName) && result.Status == CatalogMutationStatus.Success)
            await activityLogs.TryAppendAsync(ActivityLogEntries.UpdateSectionValue(
                actorName!, section, oldItem, newItem, oldImage, normalizedImage), cancellationToken);
        return result;
    }

    public Task<CatalogMutationResult<SectionDocumentRecord>> DeleteSectionValueAsync(
        string name, string? value, CancellationToken cancellationToken) =>
        DeleteSectionValueAsync(name, value, null, cancellationToken);

    public async Task<CatalogMutationResult<SectionDocumentRecord>> DeleteSectionValueAsync(
        string name, string? value, string? actorName, CancellationToken cancellationToken)
    {
        string section = Normalize(name, 200);
        string item = Normalize(value, 200);
        if (section.Length == 0 || item.Length == 0) return await InvalidSection("Giá trị phân loại không hợp lệ");
        CatalogMutationResult<SectionDocumentRecord> result = await repository.DeleteSectionValueAsync(section, item, cancellationToken);
        if (CanAudit(actorName) && result.Status == CatalogMutationStatus.Success)
            await activityLogs.TryAppendAsync(ActivityLogEntries.DeleteSectionValue(actorName!, section, item), cancellationToken);
        return result;
    }

    private static Task<CatalogMutationResult<SectionDocumentRecord>> InvalidSection(string message) =>
        Task.FromResult(new CatalogMutationResult<SectionDocumentRecord>(CatalogMutationStatus.Invalid, Message: message));

    private static string Normalize(string? value, int max) =>
        string.Join(' ', (value ?? string.Empty).Trim()[..Math.Min((value ?? string.Empty).Trim().Length, max)]
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static bool IsIdentifier(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100 &&
        value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');

    private static bool CanAudit(string? actorName) => !string.IsNullOrWhiteSpace(actorName);

    private static string NormalizeKey(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static bool SafeAssetUrl(string? value) => value is null || value.Length <= 2_000 &&
        (value.Length == 0 || value.StartsWith('/') ||
         Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https");
}
