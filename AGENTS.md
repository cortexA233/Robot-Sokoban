# Sokoban agent guide

Work within the current user request. Explicit user instructions take precedence over skill workflows and historical plans. Read the task-relevant sections below; a small change does not require a full project or skill scan.

## Project boundaries

- Keep **Unity 2022.3.51f1**, **URP 14**, **Cinemachine 2.x** and **CoplayDev MCP for Unity 10.2.0**. Tool examples and shared skills do not authorize upgrades.
- Preserve unrelated working-tree changes, third-party imports and editable art sources. Keep Unity `.meta` files and GUIDs with their assets; resource load keys are stable interfaces.
- Live Editor work uses `coplay_unity` at `http://127.0.0.1:8087/mcp`. Select `Sokoban_3D_Test`, verify its actual project path and check Editor readiness before mutations. The CLI fallback is `Tools/UnityMcp.ps1`.
- Local edits, targeted tests, builds and fixes needed for the requested result are authorized without step-by-step approval. Protect existing scenes, drafts and source files before operations that can overwrite them. Ask only for missing decisions that materially affect scope or for destructive actions not already authorized.
- After verification, stage only this iteration's files and create a focused Git commit unless the user asks otherwise. Push, pull requests, publishing and messages to others require explicit user authorization.

## Version planning

- Before starting a new implementation iteration or requirement, read the [version ledger](Docs/Versions/README.md), choose the next unused `vMAJOR.MINOR.PATCH` under its rules, and write a version plan with scope, exclusions and acceptance checks. Record it as planned, then in progress before implementation. Do this autonomously; ask only for missing decisions that materially affect scope.
- Keep follow-up fixes and clarifications within the active version unless they form a separately scoped iteration. Check the ledger before reserving a number; do not reuse completed versions or retroactively assign versions to historical phases.
- At completion, update the plan and ledger with actual verification, remaining limitations and status. Include `[vX.Y.Z]` in each focused commit for that iteration and report the version and final commit. Blocked/unverified work must not be marked complete.
- These are development tracking versions. They do not automatically change Unity's player version, create Git tags or authorize publishing.

## Completion

Complete the requested implementation, run the affected workflow, inspect the result, fix failures caused by the change and rerun the affected checks before finishing. For script changes, wait for Unity compilation and inspect Console errors; choose EditMode, PlayMode, UI checks or a build according to the behavior changed. Documentation/configuration-only changes need their parsing, links and configuration checked, not a game rebuild. Once relevant checks pass, repeat or broaden them only for new changes, failures or unresolved concerns.

A first implementation or a plan is not completion. Continue safe recovery and independent work when a tool fails. If an external dependency or required user decision still blocks verification, report the evidence and remaining work accurately; do not mark it passed. Finish with the result, verification and any material limitation.

## Read by task

| Task | Entry point |
| --- | --- |
| Find documentation by purpose, development phase or version | [Documentation index](Docs/README.md) |
| Plan a new implementation iteration or requirement | [Version ledger and numbering rules](Docs/Versions/README.md) |
| Add, move or rename files/assets | [ProjectStructure.md](Docs/Engineering/ProjectStructure.md) |
| Change game rules, state, data or module boundaries | [GameDesign.md](Docs/Gameplay/GameDesign.md), sections 2/4, 7 and 8 respectively |
| Change menus, HUD, input or page transitions | [UIImplementation.md](Docs/UI/UIImplementation.md); GDD section 3 for camera/input contracts |
| Change level authoring | [LevelEditorGuide.md](Docs/Editor/LevelEditorGuide.md); GDD section 6 for requirements |
| Implement GM or live editing | [GMAndLiveEditingPlan.md](Docs/Debug/GMAndLiveEditingPlan.md), only for that requested feature |
| Operate/recover the Editor or run Unity tests through MCP | [sokoban-unity-editor](.agents/skills/sokoban-unity-editor/SKILL.md) |
| Edit/export the Blender robot asset | [sokoban-robot-asset](.agents/skills/sokoban-robot-asset/SKILL.md); ordinary game integration uses GDD section 9 |
| Resolve package/setup problems | [UnitySetup.md](Docs/Engineering/UnitySetup.md) |
| Establish implemented status or prepare final game delivery | [ImplementationProgress.md](Docs/ImplementationProgress.md); GDD sections 11–12 for delivery |

Select skills by the operation needed, not words such as “Unity”, “robot” or “architecture”. Load only the selected route's references. Historical reports, image-generation prompts, vendor documentation and ignored tool caches are evidence/reference material, not additional project-wide instructions. Shared skill recommendations must fit the pinned versions and current task.

For automatic selection here, use the project Editor route instead of the shared `unity-developer` (Unity 6) and `unity-mcp-orchestrator` workflows. For skill authoring, use the bundled Codex `skill-creator`; for shader work, use one `unity-shader` copy, preferring the maintained `~/.codex/skills/unity-shader` copy. Explicit user skill requests still take precedence.
