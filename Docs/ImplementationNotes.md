# 实现说明

## 脚本职责
- `HangoutHudFactory`：运行时自动创建基础 UGUI，并提供编辑器菜单复用的创建入口。
- `HangoutHudController`：管理详情面板展开、按钮文本、演示进度和详情文字。
- `BattlePreviewController`：管理左 4 人和右 4 人的示意攻击循环、血条变化、闪烁和命中文字。
- `WindowsTransparentWindow`：仅在 Windows Standalone Player 中启用 Win32 透明、置顶和鼠标穿透逻辑。
- `HangoutHudSceneBuilder`：提供 `Tools/Hangout Game` 菜单，用于应用窗口设置和生成场景 UI。

## 生成流程
- 进入 Play Mode 或 Windows 构建运行时，如果场景里没有 `HangoutHudController`，会自动创建基础 UI。
- 也可以在 Unity 菜单执行 `TBD/Hangout Game/Apply All Setup` 或 `Tools/Hangout Game/Apply All Setup`，把 UI 生成到当前打开场景中方便检查。
- 菜单只删除并重建固定命名的 `HangoutHudRoot`，不会处理其他场景物体。

## Windows 窗口逻辑
- PlayerSettings 默认窗口尺寸为 `320 x 320`，窗口化、不可调大小、后台继续运行。
- Windows Player 初始化时会设置透明窗口、置顶和无边框窗口样式。
- 透明穿透只在 `UNITY_STANDALONE_WIN && !UNITY_EDITOR` 下编译启用，Editor 中保留普通窗口行为方便调 UI。
- 所有注册的可见 UI 区域都可以拖动窗口，但 `Button`、`Selectable` 和实现点击事件的控件不会触发拖拽。
- 如果 Win32 调用失败，会在控制台打印明确错误码。

## 后续扩展
- 真实挂机数据接入时，优先通过 `HangoutHudController.SetProgress` 更新进度。
- 如果要增加可点击区域，需要把对应 `RectTransform` 传给 `WindowsTransparentWindow.Bind`。
- 如果后续要支持 macOS 或 Linux，应新增平台实现，不要把平台差异堆进 UI 控制脚本。
