using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban.Editor
{
    // Produces editable UGUI prefabs. Runtime pages only bind controls; they do not rebuild their layout.
    public static class StationUIAssetBuilder
    {
        private const string Output = "Assets/Resources/UI_prefabs/screens";
        private static readonly Color Paper = new Color32(247, 247, 244, 255);
        private static readonly Color Ink = new Color32(40, 44, 48, 255);
        private static readonly Color Muted = new Color32(115, 121, 126, 255);
        private static readonly Color Line = new Color32(224, 226, 223, 255);
        private static readonly Color Orange = new Color32(218, 120, 72, 255);
        private static Font font;

        [MenuItem("Tools/Sokoban/Build Minimal UGUI Prefabs")]
        public static void Build() => BuildTo(Output);

        public static void BuildTo(string output)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before rebuilding UI prefabs.");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");
            if (!font) throw new InvalidOperationException("Noto Sans SC font has not been imported.");
            Directory.CreateDirectory(output);
            Save("MainMenu", MainMenu, output);
            Save("Hud", Hud, output);
            Save("LevelSelect", LevelSelect, output);
            Save("Pause", Pause, output);
            Save("Completion", Completion, output);
            Save("Settings", Settings, output);
            Save("Help", Help, output);
            Save("GeneralFade", Fade, output);
            // SaveAsPrefabAsset already persists each target; do not save unrelated dirty assets.
            AssetDatabase.Refresh();
            Debug.Log("Created eight editable KToolkit UGUI screen prefabs.");
        }
        private static void Save(string name, Action<RectTransform> build, string output)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            root.layer = 5;
            var rect = (RectTransform)root.transform;
            Stretch(rect);
            try { build(rect); PrefabUtility.SaveAsPrefabAsset(root, output + "/" + name + ".prefab"); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        private static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset;
        }
        private static RectTransform Center(Transform parent, string name, float width, float height)
        {
            var rect = Rect(parent, name, 0, 0, width, height);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = Vector2.zero; return rect;
        }
        private static Image Fill(RectTransform rect, Color color, bool raycast = true)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = raycast; return image;
        }
        private static Text Label(Transform parent, string name, string text, float x, float y, float w, float h,
            int size = 28, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var rect = Rect(parent, name, x, y, w, h);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.color = color ?? Ink; label.alignment = align;
            label.raycastTarget = false; label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        private static Button Button(Transform parent, string name, string text, float x, float y, float w, float h, bool primary = false)
        {
            var rect = Rect(parent, name, x, y, w, h);
            // ColorTint multiplies the Image's vertex color; keep it white and tint only once.
            var image = Fill(rect, Color.white);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors;
            // Retain the hover RGB at zero alpha. Fading from transparent black
            // interpolates RGB and alpha together, producing a dark flash over Paper.
            colors.normalColor = primary ? Orange : new Color32(230, 232, 229, 0);
            colors.highlightedColor = primary ? new Color32(231, 139, 91, 255) : new Color32(230, 232, 229, 255);
            colors.selectedColor = primary ? new Color32(198, 102, 56, 255) : new Color32(236, 222, 211, 255);
            colors.pressedColor = primary ? new Color32(181, 90, 48, 255) : new Color32(215, 218, 213, 255);
            colors.disabledColor = new Color(.8f, .8f, .8f, .6f); colors.fadeDuration = .12f;
            button.colors = colors;
            Label(rect, "Label", text, 8, 0, w - 16, h, 28, primary, TextAnchor.MiddleCenter, primary ? Color.white : Ink);
            return button;
        }
        private static void Rule(Transform parent, string name, float x, float y, float width)
            => Fill(Rect(parent, name, x, y, width, 2), Line, false);
        private static RectTransform Dialog(RectTransform root, float w, float h)
        {
            Fill(root, new Color(.08f, .1f, .12f, .45f));
            var content = Center(root, "Content", w, h);
            Fill(content, Paper);
            var outline = content.gameObject.AddComponent<Outline>();
            outline.effectColor = Line; outline.effectDistance = new Vector2(1, -1);
            return content;
        }
        private static void MainMenu(RectTransform root)
        {
            Fill(root, Paper);
            var content = Center(root, "Content", 760, 960);
            Label(content, "Title", "空间站重启", 0, 26, 760, 100, 64, true, TextAnchor.MiddleCenter);
            Label(content, "Summary", "0 / 9 已完成", 0, 138, 760, 40, 23, false, TextAnchor.MiddleCenter, Muted);
            Label(content, "ContinueHint", "", 0, 182, 760, 38, 22, false, TextAnchor.MiddleCenter, Muted);
            var actions = Rect(content, "Actions", 140, 246, 480, 580);
            var layout = actions.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            foreach (string name in new[] { "Start", "Levels", "Settings", "Help", "Retry", "Quit" })
            {
                string text = name == "Start" ? "开始游戏" : name == "Levels" ? "选择关卡" : name == "Settings" ? "设置" :
                    name == "Help" ? "操作说明" : name == "Retry" ? "重试保存进度" : "退出";
                var button = Button(actions, name, text, 0, 0, 480, 64, name == "Start");
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = name == "Start" ? 76 : 64;
            }
            Label(content, "Message", "", 0, 846, 760, 80, 21, false, TextAnchor.MiddleCenter, Muted);
        }
        private static void Hud(RectTransform root)
        {
            var header = Rect(root, "Header", 32, 28, 0, 92);
            header.anchorMax = new Vector2(1, 1); header.offsetMax = new Vector2(-32, -28);
            header.offsetMin = new Vector2(32, -120); Fill(header, Paper);
            Label(header, "Stage", "关卡 01", 26, 14, 380, 64, 30, true);
            RightLabel(header, "Power", "目标供电 0/2", 620, 230);
            RightLabel(header, "Moves", "移动 0", 432, 150);
            RightLabel(header, "Pushes", "推动 0", 254, 150);
            var pause = Button(header, "Pause", "Esc  暂停", 0, 12, 170, 66);
            var pauseRect = (RectTransform)pause.transform;
            pauseRect.anchorMin = pauseRect.anchorMax = new Vector2(1, 1);
            pauseRect.anchoredPosition = new Vector2(-196, -12);
            var notice = Rect(root, "Notice", 32, 132, 1080, 62); Fill(notice, Paper, false);
            Label(notice, "Text", "", 20, 5, 1040, 52, 22);
            notice.gameObject.SetActive(false);
            var shortcuts = Rect(root, "Shortcuts", 0, 0, 800, 68);
            shortcuts.anchorMin = shortcuts.anchorMax = new Vector2(.5f, 0);
            shortcuts.pivot = new Vector2(.5f, 0); shortcuts.anchoredPosition = new Vector2(0, 28);
            Fill(shortcuts, Paper);
            Button(shortcuts, "Undo", "Z  撤销", 8, 4, 172, 60);
            Button(shortcuts, "Restart", "R  重开", 190, 4, 172, 60);
            Button(shortcuts, "View", "V  第三人称", 372, 4, 222, 60);
            Button(shortcuts, "Help", "操作说明", 604, 4, 188, 60);
            foreach (var control in root.GetComponentsInChildren<Selectable>())
                control.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        private static void RightLabel(Transform parent, string name, string text, float distance, float width)
        {
            var label = Label(parent, name, text, 0, 20, width, 68, 28);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-distance - width, -20);
        }
        private static void LevelSelect(RectTransform root)
        {
            Fill(root, Paper);
            var content = Center(root, "Content", 1000, 850);
            Label(content, "Title", "选择关卡", 0, 12, 1000, 90, 50, true);
            Label(content, "Summary", "", 0, 102, 1000, 34, 22, false, TextAnchor.MiddleLeft, Muted);
            Rule(content, "Rule", 0, 138, 1000);
            var list = Rect(content, "List", 0, 168, 1000, 448);
            var scroll = list.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect(list, "Viewport", 0, 0, 1000, 448);
            Fill(viewport, Paper); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var rows = Rect(viewport, "Rows", 0, 0, 1000, 0);
            rows.anchorMax = new Vector2(1, 1); rows.sizeDelta = Vector2.zero;
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            layout.spacing = 12;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var row = Button(rows, "Template", "关卡 01", 0, 0, 1000, 88);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 88;
            row.gameObject.AddComponent<Sokoban.UI.ScrollSelection>();
            Label(row.transform, "Status", "待完成", 380, 0, 588, 88, 23, false, TextAnchor.MiddleRight, Muted);
            var text = row.transform.Find("Label").GetComponent<Text>(); text.alignment = TextAnchor.MiddleLeft;
            ((RectTransform)text.transform).offsetMin = new Vector2(32, -88);
            ((RectTransform)text.transform).offsetMax = new Vector2(340, 0);
            var marker = Rect(row.transform, "Marker", 0, 8, 6, 72); Fill(marker, Orange, false);
            Rule(row.transform, "Rule", 24, 86, 952);
            row.gameObject.SetActive(false);
            scroll.viewport = viewport; scroll.content = rows; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
            Button(content, "Enter", "进入关卡", 600, 648, 400, 78, true);
            Button(content, "Back", "返回主菜单", 0, 648, 300, 78);
        }
        private static void Pause(RectTransform root)
        {
            var content = Dialog(root, 680, 850);
            Label(content, "Title", "暂停", 40, 36, 600, 82, 40, true, TextAnchor.MiddleCenter);
            Button(content, "Resume", "继续游戏", 110, 162, 460, 72, true);
            Button(content, "Restart", "重开本关", 110, 250, 460, 64);
            Button(content, "Levels", "选择关卡", 110, 330, 460, 64);
            Button(content, "Settings", "设置", 110, 410, 460, 64);
            Button(content, "Help", "操作说明", 110, 490, 460, 64);
            Button(content, "Menu", "返回主菜单", 110, 570, 460, 64);
            Label(content, "PlaytestHint", "退出 Play Mode 返回编辑器", 50, 686, 580, 64, 21, false, TextAnchor.MiddleCenter, Muted);
        }
        private static void Completion(RectTransform root)
        {
            var content = Dialog(root, 900, 700);
            Label(content, "Heading", "关卡完成", 50, 38, 800, 86, 48, true, TextAnchor.MiddleCenter);
            Rule(content, "Rule", 72, 144, 756);
            Label(content, "Stats", "移动 8 · 推动 3", 50, 178, 800, 54, 30, true, TextAnchor.MiddleCenter);
            Label(content, "Best", "", 50, 246, 800, 42, 24, false, TextAnchor.MiddleCenter, Muted);
            Label(content, "Notice", "", 50, 306, 800, 64, 21, false, TextAnchor.MiddleCenter, Muted);
            Button(content, "Next", "下一关", 64, 412, 248, 74, true);
            Button(content, "Replay", "重开本关", 340, 412, 200, 74);
            Button(content, "Levels", "选择关卡", 570, 412, 266, 74);
            Button(content, "Menu", "返回主菜单", 470, 528, 320, 68);
            Button(content, "Undo", "Z  撤销最后一步", 88, 528, 330, 68);
            Button(content, "Save", "保存参考解法", 64, 412, 248, 74, true);
        }
        private static void Settings(RectTransform root)
        {
            Fill(root, Paper);
            var content = Center(root, "Content", 900, 750);
            Label(content, "Title", "设置", 0, 30, 900, 90, 54, true);
            Rule(content, "Rule", 0, 144, 900);
            Label(content, "VolumeLabel", "主音量", 0, 198, 400, 54, 28);
            Label(content, "VolumeValue", "100%", 700, 198, 200, 54, 26, false, TextAnchor.MiddleRight, Muted);
            Slider(content, "Volume", 0, 282, 900, 0, 1);
            Label(content, "SensitivityLabel", "鼠标灵敏度", 0, 365, 460, 54, 28);
            Label(content, "SensitivityValue", "1.00 ×", 700, 365, 200, 54, 26, false, TextAnchor.MiddleRight, Muted);
            Slider(content, "Sensitivity", 0, 449, 900, .25f, 2.5f);
            var toggleRect = Rect(content, "Invert", 0, 534, 900, 60);
            var toggle = toggleRect.gameObject.AddComponent<Toggle>();
            var box = Rect(toggleRect, "Box", 0, 10, 40, 40); var back = Fill(box, Line);
            var check = Rect(box, "Check", 7, 7, 26, 26); var checkImage = Fill(check, Orange);
            toggle.targetGraphic = back; toggle.graphic = checkImage;
            Label(toggleRect, "Label", "反转垂直镜头", 66, 0, 700, 60, 28);
            Button(content, "Back", "完成", 540, 654, 360, 76, true);
        }
        private static void Help(RectTransform root)
        {
            Fill(root, Paper);
            var content = Center(root, "Content", 1020, 880);
            Label(content, "Title", "操作说明", 0, 20, 1020, 90, 50, true);
            Rule(content, "Rule", 0, 126, 1020);
            Label(content, "Controls", "方向键 / WASD    移动与推箱\nZ    撤销        R    重开        Esc    暂停 / 返回\nV    俯视 / 第三人称；第三人称用鼠标环绕、滚轮缩放", 0, 158, 1020, 180, 26);
            Label(content, "Rules", "把能源箱送到所有带目标环的插槽。\n普通箱不供电，可用来挡停箱子。\n供电插槽连接受电门；通电与开门状态分别显示。\n低摩擦轨道让箱子滑行，箭头板会改变运输方向。", 0, 362, 1020, 248, 27);
            Label(content, "Tip", "可以随时撤销和重开。所有关卡均可选择。\n“继续游戏”从最近关卡的起点开始。", 0, 636, 1020, 94, 23, false, TextAnchor.MiddleLeft, Muted);
            Button(content, "Back", "返回", 660, 772, 360, 76, true);
        }

        private static void Slider(Transform parent, string name, float x, float y, float w, float min, float max)
        {
            var root = Rect(parent, name, x, y, w, 44);
            var slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = min; slider.maxValue = max;
            Fill(Rect(root, "Track", 0, 18, w, 6), Line);
            var fillArea = Rect(root, "FillArea", 12, 18, w - 24, 6);
            var fill = Rect(fillArea, "Fill", 0, 0, 0, 0); Stretch(fill); Fill(fill, Orange);
            var handleArea = Rect(root, "HandleArea", 12, 0, w - 24, 44);
            var handle = Rect(handleArea, "Handle", 0, 0, 24, 44);
            handle.anchorMin = Vector2.zero; handle.anchorMax = new Vector2(0, 1);
            handle.pivot = Vector2.one * .5f; handle.sizeDelta = new Vector2(24, 0);
            handle.anchoredPosition = Vector2.zero;
            var handleImage = Fill(handle, Orange);
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        }
        private static void Fade(RectTransform root)
        {
            Fill(root, Color.clear);
            var first = Rect(root, "First", 0, 0, 1, 1); Fill(first, Ink);
            var second = Rect(root, "Second", 0, 0, 1, 1); Fill(second, Ink);
            Stretch(first); Stretch(second);
        }
    }
}
