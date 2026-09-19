# 当前实现状态与剩余范围

基线：截至 `0dd9ca2` 的已提交实现及已有验证记录。文档整理不代表重新测试当前工作区，也不扩大历史验收范围。按用途和开发阶段查找全文见 [文档索引](README.md)。

后续需求原在 [迭代路线图](Versions/Roadmap.md) 排入 v0.4.0–v0.9.0 六个版本。v0.4.0、v0.5.0、v0.6.0 已完成，v0.7.0 基础音效已合入 main，整版进行中；2026-09-18 按用户要求取消旧 v0.8.0 引导/难度计划与 v0.9.0，记录及 [GM 使用说明](Editor/LevelEditorGuide.md#运行时-gm) 见 [v0.7.2](Versions/V0.7.2.md)。随后用户明确指定新的 [v0.8.0 收尾版本](Versions/V0.8.0Closeout.md)，已实施 R11 选关状态简化：空状态或“已完成 · N 步”，移除推动次数和全关可选说明。随后追加 R12：删除已注释 MenuItem 对应工具，当前清单为 10 项/9 个脚本，清理关联入口并保留在用资源与共享功能；追加 R13：GM 删除机关/诊断页、监视、增删/交换/朝向、动画与参考解法状态，局面页仅保留选中与移动，现场编辑入口改文案并自动展开编辑器现场操作组。已完成实现与自动/构建/原生现场草稿验证，实际结果见 [v0.8.0 验收](Versions/V0.8.0Validation/README.md)，旧范围仍已取消。

UI 与进度更新：[v0.6.0](Versions/V0.6.0.md) 已移除三项逐关文案及编辑/UI 控件，修复透明底色渐变造成的按钮暗闪，增加按需帮助和选关焦点滚动；本地完成记录、同一次最佳成绩对、最近关卡和继续入口已接入。EditMode 163/163、PlayMode 全组 57/57 与进度专项 3/3、十份解法回放、Prefab 重建及 Windows 构建通过；独立 Player 通关写盘、重启读取与 720p/1080p 画面见 [验收记录](Versions/V0.6.0Validation/README.md)，原生键盘自动化边界在记录中说明。

后续体验修正：[v0.7.1](Versions/V0.7.1.md) 已删除主菜单首关快捷入口和受电门浮动文字，插槽四边改为稳定主关联色；L12 左上为橙色，双门供电和按需线路保持。EditMode 29/29、48 个不同 PlayMode 用例、九关原解法、Windows 构建与双分辨率 Player 检查通过；[截图、过程与工具边界](Versions/V0.7.1Validation/README.md)。v0.7.0 的未完成音频范围继续保留。

镜头与表现更新：[v0.5.0](Versions/V0.5.0.md) 已实现默认俯视/V 双向切换、较高俯角与局部遮挡隐藏、插槽/目标去文字、按门配色与图形及按需连线。EditMode 149/149、PlayMode 52/52、最终相机/动作 22/22 和表现 10/10 通过；720p/1080p/灰阶、连续动作、Windows 构建与 Player 检查见 [验收记录](Versions/V0.5.0Validation/README.md)，包含原生键盘自动化的验证边界。

镜头反馈修正：[v0.5.1](Versions/V0.5.1.md) 按用户要求取消第三人称局部剔除，墙体和门上层完整显示，恢复完整避障碰撞代理；俯视隐藏与 V 切换保留。原 v0.5.0 的隐藏效果只作为历史实现记录。

关卡内容更新：[v0.2.1](Versions/V0.2.1.md) 删除原前三关 L01–L03，当时正式顺序保留 L04 → L05 → L06；v0.3.0 再追加 L07–L12，LAB01 仍为开发测试关。

表现更新：[v0.2.0](Versions/V0.2.0.md) 已实现目标环/门电源插头的独立组合、稳定门编号、供电连线与俯视标牌。EditMode 3/3、完整项目 PlayMode 41/41 通过，包含当前三关参考解法；[实机与验证记录](History/08-CircuitReadability/ImplementationReport.md)。

玩法更新：[v0.3.0](Versions/V0.3.0.md) 已实现固定转向板、schema v3、完整转向板制作/现场编辑支持及 L07–L12；正式目录共九关。全组 EditMode 122/122、PlayMode 46/46，最终定向复测与 Windows 构建/启动通过；[封版验收材料](Versions/V0.3.0Validation/README.md)。

作者工具更新：[v0.4.0](Versions/V0.4.0.md) 已实现分组入口、画笔分类、同步跨格/同格对象选择、直接试玩完整校验与临时提示、目录管理和自动构建校验；修复拖刷重做和退出试玩清理。EditMode 140/140、PlayMode 47/47及后续 UI 专项 12/12，实际作者/目录流程、坏目录构建拒绝、合法 Windows 构建与启动通过；[验收证据](Versions/V0.4.0Validation/README.md)。

作者工具简化：[v0.4.1](Versions/V0.4.1EditorSimplification.md) 已删除手动回放和擦除入口，重命名边界墙/场景相机聚焦按钮，补齐同层放置覆盖并保留合法叠放、原位 ID 与引用处理。EditMode 149/149、实机覆盖/撤销/校验反馈/保存重开通过，Console 零错误；目录和构建继续自动验证参考解法。

## 已实现

| 用途 | 当前能力 | 维护入口 |
| --- | --- | --- |
| Gameplay | 共享规则、供电门、低摩擦、固定转向板、Energy/Cargo 两类箱子、撤销/重开、L04–L12 连续游玩与参考解法 | [GDD](Gameplay/GameDesign.md) |
| 编辑器 | 分类画笔与对象选择、同层放置覆盖、直接试玩校验、原子保存/草稿、试玩录制、Inspector 维护正式目录与自动解法校验 | [操作指南](Editor/LevelEditorGuide.md) |
| UI | KToolkit UGUI 主菜单、HUD、选关、暂停、结算、按需帮助、设置及幕布过渡；本地进度/最佳成绩与继续入口 | [UI 实现](UI/UIImplementation.md) |
| Debug | Editor / 开发版 F1 GM、点格选对象与可撤销移位、两页工作台；Editor 内捕获、编辑、应用及恢复现场草稿 | [GM 与现场编辑契约](Debug/GMAndLiveEditingPlan.md) |
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
| 08 · 插槽与门可读性 | 定向 3/3 | 完整项目 PlayMode 41/41；九张实际游戏画面 | [实现与验收](History/08-CircuitReadability/ImplementationReport.md) |

第五轮曾生成 Windows 开发版与非开发版，并验证启动和 GM 类型隔离；第七阶段更新了非开发构建并检查启动。均不等于独立 Player 中的完整交互、通关或性能验收。本地构建产物不进 Git，具体路径与当时范围见报告。

## 尚未完成或缺少完整验收

1. **最终内容打包筛选**：构建前全量解法校验已完成，专用目录窗口在 v0.8.0 删除；仍放在 Resources 下但未列入目录的文件不会因此自动从包中排除。最终筛选原归 v0.9.0，现随该版取消，保留为已知未完成项。
2. **操作与音频反馈**：角色表情气泡、BGM 和独立音量等；玩家进度、最佳成绩、继续入口及设置持久化已实现，基础音效已接入（见 [音效说明](Audio/Sfx.md)），其余反馈归 v0.7.0。
3. **最终联合验收**：v0.4.0 已走通本轮作者工具链；原拟由 v0.9.0 联合复验完整流程，现按用户要求取消。独立设计师易用性评审尚未执行，历史自动检查和实机操作不替代该评审。
4. **扩展场景与构建入口**：v0.5.0 已覆盖正式关卡机位、全图取景、环绕/投影切换、暂停和输入清理；超大自定义连接网络仍需专项密度检查。默认 Build Settings 仍指向 SampleScene，现有验收包显式使用 Bootstrap；最终交付需统一标准构建入口。
5. **GM M4**：可视回放、Microstep 逐步检查和调试快照回放；v0.8.0 已移除诊断快照导出。
6. **最终交付**：正式构建的数据筛选、实际独立完成至少两关、进度重启恢复、性能测量与完整交付证据。后续非开发构建仍不能替代这些检查。

这些项目保留实际完成边界，不自动追加到当前任务。旧 v0.8.0 引导计划与 v0.9.0 取消后，其未实施内容不并入新的 v0.8.0 收尾计划或其他版本，也不补记通过。详细产品要求见 [GDD 第 11–12 节](Gameplay/GameDesign.md#11-验收矩阵)。
