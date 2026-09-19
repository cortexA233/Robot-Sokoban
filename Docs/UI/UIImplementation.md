# 简约 UGUI 与页面过渡

更新：2026-09-18（v0.8.0 收尾）。设计依据：`ArtSource/UIConcepts/minimal_ui_v2.png`，采用浅色底、深灰文字、细线、橙色主操作。所有页面由真实 UGUI 控件组成，没有使用概念图充当游戏背景。

游戏正式名称为 **Robot Sokoban**（v0.8.2），主菜单标题与 Unity Product Name 一致；历史概念图和验收截图保留生成当时的文案。

## 玩家流程

- Bootstrap → 主菜单 → 开始 / 继续 / 选关 → 关卡 → 成绩结算 → 下一关 / 重开 / 选关 / 主菜单。主菜单显示完成数量，“继续游戏”从最近关卡的起点进入；v0.7.1 移除“从第一关开始”快捷入口，重玩首关通过选关进入，已有成绩保留。
- 选关列表由 CampaignCatalog 生成，全部关卡可选，以顺序号显示关卡，当前有效成绩仅显示“已完成 · N 步”，未完成/无记录/旧版本成绩留空；页头只显示“X / Y 已完成”。推动数仍保存在存档、HUD 与结算中。键盘焦点进入屏外条目时自动滚动到可见区域；选中条目后点击“进入关卡”。返回选关前的游戏局面和暂停状态会保留。
- 进入关卡默认俯视，按 V 切换第三人称，再按 V 返回俯视；HUD 的 V 按钮显示切换目标。第三人称保持鼠标锁定并支持键盘快捷键；俯视释放鼠标，可直接点击 HUD 的撤销、重开和视角按钮。Esc 打开暂停菜单。
- 设置可调整主音量、鼠标灵敏度、反转垂直镜头，立即生效；完成设置、导航与退出时保存 PlayerPrefs。基础游戏音效已接入，主音量控制 AudioListener；音效来源与事件约定见 [音效说明](../Audio/Sfx.md)。
- 编辑器试玩直接进入原测试关，保留“保存参考解法”与撤销，不进入主菜单或正式关卡导航，不打开正式进度文件。非正式 LoadLevel、GM 修改后及现场试玩的完成不会写入正式成绩。
- 三项逐关文案及其数据/输入框已取消。HUD 显示关卡序号、目标供电、移动/推动数和必要操作；受阻提示约 4 秒后隐藏，平时无提示条占位。主菜单、暂停和 HUD 的“操作说明”打开通用帮助，关闭后恢复原暂停状态。

## KToolkit 集成

`LevelRunner` 初始化现有 KFrameworkManager，然后创建 StationUIController。每个页面继承 KUIPage，以 KUI_Info 注册，使用 KUIManager.CreateUI 加载 Prefab。KToolkit 负责 Canvas、Input System EventSystem 和页面生命周期；本次未修改 KToolkit 源码。

| 页面 | Prefab（相对 Assets/Resources/UI_prefabs/screens） | 作用 |
| --- | --- | --- |
| MainMenuPage | MainMenu.prefab | 开始、选关、设置、退出 |
| HudPage | Hud.prefab | 关卡序号、目标供电/移动/推动统计、临时提示、快捷键 |
| LevelSelectPage | LevelSelect.prefab | 可滚动的目录列表与进入按钮 |
| PausePage | Pause.prefab | 恢复、重开、选关、设置、主菜单 |
| CompletionPage | Completion.prefab | 真实结算统计；末关主按钮变为返回选关 |
| SettingsPage | Settings.prefab | 音量、灵敏度、反转镜头 |
| HelpPage | Help.prefab | 按需查看的通用操作与规则说明 |
| GeneralFadePage | GeneralFade.prefab | 覆盖画面的转场幕布 |

页面布局直接维护现有 Prefab，运行时只绑定与刷新控件；v0.8.0 删除玩家/GM 生成器。编辑时保留 GUID、资源键与绑定名称，保存后重开并检查运行加载。

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

## 按钮状态与导航

v0.6.0 的实机采样和回归发现，次要按钮从 `Color.clear`（透明黑色）渐变到不透明灰色时，RGB 与 alpha 同时插值，合成结果会先暗后亮。透明常态现在保留悬停 RGB，仅将 alpha 设为 0；仍采用 0.12 秒渐变，选中、悬停、按下、禁用分别可识别。指针进入不夺取键盘焦点；点击选择按钮，键盘移动继续使用 EventSystem 焦点。

页面仅在打开或转场解锁时设置初始焦点；先刷新可见性和可用状态，再选择首个可交互控件。选关使用明确的行间/进入/返回导航并自动显露焦点行。普通页面 Refresh 不重置当前焦点。Hover 回归通过实际 UGUI 指针事件及逐帧 CanvasRenderer 颜色检查暗闪；完整输入模块的实机采样单独保留，避免把只派发点击的旧用例当作悬停验收。

## 玩家进度

`PlayerProgress` 将最近关卡、完成记录及同一次通关的最佳步数/推动数保存到 `Application.persistentDataPath/player-progress.json`。先比较步数，同步数再比较推动数。稳定 ID 对应关卡身份，规范内容哈希区分版本；目录重排不影响记录，内容改变后旧成绩不用于新版，目录中已移除的关卡不作为继续入口。所有关卡仍可选择。

写入使用原子替换及上一份有效 `.bak`；不可读文件在覆盖前保留为 `.unreadable-*`。损坏时回退备份或空记录，写入失败保留内存进度，主菜单显示“重试保存进度”；导航、退出和应用暂停时也尝试保存。主音量、灵敏度和镜头反转的 PlayerPrefs 键名继续沿用 `Sokoban.Settings.*`。

v0.8.2 更改 Product Name 后，Windows 进度目录为 `%USERPROFILE%/AppData/LocalLow/DefaultCompany/Robot Sokoban/`，PlayerPrefs 也使用新产品名称对应的存储位置。旧 `Sokoban_3D_Test` 目录和设置仍保留，不会自动迁移；需要继续旧进度时，可在游戏关闭且新目录尚无进度文件时，将原目录的 `player-progress.json` 及已有 `.bak` 复制到新目录。设置可在新版本的设置页重新调整。

## 字体

内置 Noto Sans SC Regular，存放于 `Assets/Art/UI/Fonts`，随 Prefab 引用进入构建。来自 [Noto CJK 官方仓库](https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/SC)，原始文件名 NotoSansSC-Regular.otf；[OFL 1.1 授权](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE)同目录保存为 LICENSE.txt。避免依赖目标 Windows 的系统中文字体。

## GM 调试工作台增量

`GmPage` 使用 KToolkit KUIPage 与 `screens/GM/Gm.prefab`，F1 打开，仅含关卡/局面两页。点棋盘源格选择玩家/箱子，独立输入目标 X/Z 后移动；空格/静态机关清空选择。保留搜索、字号、宽度、拖动及固定的撤销/重开/视角按钮。已删除监视、诊断、机关状态页、交换/朝向、增删箱和动画调试。

GM 与现场编辑分别占用输入，正式暂停仍独立管理。StationUIController 禁用底层交互；LevelRunner 先消费 F1/Esc 和点选。选择跟随对象，重开/切关/现场应用/结束重置；GM 改动的录制/正式成绩限制保持。GM 类型只编译进 Editor/Development Build，Prefab 仅含内置组件，非开发构建无缺失脚本。

Editor 中“现场编辑（打开关卡编辑器）”捕获现场、打开/聚焦窗口并展开现场操作组；提示为“编辑关卡后请在关卡编辑器内保存变更或保存为新副本”。已有草稿保留保护流程；开发 Player 隐藏该入口。

叠加线使用 `Assets/Art/UI/Materials/GmOverlay.mat`，由 Prefab 内禁用的 OverlayStyle LineRenderer 显式引用，避免仅用 Shader.Find 导致 Player 构建剔除 URP Unlit Shader。运行时克隆材质并随叠加层销毁。

叠加标签在 CinemachineBrain 更新镜头之后投影；网格坐标与实体 ID 使用不同位置，避免切换镜头时错位或同时显示时重叠。

运行时按 F1 加载现有 Prefab，不依赖生成器。原生成阶段的验证仍见 [第五轮报告](../History/05-GmLiveEditing/ImplementationReport.md)；当前操作见 [运行时 GM](../Editor/LevelEditorGuide.md#运行时-gm)。

## 原有页面验证

运行时集成用例位于 `Assets/Scripts/Tests/PlayMode/StationUITests.cs`。点击测试在页面渲染后通过 EventSystem 射线确认最上层控件，再派发 PointerClick；覆盖菜单进入、选关、结算下一关、暂停返回、设置保存、撤销重开、零时间缩放与过渡销毁。另有 Input System 键盘用例验证 Esc 和长按隔离。

首轮发现测试在页面激活同一帧点击，UGUI 尚未登记图形深度；通过跨帧射线对照定位并修正测试时序。实机检查同时修正了 Button ColorTint 与 Image 底色重复叠乘导致的橙色偏暗，以及滑条填充和手柄在动态锚点下超出轨道的问题，新增两端与中点的几何边界回归。最终测试、构建与截图记录见 [第三轮报告](../History/03-UI/ImplementationReport.md) 和 [验证数据](../History/03-UI/Validation/UIImplementationResults.json)。
