using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban.Editor
{
    public static class GmUIAssetBuilder
    {
        private static Font font;
        private static readonly Color Ink = new Color32(40, 44, 48, 255);
        [MenuItem("Sokoban_Tools/Build GM UGUI Prefab")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play Mode。");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");
            if (!font) throw new InvalidOperationException("中文字体缺失。");
            var root = Rect(null, "Gm"); Stretch(root, 0);
            root.gameObject.AddComponent<CanvasGroup>();
            try
            {
                const string materialPath = "Assets/Art/UI/Materials/GmOverlay.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material)
                {
                    Directory.CreateDirectory("Assets/Art/UI/Materials");
                    var shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (!shader) throw new InvalidOperationException("URP Unlit Shader 缺失。");
                    material = new Material(shader); material.SetColor("_BaseColor", new Color(.1f, .8f, .85f));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                // A built-in, disabled renderer keeps the shader/material referenced
                // in both player builds without a development-only MonoBehaviour.
                var style = new GameObject("OverlayStyle").AddComponent<LineRenderer>(); style.transform.SetParent(root, false);
                style.enabled = false; style.positionCount = 0; style.sharedMaterial = material;
                var panel = Rect(root, "Panel"); panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1);
                panel.anchoredPosition = new Vector2(28, -68); panel.sizeDelta = new Vector2(720, 944);
                panel.gameObject.AddComponent<Image>().color = new Color32(247, 247, 244, 255);
                var stack = Column(panel, "Stack", false); Stretch(stack, 20);
                var header = Row(stack, "Header"); Size(Label(header, "Title", "GM · 调试工作台", 0).gameObject, 0);
                Button(header, "Monitor", "监视", 80); Button(header, "Close", "关闭", 80);
                var search = Row(stack, "SearchRow"); Input(search, "Search", "搜索分类或操作", "", 0);
                Button(search, "FontSmall", "字−", 70); Button(search, "FontLarge", "字+", 70);
                var width = Row(stack, "WidthRow", 32); Label(width, "WidthLabel", "面板宽度", 120);
                var slider = DefaultControls.CreateSlider(new DefaultControls.Resources()); Attach(slider, width, "Width", 0);
                var control = slider.GetComponent<Slider>(); control.minValue = 640; control.maxValue = 1000; control.value = 720;
                slider.transform.Find("Background").GetComponent<Image>().color = new Color32(214, 216, 212, 255);
                control.fillRect.GetComponent<Image>().color = new Color32(218, 120, 72, 255);
                control.handleRect.GetComponent<Image>().color = new Color32(170, 96, 55, 255);
                var tabs = Row(stack, "Tabs");
                foreach (var pair in new[] { ("LevelContent", "关卡"), ("BoardContent", "局面"), ("PowerContent", "机关"), ("LogContent", "诊断") })
                    Button(tabs, "Tab" + pair.Item1, pair.Item2, 0);
                var scroll = Rect(stack, "Scroll"); var scrollLayout = scroll.gameObject.AddComponent<LayoutElement>(); scrollLayout.flexibleHeight = 1; scrollLayout.minHeight = 240;
                var scrollControl = scroll.gameObject.AddComponent<ScrollRect>(); scrollControl.horizontal = false; scrollControl.scrollSensitivity = 30;
                var viewport = Rect(scroll, "Viewport"); Stretch(viewport, 0); viewport.gameObject.AddComponent<RectMask2D>();
                var bg = viewport.gameObject.AddComponent<Image>(); bg.color = new Color(1, 1, 1, .03f);
                var content = Column(viewport, "Content", true); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
                content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
                scrollControl.viewport = viewport; scrollControl.content = content;
                var level = Column(content, "LevelContent", true);
                Label(level, "LevelInfo", "当前关卡", 0);
                var enter = Row(level, "EnterRow"); Input(enter, "LevelIndex", "序号 1–6", "1", 0); Button(enter, "Load", "进入关卡", 180);
                Button(level, "Live", "现场编辑当前局面（Editor）", 0);
                Button(level, "EndLive", "结束现场试玩 · 返回原局面", 0);
                Label(level, "LiveHint", "结构修改建立新试玩起点；原关卡文件保留。", 0);
                var board = Column(content, "BoardContent", true);
                Label(board, "BoardInfo", "逻辑状态", 0); Dropdown(board, "Entity", new[] { "玩家" });
                var coordinates = Row(board, "Coordinates"); Label(coordinates, "CoordinateLabel", "目标格", 90);
                Input(coordinates, "X", "X", "0", 0); Input(coordinates, "Z", "Z", "0", 0); Button(coordinates, "Pick", "点选格子", 140);
                Button(board, "Place", "移动到此格", 0);
                var facing = Row(board, "FacingRow"); Dropdown(facing, "Facing", new[] { "北 N", "东 E", "南 S", "西 W" }); Button(facing, "Rotate", "修改玩家朝向", 200);
                var swap = Row(board, "SwapRow"); Dropdown(swap, "OtherEntity", new[] { "玩家" }); Button(swap, "Swap", "交换位置", 200);
                Toggle(board, "AnimationPause", "暂停动作动画（独立于游戏暂停）");
                var additions = Row(board, "Additions"); Button(additions, "AddEnergy", "+ 能源箱", 0); Button(additions, "AddCargo", "+ 普通箱", 0); Button(additions, "RemoveCrate", "删除箱子", 0);
                Label(board, "AdditionHint", "增删箱子：进入现场草稿，应用后建立新起点。", 0);
                var power = Column(content, "PowerContent", true); Label(power, "PowerInfo", "供电与占据", 0);
                var log = Column(content, "LogContent", true); Button(log, "Copy", "复制完整诊断快照", 0); Label(log, "LogInfo", "操作记录", 0);
                var history = Row(stack, "History"); Button(history, "Undo", "撤销", 0); Button(history, "Restart", "重开", 0); Button(history, "View", "切换视角", 0);
                var overlays = Row(stack, "Overlays"); Toggle(overlays, "Grid", "网格"); Toggle(overlays, "Ids", "ID/箱型"); Toggle(overlays, "Links", "连接线");
                var status = Label(stack, "Status", "F1 开关 · Esc 返回 · GM 移位可撤销", 0); status.gameObject.AddComponent<LayoutElement>().preferredHeight = 58;
                var monitor = Label(root, "MonitorText", "F1 GM", 0); monitor.rectTransform.anchorMin = monitor.rectTransform.anchorMax = monitor.rectTransform.pivot = new Vector2(0, 1);
                monitor.rectTransform.anchoredPosition = new Vector2(35, -8); monitor.rectTransform.sizeDelta = new Vector2(1600, 48);
                Directory.CreateDirectory("Assets/Resources/UI_prefabs/screens/GM");
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Resources/UI_prefabs/screens/GM/Gm.prefab");
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }
        private static RectTransform Rect(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.gameObject.layer = 5;
            if (parent) rect.SetParent(parent, false); return rect;
        }
        private static void Stretch(RectTransform rect, float margin)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * margin; rect.offsetMax = Vector2.one * -margin; }
        private static RectTransform Column(Transform parent, string name, bool fit)
        {
            var rect = Rect(parent, name); var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 9; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
            if (fit) rect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }
        private static RectTransform Row(Transform parent, string name, float height = 46)
        {
            var rect = Rect(parent, name); var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            var element = rect.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = height; element.flexibleHeight = 0; return rect;
        }
        private static void Size(GameObject obj, float width)
        { var size = obj.AddComponent<LayoutElement>(); size.minHeight = 42; size.flexibleWidth = width == 0 ? 1 : 0; if (width > 0) size.preferredWidth = width; }
        private static Text Label(Transform parent, string name, string value, float width)
        {
            var rect = Rect(parent, name); var text = rect.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = 22;
            text.text = value; text.color = Ink; text.raycastTarget = false; text.supportRichText = false;
            text.alignment = TextAnchor.MiddleLeft; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (width > 0) Size(rect.gameObject, width); return text;
        }
        private static void Attach(GameObject obj, Transform parent, string name, float width)
        {
            obj.name = name; obj.transform.SetParent(parent, false); Size(obj, width);
            foreach (var text in obj.GetComponentsInChildren<Text>(true)) { text.font = font; text.fontSize = 22; text.color = Ink; text.supportRichText = false; }
        }
        private static void Button(Transform parent, string name, string text, float width)
        {
            var obj = DefaultControls.CreateButton(new DefaultControls.Resources()); Attach(obj, parent, name, width);
            obj.GetComponentInChildren<Text>().text = text; obj.GetComponent<Image>().color = new Color32(233, 227, 215, 255);
        }
        private static void Input(Transform parent, string name, string placeholder, string value, float width)
        {
            var obj = DefaultControls.CreateInputField(new DefaultControls.Resources()); Attach(obj, parent, name, width);
            var field = obj.GetComponent<InputField>(); ((Text)field.placeholder).text = placeholder; field.text = value;
            if (name == "X" || name == "Z" || name == "LevelIndex") field.contentType = InputField.ContentType.IntegerNumber;
        }
        private static void Dropdown(Transform parent, string name, string[] options)
        {
            var obj = DefaultControls.CreateDropdown(new DefaultControls.Resources()); Attach(obj, parent, name, 0);
            var dropdown = obj.GetComponent<Dropdown>(); dropdown.ClearOptions(); dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
            dropdown.captionText.rectTransform.offsetMin = new Vector2(10, 2); dropdown.captionText.rectTransform.offsetMax = new Vector2(-25, -2);
            dropdown.captionText.verticalOverflow = VerticalWrapMode.Overflow;
            dropdown.itemText.verticalOverflow = VerticalWrapMode.Overflow;
            ((RectTransform)dropdown.itemText.transform.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 38);
            dropdown.template.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 220);
            var arrow = obj.transform.Find("Arrow"); UnityEngine.Object.DestroyImmediate(arrow.GetComponent<Image>());
            var arrowText = arrow.gameObject.AddComponent<Text>(); arrowText.font = font; arrowText.fontSize = 20;
            arrowText.text = "▾"; arrowText.color = Ink; arrowText.raycastTarget = false; arrowText.verticalOverflow = VerticalWrapMode.Overflow;
        }
        private static void Toggle(Transform parent, string name, string text)
        {
            var obj = DefaultControls.CreateToggle(new DefaultControls.Resources()); Attach(obj, parent, name, 0);
            var toggle = obj.GetComponent<Toggle>(); toggle.isOn = false; obj.GetComponentInChildren<Text>().text = text;
            toggle.targetGraphic.color = new Color32(205, 210, 204, 255); toggle.graphic.color = new Color32(218, 120, 72, 255);
        }
    }
}
