using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tav;

public record GroundItemStack
{
    public required string Id { get; init; }
    public int Quantity { get; init; } = 1;
}

/// <summary>Accepts <c>["id"]</c>, <c>[{"id":"a","quantity":2}]</c>, or a mix.</summary>
public sealed class GroundItemsJsonConverter : JsonConverter<List<GroundItemStack>?>
{
    public override List<GroundItemStack>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("groundItems must be an array.");

        var list = new List<GroundItemStack>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return list;

            if (reader.TokenType == JsonTokenType.String)
            {
                string? id = reader.GetString();
                if (string.IsNullOrWhiteSpace(id))
                    throw new JsonException("groundItems string entry must be a non-empty id.");
                list.Add(new GroundItemStack { Id = id.Trim(), Quantity = 1 });
                continue;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                string? id = null;
                int quantity = 1;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                        continue;

                    string? prop = reader.GetString();
                    reader.Read();
                    if (string.Equals(prop, "id", StringComparison.OrdinalIgnoreCase))
                        id = reader.GetString();
                    else if (string.Equals(prop, "quantity", StringComparison.OrdinalIgnoreCase))
                        quantity = reader.GetInt32();
                }

                if (string.IsNullOrWhiteSpace(id))
                    throw new JsonException("groundItems object entry must include a non-empty id.");
                if (quantity < 1)
                    throw new JsonException("groundItems quantity must be at least 1.");

                list.Add(new GroundItemStack { Id = id.Trim(), Quantity = quantity });
                continue;
            }

            throw new JsonException("groundItems entries must be strings or objects.");
        }

        throw new JsonException("Unclosed groundItems array.");
    }

    public override void Write(Utf8JsonWriter writer, List<GroundItemStack>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (GroundItemStack s in value)
        {
            if (s.Quantity == 1)
            {
                writer.WriteStringValue(s.Id);
                continue;
            }

            writer.WriteStartObject();
            writer.WriteString("id", s.Id);
            writer.WriteNumber("quantity", s.Quantity);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
