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
        private readonly Dictionary<string, Renderer[]> sockets = new Dictionary<string, Renderer[]>();
        private readonly Dictionary<string, Material> socketOffMaterials = new Dictionary<string, Material>();
        private readonly Dictionary<string, Renderer> gates = new Dictionary<string, Renderer>();
        private readonly Dictionary<string, Renderer> gateMarkers = new Dictionary<string, Renderer>();
        private readonly List<Renderer> upperWalls = new List<Renderer>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private Material floor, wall, dark, orange, cyan, track, goal, utility;
        private bool topDown;
        public RobotPresenter Robot { get; private set; }
        public IReadOnlyDictionary<string, Transform> Crates => crates;
        public static Vector3 Position(Cell cell) => new Vector3(cell.x, 0, cell.z);

        private Material Material(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is missing.");
            var material = new Material(shader) { color = color };
            material.SetFloat("_Smoothness", .3f);
            ownedMaterials.Add(material);
            return material;
        }

        private Renderer Box(string label, Vector3 center, Vector3 scale, Material material, Transform parent = null, bool collider = false)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = label; obj.transform.SetParent(parent ? parent : transform, false);
            obj.transform.localPosition = center; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) { obj.GetComponent<Collider>().enabled = false; Destroy(obj.GetComponent<Collider>()); }
            return obj.GetComponent<Renderer>();
        }

        public void Build(LevelDefinition level)
        {
            floor = Material(new Color(.34f, .4f, .46f)); wall = Material(new Color(.74f, .79f, .82f));
            dark = Material(new Color(.1f, .15f, .20f)); orange = Material(new Color(.94f, .55f, .15f));
            cyan = Material(new Color(.17f, .88f, .9f)); track = Material(new Color(.38f, .64f, .76f));
            goal = Material(new Color(.69f, .82f, .86f)); utility = Material(new Color(.75f, .61f, .3f));
            for (int z = 0; z < level.height; z++)
                for (int x = 0; x < level.width; x++)
                {
                    var cell = new Cell(x, z); var terrain = level.TerrainAt(cell); var pos = Position(cell);
                    if (terrain == Terrain.Void) continue;
                    Box("Floor " + cell, pos + Vector3.down * .12f, new Vector3(.96f, .24f, .96f), terrain == Terrain.LowFriction ? track : floor);
                    if (terrain == Terrain.Wall)
                    {
                        Box("Wall base", pos + Vector3.up * .12f, new Vector3(.98f, .24f, .98f), dark);
                        upperWalls.Add(Box("Wall " + cell, pos + Vector3.up * .83f, new Vector3(.97f, 1.42f, .97f), wall, collider: true));
                    }
                    if (terrain == Terrain.LowFriction)
                        for (int i = -1; i <= 1; i++) Box("Track stripe", pos + new Vector3(i * .22f, .013f, 0), new Vector3(.04f, .02f, .8f), dark);
                }
            foreach (var crate in level.crates)
            {
                var root = new GameObject(crate.id).transform; root.SetParent(transform, false); root.position = Position(crate.Cell);
                Box("Energy crate", new Vector3(0, .4f, 0), Vector3.one * .8f, wall, root);
                Box("Energy stripe X", new Vector3(0, .81f, 0), new Vector3(.65f, .025f, .15f), orange, root);
                Box("Energy stripe Z", new Vector3(0, .81f, 0), new Vector3(.15f, .025f, .65f), orange, root);
                crates.Add(crate.id, root);
            }
            foreach (var socket in level.sockets)
            {
                var pieces = new List<Renderer>(); var pos = Position(socket.Cell);
                for (int side = 0; side < 4; side++)
                {
                    var offset = Quaternion.Euler(0, side * 90, 0) * new Vector3(0, .025f, .43f);
                    pieces.Add(Box(socket.id, pos + offset, side % 2 == 0 ? new Vector3(.9f, .04f, .06f) : new Vector3(.06f, .04f, .9f), socket.isGoal ? goal : utility));
                }
                if (socket.isGoal) pieces.Add(Box("Goal center", pos + Vector3.up * .012f, new Vector3(.4f, .02f, .4f), goal));
                sockets.Add(socket.id, pieces.ToArray());
                socketOffMaterials.Add(socket.id, socket.isGoal ? goal : utility);
            }
            foreach (var gate in level.gates)
            {
                var root = new GameObject(gate.id).transform; root.SetParent(transform, false);
                root.position = Position(gate.Cell); root.rotation = Quaternion.Euler(0, gate.facing == "E" || gate.facing == "W" ? 90 : 0, 0);
                Box("Left jamb", new Vector3(-.43f, .7f, 0), new Vector3(.12f, 1.4f, .25f), dark, root);
                Box("Right jamb", new Vector3(.43f, .7f, 0), new Vector3(.12f, 1.4f, .25f), dark, root);
                gates.Add(gate.id, Box("Gate panel", new Vector3(0, .6f, 0), new Vector3(.74f, 1.2f, .16f), orange, root, true));
                gateMarkers.Add(gate.id, Box("Gate status", new Vector3(0, .035f, 0), new Vector3(.85f, .04f, .3f), orange, root));
            }
            var prefab = Resources.Load<GameObject>("prefabs/gameplay/player/PlayerActor");
            if (!prefab) throw new InvalidOperationException("PlayerActor prefab is missing. Run Tools > Sokoban > Prepare Gameplay Assets.");
            Robot = Instantiate(prefab, transform).GetComponent<RobotPresenter>();
            Robot.Initialize();
        }

        public void ApplyPower(PowerState power)
        {
            foreach (var pair in sockets)
                foreach (var renderer in pair.Value) renderer.sharedMaterial = power.Sockets[pair.Key] ? cyan : socketOffMaterials[pair.Key];
            foreach (var pair in gates)
            {
                bool open = power.OpenGates[pair.Key];
                pair.Value.transform.localPosition = new Vector3(0, open ? 1.9f : .6f, 0);
                pair.Value.sharedMaterial = open ? cyan : orange;
                pair.Value.GetComponent<Collider>().enabled = !open;
                pair.Value.enabled = !topDown;
                gateMarkers[pair.Key].sharedMaterial = open ? cyan : orange;
            }
        }
        public void SetTopDown(bool value)
        {
            topDown = value;
            foreach (var wallRenderer in upperWalls) wallRenderer.enabled = !value;
            foreach (var gate in gates.Values) gate.enabled = !value;
        }
        public void Restore(GameSession session)
        {
            Robot.Restore(session.State.Player, session.State.Facing);
            foreach (var crate in session.State.Crates) crates[crate.Key].position = Position(crate.Value);
            ApplyPower(session.Rules.Power(session.State));
        }
        private void OnDestroy() { foreach (var material in ownedMaterials) if (material) Destroy(material); }
    }
}
