using System.Text.Json.Serialization;

namespace stdray.Twitter;

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TweetResponseDto))]
internal partial class GraphQlContext : JsonSerializerContext;