# 履带维修机器人：Blender 建模与动画实施指南

版本：1.4 · 日期：2026-09-16 · 状态：机器人资产已交付；作为资产维护与接入规范

目标引擎：**Unity 2022.3.51f1 / URP 14.0.x**。共享资产约定：`robot-asset-v1`。

模型、三段动画、URP材质、控制器和预制体已经交付。资产维护入口为 [sokoban-robot-asset](../../.agents/skills/sokoban-robot-asset/SKILL.md)：几何/轴心用第2–4节，动画用第5节，导入导出用第6–7节，变更复验用第8节。游戏侧代码接入使用 [GameDesign.md](../Gameplay/GameDesign.md) 第9节，不要求读取整个建模流程。

资产报告与游戏中的移动、撤销、相机和表情验收分别记录；当前游戏接入状态见 [ImplementationProgress.md](../ImplementationProgress.md)。

## 0. 资产维护范围

用户要求修改机器人时，使用Blender和可用的MCP维护现有源文件、分件模型、基础材质、Idle/Move/Push动画、FBX、清单及验证预览。默认从现有 `ArtSource/Robot/Robot.blend` 和配套脚本继续，先检查本地修改；未请求重建时不运行build_robot.py覆盖现有资产。

角色由左右履带、紧凑底盘、浅色机身、屏幕脸和前置伸缩推板组成。机器人在游戏中按格移动，推动0.8 m的能源箱。现有资产满足下述尺寸与接触约定；修改后必须保持该接口，或在同一迭代中更新GDD、使用方与验证记录。

**资产必须在 Blender 内完成三种机械动画。** Unity 负责原地动画的播放、机器人与箱子的世界位移，以及头顶表情气泡。不要把应交付的机械动作留成“以后由 Unity 脚本补上”。

### 0.1 已确定的边界

- 角色是履带机器人，具备明确的左右两条履带外形。
- 机械动作只有 `Idle`、`Move`、`Push` 三种。
- 不制作双足、行走骨架、手指、复杂抓握、战斗、跳跃、受击、死亡或庆祝动画。
- 推不动和通关等通过 Unity 的头顶气泡表示；资产只提供 `EmotionAnchor`。
- 首版用前置伸缩推板完成推动，不需要两条多关节机械臂。
- 以硬质分件和父子层级实现动作，无需柔性蒙皮；不能为了通用人形重定向引入复杂骨架。
- 履带基础运动表现为静态带体加可见轮组旋转；逐节履带循环不属于首版必需资产。
- 材质以简单颜色、粗糙度和少量发光为主，不依赖付费素材或特定外部模型。

### 0.2 源文件与操作边界

- 操作前识别当前Blender版本、文件和目标Collection，保留无关内容与本地修改。沿用现有 `Robot_Asset` 和 `Robot_Reference`，不清空场景或创建重复资产。
- 本指南不固定Blender大版本；使用已安装且兼容当前源文件的版本，记录实际版本，不为本任务擅自升级。脚本使用的Action/动画槽/F-Curve接口以该版本为准。
- 保存和预览以本次变更需要为准；尺寸、锚点和导出行为通过数据检查，外观/动画变化通过相应视角或视频检查。无需每个步骤重复生成整套预览。
- Unity导入使用 [UnityMcp.md](../Debug/UnityMcp.md) 的项目路由；资源路径和GUID规则见 [ProjectStructure.md](../Engineering/ProjectStructure.md)。

### 0.3 当前交付基线

- [交付清单](../../ArtSource/Robot/robot_asset_manifest.json) 记录Blender `5.2.2 LTS`、Unity `2022.3.51f1`、URP `14.0.11`。
- 已交付FBX、四种材质、Robot.controller和Robot.prefab；三段剪辑及帧范围与本指南一致。4996三角面、15个Mesh、25个导出节点。
- 源文件检查63项、FBX回读64项、Unity检查28项的交付报告均通过；`gameplayIntegrationChecked=false`，游戏级验收仍由实现Agent完成。
- 2026-09-16核对发现工作区Robot.blend已有未提交修改，与清单哈希不同；Git已提交的源文件以及当前FBX、预制体、材质、控制器仍匹配交付清单。保留本地源修改，不更新哈希冒充复验；从修改后的源文件再次发布导出结果前，重新验证并记录对应文件哈希。
- 原始模型层级仍为本指南第3节的RobotRoot。Unity额外包装根为Robot，Animator位于Robot上；气泡与接触点相对于它的路径分别为 `RobotRoot/EmotionAnchor`、`RobotRoot/PushSlide/PushContact`。
- `Robot.controller` 当前三状态、零参数、零Transitions、默认Idle，属于资产基础控制器；游戏外层包装、动画时间控制与事件绑定见GDD第9.4节。

## 1. 视觉设计

### 1.1 角色印象

这是一台负责搬运能源箱的维修设备：低重心、圆角外壳、短小而可靠。屏幕脸提供亲近感，橙色识别条让角色在灰白空间站中容易辨认。

从第三人称背面看，应能读出：左右履带、上部机身、略高的头部、朝前伸出的推动装置。从俯视看，应能根据头部方向、屏幕朝向和推板判断机器人面向。

### 1.2 形状语言

- 主体以倒角长方体、圆柱轮组和胶囊形履带轮廓构成。
- 头部约占整体高度的四分之一到三分之一，屏幕使用两个简单发光眼形。
- 外壳倒角清晰但不过度细碎；远处看应保持大块轮廓。
- 履带与底盘使用深色，机身浅灰，橙色仅作小面积识别色。
- 推板有宽接触面，可用深色边框和浅色正面表现机械功能。
- 可增加少量检修缝、散热槽或编号，但不能遮住推板运动空间。

## 2. 坐标、尺寸与接触约定

### 2.1 坐标转换

以下尺寸表统一以 **Unity 导入后的局部坐标**描述：

- +X：机器人右侧。
- +Y：上方。
- +Z：正前方，屏幕与推板朝向。
- `RobotRoot` 原点：机器人底盘在地面的投影中心。
- 单位：1.0 对应 1 m；网格边长为 1.0 m。

Blender 建模采用米制、Unit Scale=1、+Z 向上、-Y 为机器人正前方。表中 Unity `(x,y,z)` 的建模意图可写成 Blender `(x,-z,y)`；FBX 坐标转换还可能添加导入根变换，因此最终以回读和 Unity 检查结果为准。

建模时放置标注“FRONT”的参考箭头，帮助验证朝向。参考箭头不导出。可先用 `axis_forward='-Z'`、`axis_up='Y'` 导出，验证实际结果后固定脚本；这些参数不代替朝向验收。

### 2.2 必须一致的尺寸

| 项目 | 目标值 | 允许范围/说明 |
|---|---|---|
| 静止总宽 X | 0.70 m | 约 0.68–0.72 m |
| 静止总深 Z | 0.68 m | 约 0.66–0.70 m，包含收回推板 |
| 静止总高 Y | 0.80 m | 约 0.78–0.82 m |
| 最低可见表面 | Y=0 | 接地误差不超过 0.005 m |
| 箱子尺寸 | 0.80 m 立方体 | 用参考箱验证，参考箱不导出 |
| 推板宽/高/厚 | 0.46 / 0.20 / 0.04 m | 可略调倒角，不改变接触面位置 |
| 推板中心高度 | Y=0.40 m | 与箱子中心高度相同 |
| 推板收回正面 | Z=0.36 m | 必须遵守 |
| 推板伸出正面 | Z=0.60 m | 必须遵守，接触误差≤0.01 m |
| 推板伸缩行程 | 0.24 m | 只沿角色前后方向移动 |
| 气泡锚点 | `(0,1.05,0)` | 直接跟随 RobotRoot，保持稳定 |

**接触例子：** 机器人根位于 `(0,0,0)`，参考箱根位于 `(0,0,1)`。箱子后表面在 Z=0.60，伸出的推板正面也必须在 Z=0.60。推动过程中，机器人与箱子同时沿 +Z 移动相同距离，二者保持接触。

这一接触位置由游戏网格和箱子尺寸推导而来。不要为了造型随意改变推板行程，或把推板附在会前倾的上半身节点下。

### 2.3 建议的主体几何尺寸

以下是可直接开始建模的初值；Unity 坐标顺序是宽 X、高 Y、深 Z。

| 零件 | 尺寸/形状 | 大致中心 |
|---|---|---|
| Track_L / Track_R | 宽0.14、高0.30、深0.64，侧面胶囊形 | X=-0.28/+0.28，Y=0.15，Z=0 |
| Chassis | 宽0.50、高0.16、深0.50，倒角盒 | `(0,0.24,0)` |
| Body | 宽0.46、高0.27、深0.40，倒角盒 | `(0,0.43,0)` |
| Head | 宽0.42、高0.22、深0.24，圆角盒 | `(0,0.68,0)` |
| Screen | 宽0.32、高0.12、薄片 | 头部正面 Z≈0.13 |
| 四个可见主轮 | 半径约0.115、厚约0.025，绕 X 轴转 | X≈±0.30，Y=0.15，Z≈±0.17 |
| PushPlate | 宽0.46、高0.20、厚0.04 | 收回中心 `(0,0.40,0.34)` |

履带外壳要留出侧面窗口或轮毂，让 Move 中的轮子旋转可见。静态履带表面可以做少量凸纹，但不需要把每块履带做成独立运动链。

## 3. 对象层级与命名

推荐交付层级如下。名称是与 Unity 协作的接口，应保持稳定；细节网格可以增加在相应子树内。

```text
RobotRoot                         Empty，地面原点，整段时间轴保持不动
├─ Chassis                        Mesh，底盘
├─ Track_L                        Mesh，静态履带带体
├─ Track_R                        Mesh，静态履带带体
├─ Wheel_LF_Pivot                  Empty，左前轮轴心
│  └─ Wheel_LF                    Mesh
├─ Wheel_LB_Pivot                  Empty，左后轮轴心
│  └─ Wheel_LB                    Mesh
├─ Wheel_RF_Pivot                  Empty，右前轮轴心
│  └─ Wheel_RF                    Mesh
├─ Wheel_RB_Pivot                  Empty，右后轮轴心
│  └─ Wheel_RB                    Mesh
├─ BodyPivot                      Empty，上部机身俯仰轴心
│  ├─ Body                        Mesh
│  └─ HeadYaw                     Empty，头部左右旋转轴心
│     ├─ Head                     Mesh
│     ├─ Screen                   Mesh
│     ├─ Eye_L                    Mesh
│     └─ Eye_R                    Mesh
├─ PusherHousing                  Mesh，固定伸缩结构外壳
├─ PushSlide                      Empty，沿前后方向平移
│  ├─ PusherStem                  Mesh，伸缩杆可动部分
│  ├─ PushPlate                   Mesh
│  └─ PushContact                 Empty，推板正面中心
└─ EmotionAnchor                  Empty，气泡位置
```

轴心要求：

- BodyPivot 在 `(0,0.30,0)` 附近，只有上部机身和头部随它倾斜；履带、底盘、推板不随它前倾。
- HeadYaw 在头与身体连接处，建议静止世界位置 `(0,0.56,0)`，绕上方向轴旋转。
- 每个轮轴心位于轮子几何中心，旋转轴沿机器人左右方向。
- PushSlide 静止位置 `(0,0.40,0.34)`；PushPlate 正面在该节点局部 Z=0.02。
- PushContact 局部位置为 `(0,0,0.02)`，因此静止时位于 `(0,0.40,0.36)`。
- EmotionAnchor 直接放在 RobotRoot 下，位置 `(0,1.05,0)`。

建立父子关系时保持物件世界变换，避免改变父级后零件跳动。动的零件不要与固定零件合并。可以共享左右轮的 Mesh 数据，但对象和轴心独立。所有导出节点避免负缩放；镜像几何确认法线并应用必要变换。

## 4. 建模与材质工作步骤

### 4.1 分阶段建模

以下为首次建模的可选组织方式。现有资产维护直接修改相关部件，不从白模重新走完整流程。

**阶段 A：轮廓白模**

1. 建立 1 m 网格参考、0.8 m 参考箱和坐标箭头。
2. 创建左右履带、底盘、机身、头部和推板的基础体块。
3. 在正面、侧面、背面、顶视和第三人称相机下检查比例。
4. 临时移动 PushSlide 0.24 m，测量推板与箱子的接触位置，再恢复。
5. 保存文件和四向预览，确认尺寸后进入下一阶段。

**阶段 B：分件与机械结构**

1. 建立上述稳定层级、轮轴、头轴和推板轴心。
2. 为主体添加适量倒角；小资产主体倒角初值约 0.01–0.02 m。
3. 让侧面可见轮组形成清楚的转动标记，例如少量重复辐条。
4. 伸缩杆收回时藏进外壳，伸出时仍与固定外壳有足够重叠。
5. 推板和机身在所有极限姿势下没有明显相互穿插。

**阶段 C：材质与少量细节**

使用下表四种主要材质。细节优先使用简单几何和材质分区；不要求复杂贴图。

| 材质名 | 用途 | sRGB 颜色初值 | 表面建议 |
|---|---|---|---|
| M_RobotBody | 机身和头壳 | `#D9DFE5` | 金属度0.1，粗糙度0.55 |
| M_RobotDark | 履带、底盘、屏幕底色 | `#202A35` | 金属度0.2，粗糙度0.65 |
| M_RobotAccent | 橙色识别条、推板小标记 | `#F29D38` | 金属度0.0，粗糙度0.50 |
| M_RobotEmission | 眼睛和小指示灯 | `#63DCEB` | 低强度发光，保持轮廓清楚 |

Blender 使用简单 Principled BSDF 节点，颜色与金属度/粗糙度在清单中记录。URP 中的材质需由 Unity Agent 按这些意图建立并映射；复杂 Blender 程序材质不属于运行时依赖。

如果使用贴图，采用 PNG、相对路径，并随源文件交付；默认上限为一张 1024×1024 色彩图集。UV 需要无意外拉伸，不要求自动把所有材质烘焙成图集。

### 4.2 几何预算与清理

- 目标约 3,000–6,000 三角面，首版上限 10,000；把实际统计写入清单。
- 四个主要材质，尽量不超过 25 个可见 Mesh 对象；Empty 节点不计入该数量。
- 静态细节可合并到对应主体，独立动作部件保留拆分。
- 检查法线、重复面、退化三角形、镜像负缩放和意外隐藏对象。
- 网格不要求把所有零件焊接成一个闭合壳，但每个实体零件要有合理厚度，避免从背面看穿。
- Blender 中保留便于修改的源模型；导出副本上应用/烘焙影响外形的必要修改器，确认 FBX 结果一致。

## 5. 动画资产：三种且只有三种

### 5.1 输出时间轴

帧率固定 `30 fps`。默认用同一个文件、一个连续时间轴，在不同区间制作三个逻辑动画。所有运动对象在这个时间轴上都有对应关键帧，FBX 导出后由 Unity 拆分。

| 名称 | 帧范围 | 实际时长 `(end-start)/30` | 循环 |
|---|---|---|---|
| Idle | 0–60 | 2.00 s | 是 |
| Move | 70–100 | 1.00 s | 是 |
| Push | 110–128 | 0.60 s | 否 |

61–69、101–109 为隔离区，不纳入任何运行时剪辑。在三个区间起止处，显式给所有被动画控制的属性设置值，避免前一段动作遗留到后一段。

所有区间中：

- RobotRoot 始终位于原点，旋转不变、缩放为1。
- 履带带体、底盘和 EmotionAnchor 保持静止。
- 只给 HeadYaw、BodyPivot、轮轴和 PushSlide 等机械节点添加需要的曲线。
- 不通过材质关键帧实现必须交付的动作，避免依赖 FBX 不保证携带的着色器动画。
- 轮组旋转采用可连续转动的欧拉曲线，导出前逐帧烘焙，防止 0°→360°在导入后被当成完全不动。

### 5.2 Idle：轻微观察

| 帧 | 头部 yaw | 机身前倾 | 推板行程 | 轮组 |
|---|---|---|---|---|
| 0 | 0° | 0° | 0 m | 中立姿态 |
| 15 | -6° | 0° | 0 m | 中立姿态 |
| 30 | 0° | 0° | 0 m | 中立姿态 |
| 45 | +6° | 0° | 0 m | 中立姿态 |
| 60 | 0° | 0° | 0 m | 中立姿态 |

使用平滑缓入缓出，首尾姿态与切线一致，连续播放至少三个循环检查接缝。幅度较小，让角色呈现待命观察感。首版不需要眨眼或灯光动画。

### 5.3 Move：原地运动循环

- 70–100 帧，四个主轮各完成一整圈可见旋转，左右方向要符合机器人向前行驶的视觉。
- 上部机身保持约 3°前倾，头相对机身保持朝前；第70和100帧姿态一致。
- 推板全程收回。履带外壳保持固定形状。
- 轮组旋转用线性时间变化，首尾四元数姿态等价。机身前倾是固定姿态，不随轮子循环反复点头。
- Unity 每移动一格可以在 0.25 s 内采样一次完整 Move，或按实际移动进度采样；动作本身不移动 RobotRoot。
- 轮子一圈对应一格是本版的风格化表现约定，不用轮径推导游戏位移，也不要求物理上完全无滑动。

验收必须从侧面能看见轮组在转动。只有整个机器人平移、模型内部完全静止，不能算完成 Move 资产。

### 5.4 Push：伸出、保持、收回

Push 总时长0.60 s。游戏中的前进发生在其中0.10–0.50 s，模型根在 Blender 中仍保持静止。

| 帧 | 剪辑相对时间 | PushSlide 位移 | 推板正面 Z | 机身 | 轮组 |
|---|---|---|---|---|---|
| 110 | 0.00 s | 0.00 m | 0.36 m | 中立 | 起始角度 |
| 113 | 0.10 s | 0.24 m | 0.60 m | 前倾约3° | 起始角度 |
| 125 | 0.50 s | 0.24 m | 0.60 m | 前倾约3° | 完成一圈 |
| 128 | 0.60 s | 0.00 m | 0.36 m | 回到中立 | 与起始姿态等价 |

制作说明：

1. 110–113：推板平滑伸出，末端无过冲；玩家和箱子在引擎中尚未移动。
2. 113–125：推板严格保持在 Z=0.60，机身保持前倾，轮组按推动进度转动。
3. 125–128：推板平滑收回，上部机身回到中立。
4. 113与125之间的推板位置曲线保持常量，不能因自动贝塞尔手柄产生微小前后漂移。
5. 上半身前倾不影响 PushSlide 和 PushContact 的高度及前后位置。
6. Push 结束后所有机械节点恢复与 Idle 起点兼容的姿态；轮子的整圈角度按旋转等价比较。

在 Robot_Reference 中可做一个**只用于验证的演示**：机器人展示父节点与参考箱在113–125帧同时向前1 m，均采用同一 SmoothStep 进度；验证推板保持接触。这个展示父节点的世界位移不能进入最终 RobotRoot 的动画或 FBX。

### 5.5 动画与游戏系统的边界

- Blender 提供动作；Unity 决定移动是否合法、箱子滑到哪里、门是否打开。
- Unity 的 RobotPresenter 根据整条推动的时间归一化采样 Push，不让动画事件触发箱子逻辑。
- 机器人接触阶段只推动第一格。箱子随后沿低摩擦轨道继续滑动时，机器人停在原箱子格，Push 仍按时收回。
- 不做 PushStart/PushLoop/PushEnd 三套额外资产；这些阶段已经包含在单一 Push 中。
- 推不动时保持或返回 Idle，Unity 在 EmotionAnchor 上显示气泡；不增加 Blocked 动画。
- 通关时同样使用 Idle 和气泡，不增加 Victory 动画。

### 5.6 与Cinemachine、DOTween Pro和Unity反馈系统的配合

引擎侧已安装这些插件，机器人模型尺寸、节点名、三段动画和FBX交付约定保持 `robot-asset-v1`。

- Cinemachine控制镜头；相机目标跟随稳定的角色位置，不挂在HeadYaw或BodyPivot等动画节点下。
- DOTween提供角色/箱子在世界中的平滑位移和统一动作时钟；RobotPresenter根据同一时钟采样Blender剪辑。推板、轮子和头部已有动画曲线，不能再由DOTween Pro组件重复控制同一属性。
- 游戏的FeedbackPresenter使用Unity音源、粒子和灯光播放附加反馈，表情气泡由Unity生成；不增加新的角色动画，当前方案不要求Feel依赖。
- 推动接触和释放时点仍是Push相对时间0.10 s和0.50 s。撤销、重开时引擎停止Tween与反馈，再恢复姿态和棋盘快照。
- 后续修改仍须保留真正可播放的Idle/Move/Push，不以插件配置替代现有机械动画。

## 6. 关键帧和导出策略

### 6.1 优先使用分件物体动画

每个机械动作对应父子层级中的位置或旋转变化。MCP 可以通过 bpy 为每个相关对象添加关键帧。

概念流程：

```text
建立并检查对象层级与静止姿态
    → 设置时间轴 0..128，30 fps
    → 为所有相关属性写入三个区间的明确起止值
    → 添加 Idle / Move / Push 区间内的关键姿势
    → 播放、检查极值和循环
    → 将最终求值结果逐帧烘焙到导出对象
    → 导出单个 FBX 时间轴
    → 回读 FBX 核对层级、帧范围、姿态和尺寸
```

如使用约束或驱动器辅助创作，最终导出时应烘焙它们求值后的物件/骨骼变换。不要假定 Blender 的约束对象、Python 驱动器或自定义属性会在 Unity 中继续执行。Blender FBX 文档说明约束结果可输出为关键帧，但约束本身不保存在 FBX 中。[Blender FBX 文档](https://docs.blender.org/manual/en/4.4/addons/import_export/scene_fbx.html)

### 6.2 FBX 导出初值

现有交付已把导出方案落实到 `ArtSource/Robot/scripts/export_robot.py`：脚本对节点/网格做明确坐标转换，使用 `use_space_transform=false`、`bake_space_transform=false`，Unity侧开启bakeAxisConversion。后续导出优先沿用该脚本和清单中的实际参数，下表作为其余通用设置约定；只照Forward/Up两项手工重新导出不能保证相同结果。

| 设置 | 本方案要求 |
|---|---|
| 文件 | `Assets/Art/Robot/Meshes/Robot.fbx` |
| 导出对象 | RobotRoot 及其全部需要的子节点，仅 Mesh 和 Empty |
| 摄像机/灯光/参考物 | 排除 |
| 单位 | 源文件米制；验证导入后1单位=1m |
| 轴转换初值 | Forward=-Z，Up=Y；实际以回读/导入检查为准 |
| Bake Animation | 开启 |
| 帧范围 | 0–128 |
| Sampling Rate / Step | 1 帧 |
| Simplify | 0，先保证接触与轮转正确 |
| NLA Strips | 关闭，避免把工作条带导成额外动作 |
| All Actions | 关闭，导出所有选中对象当前时间轴上的活动动画 |
| 修改器 | 在导出副本上应用/烘焙影响外形的必要修改器 |
| 嵌入贴图 | 默认关闭；贴图相对路径随资产交付 |

不同 Blender 版本的导出面板或参数可能有差别，先查询当前 exporter 的属性再执行。单个对象的 Action 名称不是跨工具的剪辑协议，最终以时间范围和清单为准。

### 6.3 Unity 导入约定

这一部分供 Blender Agent 自检及交给 Unity Agent 使用：

1. 在 Unity **2022.3.51f1** 中导入 FBX，确认模型为米制、朝 +Z、根位于地面。
2. 沿用已验证的Generic + NoAvatar，bakeAxisConversion=true、preserveHierarchy=true、optimizeGameObjects=false、动画压缩Off；保留完整机械层级与FBX的`.meta`。
3. 保持三个剪辑名称严格为 Idle、Move、Push；使用清单里的实际导出帧范围切片。
4. Idle 和 Move 设置循环；Push 不循环，时长0.60 s。
5. 关闭 Root Motion；角色世界位移由网格系统控制。
6. 设置简单的 URP 材质映射，确认四种主材质和发光眼睛正确。
7. 在采样预览中检查轮子旋转没有消失，推板的接触区间没有漂移。
8. 首次验证关闭激进动画压缩；只有测量通过后再调整压缩参数。

Unity 支持把一个连续导入动画按帧范围拆分为多个剪辑，这正是本指南选用单时间轴的交付方式。[Unity 2022.3 动画拆分文档](https://docs.unity3d.com/2022.3/Documentation/Manual/Splittinganimations.html)

## 7. 交付目录与资产清单

```text
ArtSource/Robot/
  Robot.blend
  robot_asset_manifest.json
  scripts/
    build_robot.py                 推荐：可重建自有 Collection 的脚本
    export_robot.py                必须：可重复执行的导出脚本
    validate_robot.py              必须：尺寸/动画/锚点检查
  previews/
    front.png
    side.png
    back.png
    top.png
    third_person.png
    idle.mp4                       或等价 GIF/逐帧序列
    move.mp4
    push.mp4
Assets/Art/Robot/
  Meshes/Robot.fbx
  Materials/
  Animations/Robot.controller
  Textures/                        使用贴图时才创建
Assets/Resources/prefabs/gameplay/player/
  Robot.prefab
Assets/Scripts/Editor/Robot/
  RobotAssetTools.cs                Unity 导入与验证工具
```

上述导入目录遵循 [ProjectStructure.md](../Engineering/ProjectStructure.md)；源文件与资产流水线仍保留在 `ArtSource/Robot/`。

上述工程和资源已经存在，后续修改在原路径更新并保留`.meta`/GUID。游戏行为应放在GDD约定的PlayerActor外层与项目脚本中。`Tools > Robot > Import and Validate` 会重导入、写材质/控制器/预制体以及报告和预览，应在明确需要更新资产时运行并检查完整diff；它不是无副作用的只读检查。

下面为清单结构模板。当前真实版本、已通过检查和具体数值以 `ArtSource/Robot/robot_asset_manifest.json` 为准，不用模板里的占位值覆盖已交付清单：

```json
{
  "schemaVersion": "robot-asset-v1",
  "assetName": "Robot",
  "targetUnityVersion": "2022.3.51f1",
  "targetRenderPipeline": "URP 14.0.x",
  "blenderVersion": "填写实际读取的版本",
  "unitMeters": 1.0,
  "unityAxes": { "up": "+Y", "forward": "+Z", "right": "+X" },
  "rootNode": "RobotRoot",
  "rootMotion": false,
  "fps": 30,
  "clips": [
    { "name": "Idle", "startFrame": 0, "endFrame": 60, "durationSeconds": 2.0, "loop": true },
    { "name": "Move", "startFrame": 70, "endFrame": 100, "durationSeconds": 1.0, "loop": true },
    { "name": "Push", "startFrame": 110, "endFrame": 128, "durationSeconds": 0.6, "loop": false }
  ],
  "push": {
    "contactFrame": 113,
    "releaseFrame": 125,
    "contactNormalized": 0.1666666667,
    "releaseNormalized": 0.8333333333,
    "strokeMeters": 0.24,
    "retractedFaceZ": 0.36,
    "extendedFaceZ": 0.60,
    "contactHeightY": 0.40
  },
  "anchors": {
    "emotion": { "path": "EmotionAnchor", "position": [0.0, 1.05, 0.0] },
    "pushContact": { "path": "PushSlide/PushContact" }
  },
  "validation": { "blenderChecked": false, "fbxRoundTripChecked": false, "unityChecked": false }
}
```

说明：

- 这是待填充的交付结构示例；实际交付时替换 Blender 版本、实际剪辑帧范围和验证结果。
- 在实际清单中追加测得的包围盒、三角面数、对象数、材质参数、导出参数、输出文件路径和资产文件哈希。
- 锚点路径相对于导入的 RobotRoot；若导出器加入额外包装节点，记录并由导入适配处理，保持角色内部路径稳定。
- `contactFrame` 和 `releaseFrame` 是整条导出时间轴的帧号；normalized 值相对于 Push 剪辑计算。
- Unity暂时不可用时，先按项目连接说明尝试安全恢复或CLI入口；仍不可用则保留 `unityChecked=false`，记录具体阻塞和未验证项，继续完成独立可做的检查。不能把Blender回读当作Unity已验证，也不能将未完成的接入宣称为完成。
- 所有脚本只能重建/修改本任务的 Collection 和输出文件；保存源文件、导出与验证可以重复执行。

## 8. 验收与常见问题修复

以下清单用于受影响的资产变更复验，不要求每次游戏代码修改重跑整个资产流水线。已有资产级报告见第0.3节；游戏接入已有部分验证，当前范围以实现记录为准，历史资产采样报告不能代替它。

### 8.1 Blender 数据检查

- [ ] 静止尺寸位于第2节范围，地面原点和正前方明确。
- [ ] 左右履带外形完整，侧面能看见运动轮组。
- [ ] RobotRoot、PushSlide、PushContact、EmotionAnchor 名称和层级存在。
- [ ] 所有导出物件都归属于 RobotRoot，没有相机、灯光或参考箱混入。
- [ ] RobotRoot 在0–128帧没有运动，EmotionAnchor 保持相对根固定。
- [ ] 推板收回/伸出接触面分别为Z=0.36/0.60，Y=0.40。
- [ ] Push 的113–125帧，逐帧测量接触面位置变化≤0.005 m。
- [ ] Idle/Move 首尾姿态等价，连续三个循环没有跳变。
- [ ] Move/Push 轮子实际转动，逐帧姿态检查排除整圈丢失。
- [ ] Push 结束时推板收回、机身回正，能返回 Idle。
- [ ] 没有明显穿插、反面、缺失贴图和负缩放问题。
- [ ] 面数、对象数和材质预算已测量并记录。

### 8.2 FBX 回读检查

在临时场景或独立进程中回读导出的 FBX，不覆盖源场景：

1. 检查导入根、对象层级、动画时间范围及三个区间。
2. 采样 Idle 首中尾、Move 每四分之一周期、Push 伸出/保持/收回关键帧。
3. 比较轮组旋转和推板接触位置；检查修改器烘焙后的尺寸。
4. 检查贴图路径可解析、材质分配不丢失。
5. 记录回读结果和实际帧范围，再生成最终 manifest。

### 8.3 Unity 最终接入验收

由有引擎访问权限的 Agent 执行：

- [ ] 使用2022.3.51f1导入，模型尺寸与朝向符合 robot-asset-v1。
- [ ] 正好生成 Idle/Move/Push 三条可播放剪辑，时长和循环设置正确。
- [ ] 角色根与箱子根相距1 m时，推板伸出能接触箱子后表面。
- [ ] 使用同一个移动进度推动1 m，接触误差≤0.01 m，未出现穿箱。
- [ ] 推不动只显示气泡；通关只显示气泡，无额外动画依赖。
- [ ] 切换第三人称/俯视后角色朝向仍可读，气泡锚点稳定。
- [ ] 撤销/重开时动画可以取消并立即回到中立姿态。

### 8.4 针对性修复

| 问题 | 优先检查 |
|---|---|
| 模型导入后躺倒或背朝前方 | 源坐标、导出轴转换、包装根；先修导出，不靠场景里反复补旋转 |
| 零件绕错误位置转动 | 对应 Pivot 的世界位置和父级逆变换 |
| 轮子动画在 Blender 有、Unity 没有 | FBX 烘焙、采样、整圈旋转曲线、导入压缩和剪辑范围 |
| 推板保持阶段有轻微漂移 | 位置曲线手柄、父级机身倾斜、重复动画曲线 |
| 机器人和箱子逐渐脱离 | Root Motion、动作移动区间、两个对象是否使用同一时间进度 |
| Idle 切 Move 时推板伸出 | 相关属性没有在每个剪辑起止处显式归零 |
| 眼睛材质在 Unity 变黑或变粉 | URP 材质映射和 emission 设置，检查是否依赖 Blender 专用节点 |

## 9. 本轮维护完成标准

完成请求的资产修改与受影响的导出/导入，检查结果、修复失败并复验；保留尺寸、锚点、Idle/Move/Push及使用方约定。已要求的范围不因第一版可用而自行缩减。

清单与报告保存对应文件的实际版本、哈希、尺寸/动画统计和验证结果；最终回复链接变更产物、验证记录和已知限制，无需重新罗列所有未变规格。

## 10. 参考资料

- [Blender：物体父子关系](https://docs.blender.org/manual/en/5.0/scene_layout/object/editing/parent.html)：分件机械结构和轴心组织的参考；执行时以当前 Blender 版本为准。
- [Blender：FBX 导出](https://docs.blender.org/manual/en/4.4/addons/import_export/scene_fbx.html)：动画烘焙与导出能力的参考。
- [blender-mcp 项目](https://github.com/ahujasid/blender-mcp)：一种提供场景检查和 Blender Python 执行能力的 MCP 实现；具体工具以已连接版本为准。
- [Unity 2022.3：动画拆分](https://docs.unity3d.com/2022.3/Documentation/Manual/Splittinganimations.html)：按帧范围建立三个运行时剪辑。
