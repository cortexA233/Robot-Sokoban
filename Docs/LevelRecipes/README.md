# 关卡作者配方

用途：编辑器 / 关卡制作。此目录由 `RecipeImporter`、编辑器文件选择入口及 EditMode 测试直接读取，保持 `Docs/LevelRecipes/` 路径稳定。

| 开发阶段 | 配方 | 用途 |
| --- | --- | --- |
| 01 · 作者工具闭环 | [L01](L01.json)、[L02](L02.json)、[L03](L03.json) | 原始三关的作者输入；以后续配方修改为准 |
| 01 · 作者工具闭环 | [LAB01](LAB01_LowFriction.json) | 低摩擦实验关，默认不在正式目录中 |
| 04 · 普通箱与窄道 | [L04](L04.json)、[L05](L05.json)、[L06](L06.json) | 普通箱与能源箱混合谜题 |

通过 [关卡编辑器](../Editor/LevelEditorGuide.md) 的“导入配方”使用。运行时读取 `Assets/Resources/configs/` 中的正式关卡和解法，不直接读取这些配方。

[窄道方案的草案 JSON](../History/04-CargoCrates/Drafts/NarrowSpaceDrafts.json) 是历史策划示意数据，不能作为本目录配方导入。返回 [文档索引](../README.md)。
