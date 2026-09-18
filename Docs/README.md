# 文档索引

当前维护文档按用途归类，历史方案、实现记录和验收证据按开发阶段归档。先看 [当前实现状态与剩余范围](ImplementationProgress.md)，再按下面两种入口查找。

启动与试玩入口见 [项目首页](../README.md)。

后续迭代先到 [开发版本台账](Versions/README.md) 规划版本号、范围和验收，再开始实现。当前追踪起点为 [v0.1.0 · 文档归类与版本管理](Versions/V0.1.0.md)。

后续工作已按 [迭代路线图](Versions/Roadmap.md) 分配到 v0.4.0–v0.9.0：依次为编辑器与目录、镜头与场景美术、UI 与进度、简单音效/BGM、引导与难度、完整交付验收。[v0.4.0](Versions/V0.4.0.md)、[v0.5.0](Versions/V0.5.0.md) 及 [v0.6.0](Versions/V0.6.0.md) 已完成；下一执行版本为 [v0.7.0](Versions/V0.7.0.md)，v0.7.0–v0.9.0 仍为已规划。默认俯视、V 切换和连接配色的 [v0.5.0 验收与演示](Versions/V0.5.0Validation/README.md) 已记录；后续 [v0.5.1](Versions/V0.5.1.md) 按用户反馈取消第三人称局部剔除。[v0.4.1](Versions/V0.4.1.md) 补充了 v0.6.0 的 UI 简化要求：取消关卡标题、目标提示、完成文案配置及对应 UI。

用户指定追加的 [v0.4.1 编辑器简化](Versions/V0.4.1EditorSimplification.md) 也已完成：移除手动回放与擦除入口、修改工具文案并支持同层放置覆盖；实际范围和验收与原文档规划分开记录。

[v0.5.2](Versions/V0.5.2.md) 补充了主菜单、暂停菜单鼠标移入按钮闪烁的静态检查记录；复现、修复及鼠标/键盘导航回归已在 [v0.6.0](Versions/V0.6.0.md) 完成，同时交付关卡文案移除、按需帮助和玩家进度；[验收证据与输入边界](Versions/V0.6.0Validation/README.md)。

当前玩法版本 [v0.3.0 已封版](Versions/V0.3.0.md)：[转向运输关卡与格图](Gameplay/RedirectorLevelPlan.md)、[实现验收](Versions/V0.3.0Validation/README.md)。

## 按用途查找

| 用途 | 当前文档 | 配套资料 |
| --- | --- | --- |
| Gameplay / 游戏规则 | [游戏设计与 Unity 实现方案](Gameplay/GameDesign.md) | 第 2/4 节为坐标与规则，第 5 节为关卡，第 7/8 节为数据与模块；[普通箱关卡原始方案](History/04-CargoCrates/LevelDifficultyIterationPlan.md) |
| 美术 / 机器人 | [Blender 建模与动画维护](Art/RobotBlenderGuide.md) | [源文件、导出与资产验收](../ArtSource/Robot/README.md)；[GSND 6000 课程展示 PPT](Art/GSND6000/GSND6000_Rigging_Weekly.pptx) |
| 美术 / 空间站 | [资产生产规格](Art/StationArtProduction.md)、[游戏接入与表现维护](Art/StationKitIntegration.md) | [源文件、预览与逐资产证据](../ArtSource/StationKit/README.md)、[插槽与门的可读性方案](Art/VisualReadabilityReview.md)、[v0.2.0 实机验收](History/08-CircuitReadability/ImplementationReport.md) |
| UI / 玩家流程 | [UGUI 页面、输入与幕布过渡](UI/UIImplementation.md) | [概念图 v1/v2 与取舍](../ArtSource/UIConcepts/README.md)；采用第二版简约 UI |
| 编辑器 / 关卡制作 | [关卡编辑器操作指南](Editor/LevelEditorGuide.md) | [作者配方](LevelRecipes/README.md)；GDD 第 6 节为完整编辑器要求 |
| Debug / GM 与现场编辑 | [行为契约、M1–M4 分期与验收](Debug/GMAndLiveEditingPlan.md) | [GM 与现场编辑操作](Editor/LevelEditorGuide.md#运行时-gm)、[第五轮验证](History/05-GmLiveEditing/ImplementationReport.md) |
| Debug / Unity 自动化与排障 | [MCP 连接、编译、测试与恢复](Debug/UnityMcp.md) | [项目 Editor 操作技能](../.agents/skills/sokoban-unity-editor/SKILL.md) |
| 工程维护 | [目录与命名](Engineering/ProjectStructure.md)、[Unity 依赖与 KToolkit 接入](Engineering/UnitySetup.md) | [Agent 入口与工作边界](../AGENTS.md)、[历史配置审计](History/2026-09-16-AgentConfiguration/AgentConfigurationAudit.md) |

## 按开发阶段查找

以下阶段按现有开发记录和提交顺序整理，记录日期均为 2026-09-16（部分证据的 UTC 时间为 09-17）。01–05 沿用原开发轮次，06–07 补记后续美术阶段；这些编号是归档顺序，不是游戏发行版本。原始实现基线可通过表中 Git 提交追溯。

| 阶段 | 用途 / 交付变化 | 记录与证据 | 实现提交 |
| --- | --- | --- | --- |
| 前期准备 | 工程依赖、机器人资产、产品与技术设计 | [依赖说明](Engineering/UnitySetup.md)、[机器人交付](../ArtSource/Robot/README.md)、[GDD](Gameplay/GameDesign.md)；这些入口持续维护 | — |
| 01 · 作者工具闭环 | Gameplay / Editor：共享规则、L01–L03、LAB01、制作与试玩 | [实现记录](History/01-Authoring/ImplementationReport.md)、[验证数据](History/01-Authoring/Validation/FirstIterationResults.json) | `d0da88c` |
| 02 · 关卡串联 | Gameplay / UI：下一关、选关、正式目录 | [实现记录](History/02-CampaignFlow/ImplementationReport.md)、[验证数据](History/02-CampaignFlow/Validation/CampaignFlowResults.json) | `9103a4d` |
| 03 · 简约 UGUI | UI：主菜单、设置、七个页面与幕布过渡 | [实现记录](History/03-UI/ImplementationReport.md)、[验证数据](History/03-UI/Validation/UIImplementationResults.json)、[概念图版本](../ArtSource/UIConcepts/README.md) | `029d01e` |
| 04 · 普通箱与窄道关卡 | Gameplay / Editor：Energy/Cargo、L04–L06、关卡数据 v2 | [原始方案](History/04-CargoCrates/LevelDifficultyIterationPlan.md)、[草案数据](History/04-CargoCrates/Drafts/NarrowSpaceDrafts.json)、[实现记录](History/04-CargoCrates/ImplementationReport.md)、[验证数据](History/04-CargoCrates/Validation/CargoCrateIterationResults.json) | `297c459` |
| 05 · GM 与现场编辑 | Debug / Editor / UI：可撤销调试、现场草稿、M1–M3 | [实现记录](History/05-GmLiveEditing/ImplementationReport.md)、[验证数据](History/05-GmLiveEditing/Validation/GMAndLiveEditingResults.json) | `f4a3bf8`、`2dfc0e9` |
| 06 · 空间站美术生产 | 美术：11 个主资产、Blender/FBX 与隔离场景验收 | [生产记录入口](History/06-StationKitProduction/ProductionRecord.md)、[逐资产清单](../ArtSource/StationKit/station_kit_asset_manifest.json) | `3841dba` |
| 07 · 空间站美术接入 | 美术 / Gameplay：BoardView 替换、状态标识、门动画 | [实现记录](History/07-StationKitIntegration/ImplementationReport.md)、[验证汇总](History/07-StationKitIntegration/Validation/StationKitIntegration.json)、[完整回归](History/07-StationKitIntegration/Validation/StationKitGameplayTests.json)、[定向复测](History/07-StationKitIntegration/Validation/StationKitLabelRetest.json) | `0dd9ca2` |
| 08 · 插槽与门可读性 | 美术 / 表现：组合图形、门编号与连线、双视角标牌 | [实现与实机画面](History/08-CircuitReadability/ImplementationReport.md)、[版本计划](Versions/V0.2.0.md) | `[v0.2.0]` |
| 工程维护 · Agent 配置 | 文档、技能路由和配置整理，独立于游戏轮次 | [审计记录](History/2026-09-16-AgentConfiguration/AgentConfigurationAudit.md)、[原始验证快照](History/2026-09-16-AgentConfiguration/Validation/AgentConfigurationResults.json) | `5055edf` |

## 版本含义

| 版本轴 | 现有标记 | 应如何使用 |
| --- | --- | --- |
| 开发追踪版本 | 从 `v0.1.0` 开始 | 关联新需求、版本计划、验收与提交；不追溯改号历史轮次，不自动视为发行版本 |
| 产品与技术规格 | GDD `1.9` | 文档修订号；完整验收目标与已经实现的范围分别看 GDD 和实现状态 |
| 机器人文档 / 资产协议 | 维护指南 `1.4`；首次生产依据 `1.2`；`robot-asset-v1` | 指南修订不等于重建资产，资产级证据保留原生产范围 |
| 空间站规格 / 资产协议 | 生产规格 `1.1`；`station-kit-v1` | 包含普通箱的 11 个主资产；生产与正式游戏接入分期验收 |
| 关卡数据 | `schemaVersion=1/2/3` | 旧 v1 按能源箱兼容；v2 显式保存箱型，v3 保存固定转向板。属于数据格式，非游戏发行版本 |
| UI 视觉方案 | 概念图 v1 / v2 | v1 为历史探索，v2 为已采用方向；图片是设计资料 |
| GM 功能里程碑 | M1–M3 / M4 | M1–M3 主要能力已实现；M4 可视回放与逐步检查仍待后续 |

## 维护约定

- 当前文档只保留一份，按主要用途存放；跨用途从本索引链接，不复制正文。`README.md` 为总入口，`ImplementationProgress.md` 为当前状态。
- 已结束的开发轮次放入 `History/<序号-阶段>/`；阶段报告、`Validation/` 与 `Images/` 放在一起。独立工程维护记录使用日期和主题命名。
- 旧记录中的“当前”、构建路径、测试数量和未完成项属于当时范围；本次整理未重新执行历史游戏或美术验收。查询最新范围使用当前状态页。
- 美术源文件、提示词、导出脚本、清单和原始资产验证继续随 `ArtSource/<资产>/` 保存。第三方文档保留在原包目录。
- `LevelRecipes/` 是编辑器与测试读取的稳定路径；关卡提案的示意 JSON 随历史方案归档，不作为可导入配方。
- 文件迁移对照见 [DocumentMap.json](History/DocumentMap.json)。历史 Agent 审计快照保留原路径与原哈希，通过迁移表查找现址；其他证据只修正导航路径，测试结果、时间和实测哈希不改写。

音频：[基础音效来源、触发与生命周期](Audio/Sfx.md) · [v0.7.0 音效子项验收](Versions/V0.7.0SfxValidation.md)。
