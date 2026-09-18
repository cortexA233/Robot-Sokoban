# v0.7.1 验收记录

实施基线：`8a5fe80103b4b570df83bc884d71d9471a25026b`；Unity 2022.3.51f1 / URP 14。需求与方案见 [版本计划](../V0.7.1.md)，原反馈见 [用户截图](../V0.7.1Reference.png)。

## 结果

| 检查 | 本轮结果 | 证据 |
| --- | --- | --- |
| EditMode | 29/29：单色主关联、数组重排、L12 三条连接、目录/解法与进度兼容 | [XML](EditMode.xml) |
| UI / 电路 / 转向 / 进度 PlayMode | 37/37：入口删除、继续/选关/保存重试、悬停/焦点、多门关系、Any/All、占据保开及重建 | [XML](PlayMode.xml) |
| 音效与第三人称 PlayMode | 17/17：已有十项音效回归、电路、本体查看距离和墙体遮挡 | [XML](AudioAndThirdPerson.xml) |
| 最终边框复测 | 7/7；保留原目标角标空间后复测全部电路用例 | [XML](CircuitFinal.xml) |
| 数据与解法 | 30 份关卡/实验关/配方/解法文件未改动，九关原证明实际回放通过，目录门禁通过 | [解法结果](SolutionReplay.json) |
| Prefab 重建 | 独立临时目录生成八页，MainMenu 无 NewGame；临时目录已清理，现有 MainMenu 资产 GUID 保持不变 | [汇总](Verification.json) |
| Windows 构建 | 最终普通非开发包：0 错误、0 警告，显式使用 Bootstrap | [构建结果](Build.json) |
| 独立 Player | 720p/1080p 页面流程、三条供电关系、单色边框、无门标牌、通电与双视角检查通过 | [结果](PlayerCheck.json)、[验证代码](StandaloneCheck.cs.txt) |

三次 PlayMode 运行含重复复测，共 **48 个不同用例**，不是完整项目全组计数。一次错误组合过滤条件的请求返回 0 个用例，未计为通过，随后按类过滤实际执行 17 项。所有列入结果的 XML 均含实际执行用例；保留 Unity 原日志及其行尾空白。

## 画面与交互

- 主菜单：[720p](PlayerMenu720.png)、[1080p](PlayerMenu1080.png)。无最近关卡时为“开始游戏”，有有效最近关卡时为“继续游戏”，没有“从第一关开始”或对应空位；首关仍可由选关进入。
- L12 初始画面：[Editor 720p](EditorL12Top720.png)、[Editor 1080p](EditorL12Top1080.png)、[Player 720p](PlayerL12Top720.png)、[Player 1080p](PlayerL12Top1080.png)。左上插槽四边连接色统一橙色，白色目标角标独立保留；两门上均无文字框。
- L12 供电交接后：[Editor 1080p](EditorPowered1080Final.png)、[Player 720p](PlayerL12Powered720.png)、[Player 1080p](PlayerL12Powered1080.png)。按原解法执行前 35 条规则命令后，socket_a 被能源箱占据、两门均通电，边框主色不变。
- 第三人称：[Editor 720p](EditorThird720.png)、[Editor 1080p](EditorL12Third1080.png)、[Player 720p](PlayerL12Third720.png)、[Player 1080p](PlayerL12Third1080.png)。实际透视画面中没有门上标牌，完整墙/门模型和供电/通行图形保持原表现。
- [左上插槽的双门线路](EditorSocketConnections.png)：通过 InputSystem 指针聚焦实际插槽，确认橙、蓝两条关联均显示、无关 socket_s 分支收起；捕获时暂时冻结电路组件刷新以稳定截图，退出 Play Mode 后已清理。对门本体的指向、第三人称距离与遮挡由 PlayMode 另行覆盖。

主色优先选择仅以该插槽为来源的门，并列或无此门时按稳定门 ID 选择；不会根据当前通电门切换颜色。门 Any/All、目标完成和占据保开规则、解法与进度哈希均未改变。

## 验证边界与清理

交付包为 `Builds/V0.7.1/Windows/Sokoban.exe`。普通 Player 已启动并观察到当前存档的继续入口及删除后的菜单；Windows 原生输入工具两次报告 `failed to activate captured window`，没有将失败点击记作实际操作通过。

单独的 `WindowsValidation` 包使用相同产品代码，加临时验证组件，通过实际 EventSystem 射线和完整 PointerEnter/Down/Up/Click 事件走通菜单→选关第 9 关→暂停→返回菜单，并用 InputSystem 指针检查完整连接。720p/1080p 均实际渲染和检查，不作为物理键盘或原生鼠标人工验收。Player 线路可见性断言通过；捕获普通页面时指针可能已移开，连线画面以单独的 Editor 双门线路截图为准。

验证组件使用独立内存进度，不写用户存档；临时代码及 `.meta` 已移除，普通交付包重新构建，不包含该类型。Editor 已退出 Play Mode，返回无未保存修改的 Bootstrap，Game View 尺寸及测试工具改动的 EnterPlayModeOptions 已恢复。用户原有未提交文件与本轮开始时的哈希一致，已有玩家存档及备份也保持一致；本轮没有推送或发布。

v0.7.0 的 BGM、独立音量、图形反馈及试听余项保持原状态；本轮不替代 v0.8.0 引导或 v0.9.0 整体交付验收。

最终核对 9 份 Markdown、260 个本地链接及 14 张非空实机图片，无新增失效链接；文档索引原有课程 PPT 删除造成的失效链接按既有状态保留。维护代码/文档的差异检查通过，原始测试 XML 可解析；详细文件和构建程序集哈希见 Verification.json。
