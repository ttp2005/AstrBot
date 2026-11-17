# 桌面秀实现计划

## Context

用户需要一个 Windows“桌面秀”应用，用可弹出的侧边栏分类整理桌面图标。当前 `/workspace` 是无关的 AstrBot Python/Vue 工程，没有可复用的 Windows 桌面应用，因此在 `/workspace/desktop-show` 新建独立工程，不修改 AstrBot 业务代码。

采用低风险的“侧边栏分组启动器”：读取并分类展示真实桌面项目，但不调整 Windows 原生桌面图标位置，也不移动、删除或重命名文件。

## 技术方案

使用 **C# WPF + .NET 8（`net8.0-windows`）**，仅依赖 BCL、WPF 和少量 Win32 Shell P/Invoke；测试使用 xUnit。不引入数据库、DI 容器或插件系统。

## 实现步骤

1. **创建独立 WPF 工程**
   - 新建解决方案、应用项目与测试项目。
   - 配置 Windows 10/11、x64/AnyCPU 和中文界面资源。

2. **读取桌面项目并提取图标**
   - 在 `DesktopScanner.cs` 中非递归读取当前用户桌面与公共桌面，按路径忽略大小写去重，跳过 `desktop.ini`。
   - 单个目录不可读时继续处理另一个目录。
   - 使用 `SHGetFileInfo` 获取文件、文件夹和快捷方式图标，转换为冻结的 `BitmapSource`，并释放 `HICON`。
   - 使用 `UseShellExecute = true` 双击打开项目，失败时显示明确提示。

3. **实现分组与持久化**
   - 在 `DesktopItem.cs` 中定义项目模型，默认分为“快捷方式、文件夹、文件”。
   - 支持拖放改变分组、刷新列表；拖拽只更新应用内分组，不产生文件移动效果。
   - 在 `CategoryStore.cs` 中用 `System.Text.Json` 将路径到分组的映射保存到 `%LocalAppData%\DesktopShow\categories.json`；启动时加载，损坏配置回退默认分组，刷新时清理失效映射。

4. **实现可弹出侧边栏**
   - 在 `MainWindow.xaml` 和 `MainWindow.xaml.cs` 中实现约 320 DIP 宽的右边缘窗口、Shell 图标列表、分组标题、刷新与置顶按钮。
   - 鼠标靠近边缘时展开，离开后延时收起；固定展开或拖拽期间不自动收起。
   - 处理多显示器工作区与 DPI 缩放，避免遮挡任务栏。

5. **补充测试**
   - 在 `tests/DesktopShow.Tests` 中覆盖默认分类、路径去重、缺失目录、JSON 往返、损坏配置回退和失效映射清理。

## 关键文件

- `/workspace/desktop-show/DesktopShow.csproj`
- `/workspace/desktop-show/App.xaml`
- `/workspace/desktop-show/MainWindow.xaml`
- `/workspace/desktop-show/MainWindow.xaml.cs`
- `/workspace/desktop-show/DesktopItem.cs`
- `/workspace/desktop-show/DesktopScanner.cs`
- `/workspace/desktop-show/CategoryStore.cs`
- `/workspace/desktop-show/tests/DesktopShow.Tests/DesktopShow.Tests.csproj`

## 验证

- 若沙箱具备 .NET SDK，可执行 `dotnet build -p:EnableWindowsTargeting=true` 和可运行的单元测试；Linux 无法实际运行 WPF 或验证 Shell 图标与边缘交互。
- 在 Windows 10/11 上验收：快捷方式、程序、普通文件及文件夹的图标和双击打开；拖放分组并在重启后保持；刷新、损坏配置回退；自动隐藏、固定展开、置顶；多显示器及 125%/150% DPI。
- 确认应用不会调整原生桌面图标，也不会移动、删除或重命名任何桌面文件。
