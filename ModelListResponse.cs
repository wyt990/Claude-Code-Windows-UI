using System.Text.Json.Serialization;

namespace ClaudeCodeGUI
{
    /// <summary>
    /// Represents the structure of the JSON response from `claudecode --list-models --json`.
    /// </summary>
    public class ModelListResponse
    {
        [JsonPropertyName("customModels")]
        public List<ModelInfo> CustomModels { get; set; } = new();

        [JsonPropertyName("openaiCompat")]
        public OpenAICompat OpenAICompat { get; set; } = new();

        [JsonPropertyName("zenFreeModels")]
        public ZenFreeModels ZenFreeModels { get; set; } = new();
    }

    /// <summary>
    /// Represents a single model entry, which may have different identifying properties.
    /// </summary>
    public class ModelInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("originalName")]
        public string OriginalName { get; set; } = "";

        [JsonPropertyName("routedValue")]
        public string RoutedValue { get; set; } = "";
    }

    /// <summary>
    /// Contains models compatible with OpenAI's API format.
    /// </summary>
    public class OpenAICompat
    {
        [JsonPropertyName("providers")]
        public List<Provider> Providers { get; set; } = new();
    }

    /// <summary>
    /// A provider within the openaiCompat section that contains its own list of models.
    /// </summary>
    public class Provider
    {
        [JsonPropertyName("models")]
        public List<ModelInfo> Models { get; set; } = new();
    }

    /// <summary>
    /// Contains free models provided by Zen.
    /// </summary>
    public class ZenFreeModels
    {
        [JsonPropertyName("models")]
        public List<ModelInfo> Models { get; set; } = new();
    }
}