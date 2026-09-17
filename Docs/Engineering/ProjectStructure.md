# Project structure

Use this reference when adding, moving or renaming project files. Existing organization follows the type-based layout of `Element_Ballance`; that external project is not required. The workflow and authorization boundaries live in [AGENTS.md](../../AGENTS.md).

## Repository root

- `Assets/`: Unity-imported assets only.
- `Packages/` and `ProjectSettings/`: standard Unity package and project configuration.
- `Docs/`: maintained documentation by purpose and historical records by development phase; start at [the documentation index](../README.md). Use PascalCase filenames; `README.md`, `AGENTS.md` and `SKILL.md` retain their conventional names.
- `Tools/`: tools invoked outside Unity, such as `UnityMcp.ps1`.
- `.codex/`: project Codex configuration. `.agents/skills/<skill-name>/`: project skills; `SKILL.md` is a short entry point with task-specific references loaded on demand.
- `ArtSource/<AssetName>/`: editable art sources, generation/export scripts, previews, manifests and validation evidence. Keep `.blend` files outside `Assets/`; Unity consumes exported FBX files. Asset-local Python scripts and pipeline output filenames may use snake_case.
- `Library/`, `Temp/`, `Logs/`, `Builds/`, `UserSettings/`, `.utmp/` and other generated caches stay out of Git. Keep delivery documentation and maintained assets in the source directories above.

## Documentation

| Path | Contents |
| --- | --- |
| `Docs/README.md` | Navigation by purpose, development phase and existing version identifiers |
| `Docs/ImplementationProgress.md` | Current implementation baseline, remaining scope and links to historical evidence |
| `Docs/Versions/README.md`, `V<major>.<minor>.<patch>.md` | Development version ledger and per-iteration plans, verification and commit lookup; plan before implementation |
| `Docs/Versions/Roadmap.md` | Requirement-to-version assignments, execution order, dependencies and acceptance targets; scheduled versions have individual plans and ledger entries |
| `Docs/Gameplay/`, `Art/`, `UI/`, `Editor/`, `Debug/`, `Engineering/` | Maintained specifications and guides, each stored once under its primary purpose |
| `Docs/History/<NN-Phase>/` | Completed iteration reports, original proposals, `Validation/` JSON and `Images/` screenshots |
| `Docs/History/<YYYY-MM-DD-Topic>/` | Dated engineering maintenance records outside the gameplay iteration sequence |
| `Docs/History/DocumentMap.json` | Old-to-new paths and the section split of the original cumulative implementation report |
| `Docs/LevelRecipes/` | Stable editor/test input path; do not move as part of document housekeeping |

Phase numbers preserve the existing development sequence; they are not release versions. Keep document revisions, asset protocols, data schema versions and GM milestones distinct. Asset-local source documentation and evidence stay in `ArtSource/`; third-party documentation stays with its package.

For documentation moves, update Markdown links, embedded file paths, task/skill routes and consumers such as manifest generators. Preserve historical test results, timestamps and measured asset hashes. Keep audit snapshots intact and use the migration map to resolve their original paths. Validate links, JSON and affected tooling; documentation-only moves do not require Unity asset import or a game rebuild.

## Unity assets

Put each category directly under `Assets/`, without an `_Game`, `_Project` or other sorting-prefix wrapper. Create folders when they have content.

| Path | Contents |
| --- | --- |
| `Assets/Art/<ThemeOrAsset>/Meshes/` | FBX and other imported geometry |
| `Assets/Art/<ThemeOrAsset>/Materials/` | Materials belonging to that art set |
| `Assets/Art/<ThemeOrAsset>/Animations/` | Animation controllers and standalone clips; embedded FBX clips remain in the model |
| `Assets/Art/<ThemeOrAsset>/Textures/` | Textures for that art set |
| `Assets/Material/<Theme>/` | Shared materials across art sets; do not duplicate asset-local materials |
| `Assets/Scripts/Gameplay/` | Board rules, level data, state and game flow, with feature subfolders such as `Domain/` and `LevelFlow/` |
| `Assets/Scripts/Player/`, `Input/`, `Presentation/`, `UI/`, `Audio/` | Runtime scripts grouped by responsibility |
| `Assets/Scripts/Editor/<Feature>/` | Project editor tools, including `Robot/`, `Mcp/`, `LevelEditor/` and `UI/` |
| `Assets/Scripts/Tests/EditMode/`, `PlayMode/` | Project tests with appropriately scoped assembly definitions |
| `Assets/Scripts/KToolkit_for_unity/` | KToolkit source, retaining its upstream layout, assembly definitions and bundled tests |
| `Assets/Resources/prefabs/gameplay/<Feature>/` | Runtime gameplay prefabs, for example `player/Robot.prefab` |
| `Assets/Resources/UI_prefabs/screens/`, `world/` | Runtime UI prefabs |
| `Assets/Resources/configs/` | Runtime configuration and catalogs; `levels/`, `test_levels/` and `solutions/` hold level data |
| `Assets/Resources/audio/` | Runtime-loaded audio |
| `Assets/Scenes/` | Entry and shared gameplay scenes; a scene-based level set, if introduced, belongs in `levels/` |
| `Assets/Settings/` | Render pipeline, volume and project asset settings; input action assets belong in `Input/` |
| `Assets/Shaders/` | Project shaders grouped by pipeline or feature |
| `Assets/Integrations/<VendorOrPackage>/` | Project-owned adapters for external packages |
| `Assets/Plugins/` | Third-party plugins with their original layout |

Use PascalCase for project feature folders and C# files/types. Preserve the lowercase Resources paths, `Scenes/levels`, `UI_prefabs`, `KToolkit_for_unity`, vendor names and Unity-reserved names above. Keep related art together, editor code in `Scripts/Editor`, and gameplay prefabs in the documented Resources paths. Avoid parallel locations such as `Assets/Editor` or `Assets/KToolkit`.

Reserve `Resources` for runtime content, not source art, previews or editor-only validation files. Vendor settings such as `Resources/DOTweenSettings.asset` retain required paths. Unity template documentation stays under `TutorialInfo`; do not reorganize vendor internals to impose project naming.

## Asset moves

- Prefer `AssetDatabase.MoveAsset` through the configured [Unity MCP](../Debug/UnityMcp.md). Preserve each asset/folder's `.meta` and GUID, and check the on-disk result after import.
- For case-only renames on Windows, use an intermediate name so Git records the change.
- Update callers, resource keys, export/build/validation scripts, manifests and documentation affected by the move. Validation records must identify the exact files actually validated; changing a hash alone does not revalidate a modified source.
- Directory maintenance does not require rebuilding art or changing engine/package versions. Preserve unrelated local changes and third-party imports.
- After moves, refresh Unity, wait for any compilation, inspect Console errors, verify GUIDs/references and run the affected asset checks before committing.
