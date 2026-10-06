using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevPulse.Infrastructure.ClickUp.Serialization;

/// <summary>
/// ClickUp returns Unix timestamps as string or number in JSON (e.g. "1567780450202").
/// </summary>
internal sealed class UnixTimestampJsonConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number when reader.TryGetInt64(out var value) => value,
            JsonTokenType.String => ParseTimestamp(reader.GetString()),
            _ => throw new JsonException($"Cannot convert JSON token '{reader.TokenType}' to Unix timestamp.")
        };
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
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

    private static long? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return long.TryParse(value, out var parsed) ? parsed : null;
    }
}
