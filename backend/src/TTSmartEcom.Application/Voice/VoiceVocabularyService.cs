using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Domain.Voice;

namespace TTSmartEcom.Application.Voice;

public interface IVoiceVocabularyRepository
{
    Task<VoiceVocabulary?> FindAsync(CancellationToken cancellationToken);
    Task<VoiceVocabulary?> SaveAsync(VoiceVocabulary vocabulary, int expectedVersion, CancellationToken cancellationToken);
}

public sealed class VoiceVocabularyService(
    IVoiceVocabularyRepository repository,
    IVoiceVocabularyRuntime runtime)
{
    private static readonly HashSet<string> SimpleGroups = ["stopwords", "brands", "types"];
    private static readonly HashSet<string> StructuredGroups = ["brandAliases", "typeAliases", "intentAliases", "codeMap"];

    public async Task<VoiceVocabulary> GetAsync(CancellationToken cancellationToken)
        => await GetOrCreateAsync(cancellationToken);

    /// <summary>
    /// Seed document duy nhất khi collection chưa có dữ liệu và chỉ backfill
    /// <c>intentAliases</c> khi field này thiếu/rỗng, đúng phạm vi của legacy.
    /// CAS và retry bounded giúp nhiều request/startup đồng thời không ghi đè nhau.
    /// </summary>
    public async Task<VoiceVocabulary> InitializeAsync(CancellationToken cancellationToken)
    {
        VoiceVocabulary value = await GetOrCreateAsync(cancellationToken);
        runtime.Refresh(value);
        return value;
    }

    public async Task<VoiceVocabulary> CreateAsync(string group, VoiceVocabularyMutation mutation, CancellationToken cancellationToken)
    {
        ValidateGroup(group);
        VoiceVocabulary current = await GetOrCreateAsync(cancellationToken);
        VoiceVocabulary updated = SimpleGroups.Contains(group) ? AddSimple(current, group, Required(mutation.Value, "value")) : AddStructured(current, group, mutation);
        return await SaveAsync(updated, current.Version, cancellationToken);
    }

    public async Task<VoiceVocabulary> UpdateAsync(string group, VoiceVocabularyMutation mutation, CancellationToken cancellationToken)
    {
        ValidateGroup(group);
        VoiceVocabulary current = await GetOrCreateAsync(cancellationToken);
        VoiceVocabulary updated = SimpleGroups.Contains(group)
            ? UpdateSimple(current, group, Required(mutation.OldValue, "oldValue"), Required(mutation.NewValue, "newValue"))
            : UpdateStructured(current, group, mutation);
        return await SaveAsync(updated, current.Version, cancellationToken);
    }

    public async Task<VoiceVocabulary> DeleteAsync(string group, VoiceVocabularyMutation mutation, CancellationToken cancellationToken)
    {
        ValidateGroup(group);
        VoiceVocabulary current = await GetOrCreateAsync(cancellationToken);
        VoiceVocabulary updated = SimpleGroups.Contains(group)
            ? RemoveSimple(current, group, Required(mutation.Value, "value"))
            : RemoveStructured(current, group, mutation);
        return await SaveAsync(updated, current.Version, cancellationToken);
    }

    private async Task<VoiceVocabulary> SaveAsync(
        VoiceVocabulary vocabulary,
        int version,
        CancellationToken cancellationToken)
    {
        VoiceVocabulary saved = await repository.SaveAsync(vocabulary, version, cancellationToken)
            ?? throw Error(409, "Từ vựng vừa được thay đổi bởi thao tác khác, vui lòng tải lại.");
        runtime.Refresh(saved);
        return saved;
    }

    private async Task<VoiceVocabulary> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        const int maximumAttempts = 3;
        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VoiceVocabulary? current = await repository.FindAsync(cancellationToken);
            if (current is null)
            {
                VoiceVocabulary defaults = VoiceVocabularyDefaults.Create();
                VoiceVocabulary? seeded = await repository.SaveAsync(defaults, defaults.Version, cancellationToken);
                if (seeded is not null) return seeded;
                continue;
            }

            if (current.IntentAliases.Count > 0) return current;
            VoiceVocabulary backfilled = current with
            {
                IntentAliases = VoiceVocabularyDefaults.Create().IntentAliases,
            };
            VoiceVocabulary? saved = await repository.SaveAsync(backfilled, current.Version, cancellationToken);
            if (saved is not null) return saved;
        }

        throw Error(409, "Từ vựng vừa được thay đổi bởi thao tác khác, vui lòng tải lại.");
    }

    private static VoiceVocabulary AddSimple(VoiceVocabulary value, string group, string item)
    {
        IReadOnlyList<string> items = GetSimple(value, group);
        if (items.Any(x => Equal(x, item))) throw Error(400, "Giá trị đã tồn tại.");
        return SetSimple(value, group, [.. items, item]);
    }

    private static VoiceVocabulary UpdateSimple(VoiceVocabulary value, string group, string oldValue, string newValue)
    {
        List<string> items = GetSimple(value, group).ToList();
        int index = items.FindIndex(x => Equal(x, oldValue));
        if (index < 0) throw Error(404, "Không tìm thấy giá trị cần sửa.");
        if (items.Where((_, i) => i != index).Any(x => Equal(x, newValue))) throw Error(400, "Giá trị mới đã tồn tại.");
        items[index] = newValue;
        return SetSimple(value, group, items);
    }

    private static VoiceVocabulary RemoveSimple(VoiceVocabulary value, string group, string item)
    {
        List<string> items = GetSimple(value, group).ToList();
        int removed = items.RemoveAll(x => Equal(x, item));
        if (removed == 0) throw Error(404, "Không tìm thấy giá trị cần xóa.");
        return SetSimple(value, group, items);
    }

    private static VoiceVocabulary AddStructured(VoiceVocabulary value, string group, VoiceVocabularyMutation mutation) => group switch
    {
        "brandAliases" => value.BrandAliases.Any(x => Equal(x.Name, Required(mutation.Name, "name"))) ? throw Error(400, "Thương hiệu đã tồn tại.") : value with { BrandAliases = [.. value.BrandAliases, new VoiceBrandAlias(Required(mutation.Name, "name"), Values(mutation.Aliases))] },
        "typeAliases" => value.TypeAliases.Any(x => Equal(x.Type, Required(mutation.Type, "type"))) ? throw Error(400, "Loại sản phẩm đã tồn tại.") : value with { TypeAliases = [.. value.TypeAliases, new VoiceTypeAlias(Required(mutation.Type, "type"), Optional(mutation.Keyword) ?? Required(mutation.Type, "type"), Values(mutation.Aliases))] },
        "intentAliases" => value.IntentAliases.Any(x => Equal(x.Intent, Required(mutation.Intent, "intent"))) ? throw Error(400, "Intent đã tồn tại.") : value with { IntentAliases = [.. value.IntentAliases, new VoiceIntentAlias(Required(mutation.Intent, "intent"), Optional(mutation.Label) ?? string.Empty, Values(mutation.Aliases))] },
        "codeMap" => value.CodeMap.Any(x => Equal(x.Code, Required(mutation.Code, "code"))) ? throw Error(400, "Mã model đã tồn tại.") : value with { CodeMap = [.. value.CodeMap, NewCode(mutation)] },
        _ => throw Error(400, "Nhóm từ vựng không hợp lệ."),
    };

    private static VoiceVocabulary UpdateStructured(VoiceVocabulary value, string group, VoiceVocabularyMutation mutation) => group switch
    {
        "brandAliases" => value with { BrandAliases = Replace(value.BrandAliases, x => Equal(x.Name, Required(mutation.Name, "name")), x => x with { Aliases = Values(mutation.Aliases) }) },
        "typeAliases" => value with { TypeAliases = Replace(value.TypeAliases, x => Equal(x.Type, Required(mutation.Type, "type")), x => x with { Keyword = Optional(mutation.Keyword) ?? x.Type, Aliases = Values(mutation.Aliases) }) },
        "intentAliases" => value with { IntentAliases = Replace(value.IntentAliases, x => Equal(x.Intent, Required(mutation.Intent, "intent")), x => x with { Label = Optional(mutation.Label) ?? string.Empty, Aliases = Values(mutation.Aliases) }) },
        "codeMap" => value with { CodeMap = Replace(value.CodeMap, x => Equal(x.Code, Required(mutation.Code, "code")), _ => NewCode(mutation)) },
        _ => throw Error(400, "Nhóm từ vựng không hợp lệ."),
    };

    private static VoiceVocabulary RemoveStructured(VoiceVocabulary value, string group, VoiceVocabularyMutation mutation) => group switch
    {
        "brandAliases" => value with { BrandAliases = Remove(value.BrandAliases, x => Equal(x.Name, Required(mutation.Name ?? mutation.Value, "name"))) },
        "typeAliases" => value with { TypeAliases = Remove(value.TypeAliases, x => Equal(x.Type, Required(mutation.Type ?? mutation.Value, "type"))) },
        "intentAliases" => value with { IntentAliases = Remove(value.IntentAliases, x => Equal(x.Intent, Required(mutation.Intent ?? mutation.Value, "intent"))) },
        "codeMap" => value with { CodeMap = Remove(value.CodeMap, x => Equal(x.Code, Required(mutation.Code ?? mutation.Value, "code"))) },
        _ => throw Error(400, "Nhóm từ vựng không hợp lệ."),
    };

    private static VoiceCodeMap NewCode(VoiceVocabularyMutation mutation)
    {
        string code = Required(mutation.Code, "code");
        return new VoiceCodeMap(code, Optional(mutation.Keyword) ?? code, Optional(mutation.Brand), Optional(mutation.Type), Values(mutation.Patterns), Optional(mutation.Compact) ?? string.Empty);
    }

    private static List<T> Replace<T>(IReadOnlyList<T> source, Func<T, bool> predicate, Func<T, T> transform)
    {
        List<T> values = source.ToList();
        int index = values.FindIndex(x => predicate(x));
        if (index < 0) throw Error(404, "Không tìm thấy giá trị cần sửa.");
        values[index] = transform(values[index]);
        return values;
    }

    private static List<T> Remove<T>(IReadOnlyList<T> source, Func<T, bool> predicate)
    {
        List<T> values = source.ToList();
        int removed = values.RemoveAll(x => predicate(x));
        if (removed == 0) throw Error(404, "Không tìm thấy giá trị cần xóa.");
        return values;
    }

    private static IReadOnlyList<string> GetSimple(VoiceVocabulary value, string group) => group switch { "stopwords" => value.Stopwords, "brands" => value.Brands, "types" => value.Types, _ => [] };
    private static VoiceVocabulary SetSimple(VoiceVocabulary value, string group, IReadOnlyList<string> items) => group switch { "stopwords" => value with { Stopwords = items }, "brands" => value with { Brands = items }, "types" => value with { Types = items }, _ => value };
    private static string Required(string? value, string field) { string? result = Optional(value); return result is null ? throw Error(400, $"Thiếu {field}.") : result; }
    private static string? Optional(string? value) { string? result = value?.Trim(); if (result?.Length > 200) throw Error(400, "Giá trị quá dài."); return string.IsNullOrWhiteSpace(result) ? null : result; }
    private static string[] Values(IReadOnlyList<string>? values) { string[] result = (values ?? []).Select(Optional).Where(static x => x is not null).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); if (result.Length > 100) throw Error(400, "Danh sách giá trị quá lớn."); return result; }
    private static bool Equal(string left, string right) => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    private static void ValidateGroup(string group) { if (!SimpleGroups.Contains(group) && !StructuredGroups.Contains(group)) throw Error(400, "Nhóm từ vựng không hợp lệ."); }
    private static TTSmartEcom.Application.Common.Errors.ApplicationException Error(int status, string message) => new(new ApplicationError($"TTS-VOICE-{status}", 4800 + status, status, message));
}
