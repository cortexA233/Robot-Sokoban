# 履带维修机器人交付

按 `Docs/Art/RobotBlenderGuide.md` v1.2 制作，协议为 `robot-asset-v1`。

## 成果

- 源文件：`ArtSource/Robot/Robot.blend`，Blender **5.2.2 LTS**。
- 白模阶段：`Robot_blockout.blend` 与 `previews/blockout/` 四向预览。
- 导出：`Assets/Art/Robot/Meshes/Robot.fbx`。
- Unity：**2022.3.51f1 / URP 14.0.11**。
- Prefab：`Assets/Resources/prefabs/gameplay/player/Robot.prefab`。
- 动画控制器：`Assets/Art/Robot/Animations/Robot.controller`；材质：`Assets/Art/Robot/Materials/`。
- Unity 资产直接维护现有导入设置、材质、控制器与 Prefab；v0.8.0 已删除旧导入/验证工具和自动标记触发。
- 实测静止尺寸（Unity X/Y/Z）：**0.7000 / 0.8000 / 0.6805 m**。接地面 Y=0。
- **4,996 三角面、15 个 Mesh、25 个导出节点、4 种材质**；无贴图或外部模型依赖。

| 动画 | 帧范围 | 时长 | 循环 |
| --- | --- | --- | --- |
| Idle | 0–60 | 2.00 s | 是 |
| Move | 70–100 | 1.00 s | 是 |
| Push | 110–128 | 0.60 s | 否 |

三段动作均在 Blender 内以机械节点关键帧制作。轮组有非对称橙色标记，Move 与 Push 各转一圈。Idle 轻微左右观察；Move 保持 3°前倾；Push 伸出、保持并收回。根节点始终静止。

## 验证

交付时的 `blender_validation.json`、`fbx_validation.json`、`unity_validation.json` 均通过。报告包含实际采样值和资产 SHA-256；`robot_asset_manifest.json` 汇总参数及交付文件哈希。这些结果仅适用于记录的文件版本。2026-09-16已发现工作区 `Robot.blend` 存在未提交修改、与清单哈希不符；保留该修改，采用新源导出前重新验证，不能只刷新清单哈希。

- 推板接触面收回 Z=0.36、伸出 Z=0.60，中心 Y=0.40。
- 113–125 帧逐帧保持接触；Unity 内同步推动 1 m 的计算检查误差小于 **0.000001 m**。
- `EmotionAnchor` 直接位于 `RobotRoot` 下，位置 `(0,1.05,0)`。
- Unity 验证三条剪辑名称、时长、循环、完整轮转、根节点不动、锚点、物理接触面、左右前后轮命名、循环首尾和 Push 回到中立。
- Generic、NoAvatar、无 Root Motion、无动画压缩，保留全部机械节点。
- `previews/unity.png` 是实际 Unity URP 预览；其余静帧和视频来自 Blender。Idle/Move 视频各重复三次；Push 视频末尾停留中立姿态方便观察。

## 坐标转换与导入

Blender 源模型为 +Z 向上、−Y 朝前。导出脚本创建独立临时副本，将每个节点、网格和逐帧动画统一转换为原始 FBX `(x,z,y)`；反转面的顶点顺序以配合坐标手性变化。已实测 Unity 的本组 FBX 导入约定反转 Z，最终得到 `(x,z,−y)`，即 +Y 向上、+Z 朝前。

此步骤让导入后的 `RobotRoot` 位置、旋转为零、缩放为一。`use_space_transform` 与 `bake_space_transform` 均关闭；不要改回普通的直接导出参数。回读脚本用固定逆基变换把 FBX 数据还原到源坐标后测量；Unity 再独立验证最终游戏坐标。

Unity 保留模型包装节点 `Robot`，内部 `RobotRoot` 路径为 `RobotRoot`。清单中的锚点路径相对于这个内部根。实例化 `Robot.prefab` 后，游戏只移动最外层稳定 Transform。控制器只有 Idle、Move、Push 三个状态，默认 Idle；没有自动状态转移，交由 RobotPresenter 统一采样。

URP 映射依据四种基础颜色与粗糙度；发光眼睛采用低强度线性青色，保证在没有色调映射的编辑器预览中也能看清。

## 维护与复验

只检查现有交付时，读取报告并比对其文件哈希即可。修改资产后按受影响环节选择以下命令，在仓库根目录运行；校验脚本也会写报告，执行前保留需要的旧证据。路径按本机安装位置调整：

```powershell
$robotBlender = 'D:\Steam\steamapps\common\Blender\blender.exe'
& $robotBlender -b ArtSource/Robot/Robot.blend --python-exit-code 1 -P ArtSource/Robot/scripts/validate_robot.py
& $robotBlender -b ArtSource/Robot/Robot.blend --python-exit-code 1 -P ArtSource/Robot/scripts/export_robot.py
& $robotBlender -b --factory-startup --python-exit-code 1 -P ArtSource/Robot/scripts/validate_robot.py -- --roundtrip
& $robotBlender -b ArtSource/Robot/Robot.blend --python-exit-code 1 -P ArtSource/Robot/scripts/render_previews.py
python ArtSource/Robot/scripts/package_previews.py
```

Unity 刷新后直接检查现有 FBX 导入设置、三条剪辑、材质、控制器和 Prefab 引用，保留 `.meta`/GUID。旧导入/验证工具及 `.validate-unity` 自动触发已删除；修改资产后通过受影响的游戏/表现测试和实机检查记录新的证据，旧报告仅作历史参考。所需验证通过后才执行 `python ArtSource/Robot/scripts/write_manifest.py` 更新清单。视频打包需要 Pillow 与 ffmpeg。

`build_robot.py` 是从头生成模型的入口，常规维护不运行。仅在请求包含重建且现有源修改已受到保护时使用；该脚本会重建任务所属内容。导出脚本通过临时副本处理坐标与关键帧，不覆盖源模型。

## 接入范围

本目录记录角色资产及Unity导入验收。游戏层的PlayerActor、RobotPresenter、网格动作、相机、撤销与重开已另有实现和测试；表情/反馈等剩余范围见 [ImplementationProgress.md](../../Docs/ImplementationProgress.md)。资产报告不替代游戏级证据，普通游戏代码修改也不要求重建美术资产。

参考：[Unity 动画切片](https://docs.unity3d.com/2022.3/Documentation/Manual/Splittinganimations.html)、[Unity 轴转换 API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ModelImporter-bakeAxisConversion.html)。
