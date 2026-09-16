# Sokoban_3D_Test

使用 **Unity 2022.3.51f1** 打开仓库根目录。

当前已具备作者工具基础、六关连续游玩，以及基于 KToolkit 的简约 UGUI 菜单、HUD、选关、暂停、结算与设置，尚未达到 GDD 的完整 Take-home 交付标准。

- 打开 **Sokoban_Tools > Level Editor**，左侧选择 L01–L06 或 LAB01，再点击“试玩并录制”。
- 可以从空白图放地形、玩家、能源箱、普通箱、插槽与门；箱型和门的供电来源在右侧配置。普通箱有交叉支架，可推、不供电、无需归位。校验错误可点击定位。
- 试玩：WASD 为镜头相对方向，方向键为世界方向；V 切换跟随/俯视，Z 撤销，R 重开，Esc 暂停。通关后可保存参考解法，再用 Unity Play 按钮退出并返回原设计。
- 直接打开 `Assets/Scenes/Bootstrap.unity` 并 Play 进入主菜单。“开始游戏”经幕布过渡进入 L01；“选择关卡”先选择条目，再点击“进入关卡”。六关均可直接选择，L04–L06 为新增窄道与普通箱谜题。
- 通关后点击“下一关”按目录继续；Esc 暂停页可选关、设置或返回主菜单。L06 通关显示“空间站已重启”，可返回选关或重玩。切关会重置本关计数与撤销记录。
- 主菜单与关卡使用水平幕布，关卡之间使用垂直幕布，每段约 0.6 秒。设置支持主音量、鼠标灵敏度和反转垂直镜头并保存；关卡进度与最佳成绩持久化仍待实现。
- [UGUI 页面、过渡实现与验证说明](Docs/UIImplementation.md)
- 本机开发构建：`Builds/AuthoringMilestone/StationRestart.exe`；已验证启动，尚非最终交付构建。
- [首轮关卡编辑器使用说明](Docs/LevelEditorGuide.md)
- [GM 与关卡编辑器现场试玩迭代方案（待实现）](Docs/GMAndLiveEditingPlan.md)
- [实现状态、验证结果和剩余范围](Docs/ImplementationProgress.md)
- 正式顺序由 `Assets/Resources/configs/CampaignCatalog.asset` 的 JSON 引用列表决定；LAB01 默认不在目录内。编辑器试玩不进入正式关卡切换流程。

- 渲染管线：URP 14.0.11。
- 输入：Input System 1.18.0，使用新输入系统。
- 相机：Cinemachine 2.10.7。动作时钟：当前工作区已导入的 DOTween 1.2.825 核心 DLL；本轮没有改写或提交此前未跟踪的商业插件导入。
- KToolkit 源码：`Assets/Scripts/KToolkit_for_unity`，入口为 `KFrameworkManager.instance.InitKFramework()`。
- 编辑器自动化：CoplayDev MCP for Unity 10.2.0；[连接、CLI 与验证说明](Docs/UnityMcp.md)。
- [依赖修复、KToolkit 来源及验证方法](Docs/UnitySetup.md)
- [游戏设计与实现方案](Docs/GameDesign.md)
- [机器人建模与动画指南](Docs/RobotBlenderGuide.md)
- [空间站场景与机关资产生产需求（首批10个主资产，含新会话开工提示）](Docs/StationArtProduction.md)

## 目录约定

Agent 的按需阅读入口、授权与完成标准见 [AGENTS.md](AGENTS.md)；新增/移动文件的详细规则见 [ProjectStructure.md](Docs/ProjectStructure.md)。组织方式参考 Element_Ballance，无需该参考工程在本机存在。

- `Assets/Art/`：按主题组织模型、材质和动画。
- `Assets/Scripts/`：按功能组织运行时代码；项目工具在 `Editor/`，KToolkit 在 `KToolkit_for_unity/`。
- `Assets/Resources/`：运行时 Prefab、配置等内容；机器人位于 `prefabs/gameplay/player/Robot.prefab`。
- `Assets/Scenes/`、`Assets/Settings/`：场景与项目资源设置。
- `ArtSource/`：可编辑美术源文件、导出脚本和交付证据。
- `Docs/`、`Tools/`：文档与外部命令行工具。
