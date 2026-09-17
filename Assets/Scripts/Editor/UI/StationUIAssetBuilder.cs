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
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before rebuilding UI prefabs.");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");
            if (!font) throw new InvalidOperationException("Noto Sans SC font has not been imported.");
            Directory.CreateDirectory(Output);
            Save("MainMenu", MainMenu);
            Save("Hud", Hud);
            Save("LevelSelect", LevelSelect);
            Save("Pause", Pause);
            Save("Completion", Completion);
            Save("Settings", Settings);
            Save("GeneralFade", Fade);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Created seven editable KToolkit UGUI screen prefabs.");
        }
        private static void Save(string name, Action<RectTransform> build)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            root.layer = 5;
            var rect = (RectTransform)root.transform;
            Stretch(rect);
            try { build(rect); PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + name + ".prefab"); }
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
            colors.normalColor = primary ? Orange : Color.clear;
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
            var content = Center(root, "Content", 760, 780);
            Label(content, "Title", "空间站重启", 0, 44, 760, 110, 68, true, TextAnchor.MiddleCenter);
            Button(content, "Start", "开始游戏", 140, 236, 480, 82, true);
            Button(content, "Levels", "选择关卡", 140, 350, 480, 72);
            Button(content, "Settings", "设置", 140, 444, 480, 72);
            Button(content, "Quit", "退出", 140, 538, 480, 72);
            Label(content, "Message", "", 0, 664, 760, 108, 22, false, TextAnchor.MiddleCenter, Muted);
        }
        private static void Hud(RectTransform root)
        {
            var header = Rect(root, "Header", 32, 28, 0, 110);
            header.anchorMax = new Vector2(1, 1); header.offsetMax = new Vector2(-32, -28);
            header.offsetMin = new Vector2(32, -138);
            Fill(header, Paper);
            Label(header, "Title", "关卡标题", 26, 18, 580, 64, 32, true);
            RightLabel(header, "Power", "供电 1/2", 630, 150);
            RightLabel(header, "Moves", "移动 24", 450, 150);
            RightLabel(header, "Pushes", "推动 8", 270, 150);
            var pause = Button(header, "Pause", "Esc  暂停", 0, 19, 170, 72);
            var pauseRect = (RectTransform)pause.transform;
            pauseRect.anchorMin = pauseRect.anchorMax = new Vector2(1, 1);
            pauseRect.anchoredPosition = new Vector2(-196, -19);
            var message = Label(root, "Message", "", 52, 154, 1100, 44, 22, false, TextAnchor.MiddleLeft, Ink);
            // A small opaque backing maintains readability over the existing gray blockout scene.
            var messageBack = Rect(root, "MessageBacking", 32, 146, 1140, 62);
            Fill(messageBack, Paper, false); messageBack.SetSiblingIndex(message.transform.GetSiblingIndex());
            var shortcuts = Rect(root, "Shortcuts", 0, 0, 620, 68);
            shortcuts.anchorMin = shortcuts.anchorMax = new Vector2(.5f, 0);
            shortcuts.pivot = new Vector2(.5f, 0); shortcuts.anchoredPosition = new Vector2(0, 30);
            Fill(shortcuts, Paper);
            Button(shortcuts, "Undo", "Z  撤销", 10, 4, 190, 60);
            Button(shortcuts, "Restart", "R  重开", 210, 4, 190, 60);
            Button(shortcuts, "View", "V  视角", 410, 4, 200, 60);
            var view = Label(root, "ViewLabel", "", 0, 0, 640, 42, 21, false, TextAnchor.MiddleLeft, Ink);
            var viewRect = (RectTransform)view.transform;
            viewRect.anchorMin = viewRect.anchorMax = new Vector2(0, 0);
            viewRect.pivot = Vector2.zero; viewRect.anchoredPosition = new Vector2(52, 119);
            var viewBack = Rect(root, "ViewBacking", 0, 0, 680, 58);
            viewBack.anchorMin = viewBack.anchorMax = Vector2.zero; viewBack.pivot = Vector2.zero;
            viewBack.anchoredPosition = new Vector2(32, 111); Fill(viewBack, Paper, false);
            viewBack.SetSiblingIndex(view.transform.GetSiblingIndex());
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
            Label(content, "Title", "选择关卡", 0, 30, 1000, 96, 54, true);
            Rule(content, "Rule", 0, 138, 1000);
            var list = Rect(content, "List", 0, 170, 1000, 400);
            var scroll = list.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect(list, "Viewport", 0, 0, 1000, 400);
            Fill(viewport, Paper); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var rows = Rect(viewport, "Rows", 0, 0, 1000, 0);
            rows.anchorMax = new Vector2(1, 1); rows.sizeDelta = Vector2.zero;
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            layout.spacing = 12;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var row = Button(rows, "Template", "关卡", 0, 0, 1000, 112);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 112;
            var selected = Rect(row.transform, "Selected", 0, 0, 1000, 112);
            Fill(selected, new Color32(232, 234, 231, 255), false); selected.SetAsFirstSibling();
            var text = row.transform.Find("Label").GetComponent<Text>(); text.alignment = TextAnchor.MiddleLeft;
            ((RectTransform)text.transform).offsetMin = new Vector2(32, -112);
            ((RectTransform)text.transform).offsetMax = new Vector2(980, 0);
            var marker = Rect(row.transform, "Marker", 0, 8, 6, 96); Fill(marker, Orange, false);
            Rule(row.transform, "Rule", 24, 110, 952);
            row.gameObject.SetActive(false);
            scroll.viewport = viewport; scroll.content = rows; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
            Button(content, "Enter", "进入关卡", 600, 648, 400, 78, true);
            Button(content, "Back", "返回主菜单", 0, 648, 300, 78);
        }
        private static void Pause(RectTransform root)
        {
            var content = Dialog(root, 680, 800);
            Label(content, "Title", "已暂停", 40, 42, 600, 84, 48, true, TextAnchor.MiddleCenter);
            Label(content, "Subtitle", "", 40, 132, 600, 42, 24, false, TextAnchor.MiddleCenter, Muted);
            Button(content, "Resume", "继续游戏", 110, 224, 460, 72, true);
            Button(content, "Restart", "重开本关", 110, 320, 460, 64);
            Button(content, "Levels", "选择关卡", 110, 408, 460, 64);
            Button(content, "Settings", "设置", 110, 496, 460, 64);
            Button(content, "Menu", "返回主菜单", 110, 584, 460, 64);
            Label(content, "PlaytestHint", "退出 Unity Play Mode 返回关卡编辑器", 50, 688, 580, 70, 21, false, TextAnchor.MiddleCenter, Muted);
        }
        private static void Completion(RectTransform root)
        {
            var content = Dialog(root, 900, 730);
            Label(content, "Check", "✓", 390, 32, 120, 100, 68, true, TextAnchor.MiddleCenter, Orange);
            Label(content, "Title", "维修区已通电", 50, 154, 800, 92, 48, true, TextAnchor.MiddleCenter);
            Label(content, "Stats", "移动 8 · 推动 3", 50, 268, 800, 54, 28, false, TextAnchor.MiddleCenter);
            Label(content, "Message", "", 50, 345, 800, 68, 21, false, TextAnchor.MiddleCenter, Muted);
            Button(content, "Next", "下一关", 64, 456, 248, 74, true);
            Button(content, "Replay", "重玩", 340, 456, 200, 74);
            Button(content, "Levels", "返回选关", 570, 456, 266, 74);
            Button(content, "Menu", "返回主菜单", 470, 574, 320, 68);
            Button(content, "Undo", "Z  撤销最后一步", 88, 574, 330, 68);
            Button(content, "Save", "保存参考解法", 64, 456, 248, 74, true);
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
