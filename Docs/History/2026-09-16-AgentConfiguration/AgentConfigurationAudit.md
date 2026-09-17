# Agent 配置审计与重构记录

日期：2026-09-16。范围：本仓库维护的Agent入口、项目Skill、嵌入式任务Prompt、配置及相关指导文档。此记录供配置维护时查阅，不是日常任务的额外必读文件。

归档说明：正文行数、哈希和验证结论属于 `5055edf` 对应的历史审计。配套 JSON 保留原路径与原哈希；文档迁移后通过 [路径对照](../DocumentMap.json) 查找现址，不把该快照当作当前文件的校验结果。现行入口见 [文档索引](../../README.md)。

依据用户指定的 [GPT-6 Astra Skills/Prompt文章](https://developers.openai.com/blog/rethinking-skills-and-prompts-for-gpt-6-astra)，将全局要求缩为项目约束、按需入口和明确完成标准；触发条件、持续完成与测试范围同时参考 [官方模型指导](https://developers.openai.com/api/docs/guides/latest-model/gpt-6-astra.md#prompting-best-practices)。

## 盘点与处理

| 来源 | 发现与处理 |
| --- | --- |
| 根 `AGENTS.md` | 原63行、6785字节；现35行、4200字节。移出完整目录规范，取消每次都执行Editor前置流程，保留版本、GUID、用户工作和外部操作边界。 |
| 用户级 `~/.codex/AGENTS.md` | 只有自主迭代、聚焦提交和禁止擅自发布三类约定，与项目规则一致；保持原样。祖先目录未发现其他AGENTS覆盖文件。 |
| `.codex/config.toml` 与 `Tools/UnityMcp.ps1` | 已有CoplayDev路由正确；验证服务名、地址、端口、实例及超时一致，保留原配置。 |
| 项目Skill | 原来没有自有Skill。将现有Editor操作和机器人资产维护流程提取为两个短入口，复用现有文档，不复制工具手册。 |
| `GameDesign.md` | 从“执行全部项目的Prompt”改为按章节读取的产品规格；去除重复环境检查与从头接入要求，保留规则、模块契约和完整交付标准。修正Windows交付验收中“一关/两关”的冲突，统一为至少两张正式关卡。 |
| `RobotBlenderGuide.md`、机器人README | 区分当前资产维护、首次重建与游戏代码接入。移除每阶段全量预览和重复实施顺序，保留坐标、动画、接触、GUID和真实哈希验证。 |
| `UnityMcp.md`、`UnitySetup.md` | 只在对应操作时读取；已有Editor优先MCP测试，离线批处理不再要求每次关闭Editor。补充CLI继续工作、编译终态和测试副作用边界。 |
| GDD与GM方案的界面验收 | Agent可自行通过实际界面/等价输入自动化验证、修复和复验。直接调用业务API仍不算界面通过；独立用户易用性评审另记，不作为每轮本地工作前置审批。 |
| `ImplementationProgress.md`、`LevelEditorGuide.md`、`UIImplementation.md` | 核对当前实现与历史范围；修正过时状态说明，保留已有测试结果及未验收项，不把旧报告改成新结果。UI实现说明无需额外流程改写。 |
| UI概念README与两份 `prompts*.json` | 修正“尚未实现”与首版缩略图要求；JSON是图像生成历史，原样保留，不能用于隐式追加当前实现需求。 |
| KToolkit/插件文档 | 属于第三方用法与来源记录，无项目级Agent指令；保持原样。 |
| `.utmp/coplay-unity-mcp-10.2.0/` | 找到一份 `CLAUDE.md` 和四份 `SKILL.md`（两份同名Unity MCP、包源切换、Blender导入）。它们是Git忽略的上游缓存，未进入项目Skill发现范围，不修改或提升为项目指令。 |

目录规范的唯一详细入口为 [ProjectStructure.md](../../Engineering/ProjectStructure.md)。项目根指令保留安全本地操作的授权，以及“实现→运行→检查→修复→复验→聚焦提交”的完成要求；不扩张到远程发布或无关工作。

## Skill 触发与加载

| 项目Skill | 描述长度 / 根文件 | 触发范围 |
| --- | --- | --- |
| [sokoban-unity-editor](../../../.agents/skills/sokoban-unity-editor/SKILL.md) | 120字符 / 17行 | 通过CoplayDev操作或恢复Editor、处理场景/资源、运行Unity测试 |
| [sokoban-robot-asset](../../../.agents/skills/sokoban-robot-asset/SKILL.md) | 97字符 / 21行 | 修改Blender机器人源、机械动画、FBX导出或资产验证 |

两者采用默认自动发现，无额外初始化、问卷、固定报告模板或强制子Agent。根文件只保留操作边界与引用路由；具体脚本、参数及检查表在对应任务需要时读取。[官方Skill文档](https://developers.openai.com/codex/skills)确认项目发现目录为 `.agents/skills`。

静态场景审查：README措辞修改不加载两份Skill正文；规则修改只读取对应GDD章节，运行Unity测试时再用Editor入口；相机/角色游戏代码不触发Blender重建；机器人导出只加载资产相关章节，确需Unity导入时再加载Editor入口；GM方案修改不自动启动游戏或实现完整GM功能。此项是路由审查，不冒充模型隐式选择的端到端评测。

## 共享Skill的实际边界

桌面Codex原生加载器返回的Skill元数据无解析错误，其中2份属于本项目；快照数量见验证JSON。共享目录存在同名 `skill-creator`、`unity-developer`、`unity-shader`；两份 `unity-developer` 的正文哈希完全相同，描述面向Unity 6并要求广泛主动触发。旧版Skill Creator面向Claude，通用Unity MCP流程与本项目入口重复。

尝试过项目级 `skills.config` 禁用，但在本机 `0.154.0-alpha.6.2` 中，`config/read`虽能读取这些条目，实际 `debug prompt-input` 仍包含被禁用的共享描述；命令行覆盖的对照检查才移除了目标描述。因此已移除无效配置，没有宣称共享Skill已从目录中禁用。项目通过AGENTS明确本地适用范围，选用本地Editor流程、内置Codex Skill Creator和一份Shader指导。

用户级Skill、系统Skill、插件与其他项目的全局启用配置不属于本次写入范围。共享描述仍可能显示在当前会话目录中；项目重构不会物理压缩这部分全局目录。也未提高技能上下文预算或替换用户的模型/权限设置。

## 验证结果

详细实测摘要见 [AgentConfigurationResults.json](Validation/AgentConfigurationResults.json)。

- 两份Skill通过内置 `quick_validate.py`，原生 `skills/list` 均识别为 `scope=repo`、`enabled=true`。
- Markdown本地链接、Skill引用、TOML/JSON解析、Unity版本与MCP路由核对通过。
- 用桌面当前二进制执行 `debug prompt-input`；简单README任务包含根规则和两份Skill描述，没有预载它们的正文、完整GDD或建模指南。该命令只渲染输入，没有创建模型工作任务。
- 启动已安装的Unity 2022.3.51f1后，CoplayDev自动恢复服务。项目路径为 `C:/recent_project/Sokoban_3D_Test`，场景为Bootstrap，编译/导入空闲，Console错误为0；CLI状态、实例、项目、Editor状态和层级读取均成功。
- 对722个非Markdown源文件记录哈希：721个保持不变；并行提交 `56330ae` 修改了LevelEditor菜单入口，该改动单独保留。README和指南已与新的 `Sokoban_Tools > Level Editor` 入口一致。本轮不提交该并行代码改动或原有机器人、项目设置和插件导入。
- 本轮是指导与配置维护，无游戏行为修改；未重复运行整个游戏测试、构建或重建美术。历史游戏与资产验收状态保持真实。
