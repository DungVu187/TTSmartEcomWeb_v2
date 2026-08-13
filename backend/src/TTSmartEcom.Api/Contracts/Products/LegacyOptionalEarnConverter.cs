using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TTSmartEcom.Api.Contracts.Products;

/// <summary>
/// Reads the scalar earn values emitted by the legacy JavaScript admin form.
/// The form sends an empty string when the field is left blank; legacy creation
/// normalizes that value to the 25 percent default. Objects, arrays and other
/// non-numeric values remain invalid.
/// </summary>
public sealed class LegacyOptionalEarnConverter : JsonConverter<double?>
{
    private const double DefaultEarn = 25D;

    public override double? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                if (reader.TryGetDouble(out double number) && double.IsFinite(number))
                {
                    return number;
                }

                throw new JsonException("earn must be a finite number.");
            case JsonTokenType.String:
                string value = reader.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(value))
                {
                    return DefaultEarn;
                }

                if (double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                    && double.IsFinite(number))
                {
                    return number;
                }

                throw new JsonException("earn must be a finite number or an empty string.");
            default:
                throw new JsonException("earn must be a finite number or an empty string.");
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        double? value,
        JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
