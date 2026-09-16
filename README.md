# Sokoban_3D_Test

使用 **Unity 2022.3.51f1** 打开仓库根目录。

- 渲染管线：URP 14.0.11。
- 输入：Input System 1.18.0，使用新输入系统。
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
