# Project workflow

- Use Unity **2022.3.51f1**. Keep URP 14 and Cinemachine 2.x; do not upgrade the engine or these packages as part of tool setup.
- Use **CoplayDev MCP for Unity 10.2.0** for live Editor work. Connection and recovery instructions are in `docs/UNITY_MCP.md`.
- The project MCP server is `coplay_unity` at `http://127.0.0.1:8087/mcp`. Select `Sokoban_3D_Test` and verify its project path before mutations.
- Read the Editor state and project resources before using tools. Enable the needed tool groups with `manage_tools`; optional package-specific tools require their corresponding Unity packages.
- After script changes, wait for compilation and inspect Console errors. Use relevant EditMode/PlayMode tests and screenshots to verify results.
- If the current Codex task has not loaded the new MCP configuration, use `Tools/UnityMcp.ps1` as the CLI entry point. It selects this project's server and instance explicitly.
- Inspect context, preserve unrelated work, implement the requested scope, run proportionate verification, and review the diff.
- After verification, stage only this iteration's files and create a focused Git commit. Do not push, create pull requests, or publish unless explicitly requested.
