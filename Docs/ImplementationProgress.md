# 当前实现状态与剩余范围

基线：截至 `0dd9ca2` 的已提交实现及已有验证记录。文档整理不代表重新测试当前工作区，也不扩大历史验收范围。按用途和开发阶段查找全文见 [文档索引](README.md)。

关卡内容更新：[v0.2.1](Versions/V0.2.1.md) 删除原前三关 L01–L03，当前正式顺序为 L04 → L05 → L06，LAB01 仍为开发测试关。

## 已实现

| 用途 | 当前能力 | 维护入口 |
| --- | --- | --- |
| Gameplay | 共享规则、供电门、低摩擦、Energy/Cargo 两类箱子、撤销/重开、L04–L06 连续游玩与参考解法 | [GDD](Gameplay/GameDesign.md) |
| 编辑器 | UI Toolkit / SceneView 关卡制作、严格校验、原子保存、草稿恢复、配方导入、试玩录制与解法回放 | [操作指南](Editor/LevelEditorGuide.md) |
| UI | KToolkit UGUI 主菜单、HUD、选关、暂停、结算、设置保存及幕布过渡 | [UI 实现](UI/UIImplementation.md) |
| Debug | Editor / 开发版 F1 GM、可撤销移位、状态诊断；Editor 内捕获、编辑、应用及恢复现场草稿 | [GM 与现场编辑契约](Debug/GMAndLiveEditingPlan.md) |
| 美术 | 已交付机器人；空间站首批 11 个资产接入正式关卡、作者试玩及现场重建，包含来源标识、状态灯与门动画 | [机器人](Art/RobotBlenderGuide.md)、[空间站接入](Art/StationKitIntegration.md) |

完整游戏与编辑器仍未达到 GDD 的全部 Take-home 交付标准。

## 已有验证记录

各行是对应开发轮次的实测结果，不能合并成一次全量验收。完整环境、失败与修复过程、界面覆盖、构建路径和限制保留在阶段报告中。

| 阶段 | EditMode | PlayMode / 资产检查 | 记录 |
| --- | --- | --- | --- |
| 01 · 作者工具闭环 | 51/51 | 10/10 | [首轮报告](History/01-Authoring/ImplementationReport.md) |
| 02 · 关卡串联 | 54/54 | 15/15 | [第二轮报告](History/02-CampaignFlow/ImplementationReport.md) |
| 03 · 简约 UGUI | 54/54 | 24/24 | [第三轮报告](History/03-UI/ImplementationReport.md) |
| 04 · 普通箱与 L04–L06 | 82/82 | 27/27，另有定向复核 | [第四轮报告](History/04-CargoCrates/ImplementationReport.md) |
| 05 · GM 与现场编辑 | 90/90 | 31/31，另有收尾修复复测 | [第五轮报告](History/05-GmLiveEditing/ImplementationReport.md) |
| 06 · 空间站美术生产 | 不以游戏测试计数 | 11 个资产的 Blender、FBX 与 Unity 隔离场景验收 | [生产记录](History/06-StationKitProduction/ProductionRecord.md) |
| 07 · 空间站美术接入 | 本轮未记录新的 EditMode 全量结果 | 37/37，文字深度修复后 8/8 定向复测 | [接入报告](History/07-StationKitIntegration/ImplementationReport.md) |

第五轮曾生成 Windows 开发版与非开发版，并验证启动和 GM 类型隔离；第七阶段更新了非开发构建并检查启动。均不等于独立 Player 中的完整交互、通关或性能验收。本地构建产物不进 Git，具体路径与当时范围见报告。

## 尚未完成或缺少完整验收

1. **关卡发布与目录工具**：E10 的编辑器内加入/移出/排序、构建前全量参考解法回放门禁；现有 CampaignCatalog 资源与运行时目录已经接入。
2. **玩家持久化与反馈**：关卡进度、最佳成绩、角色表情气泡和音效等；设置项持久化已经实现。
3. **完整编辑器验收**：GDD 第 11.4 节从空白图到发布的界面流程，以及 E01–E12 的完整证据。现有实际控件输入测试不自动代表所有条目通过；独立用户易用性评审另行记录。
4. **表现与相机覆盖**：空间站门动画、连接标识和俯视隐藏已接入并有定向验证；仍需完整覆盖窄门/贴墙/转角、大图全图取景、投影切换时刻、暂停相机与多键长按等验收场景。
5. **GM M4**：可视回放、Microstep 逐步检查和调试快照回放；当前仅提供诊断快照导出。
6. **最终交付**：正式构建的数据筛选、实际独立完成至少两关、进度重启恢复、性能测量与完整交付证据。后续非开发构建仍不能替代这些检查。

这些项目供后续排期与完整交付使用，不自动追加到每次维护任务。详细产品要求见 [GDD 第 11–12 节](Gameplay/GameDesign.md#11-验收矩阵)。
