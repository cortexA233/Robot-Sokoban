# 简约 UGUI 与页面过渡

日期：2026-09-16。设计依据：`ArtSource/UIConcepts/minimal_ui_v2.png`，采用浅色底、深灰文字、细线、橙色主操作。所有页面由真实 UGUI 控件组成，没有使用概念图充当游戏背景。

## 玩家流程

- Bootstrap → 主菜单 → 开始游戏 / 选关 → 关卡 → 结算 → 下一关 / 重玩 / 选关 / 主菜单。
- 选关列表由 CampaignCatalog 生成，全部关卡可选；选中条目后点击“进入关卡”。返回选关前的游戏局面和暂停状态会保留。
- 进入关卡默认俯视，按 V 切换第三人称，再按 V 返回俯视；HUD 显示当前视角及 V 的切换目标。第三人称保持鼠标锁定并支持键盘快捷键；俯视释放鼠标，可直接点击 HUD 的撤销、重开和视角按钮。Esc 打开暂停菜单。
- 设置可调整主音量、鼠标灵敏度、反转垂直镜头，立即生效；完成设置、导航与退出时保存 PlayerPrefs。基础游戏音效已接入，主音量控制 AudioListener；音效来源与事件约定见 [音效说明](../Audio/Sfx.md)。
- 编辑器试玩直接进入原测试关，保留“保存参考解法”与撤销，不进入主菜单或正式关卡导航。

## KToolkit 集成

`LevelRunner` 初始化现有 KFrameworkManager，然后创建 StationUIController。每个页面继承 KUIPage，以 KUI_Info 注册，使用 KUIManager.CreateUI 加载 Prefab。KToolkit 负责 Canvas、Input System EventSystem 和页面生命周期；本次未修改 KToolkit 源码。

| 页面 | Prefab（相对 Assets/Resources/UI_prefabs/screens） | 作用 |
| --- | --- | --- |
| MainMenuPage | MainMenu.prefab | 开始、选关、设置、退出 |
| HudPage | Hud.prefab | 关卡、真实供电/移动/推动统计、提示、快捷键 |
| LevelSelectPage | LevelSelect.prefab | 可滚动的目录列表与进入按钮 |
| PausePage | Pause.prefab | 恢复、重开、选关、设置、主菜单 |
| CompletionPage | Completion.prefab | 真实结算统计；末关主按钮变为返回选关 |
| SettingsPage | Settings.prefab | 音量、灵敏度、反转镜头 |
| GeneralFadePage | GeneralFade.prefab | 覆盖画面的转场幕布 |

页面布局保存在可编辑的 Prefab 中，运行时只绑定控件与刷新状态。菜单 `Tools > Sokoban > Build Minimal UGUI Prefabs` 可从 `Assets/Scripts/Editor/UI/StationUIAssetBuilder.cs` 重建它们；此操作会覆盖这七个 Prefab 的手工布局修改。

运行时 UI 代码在 `Assets/Scripts/UI`，通过 asmref 归入 Sokoban.Runtime。UGUI 使用 1920×1080 参考分辨率，CanvasScaler 同时匹配宽高；界面中没有屏幕截图或运行时生成的装饰贴图。

## 过渡与输入

参考文件为本机 `F:/gamer_bench_tasks/unity/Element_Ballance/Assets/Scripts/UI/GeneralFadePage.cs` 及 GeneralFadeLevelTransitionPresenter。原实现是左右/上下两片幕布开合，本项目保留其视觉方式与每段 0.6 秒时长，使用归一化锚点替代固定像素位移。

1. 锁住游戏指令、暂停当前表现、禁用底层按钮并清空 UI 焦点。
2. 幕布合拢，完全覆盖后才加载关卡或清理旧关卡返回主菜单。
3. 保持覆盖约 0.08 秒，让新相机与延迟销毁生效，再展开幕布。
4. 展开完成后恢复输入；旧的长按必须释放，才接受新的移动。

DOTween 过渡采用非缩放时间，暂停或 timeScale 为 0 时仍能完成；重复导航被拒绝。销毁控制器时使用 Kill(false)，不补执行旧回调。幕布页始终置于当前 Canvas 最上层。

v0.4.0 的作者试玩往返检查补充了退出时的清理保护：Unity 可能先销毁 Canvas 下的 GameObject，再销毁 LevelRunner；StationUIController 会跳过已销毁对象的 SetActive，同时继续注销页面包装对象。重复清理保持无害。

`LevelRunner.LoadLevel/SelectLevel/NextLevel` 保留同步作者/测试 API；玩家按钮统一通过 StationUIController 的过渡入口，不绕过遮盖阶段。规则仍在原 GameSession 中，页面不复制推箱、供电或通关判定。

## 字体

内置 Noto Sans SC Regular，存放于 `Assets/Art/UI/Fonts`，随 Prefab 引用进入构建。来自 [Noto CJK 官方仓库](https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/SC)，原始文件名 NotoSansSC-Regular.otf；[OFL 1.1 授权](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE)同目录保存为 LICENSE.txt。避免依赖目标 Windows 的系统中文字体。

## GM 调试工作台增量

`GmPage` 使用 KToolkit KUIPage 与可编辑的 `screens/GM/Gm.prefab`，按 F1 打开，包含分类搜索、坐标/点选移位、交换、朝向、机关状态和诊断导出。常用撤销、重开、视角按钮固定在滚动内容之外；只读监视条不占用游戏输入。

GM 输入占用、现场编辑占用、游戏暂停和动作动画暂停分别管理。StationUIController 在调试工作台占用输入时禁用底层交互；LevelRunner 先消费 F1/Esc 和点选，再处理普通游戏输入。运行时 GM 类型只编译进 Editor/Development Build，Prefab 本身只包含 Unity 内置组件，因此非开发构建不会出现缺失脚本。

叠加线使用 `Assets/Art/UI/Materials/GmOverlay.mat`，由 Prefab 内禁用的 OverlayStyle LineRenderer 显式引用，避免仅用 Shader.Find 导致 Player 构建剔除 URP Unlit Shader。运行时克隆材质并随叠加层销毁。

叠加标签在 CinemachineBrain 更新镜头之后投影；网格坐标与实体 ID 使用不同位置，避免切换镜头时错位或同时显示时重叠。

生成入口为 `Sokoban_Tools > Build GM UGUI Prefab`，只覆盖 GM Prefab。其验证记录见 [第五轮 GM/现场编辑报告](../History/05-GmLiveEditing/ImplementationReport.md)；原七页生成器与 KToolkit 源码未改动。

## 原有页面验证

运行时集成用例位于 `Assets/Scripts/Tests/PlayMode/StationUITests.cs`。点击测试在页面渲染后通过 EventSystem 射线确认最上层控件，再派发 PointerClick；覆盖菜单进入、选关、结算下一关、暂停返回、设置保存、撤销重开、零时间缩放与过渡销毁。另有 Input System 键盘用例验证 Esc 和长按隔离。

首轮发现测试在页面激活同一帧点击，UGUI 尚未登记图形深度；通过跨帧射线对照定位并修正测试时序。实机检查同时修正了 Button ColorTint 与 Image 底色重复叠乘导致的橙色偏暗，以及滑条填充和手柄在动态锚点下超出轨道的问题，新增两端与中点的几何边界回归。最终测试、构建与截图记录见 [第三轮报告](../History/03-UI/ImplementationReport.md) 和 [验证数据](../History/03-UI/Validation/UIImplementationResults.json)。
