# Station Kit · station-kit-v1

首批 11 个主资产已完成 **Blender / FBX 生产**和 **Unity 独立资产验收**。
正式游戏接入尚未执行：BoardView、规则、关卡 JSON、参考解法和构建场景列表均未修改。
验收日期：2026-09-16（America/New_York；报告时间使用 UTC）。

- [可编辑源文件](StationKit.blend)
- [逐资产清单、节点/材质槽、实测数据、SHA-256](station_kit_asset_manifest.json)
- [Blender 总览](previews/blender_overview.png) · [Unity 北向顶视](previews/unity_overview_top_1080.png)
- [两类箱子与机器人接触](previews/blender_crates.png) · [六种插槽占据状态](previews/blender_sockets.png)
- [Unity 多路连接](previews/unity_connections_1080.png) · [地板、直段、转角、端头、T/十字拼接](previews/blender_joins.png)
- [Blender 机械开合](previews/blender_mechanical.mp4) · [Blender 断电占据交接](previews/blender_occupancy.mp4) · [Unity 断电占据交接](previews/unity_occupancy.mp4)

## 交付资产

所有 FBX 位于 `Assets/Art/StationKit/Meshes/`；对应同名 Prefab 位于
`Assets/Resources/prefabs/gameplay/StationKit/`。Unity 包装根名为资产 ID，内部根为 `<AssetId>Root`。

| 资产 | 三角面 | Mesh / Renderer | 实测尺寸 X × Y × Z（m） |
| --- | ---: | ---: | --- |
| EnergyCrate | 3460 | 3 | 0.8005 × 0.8000 × 0.8005 |
| CargoCrate | 2540 | 2 | 0.8008 × 0.8000 × 0.8008 |
| GoalSocket | 1120 | 4 | 0.9400 × 0.0570 × 0.9400 |
| UtilitySocket | 1040 | 4 | 0.9400 × 0.0570 × 0.9400 |
| PowerGate | 2865 | 11 | 0.9800 × 1.6640 × 0.9800 |
| FloorPlain | 88 | 1 | 0.9800 × 0.2400 × 0.9800 |
| FloorService | 312 | 1 | 0.9800 × 0.2400 × 0.9800 |
| FloorGrate | 364 | 1 | 0.9800 × 0.2400 × 0.9800 |
| WallStraight | 732 | 2 | 0.9800 × 1.5400 × 0.9800 |
| WallCorner | 732 | 2 | 0.9800 × 1.5400 × 0.9800 |
| WallEnd | 856 | 2 | 0.9800 × 1.5400 × 0.9800 |

两类箱子的小标识最多超出名义侧面 0.4 mm，在 5 mm 尺寸容差内；最大推动接触误差小于 1 mm。
门顶为 Y=1.6505，标牌底部嵌入到 Y=-0.0135，所以总包围盒高度为 1.664。
相对初值 1.60 m 增加的顶盖高度用于保留收纳净空，门占地仍为 0.98 m。
打开后的中央净宽至少 0.86 m，最低门板边缘 Y=1.025 m。
插槽底部为 -0.052 m；通行范围内图形最高约 2.35 mm，四角护片 5 mm 高且位于双向扫掠范围以外。

## 源文件与编辑

使用本机实测的 Blender **5.2.2 LTS** 制作；Unity **2022.3.51f1**、URP **14.0.11**、
Cinemachine **2.10.7**、CoplayDev MCP **10.2.0**。不依赖外部图片、模型下载、节点插件或自定义 Shader。
机器人参考来自已交付 Robot.fbx，网格嵌入本源文件的参考集合，未修改或重导出 Robot.blend。

`StationKit_Workshop` 是唯一维护场景：

- `StationKit_Assets/<AssetId>`：11 个源资产，根都在原点；编辑时单独隔离相应集合。
- `StationKit_Reference`：已有机器人参考，禁止作为套件导出。
- `StationKit_Preview`：预览入口保留集合。实际摆放位于八个独立 `StationKit_*` 预览场景，预览副本复用源 Mesh。

Blender 对象使用完整层级前缀避免全局名称冲突；自定义属性 `nodeName` 保存导出节点名。
正常编辑 Mesh 后保存，再执行导出脚本。不要重跑生成器代替保存手工修改。
手工改动基础材质颜色/粗糙度后，同时维护 `StationKitAssetTools` 中的 Unity 映射；验证报告会分别记录源材质与实际 URP 参数，不假定 FBX 材质自动迁移正确。
`build_station_kit.py` 对已有源文件默认拒绝重建；明确传入 `--rebuild` 才会重建，并先备份整个源文件到 `Logs/StationKitBackups/`。
导出只处理临时副本，不应用修改器或坐标变换到原模型；与上次导出收据不一致的现有 FBX 会先备份。
Unity 工具覆盖已有材质、Prefab、展示场景前也保留备份，始终保留 `.meta` 和 GUID。

坐标采用 Blender `(x,-z,y)` 对应 Unity `(x,y,z)`；FBX 使用显式逐节点/网格基变换。
此转换已通过独立 FBX 回读和 Unity 根、包围盒及 N/E 接触挂点实测，不能替换成仅调整导出下拉项。

## 材质、状态与挂点

公共材质位于 `Assets/Material/Station/`，均为 URP/Lit。完整映射在清单的每个 Renderer/槽索引中；不要硬编码全局槽号。
颜色按 sRGB 记录，Unity `_Smoothness = 1 - Blender roughness`。发光强度使用线性颜色乘法，状态灯材质启用 `_EMISSION`。
能源窗使用独立固定角色 `FixedEnergy`，不属于 `LinkAccent` 或机关供电状态；普通箱只有固定外观角色。

| 接口 | 用法 |
| --- | --- |
| `Geometry` / `TypeMarker` | 两类箱子的固定结构和图形；`EnergyCrate → kind=Energy`，`CargoCrate → kind=Cargo` |
| `EnergyWindow/FixedWindows` | 固定能源窗外观；不能用关灯模拟 Cargo |
| 插槽 `LinkMarkers/IdentityBands` | 单一来源身份的非发光色条；四侧重复 |
| 插槽 `PowerStatus/PowerLamps` | 单独供电灯材质槽；仅 Energy 占槽点亮 |
| 门 `PowerStatus/ConditionLamp` | 电源条件灯，与实际开门状态分离 |
| `TopDownMarkers/ClosedMark`、`OpenMark` | 互斥显示，不缩放到零；与供电灯独立 |
| `UpperStructure`、`MovingParts`；墙 `Upper` | 俯视隐藏所有后代 Renderer；保留门 Base/标牌/通行标记、墙 Base |
| `SourceBadgeTemplate` | 默认第一路可见标牌，也是复制模板；不额外重叠一个隐藏模板 |
| `SourceBadge02` | 默认第二路标牌，身份和供电可独立寻址 |
| `SourceBadgeAnchor` | 更多标牌的第一挂点 `(−0.28, −0.0065, −0.446)`；沿 +X，以 0.28 m 间距排列 |
| `PowerModeAnchor` | Any/All 表现标签挂点；正式接入按实际相机距离选择可读字号/图形 |

默认两路标牌位于地面 +Z 边，模板/第二路 X 分别为 -0.14 / +0.14；额外一路演示位于 -Z 边。FBX 使用中性白底，门 Prefab 的两路默认示例配色为蓝/紫，供电灯关闭；均可独立替换。
相同模板可在后沿排列三块；更多来源应使用扩展显示或聚合面板，不能把任意数量塞进一个 1 m 格子。
标牌为朝上的平面，俯视和倾斜视角可读；不承诺地面水平视线下仍能读出所有来源。
展示用有限缓存材质变体 Blue/Purple/Lime、On/Off，不逐帧创建材质、不改写中性共享基础材质。
`SocketIdentityTriangle.asset`、`SocketIdentityCircle.asset` 是 Unity 导入网格的替换符号配套件；不增加主资产/FBX 数量。

### 门的机械姿态

相对于 `PowerGateRoot/MovingParts`，旋转都为 `(0,0,0)`，缩放都为 `(1,1,1)`：

| 动件 | Closed localPosition | Open localPosition |
| --- | --- | --- |
| LowerPanel | `(0,0.29,0.045)` | `(0,1.305,0.045)` |
| UpperPanel | `(0,0.87,-0.045)` | `(0,1.305,-0.045)` |

Blender 两个动件均有 location 动画：帧 1–15 关闭，39–64 打开，88–100 关闭，30 fps。
两段门板位于分开的轨道；源文件逐帧测了 100 帧固定结构干涉及动件相交，均为零。
FBX **不含动画剪辑**；交付的 `.blend` 动画、姿态和视频是机械方案。
正式 Unity 接入应根据 `Sockets`、`PoweredGates`、`OpenGates` 三个独立信号驱动，支持取消、暂停、撤销/重开恢复。
本轮 Editor 展示工具不是运行时状态驱动器，不修改现有规则。

## 复现与复验

在仓库根执行。`$stationBlender` 改为本机 Blender 路径；示例路径不是源文件依赖。

```powershell
$stationBlender = 'D:/Steam/steamapps/common/Blender/blender.exe'
& $stationBlender -b ArtSource/StationKit/StationKit.blend -P ArtSource/StationKit/scripts/export_station_kit.py
& $stationBlender -b ArtSource/StationKit/StationKit.blend -P ArtSource/StationKit/scripts/validate_station_kit.py
& $stationBlender -b ArtSource/StationKit/StationKit.blend -P ArtSource/StationKit/scripts/preview_station_kit.py
& $stationBlender -b ArtSource/StationKit/StationKit.blend -P ArtSource/StationKit/scripts/preview_station_kit.py -- render StationKit_Overview StationKit_Crates StationKit_Sockets StationKit_Colors StationKit_Connections StationKit_Gates StationKit_Joins StationKit_Corridor
& $stationBlender -b ArtSource/StationKit/StationKit.blend -P ArtSource/StationKit/scripts/render_motion.py
```

手工编辑源后，预览脚本重新建立摆放副本并保存；`gallery_layout()` 导出同一摆放给 Unity，调用示例：

```python
import runpy
pipeline = runpy.run_path('ArtSource/StationKit/scripts/preview_station_kit.py')
pipeline['gallery_layout']()
```

通过项目 Unity MCP 核对工程路径/就绪状态，刷新并等待编译，然后执行菜单：

1. `Tools > Station Kit > Import and Validate Assets`
2. `Tools > Station Kit > Build and Capture Gallery`
3. `Tools > Station Kit > Capture Occupancy Sequence`

工具建立隔离场景，保存 [StationKitPreview.unity](../../Assets/Scenes/StationKitPreview.unity)，随后恢复原场景。
展示场景不在正式构建列表，八个命名区域可分别观察。相机隔离在 layer 31，不修改项目层名称。
三种地板按 1 m 拼接；墙按相邻关系选直段/转角/端头并旋转 90°，T/十字邻接用完整实心格体组合，禁止使用 L 形空壳替代墙格。

安装了 Pillow、ffmpeg 后，`python ArtSource/StationKit/scripts/package_evidence.py` 将本地帧编码为三段视频并生成 QA 图板。
最后再次运行源/FBX 验证（预览保存也会改变源文件哈希），检查 Unity Console，再运行
`python ArtSource/StationKit/scripts/write_manifest.py`。该步骤核对报告对应文件哈希，不能用刷新哈希替代复验。

## 本次验收与限制

- [Blender/FBX 报告](validation/blender_fbx_validation.json)：205 项通过，包括每类箱子 36 条接触射线、四向扫掠和 100 帧机械干涉检查。
- [Unity 导入报告](validation/unity_validation.json)：65 项通过，独立实测尺寸、轴向、材质、Prefab 和扫掠。
- [Unity 展示报告](validation/unity_presentation.json)：17 项通过；含三组配色、六种占槽状态、一源多门、Any/All、额外标牌、俯视隐藏。
- [Unity 占据演示](validation/unity_occupancy.json)：72 帧、133 项状态断言通过，普通箱→机器人→腾空，全程断电。
- [视觉复核](validation/visual_review.json)：分别记录图像、观察和检查范围。1920×1080、1280×720，FOV 55°/3.2 m 四侧观察、北向正交俯视和灰阶，无 Bloom。

扫掠是保守几何检查，容许地面覆盖图形最高 3 mm；不依赖物理碰撞器，也没有抬高箱子/机器人。
当前材质与细节预算通过，但没有宣称完成大关卡性能基准、LOD、光照烘焙、正式状态同步或完整游戏回归。
小型标牌在完整棋盘缩放下依靠颜色和大类形状辅助识别，正式 UI 接入可补短编号与放大提示。
原 Robot.blend 的未提交版本保持原哈希。原有 Plugins、DOTween 设置及 ProjectSettings 的未提交内容不属于本次提交；Unity 导入期间 ProjectSettings 文件哈希发生变化，现有 DOTWEEN defines 保留，未将其回退或纳入本次提交。
