using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;

namespace Suikouda;
internal static class Program
{
    [STAThread] public static void Main(string[] args)
    {
        string root = AppContext.BaseDirectory;
        int rootIndex = Array.IndexOf(args, "--root");
        if (rootIndex >= 0 && args.Length > rootIndex + 1) root = System.IO.Path.GetFullPath(args[rootIndex + 1]);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { System.Windows.MessageBox.Show(e.Exception.Message, "随口打"); e.Handled = true; };
        app.Startup += async (_, _) =>
        {
            try
            {
                if (args.Contains("--benchmark"))
                {
                    int i = Array.IndexOf(args, "--benchmark");
                    await Benchmarks.Run(root, args[i + 1], args[i + 2]); app.Shutdown(); return;
                }
                if (args.Contains("--inject-test"))
                {
                    int i = Array.IndexOf(args, "--inject-test");
                    var result = await Output.Deliver((nint)long.Parse(args[i + 1]), args[i + 2], "auto");
                    File.WriteAllText(args[i + 3], JsonSerializer.Serialize(result, Storage.Json)); app.Shutdown(); return;
                }
                if (args.Contains("--pipeline-test"))
                {
                    int i = Array.IndexOf(args, "--pipeline-test");
                    await Benchmarks.Pipeline(root, (nint)long.Parse(args[i + 1]), args[i + 2], args[i + 3]); app.Shutdown(); return;
                }
                if (args.Contains("--recorder-smoke"))
                {
                    using var mic = new Microphone(); var smokeOverlay = new Overlay();
                    var clock = Stopwatch.StartNew(); mic.Start(); smokeOverlay.Show();
                    for (int n = 0; n < 50; n++) { smokeOverlay.Update(clock.Elapsed, mic.Level); await Task.Delay(100); }
                    var samples = mic.Stop(); smokeOverlay.Close();
                    File.WriteAllText(args[Array.IndexOf(args, "--recorder-smoke") + 1], JsonSerializer.Serialize(new { samples = samples.Length, seconds = samples.Length / 16000d, durationMs = clock.Elapsed.TotalMilliseconds, peak = samples.Select(Math.Abs).DefaultIfEmpty().Max(), audioSaved = false }, Storage.Json)); app.Shutdown(); return;
                }
                var controller = new Controller(new Storage(root)); app.Exit += (_, _) => controller.Dispose(); await controller.Initialize(); if (args.Contains("--settings")) controller.OpenSettings();
            }
            catch (Exception e)
            {
                Directory.CreateDirectory(System.IO.Path.Combine(root, "data"));
                File.WriteAllText(System.IO.Path.Combine(root, "data", "last-error.txt"), e.ToString());
                if (!args.Any(a => a.StartsWith("--benchmark"))) System.Windows.MessageBox.Show(e.Message, "随口打");
                app.Shutdown(1);
            }
        };
        app.Run();
    }
}
internal sealed class Controller : IDisposable
{
    readonly Storage storage;
    readonly Recognition recognition = new();
    readonly Forms.NotifyIcon tray;
    readonly HoldHotkey hotkey;
    readonly Overlay overlay = new();
    Microphone? microphone;
    bool busy, capturing;
    nint destination;
    CancellationTokenSource? cancellation;
    SettingsWindow? settings;
    readonly Stopwatch recordTime = new();
    string lastResult = "";
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(60) };
    public Controller(Storage store)
    {
        storage = store;
        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "随口打 · 模型准备中", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("设置与历史", null, (_, _) => OpenSettings());
        menu.Items.Add("复制最近一次结果", null, (_, _) => { if (lastResult.Length > 0) System.Windows.Clipboard.SetText(lastResult); });
        menu.Items.Add("退出", null, (_, _) => Application.Current.Shutdown());
        tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => OpenSettings();
        hotkey = new(storage.Config.Hotkey);
        hotkey.Changed += down => Application.Current.Dispatcher.BeginInvoke(new Action(() => { if (down) Begin(); else _ = Finish(); }));
        hotkey.Cancelled += () => Application.Current.Dispatcher.BeginInvoke(new Action(Cancel));
        timer.Tick += (_, _) =>
        {
            if (!capturing) return;
            overlay.Update(recordTime.Elapsed, microphone?.Level ?? 0);
            if (recordTime.Elapsed.TotalSeconds > 120) _ = Finish();
        };
    }
    public async Task Initialize()
    {
        busy = true;
        recognition.GpuDevice = storage.Config.GpuDevice;
        try { await recognition.Load(storage.ModelPath); tray.Text = $"随口打 · 按住 F{storage.Config.Hotkey - 0x6f} 说话"; }
        catch (Exception e) { Notify(e.Message); OpenSettings(); }
        finally { busy = false; }
    }
    void Notify(string text) { tray.BalloonTipTitle = "随口打"; tray.BalloonTipText = text; tray.ShowBalloonTip(4000); }
    void Begin()
    {
        if (busy || capturing) return;
        destination = Native.GetForegroundWindow();
        try
        {
            microphone = new Microphone(); microphone.Start(); capturing = true; recordTime.Restart();
            overlay.Show(); timer.Start();
        }
        catch (Exception e) { microphone?.Dispose(); microphone = null; Notify(e.Message); }
    }
    async Task Finish()
    {
        if (!capturing) return;
        capturing = false; timer.Stop(); busy = true; cancellation = new();
        var latency = Stopwatch.StartNew();
        try
        {
            float[] data = microphone!.Stop(); microphone = null; overlay.Recognizing();
            var result = await recognition.Run(data, storage.ModelPath, storage.Config.Language, storage.Config.Punctuation, cancellation.Token);
            if (!string.IsNullOrWhiteSpace(result.Text))
            {
                lastResult = result.Text;
                var delivery = await Output.Deliver(destination, result.Text, storage.Config.OutputMode);
                storage.Add(result.Text, result.Milliseconds, delivery.Status);
                if (delivery.Confirmed != true) Notify(delivery.Confirmed == false ? "未能上屏，可在托盘菜单复制最近一次结果。" : "已发送粘贴，请核对输入框；可在托盘菜单复制最近一次结果。");
                File.WriteAllText(System.IO.Path.Combine(storage.Data, "last-timing.json"), JsonSerializer.Serialize(new { result.Milliseconds, ReleaseToCompletionMs = latency.Elapsed.TotalMilliseconds, Delivery = delivery, Runtime = Recognition.SelectedRuntime }, Storage.Json));
            }
            else Notify("没有检测到有效语音。");
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Notify(e.Message); }
        finally { overlay.Hide(); busy = false; cancellation?.Dispose(); cancellation = null; settings?.Refresh(); }
    }
    void Cancel()
    {
        if (capturing) { capturing = false; timer.Stop(); microphone?.Stop(); microphone = null; overlay.Hide(); }
        cancellation?.Cancel();
    }
    public void OpenSettings()
    {
        if (settings == null)
        {
            settings = new(storage, async () => { hotkey.Key = storage.Config.Hotkey; await Initialize(); });
            settings.Closed += (_, _) => settings = null;
        }
        settings.Refresh(); settings.Show(); settings.Activate();
    }
    public void Dispose() { Cancel(); hotkey.Dispose(); tray.Dispose(); microphone?.Dispose(); if (!busy) recognition.Dispose(); overlay.Close(); }
}
internal sealed class Overlay : Window
{
    readonly TextBlock label = new() { Foreground = Brushes.White, FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
    readonly Polyline wave = new() { Stroke = Brushes.Aquamarine, StrokeThickness = 2 };
    readonly Queue<double> levels = new();
    public Overlay()
    {
        Title = "随口打 · 录音"; Width = 360; Height = 76; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false; ShowActivated = false;
        var grid = new Grid { Margin = new Thickness(18, 10, 18, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(label); Grid.SetColumn(wave, 1); grid.Children.Add(wave);
        Content = new Border { Background = new SolidColorBrush(Color.FromRgb(24, 36, 48)), CornerRadius = new CornerRadius(18), Child = grid };
        Loaded += (_, _) => { var work = SystemParameters.WorkArea; Left = work.Left + (work.Width - Width) / 2; Top = work.Bottom - Height - 32; };
    }
    public void Update(TimeSpan time, float level)
    {
        label.Text = $"正在聆听  {time.Minutes:00}:{time.Seconds:00}";
        levels.Enqueue(Math.Min(1, level * 8)); while (levels.Count > 32) levels.Dequeue();
        var points = new PointCollection(); int i = 0;
        foreach (var value in levels) { points.Add(new System.Windows.Point(i * 5, 28 - value * 22)); points.Add(new System.Windows.Point(i * 5 + 2, 28 + value * 22)); i++; }
        wave.Points = points;
    }
    public void Recognizing() { label.Text = "正在转成文字…"; wave.Points.Clear(); }
}
internal sealed class SettingsWindow : Window
{
    readonly Storage storage;
    readonly TextBlock today = new() { FontSize = 18, Margin = new Thickness(0, 8, 0, 12) };
    readonly ListBox history = new() { Height = 240, Margin = new Thickness(0, 12, 0, 8) };
    public SettingsWindow(Storage store, Func<Task> apply)
    {
        storage = store; Title = "随口打 · 设置与历史"; Width = 620; Height = 680; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(245, 248, 250));
        var panel = new StackPanel { Margin = new Thickness(28) }; Content = new ScrollViewer { Content = panel };
        panel.Children.Add(new TextBlock { Text = "随口打", FontSize = 28, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "按住热键说话，松开后写入当前输入框。识别在本机完成。", Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(today);
        ComboBox AddChoice(string title, string[] items, string selected)
        {
            panel.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 8, 0, 4) });
            var box = new ComboBox { ItemsSource = items, SelectedItem = selected, Height = 30 }; panel.Children.Add(box); return box;
        }
        var model = AddChoice("模型（只加载 models 中已有的官方模型）", new[] { "tiny", "base", "small" }, storage.Config.Model);
        var language = AddChoice("识别语言", new[] { "auto", "zh", "en" }, storage.Config.Language);
        var key = AddChoice("按住说话热键", new[] { "F6", "F7", "F8", "F9", "F10", "F11", "F12" }, $"F{storage.Config.Hotkey - 0x6f}");
        var output = AddChoice("上屏方式", new[] { "auto", "paste", "type" }, storage.Config.OutputMode);
        var gpu = AddChoice("识别设备（本机 0 为 Intel，1 为 RTX 4070；-1 为 CPU）", new[] { "-1", "0", "1" }, storage.Config.GpuDevice.ToString());
        var saveHistory = new CheckBox { Content = "保存本地历史（关闭后不新增）", IsChecked = storage.Config.History, Margin = new Thickness(0, 12, 0, 6) }; panel.Children.Add(saveHistory);
        var punctuation = new CheckBox { Content = "自动标点", IsChecked = storage.Config.Punctuation }; panel.Children.Add(punctuation);
        var save = new Button { Content = "保存设置", Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(12, 6, 12, 6) };
        save.Click += async (_, _) =>
        {
            string chosen = (string)model.SelectedItem;
            if (!File.Exists(System.IO.Path.Combine(storage.Root, "models", $"ggml-{chosen}.bin"))) { System.Windows.MessageBox.Show("此模型尚未安装，程序不会联网下载。请按 README 获取官方模型。", "随口打"); return; }
            storage.Config.Model = chosen; storage.Config.Language = (string)language.SelectedItem;
            storage.Config.Hotkey = 0x6f + int.Parse(((string)key.SelectedItem)[1..]); storage.Config.OutputMode = (string)output.SelectedItem;
            storage.Config.GpuDevice = int.Parse((string)gpu.SelectedItem);
            storage.Config.History = saveHistory.IsChecked == true; storage.Config.Punctuation = punctuation.IsChecked == true; storage.Save(); await apply(); Refresh();
        };
        panel.Children.Add(save); panel.Children.Add(history);
        var clear = new Button { Content = "清除已存历史", Padding = new Thickness(10, 5, 10, 5) }; clear.Click += (_, _) => { storage.ClearHistory(); Refresh(); }; panel.Children.Add(clear);
        panel.Children.Add(new TextBlock { Text = "历史与统计仅写入程序目录的 data 文件夹。按 Esc 取消当前录音或识别。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });
    }
    public void Refresh() { today.Text = $"今日听写：{storage.Today} 字符"; history.ItemsSource = storage.History.Select(h => $"{h.Time:HH:mm:ss}  {h.Text}").ToArray(); }
}
