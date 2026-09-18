# v0.6.0 验收记录

实施基线：`06e61c15c6a859350780832cacc5f4868ec86ef0`；Unity 2022.3.51f1 / URP 14。范围和取舍见 [版本计划](../V0.6.0.md)。

## 交付与验证

| 检查 | 本轮结果 | 证据 |
| --- | --- | --- |
| EditMode | 最终全组 163/163；包含进度损坏/写入失败、旧关卡/配方/草稿、保存重开与现场隔离 | [最终 XML](EditMode.xml) |
| PlayMode | 已发现全组 57/57；随后导入的进度专项 3/3，共 60 个不同用例；包含九关连续流程、页面输入、帮助往返、导航焦点与暗闪 | [全组 XML](PlayMode.xml)、[进度往返 XML](PlayerProgressFlow.xml) |
| 关卡迁移 | 20 份关卡/配方仅移除三项文案；10 份参考解法实际回放后重新生成，命令、终点、计数和稳定 ID 不变 | [回放及新旧哈希](ProofMigration.json) |
| 导航修复 | 原主菜单/暂停两例失败；修复后回归、Editor 原路径采样和独立 Player 悬停检查通过 | [原失败用例](HoverBefore.xml)、[修复前采样](PauseHoverBefore.json)、[修复后采样](PauseHoverAfter.json) |
| Prefab 重建 | 在新建的独立目录重建八页，检查状态色和已删除控件后清理该副本；原资源 GUID 保持不变 | [重建结果](PrefabRebuild.json) |
| Windows 构建 | 非开发验收包，0 错误 / 0 警告；九关目录及当前解法通过构建门禁 | [汇总与交付程序集哈希](Verification.json) |
| 独立 Player | 720p 完成 L07（第 04 关），10 步 / 2 推写盘；另一个 1080p 进程读取成绩、继续至起点并显示选关记录 | [完成检查](PlayerCheck720.json)、[重启检查](PlayerReload1080.json)、[实际存档](PlayerSavedProgress.json) |

首轮 EditMode 为 162/163：框架单例重置用例与打开的 Bootstrap 同时运行，退出时触发重建对象提示。改用空场景独立运行后框架 3/3 通过，随后全组 163/163 通过；未修改第三方框架。[初轮结果](EditModeInitial.xml)、[框架复测](FrameworkRetest.xml) 保留该过程。

## 暗闪根因

次要按钮的常态原为透明黑色 `Color.clear`，0.12 秒渐变同时插值 RGB 和 alpha。合成到浅色 Paper 背景时，会先暗后亮。原暂停采样有 279 个有效悬停样本，焦点一直为 Resume，最低合成亮度约 0.707，低于两端颜色下界约 0.888。

修复将透明常态的 RGB 保持为悬停色，仅将 alpha 设为 0，保留独立的悬停/选中/按下/禁用态。相同实际 InputSystem 路径重跑取得 311 个有效样本，最低亮度约 0.9033；独立 Player 的主菜单和暂停悬停也为约 0.9033。页面刷新不重置焦点，打开时在刷新可见性后选择可用控件，选关键盘焦点自动滚动到可见区域。

自动回归驱动实际 UGUI 指针事件并逐帧读取 CanvasRenderer，避免系统窗口焦点让合成鼠标未进入按钮而产生错误结论；完整输入模块另由前后实机采样及 Player 检查覆盖。没有将原来只派发 PointerClick 的测试作为“无悬停闪烁”的证据。

## 画面

- Editor：[主菜单 1080p](MainMenu1080.png)、[HUD 1080p](Hud1080.png)、[选关 720p](LevelSelect720.png)、[暂停 720p](Pause720.png)、[设置 720p](Settings720.png)、[帮助 720p](Help720.png)。
- 独立 Player：[主菜单](PlayerMenu720.png)、[HUD](PlayerHud720.png)、[第三人称暂停](PlayerPause720.png)、[设置](PlayerSettings720.png)、[帮助](PlayerHelp720.png)、[结算及最佳成绩](PlayerCompletion720.png)。
- 重启后的独立 Player：[继续游戏入口 1080p](PlayerResume1080.png)、[继续至起点](PlayerContinued1080.png)、[完成记录与成绩](PlayerScores1080.png)。

15 张图均来自实际渲染；已检查尺寸、非空画面及布局。选关列表末端的半行是滚动视口裁剪，完整条目可滚动或通过键盘焦点显示。

本轮核对 10 份 Markdown、250 个本地链接、38 份 JSON 和 7 个新增资源 GUID；无新增失效链接，文档索引既有课程 PPT 删除造成的失效链接保持原状。代码、布局及维护文档的差异检查通过；测试 XML 原样保留 Unity 日志，日志自身的行尾空白不作源码格式问题处理。

## 构建、输入边界与清理

交付包：`Builds/V0.6.0/Windows/Sokoban.exe`，显式使用 `Assets/Scenes/Bootstrap.unity`。未修改 PlayerSettings 版本或默认 Build Settings，也未推送或发布。

普通交付包已用原生 Windows 鼠标检查选关、转场、暂停和帮助。原生键盘自动化没有产生可观察到的角色移动，不能据此声称完成物理键盘人工验收。单独的 `WindowsValidation` 包使用 [临时验证脚本](StandaloneProgressCheck.cs.txt) 通过 InputSystem 排队键盘/鼠标事件，实际执行 LevelRunner 输入、V/Esc、指针悬停、通关写盘和跨进程读取；使用相同产品代码和非开发构建设置。两类验证的范围不混淆。

临时脚本及 `.meta` 已从 Assets 删除，重新编译和 Console 检查无错误；普通交付程序集不包含该验证类型。测试进度已保存为证据并清理，恢复了验收前没有玩家进度文件的状态。Editor 返回 Bootstrap、非 Play Mode、无未保存场景；原 Game View 尺寸已恢复。运行前作者草稿和 UI Prefab 备份保留在忽略的 `Logs/V0.6.0/Before`，用户既有 GM Prefab 保持逐字节一致，其他原有工作区改动未纳入提交。

旧文案仅在读取时兼容丢弃，后续保存不写回；旧参考解法必须重新验证，不能仅替换哈希。用户未提交的自建关卡保留原文件，可通过兼容读取继续编辑。本轮不提供中途局面存档、解锁门槛、音频或新手引导内容，也不替代 v0.9.0 的最终联合验收。
