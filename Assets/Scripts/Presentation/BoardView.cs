using System;
using System.Collections.Generic;
using Sokoban.Domain;
using UnityEngine;
using Terrain = Sokoban.Domain.Terrain;

namespace Sokoban
{
    public sealed class BoardView : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> crates = new Dictionary<string, Transform>();
        private readonly Dictionary<string, StationGateView> gates = new Dictionary<string, StationGateView>();
        private readonly Dictionary<string, RedirectorView> redirectors = new Dictionary<string, RedirectorView>();
        private readonly List<CameraOcclusion> occluders = new List<CameraOcclusion>();
        private StationKitTheme theme;
        public RobotPresenter Robot { get; private set; }
        public IReadOnlyDictionary<string, Transform> Crates => crates;
        public IReadOnlyDictionary<string, StationGateView> Gates => gates;
        public IReadOnlyDictionary<string, RedirectorView> Redirectors => redirectors;
        public StationCircuitView Circuits { get; private set; }
        public static Vector3 Position(Cell cell) => new Vector3(cell.x, 0, cell.z);

        private GameObject Place(string assetId, string name, Vector3 position, float yaw = 0)
        {
            var obj = Instantiate(theme.Prefab(assetId), transform);
            obj.name = name; obj.transform.localPosition = position; obj.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return obj;
        }
        private void TrackMark(Vector3 position)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = "Low friction marking";
            obj.transform.SetParent(transform, false); obj.transform.localPosition = position;
            obj.transform.localScale = new Vector3(.025f, .002f, .72f);
            obj.GetComponent<Renderer>().sharedMaterial = theme.trackMark;
            var collider = obj.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        }
        public void Build(LevelDefinition level)
        {
            theme = Resources.Load<StationKitTheme>("configs/StationKitTheme");
            if (!theme) throw new InvalidOperationException("StationKitTheme is missing. Check Assets/Resources/configs/StationKitTheme.asset.");
            Circuits = gameObject.AddComponent<StationCircuitView>(); Circuits.Initialize(level, theme);
            var featureCells = new HashSet<Cell>();
            foreach (var socket in level.sockets) featureCells.Add(socket.Cell);
            foreach (var gate in level.gates) featureCells.Add(gate.Cell);
            foreach (var redirector in level.redirectors) featureCells.Add(redirector.Cell);
            for (int z = 0; z < level.height; z++)
                for (int x = 0; x < level.width; x++)
                {
                    var cell = new Cell(x, z); var terrain = level.TerrainAt(cell); var pos = Position(cell);
                    // Wall-to-void experiment: leave no deck, wall or camera obstacle.
                    // Keep author data intact; RuleEngine still blocks these cells.
                    if (terrain == Terrain.Void || terrain == Terrain.Wall) continue;
                    string deck = terrain != Terrain.Floor || featureCells.Contains(cell) ? "FloorPlain" :
                        (x * 17 + z * 31) % 13 == 0 ? "FloorService" : (x * 19 + z * 7) % 23 == 0 ? "FloorGrate" : "FloorPlain";
                    var floor = Place(deck, "Floor " + cell, pos);
                    if (terrain == Terrain.LowFriction)
                    {
                        floor.GetComponentInChildren<Renderer>().sharedMaterial = theme.trackSurface;
                        for (int i = -1; i <= 1; i++) TrackMark(pos + new Vector3(i * .22f, .0015f, 0));
                    }
                }
            foreach (var crate in level.crates)
                crates.Add(crate.id, Place(crate.kind == CrateDefinition.Energy ? "EnergyCrate" : "CargoCrate", crate.id, Position(crate.Cell)).transform);
            foreach (var socket in level.sockets)
            {
                string asset = socket.isGoal ? "GoalSocket" : "UtilitySocket";
                var obj = Place(asset, socket.id, Position(socket.Cell));
                Circuits.AddSocket(socket, obj.transform);
            }
            foreach (var gate in level.gates)
            {
                var obj = Place("PowerGate", gate.id, Position(gate.Cell), (int)Enum.Parse(typeof(Direction), gate.facing) * 90);
                var view = obj.AddComponent<StationGateView>(); view.Initialize(gate, level, theme); gates.Add(gate.id, view);
                occluders.Add(view.Occlusion);
                Circuits.AddGate(gate, obj.transform);
            }
            Circuits.BuildWires();
            foreach (var redirector in level.redirectors)
            {
                var obj = new GameObject(redirector.id); obj.transform.SetParent(transform, false);
                obj.transform.localPosition = Position(redirector.Cell);
                var view = obj.AddComponent<RedirectorView>(); view.Initialize(redirector, theme);
                redirectors.Add(redirector.id, view);
            }
            var prefab = Resources.Load<GameObject>("prefabs/gameplay/player/PlayerActor");
            if (!prefab) throw new InvalidOperationException("PlayerActor prefab is missing. Check Assets/Resources/prefabs/gameplay/player/PlayerActor.prefab.");
            Robot = Instantiate(prefab, transform).GetComponent<RobotPresenter>(); Robot.Initialize();
        }

        public void ApplyPower(PowerState power, bool immediate = false)
        {
            Circuits.Apply(power);
            foreach (var pair in gates) pair.Value.Apply(power.PoweredGates[pair.Key], power.OpenGates[pair.Key], power.Sockets, immediate);
        }
        public void PrepareCamera(bool topDown)
        {
            foreach (var occluder in occluders) occluder.Prepare(topDown);
        }
        public void SetPaused(bool value) { foreach (var gate in gates.Values) gate.SetPaused(value); }
        public void CancelTransitions() { foreach (var gate in gates.Values) if (gate) gate.Cancel(); }
        public void Restore(GameSession session, bool immediate = true)
        {
            Robot.Restore(session.State.Player, session.State.Facing);
            foreach (var crate in session.State.Crates) crates[crate.Key].position = Position(crate.Value);
            ApplyPower(session.Rules.Power(session.State), immediate);
        }
    }
}
