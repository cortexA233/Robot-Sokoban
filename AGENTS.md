# Project workflow

- Use Unity **2022.3.51f1**. Keep URP 14 and Cinemachine 2.x; do not upgrade the engine or these packages as part of tool setup.
- Use **CoplayDev MCP for Unity 10.2.0** for live Editor work. Connection and recovery instructions are in `Docs/UnityMcp.md`.
- The project MCP server is `coplay_unity` at `http://127.0.0.1:8087/mcp`. Select `Sokoban_3D_Test` and verify its project path before mutations.
- Read the Editor state and project resources before using tools. Enable the needed tool groups with `manage_tools`; optional package-specific tools require their corresponding Unity packages.
- After script changes, wait for compilation and inspect Console errors. Use relevant EditMode/PlayMode tests and screenshots to verify results.
- If the current Codex task has not loaded the new MCP configuration, use `Tools/UnityMcp.ps1` as the CLI entry point. It selects this project's server and instance explicitly.
- Inspect context, preserve unrelated work, implement the requested scope, run proportionate verification, and review the diff.
- After verification, stage only this iteration's files and create a focused Git commit. Do not push, create pull requests, or publish unless explicitly requested.

# Directory and naming rules

Follow the type-based layout used by the reference project `Element_Ballance`. The rules below are the local source of truth; they do not require that reference project to be present.

## Repository root

- `Assets/`: Unity-imported assets only.
- `Packages/` and `ProjectSettings/`: Unity package and project configuration; retain their standard names.
- `Docs/`: design, setup, integration, and delivery documentation. Use PascalCase filenames, such as `GameDesign.md` and `RobotBlenderGuide.md`. Root `README.md` and `AGENTS.md` retain their conventional names.
- `Tools/`: tools invoked outside Unity, such as `UnityMcp.ps1`.
- `ArtSource/<AssetName>/`: editable art sources, their generation/export scripts, previews, manifests, and validation evidence. Keep `.blend` files outside `Assets/`; Unity consumes exported FBX files. Asset-local Python scripts and pipeline output filenames may use snake_case.
- `Library/`, `Temp/`, `Logs/`, `Builds/`, `UserSettings/`, and other generated caches stay out of Git. Do not put delivery assets or documentation in these directories.

## Unity assets

Do not add an `_Game`, `_Project`, or other sorting-prefix wrapper. Put each category directly under `Assets/`; create folders only when they have content.

| Path | Contents |
| --- | --- |
| `Assets/Art/<ThemeOrAsset>/Meshes/` | FBX and other imported geometry |
| `Assets/Art/<ThemeOrAsset>/Materials/` | Materials belonging to that art set |
| `Assets/Art/<ThemeOrAsset>/Animations/` | Animation controllers and standalone animation clips; embedded FBX clips remain in the model |
| `Assets/Art/<ThemeOrAsset>/Textures/` | Textures for that art set, when needed |
| `Assets/Material/<Theme>/` | Shared materials used across art sets; do not duplicate asset-local materials here |
| `Assets/Scripts/Gameplay/` | Board rules, level data, state, and game flow; use feature subfolders such as `Domain/` and `LevelFlow/` |
| `Assets/Scripts/Player/`, `Input/`, `Presentation/`, `UI/`, `Audio/` | Project runtime scripts, grouped by responsibility |
| `Assets/Scripts/Editor/<Feature>/` | Project editor tools; current groups are `Robot/` and `Mcp/` |
| `Assets/Scripts/Tests/EditMode/`, `PlayMode/` | Project tests, with appropriately scoped test assembly definitions |
| `Assets/Scripts/KToolkit_for_unity/` | KToolkit source, retaining its upstream layout, assembly definitions, and bundled tests |
| `Assets/Resources/prefabs/gameplay/<Feature>/` | Runtime gameplay prefabs, for example `player/Robot.prefab` |
| `Assets/Resources/UI_prefabs/screens/`, `world/` | Runtime UI prefabs, following the reference project's naming |
| `Assets/Resources/configs/` | Runtime configuration and level/catalog data; use `levels/`, `test_levels/`, and `solutions/` when implemented |
| `Assets/Resources/audio/` | Runtime-loaded audio, when needed |
| `Assets/Scenes/` | Entry and shared gameplay scenes; a scene-based level set, if introduced, belongs in `levels/` |
| `Assets/Settings/` | Render pipeline, volume, and project asset settings; input action assets belong in `Input/` |
| `Assets/Shaders/` | Project shaders, grouped by pipeline or feature |
| `Assets/Integrations/<VendorOrPackage>/` | Project-owned adapters for external packages |
| `Assets/Plugins/` | Third-party plugins with their original internal layout |

- Use PascalCase for project category/feature folders and C# types/files. The lowercase `Resources` subpaths, `Scenes/levels`, `UI_prefabs`, and `KToolkit_for_unity` above intentionally match the reference. Preserve vendor names and Unity-reserved folder names (`Editor`, `Resources`, `Plugins`, etc.).
- Keep related models, materials, textures, and animations together in their art set. Keep executable editor code in `Scripts/Editor`; keep gameplay prefabs in the documented `Resources` path.
- Reserve `Resources` for runtime content. Do not move source art, previews, editor-only validation files, or arbitrary assets there just to make loading convenient. Treat resource load keys as stable interfaces and update callers if they change.
- Third-party packages, their settings (for example `Resources/DOTweenSettings.asset`), and Unity's `TutorialInfo` retain required vendor/template paths. Template documentation assets belong under `TutorialInfo`, not loose at the `Assets` root. Do not reorganize vendor internals to enforce project naming.
- New project code must follow the above structure. Do not create parallel locations such as `Assets/Editor`, `Assets/KToolkit`, or per-asset editor scripts inside `Art`.

## Moving and verifying assets

- Prefer `AssetDatabase.MoveAsset` through the configured Unity MCP. Move every asset/folder together with its existing `.meta` and preserve GUIDs. Verify the on-disk result after import; do not assume a tool response alone proves the move succeeded.
- When a folder differs only in case, use an intermediate name so Git records the rename correctly on Windows.
- Update hardcoded paths, export/build/validation scripts, resource load keys, manifests, and documentation in the same iteration. Keep manifests tied to the asset hashes actually validated; do not refresh a modified source file's hash to imply it was revalidated.
- Preserve unrelated local changes and third-party imports. Directory work must not rebuild or overwrite art sources or change the engine/package versions.
- After moves: refresh Unity, wait for compilation, inspect Console errors, verify GUIDs and asset references, and run the affected asset checks/tests before committing.
