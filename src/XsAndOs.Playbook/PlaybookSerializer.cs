using System.Text.Json;
using System.Text.Json.Serialization;
using XsAndOs.Core;

namespace XsAndOs.Playbook;

/// <summary>
/// JSON persistence for drawn plays. Lives outside XsAndOs.Core so the core
/// library keeps zero dependencies (System.Text.Json is a NuGet package on
/// netstandard2.1, which matters for Unity).
/// </summary>
public static class PlaybookSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new Vec2JsonConverter(), new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(PlayDesign play) => JsonSerializer.Serialize(play, Options);

    public static PlayDesign Deserialize(string json) =>
        JsonSerializer.Deserialize<PlayDesign>(json, Options)
        ?? throw new JsonException("Play JSON deserialized to null.");

    public static void SaveToFile(PlayDesign play, string path) =>
        File.WriteAllText(path, Serialize(play));

    public static PlayDesign LoadFromFile(string path) => Deserialize(File.ReadAllText(path));

    /// <summary>Loads every *.json play in a directory, sorted by file name for determinism.</summary>
    public static IReadOnlyList<PlayDesign> LoadDirectory(string directory)
    {
        var files = Directory.GetFiles(directory, "*.json");
        Array.Sort(files, StringComparer.Ordinal);
        var plays = new List<PlayDesign>(files.Length);
        foreach (var file in files)
        {
            plays.Add(LoadFromFile(file));
        }

        return plays;
    }
}
