using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lab.AspNetCore.Serialization;

/// <summary>
/// 按 [EnumMember] 值读写枚举（线上格式 = shared OpenAPI 契约值，如 "manual"/"active"/"submit"）。
///
/// live smoke 发现：net8.0 的 JsonStringEnumConverter&lt;T&gt; 不认 [EnumMember]
/// （.NET 9 才支持），序列化走 C# 成员名（"Manual"/"Active"），与契约小写值分叉，
/// 前端 msw 按契约发小写会被 400 拒收。生成侧统一换本 converter
/// （挂接见 scripts/patch-generated.py 第 3 处修补，重跑 codegen 不丢）。
/// </summary>
public sealed class EnumMemberEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    private static readonly IReadOnlyDictionary<string, T> FromWire = BuildFromWire();
    private static readonly IReadOnlyDictionary<T, string> ToWire = BuildToWire();

    private static Dictionary<string, T> BuildFromWire()
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var field in Fields())
        {
            map[WireName(field)] = (T)field.GetValue(null)!;
        }
        return map;
    }

    private static Dictionary<T, string> BuildToWire()
    {
        var map = new Dictionary<T, string>();
        foreach (var field in Fields())
        {
            map[(T)field.GetValue(null)!] = WireName(field);
        }
        return map;
    }

    private static FieldInfo[] Fields() => typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static);

    private static string WireName(FieldInfo field) =>
        field.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? field.Name;

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString()!;
            if (FromWire.TryGetValue(s, out var value))
            {
                return value;
            }
            throw new JsonException($"unknown {typeof(T).Name} value '{s}' (expected one of: {string.Join(", ", FromWire.Keys)})");
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            return (T)Enum.ToObject(typeof(T), reader.GetInt64());
        }
        throw new JsonException($"cannot read {typeof(T).Name} from {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (!ToWire.TryGetValue(value, out var wire))
        {
            throw new JsonException($"{typeof(T).Name}.{value} has no [EnumMember] mapping");
        }
        writer.WriteStringValue(wire);
    }
}
