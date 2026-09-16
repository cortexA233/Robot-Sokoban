---
name: sokoban-unity-editor
description: Operate or recover this project's Unity Editor through CoplayDev MCP, including scene/asset changes and Unity test runs.
---

# Sokoban Unity Editor

Use this workflow for live Editor access. Static C# or documentation edits alone do not need it. Project authorization and pinned versions come from [AGENTS.md](../../../AGENTS.md).

Read only the relevant section of [UnityMcp.md](../../../Docs/UnityMcp.md):

- **Connect or recover:** “安装与连接”, “终端入口” and the matching “排查” item. Use `Tools/UnityMcp.ps1` if the task's MCP tool catalog is stale.
- **Inspect or modify Editor content:** “Agent 使用流程”. Select the instance and verify the project path/readiness before mutations; load resources and tool groups for the target operation only.
- **Compile or test:** “编译与测试”. For stalled jobs or domain reloads, use the test callback/result notes under “2022.3 兼容处理与功能边界”.
- **Unsupported camera/package tools:** the matching compatibility note. Cinemachine 3 and Unity 6 examples do not apply to this project.

For asset moves also use [ProjectStructure.md](../../../Docs/ProjectStructure.md), “Asset moves”. Protect unsaved scenes/drafts during recovery. Inspect the resulting state and test results; fix failures introduced by the operation before completing it.
