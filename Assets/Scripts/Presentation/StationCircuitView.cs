using System;
using System.Collections.Generic;
using System.Linq;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sokoban
{
    /// <summary>Socket roles and persistent connection identities, with independent power and passage signals.</summary>
    [DefaultExecutionOrder(210)]
    public sealed class StationCircuitView : MonoBehaviour
    {
        private sealed class SocketMark
        {
            public SocketDefinition definition;
            public MeshRenderer fill;
            public Transform target;
        }
        private sealed class GateMark
        {
            public Transform target;
            public GameObject closed, open;
            public MeshRenderer status;
            public string[] sources;
            public MeshRenderer[] sourceLights;
            public GameObject held;
        }
        private sealed class Wire
        {
            public StationCircuitLayout.Connection connection;
            public GameObject off, on;
            public MeshRenderer terminal;
        }
        private readonly Dictionary<string, SocketMark> sockets = new Dictionary<string, SocketMark>();
        private readonly Dictionary<string, GateMark> gates = new Dictionary<string, GateMark>();
        private readonly List<Wire> wires = new List<Wire>();
        private StationCircuitMesh graphics;
        private StationKitTheme theme;
        private CameraRig cameras;
        private BoardView board;
        public StationCircuitLayout Layout { get; private set; }

        public void Initialize(LevelDefinition level, StationKitTheme palette)
        {
            theme = palette; board = GetComponent<BoardView>();
            Layout = new StationCircuitLayout(level); graphics = new StationCircuitMesh(theme);
        }

        public void BindCamera(CameraRig rig) { cameras = rig; }

        public void AddSocket(SocketDefinition socket, Transform wrapper)
        {
            // Keep the Blender housing; the existing circuit layer owns identity and power.
            var housing = wrapper.Find(socket.isGoal ? "GoalSocketRoot/Geometry" : "UtilitySocketRoot/Geometry");
            foreach (var renderer in wrapper.GetComponentsInChildren<Renderer>())
                renderer.enabled = renderer.transform.IsChildOf(housing);
            var root = new GameObject("Circuit socket").transform; root.SetParent(wrapper, false);
            graphics.Place(root, "Socket plate", graphics.Quad, new Vector3(0, .001f, 0), graphics.Surface, new Vector3(.93f, 1, .93f));
            var primary = Layout.PrimaryGateFor(socket.id);
            if (primary != null) AddIdentity(root, primary, true);
            if (socket.isGoal)
            {
                graphics.Place(root, "Goal ring", graphics.Ring, new Vector3(0, .0022f, 0), graphics.Ink);
                graphics.Place(root, "Goal corners", graphics.Corners, new Vector3(0, .0023f, 0), graphics.Ink);
            }
            if (primary != null) graphics.Place(root, "Gate power plug", graphics.Plug, new Vector3(0, .0023f, 0), Connection(primary));
            graphics.Place(root, "Socket state outline", graphics.Quad, new Vector3(0, .0022f, -.441f), graphics.Idle, new Vector3(.24f, 1, .031f));
            graphics.Place(root, "Socket state well", graphics.Quad, new Vector3(0, .0023f, -.441f), graphics.Surface, new Vector3(.20f, 1, .015f));
            var fill = graphics.Place(root, "Socket state fill", graphics.Quad, new Vector3(0, .0025f, -.441f), graphics.Power, new Vector3(.20f, 1, .018f));
            fill.enabled = false;
            sockets.Add(socket.id, new SocketMark { definition = socket, fill = fill, target = wrapper });
        }

        private Material Connection(string gateId) => graphics.Connection(theme, Layout.GateStyles[gateId]);

        private void AddIdentity(Transform parent, string gateId, bool inset = false)
        {
            var root = new GameObject("Connection " + gateId).transform; root.SetParent(parent, false);
            for (int side = 0; side < 4; side++)
            {
                var edge = new GameObject("Edge " + side).transform; edge.SetParent(root, false);
                edge.localRotation = Quaternion.Euler(0, side * 90, 0);
                graphics.Place(edge, "Connection colour", graphics.Quad, new Vector3(0,.004f,.44f), Connection(gateId),
                    new Vector3(inset ? .61f : .644f, 1, inset ? .045f : .10f));
                graphics.Place(edge, "Connection shape", graphics.Symbol(Layout.GateStyles[gateId]), new Vector3(0,.0045f,.44f), graphics.Surface,
                    new Vector3(inset ? .085f : .14f, 1, inset ? .085f : .14f));
            }
        }

        public void AddGate(GateDefinition gate, Transform wrapper)
        {
            var imported = wrapper.Find("PowerGateRoot");
            foreach (string part in new[] { "SourceBadgeTemplate", "SourceBadge02", "PowerStatus", "TopDownMarkers" }) imported.Find(part).gameObject.SetActive(false);
            var passage = new GameObject("Circuit passage").transform; passage.SetParent(wrapper, false);
            var identity = Connection(gate.id);
            foreach (var renderer in imported.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) if (StationKitRendering.HasRole(materials[i], "LinkAccent")) materials[i] = identity;
                renderer.sharedMaterials = materials;
            }
            graphics.Place(passage, "Gate left post", graphics.Quad, new Vector3(-.40f, .03f, 0), identity, new Vector3(.07f, 1, .80f));
            graphics.Place(passage, "Gate right post", graphics.Quad, new Vector3(.40f, .03f, 0), identity, new Vector3(.07f, 1, .80f));
            var closed = graphics.Place(passage, "Closed barrier", graphics.Quad, new Vector3(0, .031f, 0), identity, new Vector3(.75f, 1, .12f));
            var open = new GameObject("Open passage"); open.transform.SetParent(passage, false);
            foreach (int side in new[] { -1, 1 }) graphics.Place(open.transform, "Open end", graphics.Quad, new Vector3(side * .345f, .031f, 0), graphics.Ink, new Vector3(.13f, 1, .06f));
            var status = graphics.Place(passage, "Gate condition", graphics.Quad, new Vector3(0, .032f, -.41f), graphics.Idle, new Vector3(.28f, 1, .028f));
            var sourceIds = Layout.Connections.Where(c => c.GateId == gate.id).Select(c => c.SocketId).ToArray();
            var lights = new MeshRenderer[sourceIds.Length];
            for (int i = 0; i < sourceIds.Length; i++)
            {
                float x = (i - (sourceIds.Length-1)/2f) * Mathf.Min(.15f,.6f/sourceIds.Length);
                graphics.Place(passage, "Source outline " + sourceIds[i], graphics.Dot, new Vector3(x,.033f,.34f), identity);
                graphics.Place(passage, "Source well " + sourceIds[i], graphics.Dot, new Vector3(x,.0335f,.34f), graphics.Surface, Vector3.one*.72f);
                lights[i] = graphics.Place(passage, "Source powered " + sourceIds[i], graphics.Dot, new Vector3(x,.034f,.34f), graphics.Power, Vector3.one*.58f);
            }
            var held = graphics.Place(passage, "Occupied hold", graphics.Symbol(4), new Vector3(0,.034f,0), graphics.Held, new Vector3(.20f,1,.20f));
            AddIdentity(passage, gate.id);
            gates.Add(gate.id, new GateMark { target = wrapper, closed = closed.gameObject, open = open,
                status = status, sources = sourceIds, sourceLights = lights, held = held.gameObject });
        }

        public void BuildWires()
        {
            for (int index = 0; index < Layout.Connections.Count; index++)
            {
                var connection = Layout.Connections[index]; var points = connection.Points;
                if (points.Length == 0) continue;
                var gaps = Crossings(index);
                var root = new GameObject("Circuit " + connection.SocketId + " to " + connection.GateId).transform; root.SetParent(transform, false);
                var off = graphics.Place(root, "Unpowered branch", graphics.Wire(points, true, gaps), Vector3.zero, Connection(connection.GateId));
                var on = graphics.Place(root, "Powered branch", graphics.Wire(points, false, gaps), Vector3.zero, Connection(connection.GateId));
                var terminal = graphics.Place(root, "Source port", graphics.Dot, points[0] + Vector3.up * .0003f, Connection(connection.GateId));
                wires.Add(new Wire { connection = connection, off = off.gameObject, on = on.gameObject, terminal = terminal });
                on.gameObject.SetActive(false);
                root.gameObject.SetActive(false);
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
            foreach (var pair in sockets) pair.Value.fill.enabled = power.Sockets[pair.Key];
            foreach (var pair in gates)
            {
                pair.Value.closed.SetActive(!power.OpenGates[pair.Key]); pair.Value.open.SetActive(power.OpenGates[pair.Key]);
                pair.Value.status.sharedMaterial = power.PoweredGates[pair.Key] ? graphics.Power : graphics.Idle;
                pair.Value.held.SetActive(power.OpenGates[pair.Key] && !power.PoweredGates[pair.Key]);
                for (int i = 0; i < pair.Value.sources.Length; i++) pair.Value.sourceLights[i].enabled = power.Sockets[pair.Value.sources[i]];
            }
            foreach (var wire in wires)
            {
                bool powered = power.Sockets[wire.connection.SocketId];
                wire.off.SetActive(!powered); wire.on.SetActive(powered);
            }
        }

        private void LateUpdate()
        {
            if (!cameras || !cameras.Output) return;
            string gateFocus = null, socketFocus = null;
            float nearest = float.MaxValue;
            foreach (var socket in sockets.Values)
            {
                float distance = FocusDistance(socket.target, false);
                if (distance >= nearest) continue;
                socketFocus = socket.definition.id; nearest = distance;
            }
            foreach (var gate in gates)
            {
                float distance = FocusDistance(gate.Value.target, true);
                if (distance >= nearest) continue;
                gateFocus = gate.Key; socketFocus = null; nearest = distance;
            }
            foreach (var wire in wires)
                wire.off.transform.parent.gameObject.SetActive(gateFocus == wire.connection.GateId || socketFocus == wire.connection.SocketId);
        }

        // Inspect the device itself. Wire discovery must not depend on a text label.
        private float FocusDistance(Transform target, bool gate)
        {
            var camera = cameras.Output;
            var world = target.position + Vector3.up * (gate && !cameras.TopDown ? 1.35f : .06f);
            var screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height)
                return float.MaxValue;
            if (cameras.TopDown)
            {
                var mouse = Mouse.current;
                if (mouse == null) return float.MaxValue;
                var pointer = mouse.position.ReadValue();
                var bounds = ProjectBounds(camera, target.position + Vector3.up * .03f, new Vector3(.48f, 0, .48f));
                float margin = Mathf.Max(5, Screen.height / 108f);
                bounds.xMin -= margin; bounds.xMax += margin; bounds.yMin -= margin; bounds.yMax += margin;
                return bounds.Contains(pointer) ? Vector2.Distance(pointer, screen) : float.MaxValue;
            }
            float distance = Vector3.Distance(board.Robot.transform.position, target.position);
            if (distance > (gate ? 2.9f : 1.45f)) return float.MaxValue;
            if (Physics.Linecast(camera.transform.position, world, out var hit, 1) &&
                hit.transform != target && !hit.transform.IsChildOf(target)) return float.MaxValue;
            return distance;
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
