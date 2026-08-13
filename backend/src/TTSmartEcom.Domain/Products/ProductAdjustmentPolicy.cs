using System.Text;

namespace TTSmartEcom.Domain.Products;

public static class ProductAdjustmentPolicy
{
    private static readonly HashSet<string> MissingRequiredValues = new(StringComparer.Ordinal)
    {
        string.Empty,
        "n/a",
        "na",
        "chua ro",
        "chua co",
        "chua phan loai",
    };

    public static bool IsAdjusted(string? type, string? brand, string? section) =>
        HasRequiredValue(type) && HasRequiredValue(brand) && HasRequiredValue(section);

    public static bool HasRequiredValue(string? value)
    {
        string normalized = RemoveVietnameseTones(value ?? string.Empty)
            .ToLowerInvariant()
            .Trim();
        return !MissingRequiredValues.Contains(normalized);
    }

    private static string RemoveVietnameseTones(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        StringBuilder result = new(decomposed.Length);
        foreach (char character in decomposed)
        {
            if (character is >= '\u0300' and <= '\u036f') continue;
            result.Append(character switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => character,
            });
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}
