# 同音频对照与验收（2026-10-25）

**结论：已形成可运行审核版，尚未完成全部验收。** 原 BridgeVoice 停在登录欢迎页，未进入识别/设置/上屏流程；因此没有原版成绩，不能声称优于原版。微信要求重新扫码，未继续登录或发送消息。下面是随口打的本机实测，不是双边已完成对比。

## 方法与口径

20句中文＋10句英文由本项目编写，通过本机 SAPI 合成成16kHz WAV（`tests/corpus.json` 与 `tests/audio/`）。同一批固定音频供后续两边复现；这次只成功喂给独立实现。音频 SHA256 在 `benchmark-release.json` 每行，统一清单在 `evidence/SHA256SUMS.txt`。合成语音不代表自然人、方言、噪声的整体表现。

本机 i9-13900HX、RTX4070 Laptop，Whisper.net 1.9.1 / Vulkan GPU1、官方 multilingual small，先预热，再逐句识别；中文使用本地繁简转换。CER 按NFKC、小写、去非字母数字后的Levenshtein距离计，数字书写不做同义归一化。英文另报按单词的WER。自动标点不计入CER，不能用CER证明标点全正确。

`benchmark-release.json` 是纯文件识别时间；`pipeline-*.json` 从WAV准备好开始，包含识别、输入、剪贴板等待和核验。**后者也不等于物理松键到屏幕首次显示**，没有以截图时间冒充精确可见延迟。1秒目标未整体过关，物理热键＋自然麦克风完整链仍需验收。

## 汇总

| 指标 | 随口打（所见：本机实测） | 原 BridgeVoice |
|---|---|---|
| 中文 CER | 8/294 = 2.72% | 未测：登录边界 |
| 英文 CER / WER | 5/387 = 1.29% / 1/76 = 1.32% | 未测：登录边界 |
| 记事本上屏 | 30/30；流程完成均值1239.1ms，范围831.9–1962.8ms；小于1秒7/30 | 未测：登录边界 |
| Word上屏 | 30/30；流程完成均值2474.6ms，范围2075.7–3327.8ms；小于1秒0/30 | 未测：登录边界 |
| 微信输入框 | 未测：提示需重新扫码；没有输入聊天或发送消息 | 未测 |
| 2秒纯静音 | VAD返回空结果（SilenceRejected=true） | 未测 |
| 麦克风/浮窗 | recorder-smoke采到98,880样本/6.18秒；峰值很低，未保存音频；只证明设备采集路径 | 未测 |
| 断网使用 | 无联网代码；运行中一次TCP采样为0。未做物理断网完整验收，不标已证成 | 未测 |

## 每句识别结果

所有行原版均“未测：登录边界”；记事本/Word两条流水线使用相同音频，但识别文本以各自流水线日志为准，外部回读与各自实际输出比较，不拿参考答案代替识别结果。

| ID | 参考句 | 识别结果 | 字错/字符 | 纯识别ms | 记事本回读 | Word回读 |
|---|---|---|---:|---:|---|---|
| zh01 | 今天下午三点开会，请提前准备好材料。 | 今天下午三点开会,请提前准备好材料。 | 0/16 | 402.0 | 通过 | 通过 |
| zh02 | 请把这份报告保存在桌面的工作文件夹里。 | 请把这份报告保存在桌面的工作文件家里。 | 1/18 | 409.6 | 通过 | 通过 |
| zh03 | 明天北京可能有雨，出门记得带伞。 | 明天北京可能有雨,出门记得带伞。 | 0/14 | 383.5 | 通过 | 通过 |
| zh04 | 这个项目需要先完成研究，再开始开发。 | 这个项目需要先完成研究,再开始开发。 | 0/16 | 373.4 | 通过 | 通过 |
| zh05 | 我想订两张周五晚上八点的电影票。 | 我想订两张周五晚上8点的电影票。 | 1/15 | 355.2 | 通过 | 通过 |
| zh06 | 会议结束以后，我们一起核对测试结果。 | 会议结束以后,我们一起核对测试结果。 | 0/16 | 406.8 | 通过 | 通过 |
| zh07 | 把背景颜色改成浅蓝色，文字保持黑色。 | 把背景颜色改成浅蓝色,文字保持黑色。 | 0/16 | 411.5 | 通过 | 通过 |
| zh08 | 如果网络断开，程序也应该正常运行。 | 如果网络断开,程序也应该正常运行。 | 0/15 | 409.2 | 通过 | 通过 |
| zh09 | 这条记录包含中文、英文和数字。 | 这条记录包含中文、英文和数字。 | 0/13 | 409.3 | 通过 | 通过 |
| zh10 | 请确认文件已经备份，然后再修改设置。 | 请确认文件已经备份,然后再修改设置。 | 0/16 | 439.2 | 通过 | 通过 |
| zh11 | 这杯咖啡不要加糖，谢谢。 | 这杯咖啡不要加糖,谢谢! | 0/10 | 373.0 | 通过 | 通过 |
| zh12 | 周末我准备去图书馆借几本书。 | 周末我准备去图书馆借几本书。 | 0/13 | 416.6 | 通过 | 通过 |
| zh13 | 当前窗口没有响应，需要记录真实状态。 | 当前窗口没有响应,需要记录真实状态。 | 0/16 | 438.7 | 通过 | 通过 |
| zh14 | 请不要把推断写成已经验证的事实。 | 请不要把推断写成已经验证的事实。ᄹ。 | 1/15 | 480.7 | 通过 | 通过 |
| zh15 | 按住快捷键说话，松开以后显示文字。 | 按住快捷键说话,松开以后显示文字。 | 0/15 | 561.6 | 通过 | 通过 |
| zh16 | 我们需要比较准确率、延迟和输入成功率。 | 我们需要比较准确率,延迟和输入成功率。 | 0/17 | 538.3 | 通过 | 通过 |
| zh17 | 这段录音前后有一点静音。 | 这段录音前後有一点静音。 | 1/11 | 407.9 | 通过 | 通过 |
| zh18 | 新的版本修复了文件对话框的问题。 | 新的版本修复了文件对话矿的问题。 | 1/15 | 512.9 | 通过 | 通过 |
| zh19 | 请检查所有证据是否都有摘要。 | 请检查所有证据是否都有摘药。 | 1/13 | 469.3 | 通过 | 通过 |
| zh20 | 完成以后停止，不要继续增加功能。 | 完成以後停止,不要继续增加功能。ఽ。 | 2/14 | 412.0 | 通过 | 通过 |
| en01 | Please save the report in the project folder. | Please save the report in the project folder. | 0/37 | 371.7 | 通过 | 通过 |
| en02 | The meeting starts at three in the afternoon. | The meeting starts at 3 in the afternoon. | 5/37 | 391.0 | 通过 | 通过 |
| en03 | This application should work without an internet connection. | This application should work without an internet connection. | 0/52 | 382.8 | 通过 | 通过 |
| en04 | Hold the shortcut key and speak clearly. | Hold the shortcut key and speak clearly. | 0/33 | 410.3 | 通过 | 通过 |
| en05 | Check the evidence before writing the conclusion. | Check the evidence before writing the conclusion. | 0/42 | 380.4 | 通过 | 通过 |
| en06 | I would like a cup of coffee without sugar. | I would like a cup of coffee without sugar. | 0/34 | 389.8 | 通过 | 通过 |
| en07 | The new version includes automatic punctuation. | The new version includes automatic punctuation. | 0/41 | 406.5 | 通过 | 通过 |
| en08 | Keep a backup before changing the settings. | Keep a backup before changing the settings. | 0/36 | 399.7 | 通过 | 通过 |
| en09 | We need to measure accuracy and response time. | We need to measure accuracy and response time. | 0/38 | 384.8 | 通过 | 通过 |
| en10 | Stop when all the requested work is complete. | Stop when all the requested work is complete. | 0/37 | 394.0 | 通过 | 通过 |

## 输入故障、修复与证据

- **所见（本机实测）**：早恢复剪贴板会让Word粘入旧内容；恢复等待由200ms延至1500ms后，`pipeline-word-restoration-fix.json`＋`word-restoration-fix-readback.json`确认30/30。COM只读本次从空白开始的“文档1”，没有打开/保存用户文件。
- **所见（本机实测）**：本机新记事本 `RichEditD2DPT` 对异步Unicode输入出现丢字/重复，剪贴板方案亦未全过；失败日志保留，不能挑选成功行当最终结果。当前auto对这个已识别控件走有超时的同步 `WM_CHAR` 字符输入，最终 `pipeline-notepad-sync.json`＋`notepad-sync-readback.json`确认30/30，每句只出现一次；其他程序仍先剪贴板。此兼容分支是自研选择，不声称原版这样做。不是系统级输入驱动修复。
- **推断**：异步投递与编辑器消息消费有关，但没有完成内核/编辑器内部根因证明。同步输入只是经过该用例验证的规避方式。通用SendInput fallback在其他应用仍需测试。
- **所见（本机实测）**：设置页可用，见settings-review.jpg；采集计数见recorder-smoke.json。历史/统计、物理按住热键、中英自动识别、噪声/VAD边界还没有完成同条件端到端测试，不能因代码存在就标验收通过。
- **未知/未过**：原版识别准确率、上屏机制、延迟、离线/命令功能；微信上屏；物理松键1秒指标；其他权限层级/密码框；剪贴板私有格式全面保真。自动标点独立实现已具备，但语音口令解释器没有做，未冒充原版兼容。

## 审核包

[私有审核版 Release](https://github.com/PrincePig051219/bridgevoice-rebuild/releases/tag/v0.1.0-review)包含Windows x64自包含程序、官方small模型、许可证与本报告。先核对SHA256再解压到E盘。源码、30个测试音频和原始证据在Git；包摘要见evidence/package.json。不能把本次提交称为全部验收达标。
