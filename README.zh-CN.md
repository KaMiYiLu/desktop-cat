# 桌面猫咪 / Desktop Cat

[English](README.md) | **简体中文** | [日本語](README.ja.md)

一只会在 Windows 桌面闲逛、舔爪、跳跃、冲刺和抱爪睡觉的猫咪。使用 C#、Windows Forms 与 GDI+ 绘制连续朝向的猫咪，并支持抓起放下、跟随鼠标和声音开关。

[项目网站与下载](https://desktop-cat.aitoast.cn/) · [问题反馈](https://github.com/KaMiYiLu/desktop-cat/issues)

本仓库公开提供桌面猫咪的源代码。项目许可证尚未确定，代码和素材来源核对状态见 [SOURCE-STATUS.md](SOURCE-STATUS.md)。

## 功能

- 左键按住猫咪可以拎起，放下后出现随机动作。
- 闲逛、舔爪、原地跳、前进跳、跑步、冲刺、泥脚印和抱爪睡觉。
- 行走时偶尔抖单耳或双耳，停下时有摆尾与观察鼠标的小动作。
- 控制台可切换跟随、允许攻击鼠标、允许抓猫、声音总开关及独立脚步声。
- 默认允许攻击鼠标；跟随和定时随机攻击关闭。抓放反应可能抽到追鼠标，可在控制台关闭。
- 启动只显示猫咪，不显示系统托盘图标；按住 Esc 并右键点击猫咪打开控制台。使用组合键后先松开 Esc，再单独长按约 3 秒退出。
- 设置保存在程序旁 config.ini；日常运行不创建日志。

## 构建与运行

需要 Windows、PowerShell 和系统 .NET Framework 4.x C# 编译器。当前开发机可构建；Windows 各版本、高 DPI 与多屏尚未完成全面兼容性测试。本工程不依赖宣传网页、原版大鹅程序或私有服务器。

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\artifacts\bin\DesktopCat-v75.exe
```

版本号只在 [VERSION](VERSION) 维护，EXE 版本信息和发布包文件名由脚本生成。首次构建从 config.example.ini 复制配置；再次构建保留输出目录中已有的用户设置。可使用 build.ps1 的 OutputDirectory 参数选择输出目录。

## 检查与用户包

```powershell
# 绘制、姿态、小动作、舔爪和发布包检查
powershell -ExecutionPolicy Bypass -File .\tools\test.ps1

# 有桌面环境时，加测控制台、抓放、动作完成和无日志行为
powershell -ExecutionPolicy Bypass -File .\tools\test.ps1 -Desktop

# 有音频输出设备时，加测真实音频缓冲，测试样本振幅为零
powershell -ExecutionPolicy Bypass -File .\tools\test.ps1 -Audio

powershell -ExecutionPolicy Bypass -File .\tools\package-release.ps1
```

发布输出在 artifacts/release。用户 ZIP 里只有 DesktopCat.exe、config.ini 和使用说明.txt；SHA-256 文件在 ZIP 外。诊断命令只有显式运行时才会写测试报告，均由脚本放在 artifacts 下。

导出供审阅的源码候选包：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\export-source.ps1
```

导出范围由 SOURCE-FILES.txt 明确列出，结果位于 artifacts/source；新增公开源文件时同步维护清单。此源码候选包与供普通用户运行的三文件包是两种不同产物。

`.github/workflows/ci.yml` 是 GitHub 上的 Windows 检查流程，只构建、检查和打包，不自动发布，不需要网页或网站部署凭据。首轮基础 Windows CI 已在 GitHub runner 通过；桌面交互和音频缓冲检查另在本地验证。

## 目录

```text
src/                    应用源码及诊断入口
Assets/                 构建所需的猫咪图标、已选音效和文本
tools/                  用户包与检查脚本、图标生成器源码
docs/USER-GUIDE.txt      用户说明模板
VERSION                 唯一版本来源
config.example.ini      干净的默认配置
THIRD-PARTY-NOTICES.md   第三方音效作者与来源
SOURCE-STATUS.md         发布前来源核对与授权待办
```

## 灵感与来源

受 [samperson 的 Desktop Goose](https://samperson.itch.io/desktop-goose) 启发的独立猫咪改编项目，非官方作品。本项目不声称获得原作者背书。第三方音效单独记录在 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)，项目代码及图标的最终授权范围仍待确认。

问题反馈与功能建议欢迎提交到 [Issues](https://github.com/KaMiYiLu/desktop-cat/issues)。
