# TaskbarOLED · 任务栏 OLED 调暗工具

一个用于 Windows 11 的小工具：鼠标连续 60 秒没有进入任务栏时，在主任务栏上覆盖半透明黑色遮罩；鼠标移入后撤下遮罩。

项目源于 OLED 笔记本的日常使用需求。它提供任务栏调暗功能，不能保证避免 OLED 烧屏，也不提供电池充电限制或整屏像素维护功能。

## 功能

- 默认等待 60 秒，黑色遮罩不透明度 70%，鼠标检查间隔 100 ms。
- 无边框、鼠标穿透、不抢焦点，仅覆盖主任务栏。
- 尝试检测任务栏隐藏、窗口覆盖任务栏或全屏窗口，并在检测到时撤下遮罩；兼容范围见下方说明。
- 托盘菜单支持暂停、恢复和退出；重复启动只保留一个实例。
- 不修改 Explorer、不注入 DLL、不更改注册表或电源方案。

## 编译和启动

已在 Windows 11、Windows PowerShell 5.1 和 .NET Framework 4.x 环境中编译、测试。无需额外下载 .NET SDK，无需管理员权限。

仓库当前提供源码和编译脚本，没有发布预编译 EXE 或安装包。运行时依赖 Windows 自带的 .NET Framework、Windows Forms、System.Drawing，以及 Windows 的 `user32.dll`、`dwmapi.dll`；没有 NuGet 包、第三方控制器或随仓库分发的运行库。

下载或克隆源码后，在项目目录运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

构建结果位于 `dist` 文件夹，包含 `TaskbarOLED.exe` 和 `TaskbarOLED.ini`。双击 EXE 启动，并将鼠标移出任务栏，等待 60 秒。

执行策略参数只用于本次编译进程，不修改系统执行策略。重新编译前，请将旧的 `dist\TaskbarOLED.exe` 移至其他位置；脚本不会覆盖已有 EXE。

## 退出与暂停

- 退出快捷键：**Ctrl + Alt + Shift + O**（字母 O）。
- 或右键系统托盘里的 **Taskbar OLED** 图标，选择 **Exit**。
- 或在 EXE 所在目录运行 `TaskbarOLED.exe --stop`。
- 托盘菜单 **Pause / resume** 可暂停或恢复。

退出会销毁遮罩窗口。程序没有需要撤销的系统设置。

## 参数

编辑 EXE 旁边的 `TaskbarOLED.ini`，保存后退出并重新启动：

```ini
IdleSeconds=60
BlackPercent=70
PollMilliseconds=100
```

`BlackPercent` 是黑色混合不透明度，不能当作屏幕物理亮度的精确降幅。支持范围分别为 1～86400 秒、1～95%、50～200 ms；超出范围的数字会被限制到边界。

## 登录自启（可选）

1. 将 EXE 和 INI 放在准备长期保留的文件夹中。
2. 按 `Win + R`，输入 `shell:startup` 并回车。
3. 在该文件夹中放入 EXE 的快捷方式。

删除此快捷方式即可取消自启。移动程序位置后，请重新创建快捷方式。

## 已有验证与限制

最初版本在一台 Windows 11 OLED 笔记本上完成了任务栏调暗、鼠标穿透、不抢焦点、鼠标移入恢复、全屏窗口避让和退出清理检查。一次检查中，鼠标移入约 106 ms 后撤下遮罩；这是单机观测结果，不是所有机器的响应保证。使用者随后试用一天并反馈使用正常。这些检查和反馈不等同于长期可靠性或安全性认证，也没有测量 OLED 老化效果。

目前仅支持主任务栏。其他 Windows 版本、HDR、显示器热插拔、待机恢复、第三方任务栏及所有独占全屏游戏尚未全面测试。它使用 Windows 自带的 `Shell_TrayWnd` 任务栏窗口，系统更新后的兼容性可能需要调整。

源码见 `source/TaskbarOLED.cs`。启动时加入 `--diagnostics` 可在 EXE 旁生成 `verification.log`，供本地排查；默认不生成日志。日志可能包含本地时间、窗口句柄、屏幕坐标，以及出错时的异常信息和路径。提交问题时请先检查并删去不希望公开的内容，避免上传完整桌面截图或未处理的日志。

## 隐私与程序行为

程序在本机定时读取鼠标位置、任务栏区域及前台窗口区域，用于判断遮罩是否应该显示。它注册一个退出快捷键，不记录键盘输入，也不读取窗口标题、截图或剪贴板内容。源码没有联网、遥测、自动更新、进程注入或自动安装功能。

程序读取同目录的 INI；仅在明确启用 `--diagnostics` 时写入上述本地日志。自启需要使用者手动创建快捷方式，程序不会自行添加。退出快捷键若被其他应用占用，可能无法注册；仍可使用托盘菜单或 `--stop` 退出。

## AI 参与与项目定位

这是一个个人学习项目。需求和使用反馈由项目维护者提供；初始 C# 实现、编译脚本、部分验证及文档整理由 OpenAI 的 ChatGPT / Codex 协助完成。该说明不代表维护者独立手写了全部代码，也不代表已经接受独立安全审计。

## 许可状态

当前尚未选择开源许可证，因此没有 `LICENSE` 文件。公开源码用于项目展示和学习交流，不代表授予通用的修改、分发或商用许可；GitHub 平台条款允许的查看和 fork 不受此说明限制。若希望获得其他使用授权，请先与维护者确认。[GitHub 关于未指定许可证的说明](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository)。
