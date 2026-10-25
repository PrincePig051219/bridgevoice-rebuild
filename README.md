# 随口打

Windows x64 本地听写审核版。独立 C# / .NET 8 WPF 实现，使用官方 Whisper small 多语言模型，未复制 BridgeVoice 的代码、图标、文案或模型。原版需要登录的功能未测，见 [研究规格](research/bridgevoice-spec.md)。

## 运行

从本私有仓库的 Releases 下载完整便携包，核对同页 SHA256 后解压到 E 盘，运行 `Suikouda.exe`。包内含 .NET 运行时和 small 模型，无需联网下载。首次启动加载模型；托盘就绪后，把光标放入输入框，按住 F8 说话，松开识别并上屏，Esc 取消。托盘双击打开设置与历史，右键可退出或复制最近一次结果。

设置支持热键 F6–F12、auto/中文/英文、tiny/base/small（只加载已有本地模型）、剪贴板/模拟输入、历史开关、自动标点、识别设备。此机 Vulkan 设备 0 是 Intel 核显，1 是 RTX 4070；提供的本机配置选 1。换机请按实际设备调整，-1 使用 CPU。缺少模型会明确提示，不自动联网。

数据仅写程序目录 `data/`。关闭历史后不再新增；“清除已存历史”移除本地历史，字数统计独立保存。最近一次结果只保留在内存供复制。程序没有账号、云识别、更新器、遥测或网络请求代码。

## 状态与限制

- 这是可运行的审核版，验收数据与未达标项见 [对照表](research/comparison.md)，不能将纯识别时间当作物理松键上屏时间。
- 需要麦克风访问权限；提升权限的软件、密码框、非标准输入框不保证可输入。焦点无法恢复时停止注入，结果可从托盘复制。
- 剪贴板先保存再粘贴，若期间用户复制了新内容，程序不会覆盖新剪贴板。某些私有剪贴板格式不能保证无损保存；未测应用不会标为成功。
- VAD 为本地能量门限＋边缘留白，不是神经网络 VAD；极小声、嘈杂环境和持续背景声需进一步验证。自动标点来自模型并补末尾句号，不包含独立语音口令系统。
- 中文输出用 Windows 本地繁简映射，缺少映射时保留原结果。尚无词级领域纠错；数字书写差异按原始对照计错。

## 构建与复现

安装 Microsoft 官方 .NET 8 SDK 至 E 盘。当前 `scripts/build.ps1` 使用 `E:/dev-tools/dotnet8`，包缓存与临时目录均在 E 盘。依赖锁定在 `packages.lock.json`，构建只从 NuGet 获取 Whisper.net 1.9.1 官方包。

模型来源：whisper.cpp 官方作者仓库 [ggerganov/whisper.cpp](https://huggingface.co/ggerganov/whisper.cpp)，文件 `ggml-small.bin`，487601967 bytes，SHA256 `1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b`。放入 `models/`。模型与发布包作为私有 Release 资产交付，避免 Git 单文件100MB限制；源码、测试音频、日志及逐文件大小清单在 Git。

```powershell
./scripts/build.ps1
./dist/Suikouda/Suikouda.exe --root . --benchmark ./tests/corpus.json ./research/evidence/reproduction.json
```

`--pipeline-test <HWND> <corpus.json> <output.json>` 会向指定窗口追加测试文本，仅用于自己新建的空白文档；不要指向聊天发送区或用户文档。`--recorder-smoke <output.json>` 测麦克风采集与浮窗，记录计数和峰值，不保存音频。测试语料为本项目写的20中10英短句，用本机 SAPI 合成，不代表自然说话人表现。

第三方许可证在 `licenses/`；软件本体源码仅在本私有仓库供审核。

审核入口：可用 `Suikouda.exe --settings` 直接打开设置页。本机记事本auto采用同步字符消息兼容；通用目标先走剪贴板。最后30句测试在记事本和Word各30/30，但端到端1秒与微信/原版对照尚未通过，详见对照表。
