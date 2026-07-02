using System.Text.Json;
using System.Text.Json.Serialization;
using XsAndOs.Core;

namespace XsAndOs.Playbook;

/// <summary>Serializes Vec2 as a compact [x, y] array.</summary>
public sealed class Vec2JsonConverter : JsonConverter<Vec2>
{
    public override Vec2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected [x, y] array for Vec2.");
        }

        reader.Read();
        var x = reader.GetSingle();
        reader.Read();
        var y = reader.GetSingle();
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("Vec2 array must have exactly two elements.");
        }

        return new Vec2(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Vec2 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}
