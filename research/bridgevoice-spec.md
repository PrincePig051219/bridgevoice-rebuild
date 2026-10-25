# BridgeVoice 行为研究（登录边界内）

2026-10-25；本机目标 `D:\BridgeVoice\bridgevoice.exe`，SHA256 `52eb5985fd6838f4bcc4e84924544eb84205cef8c031b5e472804bcc8456aa28`。本报告不声称完成登录后功能的行为验证，不读取凭据文件或操作账号、订阅。

## 技术栈与体积

**所见（本机实测）**：[REA 目录证据](evidence/original-bridgevoice/desktop.json)记录主程序 68,923,904 字节、原生 x64 PE，安装目录模型文件夹只有空占位文件。界面可访问性树出现 `TAURI_DRAG_RESIZE_WINDOW`、`http://tauri.localhost/`；[Rust 检查](evidence/original-bridgevoice/rust.json)见编译器路径，但没有 cargo-auditable 元数据。因此 Tauri/WebView 界面由运行观察支持，完整依赖版本仍未知，不能称为 Electron/.NET。

**所见（本机实测）**：本地数据目录另有 `ggml-base.en.bin`，147,964,211 字节，摘要见[模型身份](evidence/original-bridgevoice/installed-model-identity.json)。只读取身份，不复制模型。二进制中的 whisper/ggml、cpal、Tauri 等字符串为静态线索；不能把字符串存在当作实际调用证明。**推断**：使用 Whisper 系列本地识别链，base.en 是英文模型；本机设置指向该模型。完整可选引擎、模型集合及中英文实际效果未知。

## 已见配置与功能边界

[设置白名单记录](evidence/original-bridgevoice/settings-observation.json)只采集听写相关设置，排除云端值、词典内容、指令内容与凭据。配置文件字段不等于设置页全表或执行路径。

| 功能 | 所见（本机实测：配置/界面） | 行为未知项 |
| --- | --- | --- |
| 触发 | activate=Ctrl+Shift+Space；push_to_talk=Control+Meta；cancel=Escape；toggle_recording=null | 实际按住、释放、取消处理和冲突；本轮未发送系统组合热键 |
| 录音指示 | show_overlay=true，bottom-center，logo-and-text | 录音波形、计时、麦克风使用全过程未测 |
| 引擎 | mode=local，model=base.en，language=null | 实际加载、语言自动检测和中英识别未测 |
| 文本处理 | auto_punctuate=true，remove_fillers=true | 标点质量、语音“换行/句号”命令未测 |
| 音频 | 首选/备用设备为空，input_gain=1，提示音开启，不静音系统音频 | 设备切换、掉线、静音边界未测 |
| 上屏 | copy_to_clipboard=false | 不能据此排除临时剪贴板粘贴；实际粘贴/模拟键入链及延迟未知 |
| 历史/统计 | 登录前没有可观察入口 | 保存位置、关闭行为、保留策略未知 |
| 离线 | 欢迎页宣称本地；配置有 local，也有 cloud_provider 字段 | 未完成断网听写，不能认定全产品绝不联网 |

## 运行门禁与实测证据

**所见（本机实测）**：[欢迎页截图](evidence/original-bridgevoice/welcome.jpg)与[可访问性记录](evidence/original-bridgevoice/welcome-accessibility.json)只有登录/注册入口，未看到无需登录的听写入口。未点击认证按钮，未尝试绕过登录。故中英文、自动标点、语音口令、历史、设置页全表、断网听写和上屏延迟均标为**未测/未知**。单看 local 配置或欢迎页承诺，不能代替断网验证。

## 独立实现约束

“随口打”的功能来自用户给定需求，由独立设计实现；不复制该程序代码、图标、文案、模型或认证流程。采用开源官方获取的 multilingual small 模型。测试集为固定 20 中文＋10 英文合成音频；原版因当前登录边界未能接收该音频，原版各项结果保持未测。自研侧的准确率、延迟和上屏实测独立记录，不宣称优于原版。
