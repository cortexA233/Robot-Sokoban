# Unity 项目与 KToolkit 接入

## 引擎与依赖

本工程使用 **Unity 2022.3.51f1**。在 Unity Hub 中添加仓库根目录即可打开。

本文记录依赖接入及故障复现，只在环境/包问题或框架初始化修改时读取对应部分。日常实机操作和测试入口见 [UnityMcp.md](../Debug/UnityMcp.md)。

原始工程的依赖清单混用了 Unity 6 模板的包版本和模块，导致导航 API 缺失、测试框架无法解析，并进一步引起 Visual Studio 集成包的编译错误。本次使用本机 2022.3.51f1 自带的 Package Manager 版本清单修复：

| 包 | 原版本 | 当前版本 |
| --- | --- | --- |
| Universal RP | 17.3.0 | 14.0.11 |
| AI Navigation | 2.0.10 | 1.1.5 |
| Test Framework | 1.6.0 | 1.1.33 |
| Unity UI | 2.0.0 | 1.0.0 |

移除当前编辑器不提供的 Multiplayer Center、Accessibility、Adaptive Performance 和 Vector Graphics 模块依赖。保留兼容 2022.3 的 Input System 1.18.0、Timeline 1.8.10 和原有 IDE 工具版本。解析结果保存在 `Packages/packages-lock.json`。

Unity 2022.3 使用 URP 14，核心包版本随编辑器固定，参见 [Unity 官方包说明](https://docs.unity3d.com/2022.3/Documentation/Manual/com.unity.render-pipelines.universal.html)。

## KToolkit

- 上游：[cortexA233/KToolkit_for_unity](https://github.com/cortexA233/KToolkit_for_unity)。
- 来源提交：[`e85a1747f0524679cbfb43aecb7bbff41616e4ac`](https://github.com/cortexA233/KToolkit_for_unity/tree/e85a1747f0524679cbfb43aecb7bbff41616e4ac)。
- 接入目录：`Assets/Scripts/KToolkit_for_unity`。包含 Framework、Editor、Tests、程序集定义、原始 README 和 MIT 许可证。
- 采用上游支持的源码导入方式，源码随本仓库提交，后续更新可与上述来源提交比较。

### 本地兼容修改

1. `KToolkit.asmdef` 移除 `Unity.InputSystem.ForUI` 引用。该程序集属于 Unity 6 的输入集成，框架在本项目所需的 `Unity.InputSystem` 和 `UnityEngine.UI` 引用继续保留。
2. 上游单例初始化测试改为进入 Play Mode 后执行，避免 Unity 2022.3 在编辑模式调用 `DontDestroyOnLoad` 抛出异常。
3. 增加初始化检查，覆盖 Canvas、EventSystem、对象池根节点的创建和复用，以及框架 Update 驱动定时器的行为。

运行时框架源码保持上游内容。`KToolkit.Editor` 仅参与编辑器编译，测试程序集通过 `UNITY_INCLUDE_TESTS` 隔离。

导入完成后，Unity 顶部的 `KToolkit` 菜单提供状态机生成、可视化和运行时调试工具。

### 启动方式

在游戏自己的启动组件中调用一次：

```csharp
using KToolkit;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    private void Awake()
    {
        KFrameworkManager.instance.InitKFramework();
    }
}
```

初始化会建立持久化的框架对象、UI Canvas、EventSystem 和对象池父节点。项目启用新输入系统，框架会使用 `InputSystemUIInputModule`。框架管理器负责每帧更新 UI、定时器和 Tick 系统。上述代码是接入示例；当前正式入口为Bootstrap与LevelRunner，已有初始化调用。SampleScene保留为模板，不要再添加第二个初始化宿主。

## 验证方法

已有Editor实例时，优先通过 [MCP测试入口](../Debug/UnityMcp.md) 或Test Runner运行相关测试，无需关闭编辑器。仅在没有进程占用本工程、确需批处理复现时，使用下面的离线入口；切换到离线验证前保护未保存场景和草稿：

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.51f1\Editor\Unity.exe'
& $unityEditor -batchmode -nographics -projectPath $PWD.Path `
  -runTests -testPlatform EditMode -assemblyNames KToolkit.Tests.Editor `
  -testResults "$PWD/Logs/ktoolkit-tests.xml" -logFile "$PWD/Logs/ktoolkit-tests.log"
```

也可以在 Unity 的 Test Runner 中运行 `KToolkit.Tests.Editor`。其中运行时测试会自动进入并退出 Play Mode。

### 本次验证结果（2026-09-15）

- Unity 2022.3.51f1 编辑器和 Windows Player 脚本编译通过，无 C# 编译错误。
- KToolkit 测试 **3/3 通过**，包括两项自动进入 Play Mode 的运行时检查。本地结果：`Logs/ktoolkit-tests.xml`。
- Windows x64 构建成功。本地产物：`Builds/SetupSmoke/Sokoban_3D_Test.exe`；日志：`Logs/setup-build.log`。
- 本次构建使用现有 SampleScene 验证项目接入，游戏功能按 GDD 后续实现。

构建和验证日志由 `.gitignore` 排除。复现构建：

```powershell
& $unityEditor -batchmode -quit -projectPath $PWD.Path `
  -buildWindows64Player "$PWD/Builds/SetupSmoke/Sokoban_3D_Test.exe" `
  -logFile "$PWD/Logs/setup-build.log"
```
