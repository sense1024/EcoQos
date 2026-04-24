using System.Text.Json.Serialization;

namespace EfficiencyModeScanner;

public sealed class AppSettings
{
    /// <summary>Process names to skip when applying -ecore affinity (same convention as Process.ProcessName, without .exe).</summary>
    [JsonPropertyName("processExclusions")]
    public string[] ProcessExclusions { get; init; } = [];

    /// <summary>Distinct process names discovered in efficiency mode.</summary>
    [JsonPropertyName("processes")]
    public string[] Processes { get; init; } = [];

    /// <summary>UTC timestamp when processes were last refreshed by -v.</summary>
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset? GeneratedAt { get; init; }
}
