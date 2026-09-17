#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.UI;
using Terrain = Sokoban.Domain.Terrain;

namespace Sokoban
{
    // CinemachineBrain runs at 100; project labels after its final camera pose.
    [DefaultExecutionOrder(200)]
    public sealed class GmOverlay : MonoBehaviour
    {
        public bool ShowGrid, ShowIds, ShowLinks;
        public string SelectedId;
        private LevelRunner runner;
        private RectTransform root;
        private Font font;
        private readonly List<Text> labels = new List<Text>();
        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private Material material;
        private int usedLabels, usedLines;
        public void Initialize(LevelRunner owner, RectTransform canvas, Material style)
        {
            runner = owner;
            root = new GameObject("SceneLabels", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvas, false); root.SetAsFirstSibling(); root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            font = canvas.GetComponentInChildren<Text>(true).font;
            if (!style) throw new System.InvalidOperationException("GM 叠加材质缺失，请重建 GM Prefab。");
            material = new Material(style);
        }
        private void LateUpdate()
        {
            usedLabels = usedLines = 0;
            if (runner && runner.Session != null && root)
            {
                var level = runner.Definition; var state = runner.Session.State;
                if (ShowGrid)
                {
                    for (int x = 0; x <= level.width; x++) Line(new Vector3(x - .5f, .04f, -.5f), new Vector3(x - .5f, .04f, level.height - .5f));
                    for (int z = 0; z <= level.height; z++) Line(new Vector3(-.5f, .04f, z - .5f), new Vector3(level.width - .5f, .04f, z - .5f));
                    if (runner.Cameras.TopDown) for (int z = 0; z < level.height; z++) for (int x = 0; x < level.width; x++)
                        if (level.TerrainAt(new Cell(x, z)) != Terrain.Void) Label(new Vector3(x - .22f, .08f, z - .32f), x + "," + z, 14);
                }
                if (ShowIds)
                {
                    Label(runner.Board.Robot.transform.position + Vector3.up * 1.1f, "玩家 " + state.Player, 20);
                    foreach (var c in level.crates) Label(runner.Board.Crates[c.id].position + Vector3.up, c.id + (c.IsEnergy ? " E" : " C"), 20);
                }
                if (ShowLinks) foreach (var gate in level.gates) foreach (string id in gate.sourceSocketIds)
                    foreach (var socket in level.sockets) if (socket.id == id)
                        Line(BoardView.Position(gate.Cell) + Vector3.up * .1f, BoardView.Position(socket.Cell) + Vector3.up * .1f);
                if (runner.DebugVisible && !string.IsNullOrEmpty(SelectedId))
                {
                    Cell selected;
                    if (SelectedId == DebugBoardEdit.PlayerId) selected = state.Player;
                    else if (!state.Crates.TryGetValue(SelectedId, out selected)) selected = state.Player;
                    Label(BoardView.Position(selected) + new Vector3(0, 1.35f, .34f), "▼", 30);
                }
            }
            for (int i = usedLabels; i < labels.Count; i++) labels[i].gameObject.SetActive(false);
            for (int i = usedLines; i < lines.Count; i++) lines[i].gameObject.SetActive(false);
        }
        private void Label(Vector3 world, string value, int size)
        {
            Vector3 screen = runner.Cameras.Output.WorldToScreenPoint(world);
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) return;
            if (usedLabels == labels.Count)
            {
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                label.transform.SetParent(root, false); label.font = font; label.raycastTarget = false; label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white; label.gameObject.AddComponent<Outline>().effectColor = Color.black;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.rectTransform.sizeDelta = new Vector2(260, 42); labels.Add(label);
            }
            var text = labels[usedLabels++]; text.gameObject.SetActive(true); text.text = value; text.fontSize = size;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 point); text.rectTransform.anchoredPosition = point;
        }
        private void Line(Vector3 from, Vector3 to)
        {
            if (usedLines == lines.Count)
            {
                var line = new GameObject("Debug link").AddComponent<LineRenderer>(); line.transform.SetParent(transform, false);
                line.sharedMaterial = material; line.positionCount = 2; line.startWidth = line.endWidth = .024f; lines.Add(line);
            }
            var current = lines[usedLines++]; current.gameObject.SetActive(true); current.SetPosition(0, from); current.SetPosition(1, to);
        }
        private void OnDestroy()
        {
            if (root) Destroy(root.gameObject);
            foreach (var line in lines) if (line) Destroy(line.gameObject);
            if (material) Destroy(material);
        }
    }
}
#endif
