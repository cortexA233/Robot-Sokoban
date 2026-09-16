# CoplayDev Unity MCP

本项目使用 **Unity 2022.3.51f1 + CoplayDev MCP for Unity 10.2.0**。

## 安装与连接

| 项目 | 配置 |
| --- | --- |
| Unity 包 | `com.coplaydev.unity-mcp`，通过 `Packages/manifest.json` 安装 |
| 上游版本 | `v10.2.0`，提交 `30d22075093d1d35dfb0091c1c7550e9ad948577` |
| Python 服务 | `mcpforunityserver==10.2.0` |
| MCP 地址 | `http://127.0.0.1:8087/mcp` |
| Codex 服务名 | `coplay_unity` |
| Unity 实例 | `Sokoban_3D_Test` |

Unity 包由 Package Manager 自动恢复。Python 服务和 CLI 安装在本机 uv 的独立工具环境中，复现安装：

```powershell
uv tool install --python 3.13 mcpforunityserver==10.2.0
```

打开工程，执行 **Tools > Sokoban > Configure CoplayDev MCP**，再重新打开 Unity。包会在编辑器载入后自动启动后台 HTTP 服务并连接；也可在 **Window > MCP for Unity** 中管理启动、停止和连接。

该配置菜单使用上游的 Unity `EditorPrefs` 保存本机传输方式、地址和自动启动选项，这些偏好由同一 Windows 用户下的 Unity 项目共享。切换到使用其他 MCP 地址的工程时，可在 MCP 窗口调整，返回本工程时重新执行上述菜单。

`.codex/config.toml` 为本工程启用 `coplay_unity`，并在项目范围内禁用继承的 `unity` 和 `unityMCP` 配置，避免使用其他工程的官方服务或旧版 Coplay 服务。用户级 Codex 配置继续保留。项目需处于 Codex 的受信任范围。

已打开的 Codex 任务可能仍持有旧工具目录；重新打开任务以加载新配置。可先运行下方 CLI，无需等待工具目录刷新。

## 终端入口

在项目根目录使用包装脚本，它固定了服务地址和项目实例，并处理中文 Windows 的 Python 输出编码：

```powershell
.\Tools\UnityMcp.ps1 status
.\Tools\UnityMcp.ps1 -f json scene hierarchy
.\Tools\UnityMcp.ps1 editor console
.\Tools\UnityMcp.ps1 editor tests --mode EditMode --async
.\Tools\UnityMcp.ps1 editor poll-test <job_id> --wait 30 --details
```

上游 CLI 的 `--version` 当前显示自身的 `1.0.0`；安装包版本以 `uv tool list` 中的 `mcpforunityserver v10.2.0` 为准。

## Agent 使用流程

1. 读取 `mcpforunity://instances`，用 `set_active_instance` 选中本工程。
2. 读取 `mcpforunity://project/info`，确认 `projectRoot`；读取 `mcpforunity://editor/state`，确认编辑器就绪。
3. 按需用 `manage_tools(action="activate", group="...")` 启用 `testing`、`docs`、`ui`、`animation`、`profiling`、`scripting_ext` 或 `vfx`。工具分组的可见性按会话生效。
4. 使用场景、对象、组件和 Prefab 工具操作项目。批量操作可使用 `batch_execute`。
5. 从文件系统新增、删除或移动 Unity 资源后，使用 `refresh_unity(mode="force", scope="all", compile="request")`；只请求脚本编译不会导入尚未登记的新文件。等待编译/重载完成，再读 Console。

运行 KToolkit 验收测试的 MCP 参数：

```json
{
  "mode": "EditMode",
  "assembly_names": ["KToolkit.Tests.Editor"],
  "include_details": true,
  "include_failed_tests": true
}
```

将上述参数交给 `run_tests`，随后使用 `get_test_job(job_id=..., wait_timeout=30)` 获取结果。

## 2022.3 兼容处理与功能边界

- **测试回调恢复**：`Assets/Scripts/Editor/Mcp/SokobanMcpTestCallbacks.cs` 在每次脚本域载入时创建上游测试服务。10.2.0 的服务默认延迟创建；本项目的 EditMode 测试会进入 Play Mode 并重载脚本域，原回调可能丢失，造成测试结束后 MCP job 仍显示 `running`。此文件恢复上游回调注册，不修改 KToolkit 测试或包缓存。
- **测试结果读取**：跨脚本域重载后，上游 `progress` 计数和逐项 `results` 列表仍可能不完整；最终判定使用 `status` 和 Unity Test Runner 返回的 `result.summary`，不要把中途进度当作最终通过数。
- **Cinemachine 2.x**：本项目保留 `2.10.7`。上游相机高级工具探测的是 Cinemachine 3 的 `CinemachineCamera`，因此可能报告未安装 Cinemachine。基础 Camera 和截图仍可使用；2.x 相机通过 `manage_components`、`unity_reflect` 或 `execute_code` 操作，不为启用相机预设升级依赖。
- **动态 C#**：`execute_code` 默认在可用时使用 Roslyn，否则使用 CodeDom。应检查返回的编译器/错误信息，不能假设动态代码支持项目脚本的全部语言特性。
- **可选扩展**：ProBuilder、VFX Graph 等需要相应 Unity 包；资产生成工具还需要用户自己的服务凭据。MCP 接入不会自动补装这些开发依赖。
- **Build Profiles**：属于 Unity 6 功能；2022 使用普通 Build Settings 和构建命令。

## 验收记录

2026-09-15，在本机 Unity 2022.3.51f1 中完成以下验证：

| 检查 | 结果 | 本地证据 |
| --- | --- | --- |
| Package Manager 解析和编辑器编译 | 通过；批处理退出码 0 | `Logs/mcp-install.log` |
| 重开 Unity 后自动启动并连接 | 通过；监听 `127.0.0.1:8087`，连接到本工程 | `Logs/mcp-editor.log` |
| MCP 场景载入和层级读取 | 通过；SampleScene 的相机、灯光、Volume 可见 | `Logs/mcp-hierarchy.json` |
| 相机截图 | 通过；已查看生成图片 | `Logs/McpAcceptance/mcp-acceptance.png` |
| 资源刷新、重新编译和断线恢复 | 通过 | `Logs/mcp-refresh.json` |
| KToolkit 自动测试 | **3/3 通过**，MCP job 状态为 `succeeded` | `Logs/mcp-test-results.json` |
| 动态 C# 执行 | 通过；CodeDom 返回 `2022.3.51f1` | `Logs/mcp-code-execution.json` |
| Cinemachine 2.x API 反射 | 通过；找到 `Cinemachine.CinemachineVirtualCamera` | `Logs/mcp-cinemachine-reflection.json` |
| Console 错误读取 | 0 条错误 | `Logs/mcp-console.json` |
| 项目 CLI 与 Codex 配置 | CLI 层级读取成功；`coplay_unity` 启用，地址和超时正确 | `Tools/UnityMcp.ps1`、`.codex/config.toml` |

验收使用现有 SampleScene 和 KToolkit 测试，未覆盖全部可选工具或重新构建游戏。上述本地日志和截图由 `.gitignore` 排除。

## 排查

- 无连接：先打开本工程，检查 **Window > MCP for Unity** 的 HTTP 地址是否为 `http://127.0.0.1:8087`，等待服务就绪后重新连接。
- Codex 工具缺失：在项目目录执行 `codex mcp list`，确认 `coplay_unity` 启用，再重新打开任务。
- CLI 找不到：重新执行上面的 `uv tool install`，并让 uv 的工具目录进入 PATH。可用 `uv tool update-shell`，然后重开终端。
- 测试任务异常中断：先确认编辑器状态中的 `tests.is_running` 为 false，再用 `run_tests(clear_stuck=true)` 清除孤立任务。不要清除仍在执行的测试。

## 来源

- [CoplayDev Unity MCP](https://github.com/CoplayDev/unity-mcp)
- [安装文档](https://coplaydev.github.io/unity-mcp/getting-started/install)
- [CLI 文档](https://coplaydev.github.io/unity-mcp/guides/cli)
- [Codex MCP 配置](https://developers.openai.com/codex/mcp)
