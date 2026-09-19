using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Sokoban.Tests
{
    public sealed class StationCircuitTests
    {
        private LevelRunner runner;
        public static LevelDefinition ComparisonFixture() => new LevelDefinition
        {
            schemaVersion=2, id="CircuitComparison",
            width=13,height=9,gridSize=1,
            terrainRows=new[]{"#############","#.....#.....#","#.....#.....#","#.....#.....#","#...........#","#.....#.....#","#.....#.....#","#.....#.....#","#############"},
            playerSpawn=new PlayerSpawn{x=2,z=2,facing="E"},
            crates=new[]{new CrateDefinition{id="energy_1",x=3,z=2,kind=CrateDefinition.Energy},new CrateDefinition{id="energy_2",x=3,z=4,kind=CrateDefinition.Energy}},
            sockets=new[]{new SocketDefinition{id="socket_s",x=4,z=2},new SocketDefinition{id="socket_a",x=9,z=4,isGoal=true},new SocketDefinition{id="socket_b",x=9,z=2,isGoal=true}},
            gates=new[]{new GateDefinition{id="gate",x=6,z=4,facing="E",powerMode="Any",sourceSocketIds=new[]{"socket_s","socket_a"}}},
            decorations=Array.Empty<DecorationDefinition>()
        };

        [UnitySetUp] public IEnumerator SetUp()
        {
            LevelRunner.PlaytestDefinition=null;
            runner=new GameObject("Circuit test runner").AddComponent<LevelRunner>(); runner.Progress = new PlayerProgress(() => null, _ => { }); yield return null;
            runner.enabled=false;
        }
        [UnityTearDown] public IEnumerator TearDown()
        { if(runner) Object.Destroy(runner.gameObject); yield return null; LevelRunner.PlaytestDefinition=null; }
        private Transform Mark(string id,string name) => runner.Board.transform.Find(id+"/Circuit socket/"+name);
        private bool Lit(string id) => Mark(id,"Socket state fill").GetComponent<Renderer>().enabled;
        private bool Branch(string id,bool powered) => runner.Board.transform.Find("Circuit "+id+" to gate/"+(powered?"Powered branch":"Unpowered branch")).gameObject.activeSelf;
        private IEnumerator Move(char c)
        {
            Assert.That(runner.TryMove((Direction)Enum.Parse(typeof(Direction),c.ToString())),Is.True);
            float deadline=Time.realtimeSinceStartup+4;
            while(runner.Presenter.Busy && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(runner.Presenter.Busy,Is.False);
        }

        [UnityTest] public IEnumerator GoalAndGateSourceAreIndependentVisualRolesInTheRealCampaign()
        {
            runner.LoadLevel(LevelJson.Read(Resources.Load<TextAsset>("configs/levels/L06").text)); yield return null;
            Assert.That(Mark("socket_a","Goal ring"),Is.Not.Null); Assert.That(Mark("socket_a","Gate power plug"),Is.Not.Null);
            Assert.That(Mark("socket_b","Goal ring"),Is.Not.Null); Assert.That(Mark("socket_b","Gate power plug"),Is.Null);
            Assert.That(Mark("socket_s","Goal ring"),Is.Null); Assert.That(Mark("socket_s","Gate power plug"),Is.Not.Null);
            Assert.That(runner.Board.transform.Find("Circuit labels/socket_b circuit label"),Is.Null);
            foreach (var socket in runner.Definition.sockets)
                Assert.That(runner.Board.transform.Find("Circuit labels/"+socket.id+" circuit label"),Is.Null);
            var source = Mark("socket_s","Connection gate_D/Edge 0/Connection colour").GetComponent<Renderer>();
            var goal = Mark("socket_a","Connection gate_D/Edge 0/Connection colour").GetComponent<Renderer>();
            var gate = runner.Board.Gates["gate_D"].transform.Find("Circuit passage/Gate left post").GetComponent<Renderer>();
            Assert.That(source.sharedMaterial,Is.SameAs(goal.sharedMaterial));
            Assert.That(source.sharedMaterial,Is.SameAs(gate.sharedMaterial));
            Assert.That(Mark("socket_b","Connection gate_D"),Is.Null);
            Assert.That(runner.Board.Circuits.Layout.Connections.All(c=>c.SocketId!="socket_b"),Is.True);
            foreach(var socket in runner.Definition.sockets)
                Assert.That(runner.Board.transform.Find(socket.id).GetComponentsInChildren<Collider>(),Is.Empty);
        }

        [UnityTest] public IEnumerator ActualPowerHandoffUpdatesOnlyItsBranchAndUndoRestoresBothSignals()
        {
            runner.LoadLevel(ComparisonFixture()); yield return null;
            var identity = Mark("socket_s","Connection gate/Edge 0/Connection colour").GetComponent<Renderer>().sharedMaterial;
            Assert.That(Branch("socket_s",false)&&Branch("socket_a",false),Is.True);
            yield return Move('E');
            Assert.That(Lit("socket_s")&&!Lit("socket_a"),Is.True);
            Assert.That(Branch("socket_s",true)&&Branch("socket_a",false),Is.True);
            foreach(char c in "NWNEEEEEEWWWSSSWN") yield return Move(c);
            Assert.That(runner.Session.State.Moves,Is.EqualTo(18));
            Assert.That(!Lit("socket_s")&&Lit("socket_a")&&!Lit("socket_b"),Is.True);
            Assert.That(Branch("socket_s",false)&&Branch("socket_a",true),Is.True);
            Assert.That(runner.Board.Circuits.GetComponentsInChildren<Text>(true), Is.Empty);
            Assert.That(runner.Board.Gates["gate"].transform.Find("Circuit passage/Source powered socket_s").GetComponent<Renderer>().enabled,Is.False);
            Assert.That(runner.Board.Gates["gate"].transform.Find("Circuit passage/Source powered socket_a").GetComponent<Renderer>().enabled,Is.True);
            Assert.That(Mark("socket_s","Connection gate/Edge 0/Connection colour").GetComponent<Renderer>().sharedMaterial,Is.SameAs(identity));
            Assert.That(runner.Board.Gates["gate"].IsPowered && runner.Board.Gates["gate"].IsOpen,Is.True);
            runner.Undo(); yield return null;
            Assert.That(Lit("socket_s")&&Lit("socket_a"),Is.True);
            runner.Restart(); yield return null;
            Assert.That(!Lit("socket_s")&&!Lit("socket_a"),Is.True);
            Assert.That(Branch("socket_s",false)&&Branch("socket_a",false),Is.True);
        }

        [UnityTest] public IEnumerator OccupiedUnpoweredGateHasAnOpenPassageAndExplicitReason()
        {
            var level=ComparisonFixture(); level.crates[1].kind=CrateDefinition.Cargo; level.crates[1].x=6;
            level.crates=level.crates.Concat(new[]{new CrateDefinition{id="spare_energy",x=10,z=6,kind=CrateDefinition.Energy}}).ToArray();
            runner.LoadLevel(level); yield return new WaitForSeconds(.35f);
            Assert.That(runner.Board.Circuits.GetComponentsInChildren<Text>(true), Is.Empty);
            var passage=runner.Board.Gates["gate"].transform.Find("Circuit passage");
            Assert.That(passage.Find("Open passage").gameObject.activeInHierarchy,Is.True);
            Assert.That(passage.Find("Closed barrier").gameObject.activeSelf,Is.False);
            Assert.That(passage.Find("Gate condition").GetComponent<Renderer>().sharedMaterial.name,Does.EndWith("Idle"));
            Assert.That(passage.Find("Occupied hold").gameObject.activeSelf,Is.True);
        }

        [UnityTest] public IEnumerator CircuitMarksHaveNoLabelsOrCanvasAndDoNotLeakOnRebuild()
        {
            for(int i=0;i<3;i++)
            {
                runner.LoadLevel(ComparisonFixture()); yield return new WaitForSeconds(.35f);
                Assert.That(runner.Board.Circuits.GetComponentsInChildren<Text>(true), Is.Empty);
                Assert.That(runner.Board.Circuits.GetComponentsInChildren<Canvas>(true), Is.Empty);
                Assert.That(runner.Board.transform.Find("Circuit labels"), Is.Null);
                Assert.That(runner.Board.Circuits.GetComponentsInChildren<GraphicRaycaster>(),Is.Empty);
                Assert.That(Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.name.StartsWith("Station circuit ",StringComparison.Ordinal)),Is.EqualTo(6));
                Assert.That(Object.FindObjectsOfType<StationCircuitView>().Length,Is.EqualTo(1));
                runner.ToggleCamera(); yield return new WaitForSeconds(.35f);
                Assert.That(runner.Board.Gates["gate"].transform.Find("Circuit passage").gameObject.activeSelf,Is.True,
                    "Low passage and connection marks stay available when a gate's upper structure is occluded.");
            }
        }

        [UnityTest] public IEnumerator NinthLevelFrameStaysOrangeThroughPowerHandoffUndoAndRebuild()
        {
            var level = LevelJson.Read(Resources.Load<TextAsset>("configs/levels/L12").text);
            var proof = JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/L12.solution").text);
            // Use the current solution's powered goal state; step 35 belongs to an older route.
            foreach (int prefix in new[] { 0, proof.commands.Length })
            {
                runner.LoadLevel(level); yield return null;
                foreach (char command in proof.commands.Take(prefix))
                    Assert.That(runner.Session.Move((Direction)Enum.Parse(typeof(Direction), command.ToString())).Accepted, Is.True);
                runner.Board.Restore(runner.Session);
                var identity = runner.Board.Gates["gate_F"].transform.Find("Circuit passage/Gate left post").GetComponent<Renderer>().sharedMaterial;
                Assert.That(Mark("socket_a", "Connection gate_D"), Is.Null);
                for (int i = 0; i < 4; i++)
                    Assert.That(Mark("socket_a", "Connection gate_F/Edge " + i + "/Connection colour").GetComponent<Renderer>().sharedMaterial, Is.SameAs(identity));
                Assert.That(Mark("socket_a", "Goal ring"), Is.Not.Null);
                Assert.That(runner.Board.Circuits.Layout.Connections.Count, Is.EqualTo(3));
                if (prefix > 0)
                {
                    Assert.That(Lit("socket_a"), Is.True);
                    Assert.That(runner.Board.Gates.Values.All(g=>g.IsPowered), Is.True);
                    runner.Undo(); runner.Restart(); yield return null;
                    Assert.That(Lit("socket_a"), Is.False);
                    Assert.That(Mark("socket_a", "Connection gate_F/Edge 0/Connection colour").GetComponent<Renderer>().sharedMaterial, Is.SameAs(identity));
                }
            }
        }

        [UnityTest] public IEnumerator PointingAtDevicesShowsAllTheirConnectionsWithoutLabels()
        {
            runner.LoadLevel(LevelJson.Read(Resources.Load<TextAsset>("configs/levels/L12").text));
            yield return new WaitForSeconds(.4f);
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var view = runner.Board.Circuits;
                Action<Vector2> point = p =>
                {
                    mouse.MakeCurrent(); InputState.Change(mouse.position, p);
                    typeof(StationCircuitView).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(view, null);
                };
                Func<string,string,bool> visible = (socket,gate) => runner.Board.transform.Find("Circuit " + socket + " to " + gate).gameObject.activeSelf;
                point(runner.Cameras.Output.WorldToScreenPoint(runner.Board.transform.Find("socket_a").position));
                Assert.That(visible("socket_a", "gate_F") && visible("socket_a", "gate_D"), Is.True);
                Assert.That(visible("socket_s", "gate_D"), Is.False);
                point(runner.Cameras.Output.WorldToScreenPoint(runner.Board.Gates["gate_D"].transform.position));
                Assert.That(visible("socket_a", "gate_D") && visible("socket_s", "gate_D"), Is.True);
                Assert.That(visible("socket_a", "gate_F"), Is.False);
                point(new Vector2(-100, -100));
                Assert.That(visible("socket_a", "gate_D") || visible("socket_s", "gate_D") || visible("socket_a", "gate_F"), Is.False);
                Assert.That(view.GetComponentsInChildren<Canvas>(true), Is.Empty);
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest] public IEnumerator ThirdPersonGateInspectionRespectsDistanceAndOcclusion()
        {
            var level = ComparisonFixture(); level.playerSpawn.x = 5; level.playerSpawn.z = 4;
            runner.LoadLevel(level); runner.ToggleCamera(); yield return new WaitForSeconds(.5f);
            var target = runner.Board.Gates["gate"].transform;
            var view = runner.Board.Circuits;
            var refresh = typeof(StationCircuitView).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var wire = runner.Board.transform.Find("Circuit socket_a to gate").gameObject;
            refresh.Invoke(view, null); Assert.That(wire.activeSelf, Is.True, "Nearby visible gate exposes every source without a label.");
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                blocker.transform.position = Vector3.Lerp(runner.Cameras.Output.transform.position, target.position + Vector3.up * 1.35f, .5f);
                blocker.transform.localScale = Vector3.one * .5f;
                Physics.SyncTransforms(); refresh.Invoke(view, null);
                Assert.That(wire.activeSelf, Is.False, "A wall between camera and device hides its inspection wires.");
                blocker.SetActive(false); Physics.SyncTransforms(); refresh.Invoke(view, null);
                Assert.That(wire.activeSelf, Is.True);
                runner.Board.Robot.transform.position = target.position + Vector3.right * 5;
                refresh.Invoke(view, null); Assert.That(wire.activeSelf, Is.False, "A visible but distant gate is not selected.");
            }
            finally { Object.Destroy(blocker); }
        }
    }
}
