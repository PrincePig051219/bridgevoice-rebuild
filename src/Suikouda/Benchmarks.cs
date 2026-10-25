using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Suikouda;
internal sealed record CorpusItem(string Id, string Language, string Text, string Wav);
internal static class Benchmarks
{
    public static async Task Pipeline(string root, nint target, string corpusPath, string outputPath)
    {
        var corpus = JsonSerializer.Deserialize<CorpusItem[]>(File.ReadAllText(corpusPath), Storage.Json)!;
        using var engine = new Recognition { GpuDevice = new Storage(root).Config.GpuDevice };
        string model = Path.Combine(root, "models", "ggml-small.bin"), corpusDir = Path.GetDirectoryName(Path.GetFullPath(corpusPath))!;
        await engine.Load(model);
        await engine.Run(Audio.ReadWav(Path.Combine(corpusDir, corpus[0].Wav)), model, corpus[0].Language, true);
        var rows = new List<object>();
        foreach (var item in corpus)
        {
            var samples = Audio.ReadWav(Path.Combine(corpusDir, item.Wav));
            var clock = Stopwatch.StartNew();
            var text = await engine.Run(samples, model, item.Language, true);
            var delivery = await Output.Deliver(target, text.Text + "\n", "auto");
            rows.Add(new { item.Id, text.Text, text.Milliseconds, Delivery = delivery, FileReadyToDeliveryMs = clock.Elapsed.TotalMilliseconds });
            File.WriteAllText(outputPath, JsonSerializer.Serialize(new { Completed = false, Target = (long)target, Results = rows }, Storage.Json));
            if (delivery.Status is "target-unavailable" or "focus-refused") break;
        }
        File.WriteAllText(outputPath, JsonSerializer.Serialize(new { Completed = rows.Count == corpus.Length, Target = (long)target, Note = "Preloaded WAV to ASR to actual input; excludes physical microphone/key-release, includes clipboard settle/verification; unverified injection is not success", Results = rows }, Storage.Json));
    }
    static string Normalize(string text) => string.Concat(text.Normalize(NormalizationForm.FormKC).ToLowerInvariant().Where(char.IsLetterOrDigit));
    static int Distance<T>(T[] a, T[] b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (int i = 1; i <= a.Length; i++)
        {
            int previous = row[0]; row[0] = i;
            for (int j = 1; j <= b.Length; j++) { int old = row[j]; row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), previous + (EqualityComparer<T>.Default.Equals(a[i - 1], b[j - 1]) ? 0 : 1)); previous = old; }
        }
        return row[^1];
    }
    static string[] Words(string text) => string.Concat(text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ')).Split(' ', StringSplitOptions.RemoveEmptyEntries);
    public static async Task Run(string root, string corpusPath, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        object logLock = new();
        using var nativeLog = Whisper.net.Logger.LogProvider.AddLogger((level, message) => { lock (logLock) File.AppendAllText(outputPath + ".native.log", $"{DateTimeOffset.Now:O} {level} {message}\n"); });
        var corpus = JsonSerializer.Deserialize<CorpusItem[]>(File.ReadAllText(corpusPath), Storage.Json)!;
        var model = Path.Combine(root, "models", "ggml-small.bin");
        using var engine = new Recognition();
        engine.GpuDevice = new Storage(root).Config.GpuDevice;
        var load = Stopwatch.StartNew(); await engine.Load(model); double loadMs = load.Elapsed.TotalMilliseconds;
        string corpusDir = Path.GetDirectoryName(Path.GetFullPath(corpusPath))!;
        var warm = await engine.Run(Audio.ReadWav(Path.Combine(corpusDir, corpus[0].Wav)), model, corpus[0].Language, true);
        var results = new List<object>();
        foreach (var item in corpus)
        {
            var wav = Path.Combine(corpusDir, item.Wav); var audio = Audio.ReadWav(wav);
            var result = await engine.Run(audio, model, item.Language, true);
            string expected = Normalize(item.Text), actual = Normalize(result.Text);
            int errors = Distance(expected.ToCharArray(), actual.ToCharArray());
            int wordErrors = Distance(Words(item.Text), Words(result.Text));
            results.Add(new { item.Id, item.Language, Reference = item.Text, Hypothesis = result.Text, CharacterErrors = errors, ReferenceCharacters = expected.Length, CER = errors / (double)Math.Max(1, expected.Length), WordErrors = wordErrors, ReferenceWords = Words(item.Text).Length, result.Milliseconds, result.InputSeconds, result.VoicedSeconds, UnderOneSecond = result.Milliseconds < 1000, WavSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(wav))).ToLowerInvariant(), Original = "未测：登录边界", Note = "预热后离线文件识别计时；不等于端到端松键上屏延迟" });
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, JsonSerializer.Serialize(new { Completed = false, Runtime = Recognition.SelectedRuntime, ColdModelLoadMs = loadMs, WarmupMs = warm.Milliseconds, Results = results }, Storage.Json));
        }
        var silence = await engine.Run(new float[32000], model, "auto", true);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(new { Completed = true, At = DateTimeOffset.Now, Runtime = Recognition.SelectedRuntime, ModelSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(model))).ToLowerInvariant(), ColdModelLoadMs = loadMs, WarmupMs = warm.Milliseconds, SilenceRejected = silence.Text == "", Results = results }, Storage.Json));
    }
}
