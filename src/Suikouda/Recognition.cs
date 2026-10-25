using System.Diagnostics;
using System.IO;
using System.Text;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Suikouda;
internal sealed record RecognitionResult(string Text, double Milliseconds, double InputSeconds, double VoicedSeconds);
internal sealed class Recognition : IDisposable
{
    WhisperFactory? factory;
    string? loaded;
    public int GpuDevice { get; set; }
    int loadedGpu = int.MinValue;
    readonly SemaphoreSlim gate = new(1, 1);
    public static string SelectedRuntime => RuntimeOptions.LoadedLibrary?.ToString() ?? "unknown";
    public async Task Load(string model)
    {
        await gate.WaitAsync();
        try { await Task.Run(() => EnsureModel(model)); } finally { gate.Release(); }
    }
    void EnsureModel(string model)
    {
        if (loaded == model && loadedGpu == GpuDevice && factory != null) return;
        if (!File.Exists(model)) throw new FileNotFoundException("没有找到本地模型；请按 README 将官方模型放入 models。", model);
        RuntimeOptions.RuntimeLibraryOrder = new List<RuntimeLibrary> { RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu };
        factory?.Dispose(); factory = null; loaded = null;
        factory = WhisperFactory.FromPath(model, new WhisperFactoryOptions { UseGpu = GpuDevice >= 0, GpuDevice = Math.Max(0, GpuDevice), UseFlashAttention = true }); loaded = model; loadedGpu = GpuDevice;
    }
    public async Task<RecognitionResult> Run(float[] input, string model, string language, bool punctuation, CancellationToken cancel = default)
    {
        await gate.WaitAsync(cancel);
        try
        {
            return await Task.Run(async () =>
            {
                var watch = Stopwatch.StartNew();
                var speech = Audio.TrimSilence(input);
                if (speech.Length < 1600) return new RecognitionResult("", watch.Elapsed.TotalMilliseconds, input.Length / 16000d, speech.Length / 16000d);
                EnsureModel(model);
                using var processor = factory!.CreateBuilder().WithLanguage(language).WithThreads(8).WithNoContext().WithTemperature(0).WithGreedySamplingStrategy(g => g.WithBestOf(1)).Build();
                var text = new StringBuilder();
                await foreach (var segment in processor.ProcessAsync(speech, cancel)) text.Append(segment.Text);
                string result = text.ToString().Trim();
                if (result.Any(c => c >= '\u4e00' && c <= '\u9fff'))
                {
                    result = Native.SimplifyChinese(result);
                }
                if (punctuation && result.Length > 0 && !"。！？.!?，,：:；;".Contains(result[^1])) result += result.Any(c => c >= '\u4e00' && c <= '\u9fff') ? "。" : ".";
                return new RecognitionResult(result, watch.Elapsed.TotalMilliseconds, input.Length / 16000d, speech.Length / 16000d);
            }, cancel);
        }
        finally { gate.Release(); }
    }
    public void Dispose() { factory?.Dispose(); gate.Dispose(); }
}
