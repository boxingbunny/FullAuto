# AutoRaidHelper 工作记录

日期：2026-05-04

## 背景

这个项目在 AEAssistV3 宿主里加载时，先后出现了两类运行时异常：

- `MissingMethodException`，指向 `Dalamud.Interface.Windowing.WindowSystem.AddWindow(Window)`
- `TypeLoadException`，指向 `Dalamud.Interface.Windowing.Window.WindowSizeConstraints`

另外，项目编译配置还存在 `MSIL` 和 `AMD64` 引用架构不一致的警告。

## 已处理内容

### 1. 处理 `WindowSystem.AddWindow` 的运行时签名不匹配

在 `AutoRaidHelper.Plugin.AutoRaidHelper.OnLoad` 中，原先直接调用 `WindowSystem.AddWindow(...)`。
为了避免宿主环境中 `Dalamud` 版本差异导致的静态绑定失败，改成了反射兼容层：

- 新增 `AutoRaidHelper/Utils/WindowSystemCompat.cs`
- 用 `WindowSystemCompat.AddWindow(...)` 替换直接调用
- 卸载时也通过兼容层调用 `RemoveAllWindows(...)`

### 2. 处理 `WindowSizeConstraints` 的类型加载失败

`MainWindow` 构造函数里原先直接使用 `SizeConstraints = new WindowSizeConstraints { ... }`。
这个类型在当前运行时 `Dalamud` 版本里触发了 `TypeLoadException`。

处理方式：

- 移除构造函数里对 `WindowSizeConstraints` 的直接引用
- 在 `MainWindow.PreDraw()` 里改为直接调用 `ImGui.SetNextWindowSizeConstraints(...)`

### 3. 对齐项目平台目标

`AutoRaidHelper.csproj` 原本没有显式设置 `x64`，构建时出现 `MSIL` 对 `AMD64` 的引用警告。

处理方式：

- 在 `AutoRaidHelper.csproj` 中增加：
  - `PlatformTarget>x64</PlatformTarget>`
  - `Platforms>x64</Platforms>`

## 验证结果

已执行：

```bash
dotnet build E:\git\ARH\AutoRaidHelper\AutoRaidHelper.csproj -c Debug
```

结果：

- 编译成功
- 仍有少量既有警告
- 关键的 `MissingMethodException` / `TypeLoadException` 已通过代码层面规避

## 当前残留项

- `UI/DebugPrintTab.cs` 仍有一个空引用相关警告
- `Helpers/CleanBackgroundManager.cs` 仍有一个未使用字段警告
- `AutoRaidHelper/Output` 下的生成文件仍会随着编译更新

## 备注

这份记录只总结了本次我在项目里做的排查和修复，不包含仓库里其他未处理的本地改动。
