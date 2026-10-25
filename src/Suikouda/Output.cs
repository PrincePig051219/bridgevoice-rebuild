using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using WpfClipboard = System.Windows.Clipboard;

namespace Suikouda;
internal sealed record Delivery(string Status, bool? Confirmed, string Method, double Milliseconds, string? Detail = null);
internal static class Output
{
    [StructLayout(LayoutKind.Sequential)] struct GuiInfo
    {
        public uint size, flags; public nint active, focus, capture, menuOwner, moveSize, caret;
        public int left, top, right, bottom;
    }
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern nint SendMessageTimeout(nint window, uint message, nuint wparam, nint lparam, uint flags, uint timeout, out nuint result);
    static (nint window, string? text) Probe()
    {
        var info = new GuiInfo { size = (uint)Marshal.SizeOf<GuiInfo>() };
        if (!GetGUIThreadInfo(0, ref info)) return (0, null);
        string cls = Native.Class(info.focus);
        if (!cls.Contains("Edit", StringComparison.OrdinalIgnoreCase)) return (info.focus, null);
        if (SendMessageTimeout(info.focus, 0xe, 0, 0, 2, 150, out var length) == 0 || length > 100000) return (info.focus, null);
        nint buffer = Marshal.AllocHGlobal(((int)length + 1) * 2);
        try
        {
            Marshal.WriteInt16(buffer, 0);
            if (SendMessageTimeout(info.focus, 0xd, length + 1, buffer, 2, 150, out _) == 0) return (info.focus, null);
            return (info.focus, Marshal.PtrToStringUni(buffer));
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    static async Task<bool> Unicode(string text, nint synchronizedEdit = 0)
    {
        foreach (char c in text)
        {
            if (c == '\r') continue;
            if (synchronizedEdit != 0)
            {
                if (SendMessageTimeout(synchronizedEdit, 0x102, (nuint)(c == '\n' ? '\r' : c), 1, 2, 1000, out _) == 0) return false;
                continue;
            }
            // Unicode linefeed avoids turning a dictated newline into a chat application's Send key.
            var down = new Native.Input { type = 1, data = new() { keyboard = new() { scan = c, flags = 4 } } };
            var up = down; up.data.keyboard.flags = 6;
            if (!Native.Send(down, up)) return false;
            await Task.Delay(8);
        }
        return true;
    }
    static System.Windows.DataObject? Backup()
    {
        var source = WpfClipboard.GetDataObject(); if (source == null) return null;
        var copy = new System.Windows.DataObject();
        foreach (string format in source.GetFormats(false))
        {
            var value = source.GetData(format, false);
            if (value is MemoryStream stream) value = new MemoryStream(stream.ToArray());
            if (value is System.Windows.Media.Imaging.BitmapSource bitmap) { var clone = bitmap.Clone(); clone.Freeze(); value = clone; }
            if (value != null) copy.SetData(format, value, false);
        }
        return copy;
    }
    public static async Task<Delivery> Deliver(nint target, string text, string mode)
    {
        var watch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(text)) return new("empty", false, "none", 0);
        target = Native.ActiveModal(target);
        if (!Native.IsWindow(target) || !Native.IsWindowEnabled(target)) return new("target-unavailable", false, "none", 0);
        Native.SetForegroundWindow(target); await Task.Delay(80);
        if (Native.GetForegroundWindow() != target) return new("focus-refused", false, "none", watch.Elapsed.TotalMilliseconds);
        for (int i = 0; i < 20 && new[] { 0x10, 0x11, 0x12, 0x5b, 0x5c }.Any(k => Native.GetAsyncKeyState(k) < 0); i++) await Task.Delay(25);
        if (new[] { 0x10, 0x11, 0x12, 0x5b, 0x5c }.Any(k => Native.GetAsyncKeyState(k) < 0)) return new("modifiers-held", false, "none", watch.Elapsed.TotalMilliseconds);
        var before = Probe();
        bool notepadCompatibility = mode == "auto" && Native.Class(before.window) == "RichEditD2DPT";
        if (notepadCompatibility) mode = "type";
        bool sent = false; string method = notepadCompatibility ? "wm-char-notepad-compatibility" : mode == "type" ? "unicode" : "clipboard";
        System.Windows.DataObject? backup = null; uint ourSequence = 0; bool clipboardChanged = false;
        try
        {
            if (mode != "type")
            {
                try { backup = Backup(); WpfClipboard.SetText(text); clipboardChanged = true; ourSequence = Native.GetClipboardSequenceNumber(); }
                catch (Exception e) when (e is ExternalException || e is System.Threading.ThreadStateException)
                {
                    method = "unicode-clipboard-busy";
                }
            }
            if (clipboardChanged)
            {
                sent = Native.Send(Native.Key(0x11), Native.Key(0x56), Native.Key(0x56, true), Native.Key(0x11, true));
                // Keep our clipboard until the editor has consumed it. Word does not expose
                // a safe Edit readback; early restore was observed to paste the old contents.
                await Task.Delay(1500);
                var afterPaste = Probe();
                for (int i = 0; i < 20 && before.text != null && afterPaste.text == before.text; i++) { await Task.Delay(50); afterPaste = Probe(); }
                bool unchanged = before.window == afterPaste.window && before.text != null && afterPaste.text == before.text;
                if (mode == "auto" && !sent) { sent = await Unicode(text); method = "unicode-fallback"; await Task.Delay(250); }
            }
            else { sent = await Unicode(text, notepadCompatibility ? before.window : 0); await Task.Delay(150); }
            var after = Probe();
            for (int i = 0; i < 10 && before.window == after.window && after.text != null && !after.text.Contains(text.Trim()); i++) { await Task.Delay(50); after = Probe(); }
            bool? verified = before.window == after.window && before.text != null && after.text != null ? after.text != before.text && after.text.Contains(text.Trim()) : null;
            return new(!sent ? "injection-rejected" : verified == true ? "confirmed" : verified == false ? "not-observed" : "sent-unverified", sent ? verified : false, method, watch.Elapsed.TotalMilliseconds,
                verified == null ? "目标未提供可安全读取的 Edit 文本；只确认输入已发送，不冒充成功上屏。" : null);
        }
        finally
        {
            if (clipboardChanged && Native.GetClipboardSequenceNumber() == ourSequence)
            {
                try { if (backup == null) WpfClipboard.Clear(); else WpfClipboard.SetDataObject(backup, true); }
                catch (ExternalException) { /* Another process owns clipboard; never overwrite a later user copy. */ }
            }
        }
    }
}
