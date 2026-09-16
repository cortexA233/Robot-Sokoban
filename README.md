# Sokoban_3D_Test

使用 **Unity 2022.3.51f1** 打开仓库根目录。

当前为**第一轮可玩作者工具里程碑**，尚未达到 GDD 的完整 Take-home 交付标准。

- 打开 **Tools > Sokoban > Level Editor**，左侧选择 L01/L02/L03 或 LAB01，再点击“试玩并录制”。
- 可以从空白图放地形、玩家、箱子、插槽与门；门的供电来源在右侧勾选。校验错误可点击定位。
- 试玩：WASD 为镜头相对方向，方向键为世界方向；V 切换跟随/俯视，Z 撤销，R 重开，Esc 暂停。通关后可保存参考解法，再用 Unity Play 按钮退出并返回原设计。
- 直接打开 `Assets/Scenes/Bootstrap.unity` 并 Play 会加载 L01。当前没有正式主菜单、选关或进度存档。
- 本机开发构建：`Builds/AuthoringMilestone/StationRestart.exe`；已验证启动，尚非最终交付构建。
- [首轮关卡编辑器使用说明](Docs/LevelEditorGuide.md)
- [实现状态、验证结果和剩余范围](Docs/ImplementationProgress.md)

- 渲染管线：URP 14.0.11。
- 输入：Input System 1.18.0，使用新输入系统。
- 相机：Cinemachine 2.10.7。动作时钟：当前工作区已导入的 DOTween 1.2.825 核心 DLL；本轮没有改写或提交此前未跟踪的商业插件导入。
- KToolkit 源码：`Assets/Scripts/KToolkit_for_unity`，入口为 `KFrameworkManager.instance.InitKFramework()`。
- 编辑器自动化：CoplayDev MCP for Unity 10.2.0；[连接、CLI 与验证说明](Docs/UnityMcp.md)。
- [依赖修复、KToolkit 来源及验证方法](Docs/UnitySetup.md)
- [游戏设计与实现方案](Docs/GameDesign.md)
- [机器人建模与动画指南](Docs/RobotBlenderGuide.md)

## 目录约定

完整规则见 [AGENTS.md](AGENTS.md)，组织方式参考 Element_Ballance。

- `Assets/Art/`：按主题组织模型、材质和动画。
- `Assets/Scripts/`：按功能组织运行时代码；项目工具在 `Editor/`，KToolkit 在 `KToolkit_for_unity/`。
- `Assets/Resources/`：运行时 Prefab、配置等内容；机器人位于 `prefabs/gameplay/player/Robot.prefab`。
- `Assets/Scenes/`、`Assets/Settings/`：场景与项目资源设置。
- `ArtSource/`：可编辑美术源文件、导出脚本和交付证据。
- `Docs/`、`Tools/`：文档与外部命令行工具。
