# Sokoban_3D_Test

使用 **Unity 2022.3.51f1** 打开仓库根目录。

当前已具备作者工具基础、九关连续游玩、KToolkit 简约 UGUI、可撤销 GM 和 Play Mode 现场关卡编辑，空间站首批美术套件已接入游戏；尚未达到 GDD 的完整 Take-home 交付标准。

**v0.3.0 已封版**：固定转向板与六张新关卡已接入，正式目录 L04–L12，共九关。[封版记录与验证](Docs/Versions/V0.3.0.md)。最新 Windows 验收包为 `Builds/V0.5.0/Windows/Sokoban.exe`，默认俯视与镜头/连接表现见 [v0.5.0 验收](Docs/Versions/V0.5.0Validation/README.md)。本包显式使用 Bootstrap；默认 Build Settings 仍为原有 SampleScene，重新构建时需指定 `Assets/Scenes/Bootstrap.unity`。

文档从 [分类索引](Docs/README.md) 进入，可按 Gameplay、美术、UI、编辑器、Debug、工程维护或开发阶段查找；最新范围见 [当前实现状态](Docs/ImplementationProgress.md)。

新迭代先按 [开发版本台账](Docs/Versions/README.md) 规划编号、范围和验收，再开始实现；提交使用对应版本前缀追踪。

- 打开 **Sokoban_Tools > Level Editor**，左侧选择 L04–L12 或 LAB01，再点击“试玩并录制”。
- 可以从空白图放地形、玩家、能源箱、普通箱、插槽、门与固定转向板；箱型和门的供电来源在右侧配置。普通箱有交叉支架，可推、不供电、无需归位。校验错误可点击定位。
- 试玩：进入关卡默认俯视，V 切换第三人称/返回俯视；WASD 为镜头相对方向，方向键为世界方向，Z 撤销，R 重开，Esc 暂停。通关后可保存参考解法，再用 Unity Play 按钮退出并返回原设计。
- 直接打开 `Assets/Scenes/Bootstrap.unity` 并 Play 进入主菜单。“开始游戏”经幕布过渡进入 L04；“选择关卡”先选择条目，再点击“进入关卡”。九关均可直接选择；L04–L06 为窄道与普通箱谜题，L07–L12 组合供电门、滑动轨道与转向板，最大 9×9。
- 通关后点击“下一关”按目录继续；Esc 暂停页可选关、设置或返回主菜单。L12 通关显示“空间站已重启”，可返回选关或重玩。切关会重置本关计数与撤销记录。
- 主菜单与关卡使用水平幕布，关卡之间使用垂直幕布，每段约 0.6 秒。设置支持主音量、鼠标灵敏度和反转垂直镜头并保存；关卡进度与最佳成绩持久化仍待实现。
- [UGUI 页面、过渡实现与验证说明](Docs/UI/UIImplementation.md)
- Editor 或开发版按 **F1** 打开 GM，移动玩家/箱子可撤销；普通箱与能源箱的占据、供电和门状态分别显示。
- 在 GM 关卡页或 Level Editor 点击“捕获当前游戏局面”，编辑独立现场草稿后“应用并继续试玩”。箱型、数量、地形变更建立新的试玩起点；“结束现场试玩”返回原局面。
- 已有构建记录：第五轮开发版 `Builds/GMIterationDevelopment/StationRestart.exe`，空间站接入后的非开发版 `Builds/StationKitIntegration/StationRestart.exe`。构建产物仅保存在本机，具体覆盖见 [实现状态](Docs/ImplementationProgress.md)；完整关卡编辑器在 Unity Editor 内使用。
- [关卡编辑器与 GM 使用说明](Docs/Editor/LevelEditorGuide.md)
- [GM 与关卡编辑器现场试玩方案（M1–M3 已实现）](Docs/Debug/GMAndLiveEditingPlan.md)
- [实现状态、验证结果和剩余范围](Docs/ImplementationProgress.md)
- 正式顺序由 `Assets/Resources/configs/CampaignCatalog.asset` 的 JSON 引用列表决定；LAB01 默认不在目录内。编辑器试玩不进入正式关卡切换流程。

- 渲染管线：URP 14.0.11。
- 输入：Input System 1.18.0，使用新输入系统。
- 相机：Cinemachine 2.10.7。动作时钟：当前工作区已导入的 DOTween 1.2.825 核心 DLL；本轮没有改写或提交此前未跟踪的商业插件导入。
- KToolkit 源码：`Assets/Scripts/KToolkit_for_unity`，入口为 `KFrameworkManager.instance.InitKFramework()`。
- 编辑器自动化：CoplayDev MCP for Unity 10.2.0；[连接、CLI 与验证说明](Docs/Debug/UnityMcp.md)。
- [依赖修复、KToolkit 来源及验证方法](Docs/Engineering/UnitySetup.md)
- [游戏设计与实现方案](Docs/Gameplay/GameDesign.md)
- [机器人建模与动画指南](Docs/Art/RobotBlenderGuide.md)
- [空间站场景与机关资产生产需求（含普通箱，首批11个主资产及新会话开工提示）](Docs/Art/StationArtProduction.md)

## 目录约定

Agent 的按需阅读入口、授权与完成标准见 [AGENTS.md](AGENTS.md)；新增/移动文件的详细规则见 [ProjectStructure.md](Docs/Engineering/ProjectStructure.md)。组织方式参考 Element_Ballance，无需该参考工程在本机存在。

- `Assets/Art/`：按主题组织模型、材质和动画。
- `Assets/Scripts/`：按功能组织运行时代码；项目工具在 `Editor/`，KToolkit 在 `KToolkit_for_unity/`。
- `Assets/Resources/`：运行时 Prefab、配置等内容；机器人位于 `prefabs/gameplay/player/Robot.prefab`。
- `Assets/Scenes/`、`Assets/Settings/`：场景与项目资源设置。
- `ArtSource/`：可编辑美术源文件、导出脚本和交付证据。
- `Docs/`、`Tools/`：文档与外部命令行工具。
