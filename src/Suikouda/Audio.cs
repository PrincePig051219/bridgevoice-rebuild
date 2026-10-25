using System.IO;
using System.Runtime.InteropServices;

namespace Suikouda;
internal sealed class Microphone : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] struct Format { public ushort tag, channels; public uint rate, bytesPerSecond; public ushort alignment, bits, extra; }
    [StructLayout(LayoutKind.Sequential)] struct Header { public nint data; public uint length, recorded; public nuint user; public uint flags, loops; public nint next; public nuint reserved; }
    delegate void Callback(nint device, uint message, nuint instance, nint p1, nint p2);
    [DllImport("winmm.dll")] static extern uint waveInOpen(out nint device, uint id, ref Format format, Callback callback, nuint instance, uint flags);
    [DllImport("winmm.dll")] static extern uint waveInPrepareHeader(nint device, nint header, uint size);
    [DllImport("winmm.dll")] static extern uint waveInUnprepareHeader(nint device, nint header, uint size);
    [DllImport("winmm.dll")] static extern uint waveInAddBuffer(nint device, nint header, uint size);
    [DllImport("winmm.dll")] static extern uint waveInStart(nint device);
    [DllImport("winmm.dll")] static extern uint waveInReset(nint device);
    [DllImport("winmm.dll")] static extern uint waveInStop(nint device);
    [DllImport("winmm.dll")] static extern uint waveInClose(nint device);
    readonly Callback callback;
    readonly List<(nint header, nint data)> buffers = new();
    readonly List<float> samples = new();
    readonly object sync = new();
    nint device;
    volatile bool active;
    public float Level { get; private set; }
    public Microphone() => callback = OnData;
    static void Check(uint code) { if (code != 0) throw new InvalidOperationException($"麦克风错误 {code}"); }
    public void Start()
    {
        lock (sync) samples.Clear();
        var format = new Format { tag = 1, channels = 1, rate = 16000, bytesPerSecond = 32000, alignment = 2, bits = 16 };
        Check(waveInOpen(out device, uint.MaxValue, ref format, callback, 0, 0x30000));
        try
        {
            for (int i = 0; i < 8; i++)
            {
                nint data = Marshal.AllocHGlobal(3200), h = Marshal.AllocHGlobal(Marshal.SizeOf<Header>());
                Marshal.StructureToPtr(new Header { data = data, length = 3200 }, h, false);
                Check(waveInPrepareHeader(device, h, (uint)Marshal.SizeOf<Header>()));
                buffers.Add((h, data)); Check(waveInAddBuffer(device, h, (uint)Marshal.SizeOf<Header>()));
            }
            active = true; Check(waveInStart(device));
        }
        catch { Dispose(); throw; }
    }
    void OnData(nint d, uint message, nuint instance, nint p1, nint p2)
    {
        if (message != 0x3c0 || p1 == 0) return;
        var h = Marshal.PtrToStructure<Header>(p1);
        if (h.recorded > 0)
        {
            var pcm = new short[h.recorded / 2]; Marshal.Copy(h.data, pcm, 0, pcm.Length);
            var floats = pcm.Select(x => x / 32768f).ToArray();
            lock (sync) { samples.AddRange(floats); Level = (float)Math.Sqrt(floats.Select(x => (double)x * x).DefaultIfEmpty().Average()); }
        }
        if (active) waveInAddBuffer(d, p1, (uint)Marshal.SizeOf<Header>());
    }
    public float[] Stop()
    {
        active = false;
        if (device != 0) { waveInStop(device); waveInReset(device); }
        float[] result; lock (sync) result = samples.ToArray();
        Dispose(); return result;
    }
    public void Dispose()
    {
        active = false;
        if (device != 0)
        {
            waveInReset(device);
            foreach (var b in buffers) { waveInUnprepareHeader(device, b.header, (uint)Marshal.SizeOf<Header>()); Marshal.FreeHGlobal(b.header); Marshal.FreeHGlobal(b.data); }
            buffers.Clear(); waveInClose(device); device = 0;
        }
    }
}
internal static class Audio
{
    public static float[] ReadWav(string path)
    {
        using var f = File.OpenRead(path); using var r = new BinaryReader(f);
        if (new string(r.ReadChars(4)) != "RIFF") throw new InvalidDataException("需要 PCM WAV");
        r.ReadUInt32(); if (new string(r.ReadChars(4)) != "WAVE") throw new InvalidDataException("WAV 格式错误");
        int rate = 0, channels = 0, bits = 0, tag = 0; byte[]? data = null;
        while (f.Position + 8 <= f.Length)
        {
            string id = new(r.ReadChars(4)); int n = r.ReadInt32(); long end = f.Position + n;
            if (n < 0 || end > f.Length) throw new InvalidDataException("WAV 块越界");
            if (id == "fmt ") { tag = r.ReadUInt16(); channels = r.ReadUInt16(); rate = r.ReadInt32(); r.ReadInt32(); r.ReadUInt16(); bits = r.ReadUInt16(); }
            if (id == "data") data = r.ReadBytes(n);
            f.Position = end + (n & 1);
        }
        if (tag != 1 || rate != 16000 || channels != 1 || bits != 16 || data == null) throw new InvalidDataException("仅接收 16kHz/16-bit/mono PCM");
        return Enumerable.Range(0, data.Length / 2).Select(i => BitConverter.ToInt16(data, i * 2) / 32768f).ToArray();
    }
    public static float[] TrimSilence(float[] input)
    {
        const int frame = 320, padding = 8;
        int count = (input.Length + frame - 1) / frame;
        var voice = new bool[count];
        for (int i = 0; i < count; i++)
        {
            int n = Math.Min(frame, input.Length - i * frame); double sum = 0;
            for (int j = 0; j < n; j++) sum += input[i * frame + j] * input[i * frame + j];
            voice[i] = Math.Sqrt(sum / Math.Max(1, n)) >= 0.006;
        }
        var keep = new bool[count];
        for (int i = 0; i < count; i++) if (voice[i]) for (int j = Math.Max(0, i - padding); j <= Math.Min(count - 1, i + padding); j++) keep[j] = true;
        var output = new List<float>();
        for (int i = 0; i < count; i++) if (keep[i]) output.AddRange(input.AsSpan(i * frame, Math.Min(frame, input.Length - i * frame)).ToArray());
        return output.ToArray();
    }
}
