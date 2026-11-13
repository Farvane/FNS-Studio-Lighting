using System.IO;
using System.Text.Json;

namespace Farvane.StudioLighting;

public sealed class Settings
{
    // Enabling the Pulsar plugin exposes settings; studio changes require opt-in here.
    public bool Enabled { get; set; }

    public static Settings Load(string file)
    {
        if (!File.Exists(file)) return new Settings();
        return JsonSerializer.Deserialize<Settings>(File.ReadAllText(file))
            ?? throw new JsonException("Expected a settings object");
    }

    public void Save(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporary = file + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, file, overwrite: true);
    }
}
