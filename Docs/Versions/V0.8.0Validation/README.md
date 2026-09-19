# v0.8.0 收尾验收

本版实现 R11 选关显示、R12 停用工具删除、R13 GM 两页/选格移动与现场编辑入口。起始提交 `a84fb73`。状态：**已完成**；实际范围与过程边界见下文。关卡布局、目录、现有美术与资产 GUID 保持；用户未跟踪的 `test111.json` 不纳入提交。

## 已执行检查

| 检查 | 结果与证据 |
| --- | --- |
| EditMode | [161/161](EditMode.xml)，含作者编辑、单份配方导入、保存/恢复、现场应用、草稿保护与 Foldout 重入/重建 |
| UI / 进度 / GM / 关卡 PlayMode | [首组](PlayModeInitial.xml) 33 通过，1 项旧九关断言失败；目录断言修正后 [七关连续回放](CampaignPlayMode.xml) 通过；GM 最终专项 [9/9](GmFinal.xml)，输入框最大字号边距修正后 [1/1](GmFontFinal.xml)，合计 [35 项保留用例](PassingPlayModeCases.json) 分组验证通过 |
| 当前参考解法 | [七关回放](Solutions.json) 通过；L08/L10/L11/L12 原证明过期且旧指令被拒绝，重新求解并由项目原生 ReplayCommands/Capture/Verify 验证后更新，未改地图 |
| 停用工具 | [删除清单与 GUID 引用检查](RemovedTools.json)，8 个专用脚本及元数据删除，保留单份配方导入；移除批量入口及专用目录编辑辅助，保留构建校验 |
| 占据取证入口 | [72 帧 / 133 项](Occupancy.json) 通过；独立使用现有资产，输出 Logs，不重建美术或覆盖历史证据 |
| 构建 | [开发/非开发构建](Builds.json)，显式使用 Bootstrap；最终交付构建不含临时验证组件 |
| 独立 Player | [开发验证包](Standalone.json)、[非开发验证包](StandaloneRelease.json) 与 [验证代码](StandaloneCheck.cs.txt)，完整 InputSystem 排队输入和射线检查的 UGUI 事件；使用独立进度文件 |

## 画面

- 选关：[720p](PlayerSelection720.png)、[1080p](PlayerSelection1080.png)。当前有效记录只显示“已完成 · N 步”，旧记录及无记录留空，页头只保留完成数量。
- 开发版局面页：[720p](PlayerGmBoard720.png)、[1080p](PlayerGmBoard1080.png)。明确源对象与 X/Z，面板随内容收拢，选择移动/撤销保留计数。
- 开发版关卡页：[720p](PlayerGmLevel720.png)、[1080p](PlayerGmLevel1080.png)。Editor 现场编辑入口隐藏。
- Editor 关卡页：[720p](EditorGmLevel720.png)；最大字号/最窄面板：[720p](EditorGmLargeFont720.png)、[1080p](EditorGmLargeFont1080.png)。输入文字和指定按钮/提示完整可读；[现场入口记录](LiveEntry.json)确认成功捕获后展开，手动折叠及 CreateGUI 后重入有效。

## 过程与边界

- 首次新增测试有一处 KUIPage 组件访问编译错误，修正后 Console 无编译错误；该次未实际运行的 MCP 测试请求清理为孤立任务，未计入通过数。
- 首次 UI/进度回归定位到旧九关断言、过期解法，以及模拟输入框点击需释放鼠标后才能观察 focus 的测试时序；修正后分别复验，原始首组结果保留。
- 删控件后进一步收拢 GM 面板；大字号/窄面板检查用于发现并修复布局更新的裁切，最终结果以最后一次专项为准。
- 普通开发 Player 已实际原生鼠标走通菜单→选关→首关，截图见 [原生窗口](PlayerSelect720.png)。随后 Windows 防火墙网络许可弹窗阻止原生桌面输入，未点击许可、取消或修改系统设置；已请求用户手动关闭；随后观察到弹窗消失并恢复原生验收。独立验证包使用模拟输入，不冒充原生键盘验收。最后一处输入框上下边距调整在 Editor 专项和截图中复验，交付包包含该修正。
- Editor 入口点击、常规/折叠/重建展开与保存副本/应用/结束已通过自动检查；[原生 GM 点击自动展开](NativeLiveWindow.png)与[保护弹窗](NativeDraftDialog.png)已实际操作；继续保留并展开、取消保留且不强制展开、替换重新捕获并保留旧备份，三项均由 [运行状态与备份内容](LiveEntry.json)复核通过。本轮不恢复已取消 R07/R09。
- 用户原草稿和运行局面备份位于 `Logs/V0.8.0/Before/`；验收结束恢复用户草稿索引/玩家进度及 Editor 设置。历史音频余项继续归 v0.7.0。

当前维护文档与链接、最终程序集隔离及用户数据恢复哈希见 [核对记录](Verification.json)。只有文档索引中既存的课程 PPT 缺失链接，无新增失效。
