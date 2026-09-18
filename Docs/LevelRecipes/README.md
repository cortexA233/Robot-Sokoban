# 关卡作者配方

用途：编辑器 / 关卡制作。此目录由 `RecipeImporter`、编辑器文件选择入口及 EditMode 测试直接读取，保持 `Docs/LevelRecipes/` 路径稳定。

| 开发阶段 | 配方 | 用途 |
| --- | --- | --- |
| 01 · 作者工具闭环 | [LAB01](LAB01_LowFriction.json) | 低摩擦实验关，默认不在正式目录中 |
| 04 · 普通箱与窄道 | [L04](L04.json)、[L05](L05.json)、[L06](L06.json) | 普通箱与能源箱混合谜题 |
| v0.3.0 · 转向运输 | [L07](L07.json)、[L08](L08.json)、[L09](L09.json)、[L10](L10.json)、[L11](L11.json)、[L12](L12.json) | 固定转向板、轨道、挡停与供电交接；最大9×9 |

通过 [关卡编辑器](../Editor/LevelEditorGuide.md) 的“导入配方”使用。运行时读取 `Assets/Resources/configs/` 中的正式关卡和解法，不直接读取这些配方。

[v0.2.1](../Versions/V0.2.1.md) 已删除原 L01–L03 的关卡、解法与配方；v0.3.0 追加六关后，当前正式目录为 L04 → L05 → L06 → L07 → L08 → L09 → L10 → L11 → L12，关卡 ID 保持不变。原始设计保留在 GDD 的历史示例中。

[窄道方案的草案 JSON](../History/04-CargoCrates/Drafts/NarrowSpaceDrafts.json) 是历史策划示意数据，不能作为本目录配方导入。返回 [文档索引](../README.md)。

[v0.6.0](../Versions/V0.6.0.md) 已移除配方中的三项逐关文案；布局、稳定 ID 和命令保持不变。导入仍兼容带旧文案的 v1/v2/v3 配方，重新保存不写回这些字段。
