---
name: sokoban-robot-asset
description: Modify this project's Blender robot source, mechanical animation, FBX export or asset validation.
---

# Sokoban robot asset

Use this workflow for robot art maintenance. Gameplay movement, camera behavior and animation playback code use [GameDesign.md](../../../Docs/GameDesign.md) section 9 instead.

Start from the existing source and protect local edits. `build_robot.py` and Unity's **Import and Validate** rebuild outputs; ordinary inspection does not require them. Keep `robot-asset-v1` dimensions, anchors, three clips and GUIDs unless the requested change includes updating their consumers.

Load only the needed route:

| Operation | Reference |
| --- | --- |
| Inspect current assets, baseline or validation evidence | [Robot README](../../../ArtSource/Robot/README.md), “成果” and “验证” |
| Change geometry, pivots or materials | [RobotBlenderGuide.md](../../../Docs/RobotBlenderGuide.md), sections 2–4 |
| Change Idle/Move/Push or their export timing | RobotBlenderGuide sections 5–6 |
| Export, reimport or diagnose a failed asset check | Robot README “维护与复验”; RobotBlenderGuide sections 6–8 |

Read or run only the script needed for the chosen operation. Validate changed exports in Blender/FBX and Unity as applicable, then update the manifest against those actual results. Use the [Unity Editor skill](../sokoban-unity-editor/SKILL.md) for live import/test work. A refreshed hash or an old passing report is not proof for modified assets.
