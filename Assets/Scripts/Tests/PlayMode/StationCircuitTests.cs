using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Sokoban.Tests
{
    public sealed class StationCircuitTests
    {
        private LevelRunner runner;
        public static LevelDefinition ComparisonFixture() => new LevelDefinition
        {
            schemaVersion=2, id="CircuitComparison", title="目标与供电 · 视觉验证", briefing="S 或 A 通电即可打开 G1；圆环表示通关目标。", completionText="完成",
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
            runner=new GameObject("Circuit test runner").AddComponent<LevelRunner>(); yield return null;
            runner.enabled=false;
        }
        [UnityTearDown] public IEnumerator TearDown()
        { if(runner) Object.Destroy(runner.gameObject); yield return null; LevelRunner.PlaytestDefinition=null; }
        private Transform Mark(string id,string name) => runner.Board.transform.Find(id+"/Circuit socket/"+name);
        private bool Lit(string id) => Mark(id,"Socket state fill").GetComponent<Renderer>().enabled;
        private Text Caption(string id) => runner.Board.transform.Find("Circuit labels/"+id+" circuit label/Caption").GetComponent<Text>();
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
            Assert.That(Caption("gate").text,Is.EqualTo("任一 · 1/2"));
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
            Assert.That(Caption("gate").text,Does.Contain("占据保开").And.Contain("0/2"));
            var passage=runner.Board.Gates["gate"].transform.Find("Circuit passage");
            Assert.That(passage.Find("Open passage").gameObject.activeInHierarchy,Is.True);
            Assert.That(passage.Find("Closed barrier").gameObject.activeSelf,Is.False);
            Assert.That(passage.Find("Gate condition").GetComponent<Renderer>().sharedMaterial.name,Does.EndWith("Idle"));
            Assert.That(passage.Find("Occupied hold").gameObject.activeSelf,Is.True);
        }

        [UnityTest] public IEnumerator LabelsStayUprightAndDoNotInterceptInputOrLeakOnRebuild()
        {
            for(int i=0;i<3;i++)
            {
                runner.LoadLevel(ComparisonFixture()); yield return new WaitForSeconds(.35f);
                var texts=runner.Board.Circuits.GetComponentsInChildren<Text>();
                Assert.That(texts.Length,Is.EqualTo(1), "Only the compact gate condition remains; sockets have no text labels.");
                foreach(var text in texts)
                {
                    Assert.That(text.fontSize,Is.GreaterThanOrEqualTo(14)); Assert.That(text.raycastTarget,Is.False);
                    Assert.That(Quaternion.Angle(text.transform.rotation,Quaternion.identity),Is.LessThan(.01f));
                }
                Assert.That(runner.Board.Circuits.GetComponentsInChildren<GraphicRaycaster>(),Is.Empty);
                Assert.That(Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.name.StartsWith("Station circuit ",StringComparison.Ordinal)),Is.EqualTo(6));
                Assert.That(Object.FindObjectsOfType<StationCircuitView>().Length,Is.EqualTo(1));
                runner.ToggleCamera(); yield return new WaitForSeconds(.35f);
                Assert.That(runner.Board.Gates["gate"].transform.Find("Circuit passage").gameObject.activeSelf,Is.True,
                    "Low passage and connection marks stay available when a gate's upper structure is occluded.");
            }
        }
    }
}
