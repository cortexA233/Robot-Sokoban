# v0.11.0 美术细化实验验收

基线提交 786a08e；在当前目录的 codex/experiment-art-polish 分支实施。没有创建工作树，没有平台收边。

## 制作与资产检查

- Blender 5.2.2 LTS，通过 Blender MCP 修改现有源文件；原始源文件备份于本地 Logs/V0.11.0/Backups。
- 机器人增加检修板、头部接缝、背部接口、轮毂、履带标记和推杆套筒。7,468 三角面，仍为 15 个 Mesh、25 个节点和四种材质；尺寸和三段动作保持。
- 空间站细化两个箱子、两类插槽和门，增加 RedirectorPlate/LowFrictionDeck。原 Renderer 数量保持；普通地板、墙体 FBX 未重新导出。
- 五张共享金属度/光滑度纹理和一张微表面法线，均为 256² PNG；Blender 相对路径与 URP 线性导入。材质无外部下载依赖。
- [机器人源检查](../../../ArtSource/Robot/blender_validation.json) 63 项、[FBX 回读](../../../ArtSource/Robot/fbx_validation.json) 64 项、[空间站源/FBX](../../../ArtSource/StationKit/validation/blender_fbx_validation.json) 237 项，合计 **364 项通过**。空间站含箱子四向接触、门/插槽扫掠和 100 帧门运动检查。
- [UnityAssets.json](UnityAssets.json)：**14 个资产、96 项通过**，记录实际 FBX/Prefab 哈希、尺寸、网格、材质/纹理引用、三段动画、轮转、推板接触和资源依赖。

## 游戏回归与实机画面

- [首轮 PlayMode](PlayModeInitial.xml)：38 项中 35 项通过。发现两个新模型导入设置缺项，以及旧测试沿用已经失效的第 35 步通电检查点。
- 修复 preserveHierarchy/Read-Write 导入设置；转向板测试明确读取箭头网格；通电测试改用当前参考解法完成状态，不改规则或关卡数据。
- [定向复测](PlayModeRetest.xml)：16/16，通过机关、转向、插槽与新增表现生命周期测试。两次运行按用例取最新结果，**38 项相关用例均通过**；没有把 Test Runner 的全程序集计数 83 当作实际执行数量。
- [第三人称](L12ThirdPerson.png) · [俯视](L12TopDown.png) · [供电后](L12Powered.png)：实际 Editor 游戏相机 1920×1080 渲染，不含 Overlay HUD。门顶旧平面栅格已用立体散热片替换，避免交叠细纹。
- [机器人正面](../../../ArtSource/Robot/previews/refined_front.png) · [背面](../../../ArtSource/Robot/previews/refined_back.png)：Blender 渲染，不能代替 Unity 实机证据。
- 轻微 Bloom、Neutral 色调映射和 SMAA 仅绑定游戏相机；补光与 Volume 随关卡销毁。切关检查确认不累积，不修改全局天空盒或环境光设置。

## 构建与保护

- Windows x64 非开发构建成功，0 错误、0 警告，约 91.41 MB；产物为 Builds/V0.11.0/Windows/Robot Sokoban.exe。
- 独立 Player 已通过主菜单、选关并进入 L12；1280×720 窗口配置已核对。[原生窗口俯视截图](PlayerTopWindow.png)包含 Windows 标题栏，受系统 DPI 缩放，不能把 PNG 像素尺寸当作游戏渲染分辨率。
- [原生窗口第三人称](PlayerThirdWindow.png)已目视检查。原生短按 V 注入未触发游戏切换，使用 HUD 按钮完成切换；自动相机测试中的 V 输入通过。测试程序已退出，原进度和 .bak 文件恢复并核对哈希。
- [构建报告](Build.json)确认新模型、六张纹理和 VolumeProfile 实际进入 Player。独立程序日志与最终 Console 未发现错误。
- 69 个受保护文件哈希保持，包括原有关卡/解法、场景、普通地板/墙体 FBX 与既有模型 .meta；test111 及其 .meta 保留未跟踪状态。
- 旧初次生产的 Unity 展示报告与旧预览保留历史含义；当前清单改用本轮源/FBX/Unity 和回归证据，不刷新旧报告来冒充复验。
