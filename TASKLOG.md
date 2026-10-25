# 任务记录

2026-10-25：从已安装 BridgeVoice 的技术栈、目录、配置白名单和原生导入收集行为线索。只观察登录欢迎页，未进入账户、订阅，未复制其模型、界面素材或程序代码。

独立创建 C#/.NET8 WPF 托盘程序。使用 WinMM 采音、全局键盘 Hook、能量 VAD、Whisper.net 本地 small、Vulkan/CPU、底部波形计时浮窗、剪贴板恢复与模拟输入、历史开关和字数统计。全部新工具、模型、项目、缓存放 E 盘，官方下载文件先核验摘要。

先用默认设备完成30句基线，平均识别约5秒；加入原生日志证实 GPU 0 是 Intel、GPU 1 是 RTX4070。切换设备并启用 FlashAttention/greedy best-of 1 后识别显著缩短。保留失败与中止试验，不覆盖原始基线。繁简映射由 Windows 本地 API 完成。

上屏与识别独立计时，只有实际回读出现测试文本才算确认；未验证不能计成功。原版30句因登录边界全部未测，未编造原版结果。最终数字见 comparison.md 及 evidence 原件。

## 最终复测与交付

- 发布源码为C#/.NET8 WPF独立实现，官方Whisper.net1.9.1和官方small模型。模型SHA256核验后存E盘。
- 原BridgeVoice登录门禁未进入，微信重新扫码门禁未进入，不碰账号。原版未测数据没有填数。
- 最终benchmark-release：中文CER8/294，英文WER1/76。最终记事本同步WM_CHAR兼容和Word延后剪贴板恢复：各30/30外部回读。失败试验保留，误差与计时范围见comparison.md。
- 麦克风冒烟采到98880样本，设置页已验看；物理热键整链、真实断网、1秒全链仍未完整过验收。以审核版交付，不伪称正式完成版。
- 程序/模型完整包为私有Release资产；Git含源码、规格、30句音频、原始日志、SHA256和大小清单。上传前秘密扫描，上传后核对私有状态、提交、Git blob/大小与Release摘要。

- 实际解压最终审核ZIP到E:/rea-tools/suikouda-package-review后，从包内启动程序、加载包内small模型：退出0，Vulkan、模型摘要一致，单句识别与纯静音拒绝通过。package-smoke.json及native.log存证；该冒烟不代替完整物理热键验收。
