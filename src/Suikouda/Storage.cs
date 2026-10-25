using System.IO;
using System.Text.Json;

namespace Suikouda;

public sealed class Settings
{
    public string Model { get; set; } = "small";
    public string Language { get; set; } = "auto";
    public int Hotkey { get; set; } = 0x77; // F8
    public bool History { get; set; } = true;
    public bool Punctuation { get; set; } = true;
    public string OutputMode { get; set; } = "auto";
    public int GpuDevice { get; set; } = 0;
}
public sealed record HistoryEntry(DateTimeOffset Time, string Text, double RecognitionMs, string Delivery);
public sealed class Storage
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string Root { get; }
    public string Data => Path.Combine(Root, "data");
    public Settings Config { get; private set; }
    public List<HistoryEntry> History { get; private set; } = new();
    public Dictionary<string, int> Counts { get; private set; } = new();
    public Storage(string root)
    {
        Root = root; Directory.CreateDirectory(Data);
        Config = Read<Settings>("settings.json") ?? new();
        History = Read<List<HistoryEntry>>("history.json") ?? new();
        Counts = Read<Dictionary<string, int>>("counts.json") ?? new();
    }
    T? Read<T>(string name)
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(Data, name)), Json); }
        catch (FileNotFoundException) { return default; }
        catch (JsonException) { return default; }
    }
    void Write(string name, object value)
    {
        var path = Path.Combine(Data, name);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(value, Json));
        File.Move(path + ".tmp", path, true);
    }
    public void Save() => Write("settings.json", Config);
    public int Today => Counts.GetValueOrDefault(DateTime.Now.ToString("yyyy-MM-dd"));
    public void Add(string text, double ms, string delivery)
    {
        var day = DateTime.Now.ToString("yyyy-MM-dd");
        Counts[day] = Counts.GetValueOrDefault(day) + text.EnumerateRunes().Count(x => !System.Text.Rune.IsWhiteSpace(x));
        Write("counts.json", Counts);
        if (Config.History)
        {
            History.Insert(0, new(DateTimeOffset.Now, text, ms, delivery));
            if (History.Count > 1000) History.RemoveRange(1000, History.Count - 1000);
            Write("history.json", History);
        }
    }
    public void ClearHistory() { History.Clear(); Write("history.json", History); }
    public string ModelPath => Path.Combine(Root, "models", $"ggml-{Config.Model}.bin");
}
