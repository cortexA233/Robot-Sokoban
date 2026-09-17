using System;
using System.Collections.Generic;
using System.Linq;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sokoban
{
    /// <summary>Readable socket roles, circuit branches and camera-facing labels. Consumes the rule engine's three power signals.</summary>
    [DefaultExecutionOrder(210)]
    public sealed class StationCircuitView : MonoBehaviour
    {
        private sealed class SocketMark
        {
            public SocketDefinition definition;
            public MeshRenderer fill;
        }
        private sealed class GateMark
        {
            public GateDefinition definition;
            public Transform passage;
            public GameObject closed, open;
            public MeshRenderer status;
            public Label label;
            public string[] sources;
        }
        private sealed class Wire
        {
            public StationCircuitLayout.Connection connection;
            public GameObject off, on;
            public MeshRenderer terminal;
        }
        private sealed class Label
        {
            public Transform target;
            public string socketId, gateId;
            public RectTransform panel;
            public RectTransform leader;
            public Text text;
        }

        private readonly Dictionary<string, SocketMark> sockets = new Dictionary<string, SocketMark>();
        private readonly Dictionary<string, GateMark> gates = new Dictionary<string, GateMark>();
        private readonly Dictionary<string, string> sourceLabels = new Dictionary<string, string>();
        private readonly List<Wire> wires = new List<Wire>();
        private readonly List<Label> labels = new List<Label>();
        private readonly List<Rect> labelRects = new List<Rect>();
        private readonly List<Rect> protectedRects = new List<Rect>();
        private StationCircuitMesh graphics;
        private StationKitTheme theme;
        private RectTransform canvasRoot;
        private CameraRig cameras;
        private BoardView board;
        private PowerState lastPower;
        private string focusedGate;
        private string focusedSocket;
        public StationCircuitLayout Layout { get; private set; }

        public void Initialize(LevelDefinition level, StationKitTheme palette)
        {
            theme = palette; board = GetComponent<BoardView>();
            Layout = new StationCircuitLayout(level); graphics = new StationCircuitMesh(theme);
            foreach (var socket in level.sockets)
            {
                theme.Style(level.id, socket.id, out string label); sourceLabels.Add(socket.id, label);
            }
            var obj = new GameObject("Circuit labels", typeof(RectTransform), typeof(Canvas)); obj.transform.SetParent(transform, false);
            var canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            canvasRoot = obj.GetComponent<RectTransform>();
        }

        public void BindCamera(CameraRig rig) { cameras = rig; }

        public void AddSocket(SocketDefinition socket, Transform wrapper)
        {
            // Preserve the imported model and its GUID; replace its busy, mutually exclusive markings in presentation only.
            foreach (var renderer in wrapper.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            var root = new GameObject("Circuit socket").transform; root.SetParent(wrapper, false);
            graphics.Place(root, "Socket plate", graphics.Quad, new Vector3(0, .001f, 0), graphics.Surface, new Vector3(.93f, 1, .93f));
            var connected = Layout.GatesFor(socket.id);
            if (socket.isGoal)
            {
                graphics.Place(root, "Goal ring", graphics.Ring, new Vector3(0, .0022f, 0), graphics.Ink);
                graphics.Place(root, "Goal corners", graphics.Corners, new Vector3(0, .0023f, 0), graphics.Ink);
            }
            if (connected.Length > 0) graphics.Place(root, "Gate power plug", graphics.Plug, new Vector3(0, .0023f, 0), graphics.Ink);
            graphics.Place(root, "Socket state outline", graphics.Quad, new Vector3(0, .0022f, -.441f), graphics.Idle, new Vector3(.24f, 1, .031f));
            graphics.Place(root, "Socket state well", graphics.Quad, new Vector3(0, .0023f, -.441f), graphics.Surface, new Vector3(.20f, 1, .015f));
            var fill = graphics.Place(root, "Socket state fill", graphics.Quad, new Vector3(0, .0025f, -.441f), graphics.Power, new Vector3(.20f, 1, .018f));
            fill.enabled = false;
            sockets.Add(socket.id, new SocketMark { definition = socket, fill = fill });
            var caption = sourceLabels[socket.id] + (connected.Length == 0 ? "" : " · " + string.Join(" / ", connected));
            var label = NewLabel(wrapper, socket.id, null); label.text.text = caption;
        }

        public void AddGate(GateDefinition gate, Transform wrapper)
        {
            var imported = wrapper.Find("PowerGateRoot");
            foreach (string part in new[] { "SourceBadgeTemplate", "SourceBadge02", "PowerStatus", "TopDownMarkers" }) imported.Find(part).gameObject.SetActive(false);
            var passage = new GameObject("Circuit passage").transform; passage.SetParent(wrapper, false);
            graphics.Place(passage, "Gate left post", graphics.Quad, new Vector3(-.40f, .03f, 0), graphics.Ink, new Vector3(.07f, 1, .80f));
            graphics.Place(passage, "Gate right post", graphics.Quad, new Vector3(.40f, .03f, 0), graphics.Ink, new Vector3(.07f, 1, .80f));
            var closed = graphics.Place(passage, "Closed barrier", graphics.Quad, new Vector3(0, .031f, 0), graphics.Ink, new Vector3(.75f, 1, .12f));
            var open = new GameObject("Open passage"); open.transform.SetParent(passage, false);
            foreach (int side in new[] { -1, 1 }) graphics.Place(open.transform, "Open end", graphics.Quad, new Vector3(side * .345f, .031f, 0), graphics.Ink, new Vector3(.13f, 1, .06f));
            var status = graphics.Place(passage, "Gate condition", graphics.Quad, new Vector3(0, .032f, -.41f), graphics.Idle, new Vector3(.28f, 1, .028f));
            var sourceIds = Layout.Connections.Where(c => c.GateId == gate.id).Select(c => c.SocketId).ToArray();
            gates.Add(gate.id, new GateMark { definition = gate, passage = passage, closed = closed.gameObject, open = open,
                status = status, sources = sourceIds, label = NewLabel(wrapper, null, gate.id) });
            passage.gameObject.SetActive(false);
        }

        public void BuildWires()
        {
            for (int index = 0; index < Layout.Connections.Count; index++)
            {
                var connection = Layout.Connections[index]; var points = connection.Points;
                if (points.Length == 0) continue;
                var gaps = Crossings(index);
                var root = new GameObject("Circuit " + connection.SocketId + " to " + connection.GateId).transform; root.SetParent(transform, false);
                var off = graphics.Place(root, "Unpowered branch", graphics.Wire(points, true, gaps), Vector3.zero, graphics.Idle);
                var on = graphics.Place(root, "Powered branch", graphics.Wire(points, false, gaps), Vector3.zero, graphics.Power);
                var terminal = graphics.Place(root, "Source port", graphics.Dot, points[0] + Vector3.up * .0003f, graphics.Idle);
                wires.Add(new Wire { connection = connection, off = off.gameObject, on = on.gameObject, terminal = terminal });
                on.gameObject.SetActive(false);
            }
        }

        private List<Vector3> Crossings(int index)
        {
            var gaps = new List<Vector3>(); var points = Layout.Connections[index].Points;
            for (int other = 0; other < index; other++)
            {
                var previous = Layout.Connections[other].Points;
                for (int i = 1; i < points.Length; i++) for (int j = 1; j < previous.Length; j++)
                {
                    var a = points[i - 1]; var b = points[i]; var c = previous[j - 1]; var d = previous[j];
                    bool vertical = Mathf.Abs(a.x - b.x) < .001f;
                    if (vertical == (Mathf.Abs(c.x - d.x) < .001f)) continue;
                    var cross = new Vector3(vertical ? a.x : c.x, a.y, vertical ? c.z : a.z);
                    if (Inside(cross, a, b) && Inside(cross, c, d) &&
                        (cross - points[0]).sqrMagnitude > .04f && (cross - points[points.Length - 1]).sqrMagnitude > .04f)
                        gaps.Add(cross);
                }
            }
            return gaps;
        }
        private static bool Inside(Vector3 p, Vector3 a, Vector3 b) => p.x >= Mathf.Min(a.x,b.x)-.001f && p.x <= Mathf.Max(a.x,b.x)+.001f &&
            p.z >= Mathf.Min(a.z,b.z)-.001f && p.z <= Mathf.Max(a.z,b.z)+.001f;

        public void Apply(PowerState power)
        {
            lastPower = power;
            foreach (var pair in sockets) pair.Value.fill.enabled = power.Sockets[pair.Key];
            foreach (var pair in gates)
            {
                pair.Value.closed.SetActive(!power.OpenGates[pair.Key]); pair.Value.open.SetActive(power.OpenGates[pair.Key]);
                pair.Value.status.sharedMaterial = power.PoweredGates[pair.Key] ? graphics.Power : graphics.Idle;
            }
            foreach (var wire in wires)
            {
                bool powered = power.Sockets[wire.connection.SocketId];
                wire.off.SetActive(!powered); wire.on.SetActive(powered); wire.terminal.sharedMaterial = powered ? graphics.Power : graphics.Idle;
            }
            UpdateGateText();
        }

        private void UpdateGateText()
        {
            if (lastPower == null) return;
            foreach (var pair in gates)
            {
                var gate = pair.Value;
                string title = Layout.GateLabels[pair.Key] + " · " + (gate.definition.powerMode == "All" ? "全部" : "任一");
                bool occupiedOpen = lastPower.OpenGates[pair.Key] && !lastPower.PoweredGates[pair.Key];
                if (gate.sources.Length > 4 && focusedGate != pair.Key && !gate.sources.Contains(focusedSocket))
                    gate.label.text.text = title + "\n" + gate.sources.Count(id => lastPower.Sockets[id]) + "/" + gate.sources.Length + " 通电";
                else
                {
                    var rows = new List<string>();
                    for (int i = 0; i < gate.sources.Length; i += 3)
                        rows.Add(string.Join("   ", gate.sources.Skip(i).Take(3).Select(id => sourceLabels[id] + (lastPower.Sockets[id] ? " ●" : " ○"))));
                    gate.label.text.text = title + "\n" + string.Join("\n", rows);
                }
                if (occupiedOpen) gate.label.text.text += "\n占据保开";
            }
        }

        public void SetTopDown(bool value) { foreach (var gate in gates.Values) gate.passage.gameObject.SetActive(value); }

        private Label NewLabel(Transform target, string socketId, string gateId)
        {
            var leaderObject = new GameObject("Label anchor", typeof(RectTransform), typeof(Image)); leaderObject.transform.SetParent(canvasRoot, false);
            var leader = leaderObject.GetComponent<RectTransform>(); leader.anchorMin = leader.anchorMax = Vector2.zero; leader.pivot = new Vector2(0,.5f);
            var line = leaderObject.GetComponent<Image>(); line.raycastTarget = false; line.color = new Color(.8f,.85f,.87f,.65f);
            var panel = new GameObject((socketId ?? gateId) + " circuit label", typeof(RectTransform), typeof(Image));
            var rect = panel.GetComponent<RectTransform>(); rect.SetParent(canvasRoot, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            var background = panel.GetComponent<Image>(); background.color = new Color(.045f, .065f, .085f, .92f); background.raycastTarget = false;
            var child = new GameObject("Caption", typeof(RectTransform), typeof(Text)); child.transform.SetParent(rect, false);
            var text = child.GetComponent<Text>(); text.font = theme.labelFont; text.fontSize = 18; text.color = new Color(.96f, .97f, .94f);
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(5, 2); text.rectTransform.offsetMax = new Vector2(-5, -2);
            var label = new Label { target = target, socketId = socketId, gateId = gateId, panel = rect, leader = leader, text = text };
            labels.Add(label); return label;
        }

        private void LateUpdate()
        {
            if (!cameras || !cameras.Output) return;
            var camera = cameras.Output; string focus = null, socketFocus = null; float nearest = float.MaxValue;
            var mouse = Mouse.current;
            labelRects.Clear(); protectedRects.Clear();
            foreach (var label in labels) protectedRects.Add(ProjectBounds(camera, label.target.position + Vector3.up*.03f,
                new Vector3(label.gateId == null ? .33f : .45f, 0, label.gateId == null ? .33f : .45f)));
            foreach (var crate in board.Crates.Values) protectedRects.Add(ProjectBounds(camera,crate.position+Vector3.up*.4f,new Vector3(.42f,.4f,.42f)));
            foreach (var plate in board.Redirectors.Values) protectedRects.Add(ProjectBounds(camera,plate.transform.position+Vector3.up*.03f,new Vector3(.49f,0,.49f)));
            protectedRects.Add(ProjectBounds(camera,board.Robot.transform.position+Vector3.up*.35f,new Vector3(.35f,.35f,.35f)));
            int fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 60f), 14, 20);
            foreach (var label in labels)
            {
                var world = label.target.position + (label.gateId != null && !cameras.TopDown ? Vector3.up * 1.72f :
                    new Vector3(0, .06f, label.gateId != null ? .67f : .55f));
                var screen = camera.WorldToScreenPoint(world);
                bool visible = screen.z > 0 && screen.x > 0 && screen.x < Screen.width && screen.y > 0 && screen.y < Screen.height;
                if (visible && !cameras.TopDown && Physics.Linecast(camera.transform.position, world, out var hit, 1))
                    visible = hit.transform == label.target || hit.transform.IsChildOf(label.target);
                label.panel.gameObject.SetActive(visible); label.leader.gameObject.SetActive(false); if (!visible) continue;
                label.text.fontSize = fontSize;
                float width = Mathf.Clamp(label.text.preferredWidth + 12, 26, Mathf.Min(240, Screen.width * .35f));
                label.panel.sizeDelta = new Vector2(width, 100);
                float height = label.text.preferredHeight + 6;
                label.panel.sizeDelta = new Vector2(width, height);
                // HUD lives above this canvas. Keep labels out of its header/message and bottom shortcut bands.
                var tile = ProjectBounds(camera, label.target.position+Vector3.up*.03f,new Vector3(.48f,0,.48f));
                var size = new Vector2(width,height);
                var preferred = new Vector2(screen.x,screen.y);
                var position = PositionLabel(preferred, size, tile);
                var bounds = new Rect(position-size/2,size);
                label.panel.anchoredPosition = position; labelRects.Add(bounds);
                if ((position-preferred).sqrMagnitude > 16)
                {
                    var direction = (position-tile.center).normalized;
                    float fromDistance = Mathf.Min(tile.width/2/Mathf.Max(.001f,Mathf.Abs(direction.x)),tile.height/2/Mathf.Max(.001f,Mathf.Abs(direction.y)));
                    float toDistance = Mathf.Min(width/2/Mathf.Max(.001f,Mathf.Abs(direction.x)),height/2/Mathf.Max(.001f,Mathf.Abs(direction.y)));
                    var start = tile.center+direction*fromDistance; var end = position-direction*toDistance;
                    if (Vector2.Dot(end-start,direction)>2)
                    {
                        label.leader.gameObject.SetActive(true); label.leader.anchoredPosition=start;
                        label.leader.sizeDelta=new Vector2(Vector2.Distance(start,end),1);
                        label.leader.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
                    }
                }
                float distance = cameras.TopDown && mouse != null ? Vector2.Distance(mouse.position.ReadValue(), position) :
                    Vector3.Distance(board.Robot.transform.position, label.target.position) * 22;
                if (distance < 65 && distance < nearest)
                {
                    var id = label.gateId ?? Layout.Connections.FirstOrDefault(c => c.SocketId == label.socketId)?.GateId;
                    if (id != null) { focus = id; socketFocus = label.socketId; nearest = distance; }
                }
            }
            if (focusedGate != focus || focusedSocket != socketFocus) { focusedGate = focus; focusedSocket = socketFocus; UpdateGateText(); }
            foreach (var wire in wires)
                wire.off.transform.parent.gameObject.SetActive(wires.Count <= 8 || focusedGate == wire.connection.GateId ||
                    (focusedSocket != null && gates[wire.connection.GateId].sources.Contains(focusedSocket)));
        }

        private Vector2 PositionLabel(Vector2 preferred, Vector2 size, Rect tile)
        {
            var safe = new Rect(6,Screen.height*.17f,Screen.width-12,Screen.height*.62f);
            var candidates = new List<Vector2> { preferred, new Vector2(tile.center.x,tile.yMax+size.y/2+7),
                new Vector2(tile.xMax+size.x/2+7,tile.center.y), new Vector2(tile.xMin-size.x/2-7,tile.center.y),
                new Vector2(tile.center.x,tile.yMin-size.y/2-7) };
            // Adjacent crates, gates and turn plates may occupy all four direct
            // neighbours. Try nearby corners before falling back over a glyph.
            var offset = (tile.size + size) / 2 + Vector2.one * 7;
            for (int radius = 1; radius <= 2; radius++)
                foreach (var direction in new[] { new Vector2(-1,1), new Vector2(1,1), new Vector2(-1,-1), new Vector2(1,-1) })
                    candidates.Add(tile.center + Vector2.Scale(offset, direction) * radius);
            foreach (var point in candidates)
            {
                var rect = new Rect(point-size/2,size);
                if (rect.xMin < safe.xMin || rect.xMax > safe.xMax || rect.yMin < safe.yMin || rect.yMax > safe.yMax) continue;
                if (labelRects.Any(r=>r.Overlaps(rect)) || protectedRects.Any(r=>r.Overlaps(rect))) continue;
                return point;
            }
            return new Vector2(Mathf.Clamp(preferred.x,safe.xMin+size.x/2,safe.xMax-size.x/2),
                Mathf.Clamp(preferred.y,safe.yMin+size.y/2,safe.yMax-size.y/2));
        }

        private static Rect ProjectBounds(Camera camera, Vector3 center, Vector3 extent)
        {
            var min = new Vector2(float.MaxValue,float.MaxValue); var max = new Vector2(float.MinValue,float.MinValue);
            for (int x=-1;x<=1;x+=2) for (int y=-1;y<=1;y+=2) for (int z=-1;z<=1;z+=2)
            {
                var point = camera.WorldToScreenPoint(center+Vector3.Scale(extent,new Vector3(x,y,z)));
                if (point.z<=0) continue;
                min=Vector2.Min(min,point); max=Vector2.Max(max,point);
            }
            return min.x==float.MaxValue ? new Rect(-10000,-10000,0,0) : Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }

        private void OnDestroy() { graphics?.Dispose(); }
    }
}
