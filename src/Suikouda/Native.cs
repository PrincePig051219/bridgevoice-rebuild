using System.Runtime.InteropServices;
using System.Text;

namespace Suikouda;
internal static class Native
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern int LCMapStringEx(string locale, uint flags, string source, int sourceLength, StringBuilder target, int capacity, nint version, nint reserved, nint sortHandle);
    public static string SimplifyChinese(string text)
    {
        var converted = new StringBuilder(text.Length * 2 + 4);
        return LCMapStringEx("zh-CN", 0x02000000, text, text.Length, converted, converted.Capacity, 0, 0, 0) > 0 ? converted.ToString() : text;
    }
    public delegate nint HookProc(int code, nint message, nint data);
    public delegate bool EnumProc(nint window, nint parameter);
    [DllImport("user32.dll")] public static extern nint SetWindowsHookEx(int id, HookProc proc, nint module, uint thread);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] public static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] public static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] public static extern bool IsWindowEnabled(nint window);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] public static extern nint GetLastActivePopup(nint window);
    [DllImport("user32.dll")] public static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc, nint param);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(nint parent, EnumProc proc, nint param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(nint window, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(nint window, StringBuilder text, int max);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint count, Input[] input, int size);
    [StructLayout(LayoutKind.Sequential)] public struct Input { public uint type; public InputUnion data; }
    [StructLayout(LayoutKind.Explicit)] public struct InputUnion
    {
        [FieldOffset(0)] public Keyboard keyboard;
        [FieldOffset(0)] public Mouse mouse;
    }
    [StructLayout(LayoutKind.Sequential)] public struct Keyboard { public ushort vk, scan; public uint flags, time; public nuint extra; }
    [StructLayout(LayoutKind.Sequential)] public struct Mouse { public int x, y; public uint mouseData, flags, time; public nuint extra; }
    public static Input Key(ushort vk, bool up = false) => new() { type = 1, data = new() { keyboard = new() { vk = vk, flags = up ? 2u : 0u } } };
    public static bool Send(params Input[] keys) => SendInput((uint)keys.Length, keys, Marshal.SizeOf<Input>()) == keys.Length;
    public static string Class(nint h) { var s = new StringBuilder(256); GetClassName(h, s, s.Capacity); return s.ToString(); }
    public static string Title(nint h) { var s = new StringBuilder(512); GetWindowText(h, s, s.Capacity); return s.ToString(); }
    public static nint ActiveModal(nint owner)
    {
        var current = owner;
        for (int i = 0; i < 8; i++)
        {
            var popup = GetLastActivePopup(current);
            if (popup == current || popup == 0 || !IsWindowVisible(popup)) break;
            current = popup;
        }
        return current;
    }
}

internal sealed class HoldHotkey : IDisposable
{
    readonly Native.HookProc callback;
    readonly nint hook;
    bool pressed;
    public int Key { get; set; }
    public event Action<bool>? Changed;
    public event Action? Cancelled;
    public HoldHotkey(int key)
    {
        Key = key; callback = Handle;
        hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
        if (hook == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    nint Handle(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            int key = Marshal.ReadInt32(data);
            int flags = Marshal.ReadInt32(data, 8);
            bool down = message == 0x100 || message == 0x104;
            bool up = message == 0x101 || message == 0x105;
            if ((flags & 0x10) == 0 && key == Key)
            {
                if (down && !pressed) { pressed = true; Changed?.Invoke(true); }
                if (up && pressed) { pressed = false; Changed?.Invoke(false); }
                return 1;
            }
            if (down && key == 0x1b) Cancelled?.Invoke();
        }
        return Native.CallNextHookEx(hook, code, message, data);
    }
    public void Dispose() => Native.UnhookWindowsHookEx(hook);
}
