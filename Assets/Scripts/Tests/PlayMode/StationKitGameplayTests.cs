using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Sokoban.Tests
{
    public sealed class StationKitGameplayTests
    {
        private LevelRunner runner;
        [UnitySetUp] public IEnumerator SetUp()
        {
            LevelRunner.PlaytestDefinition = null;
            runner = new GameObject("Station integration test").AddComponent<LevelRunner>();
            yield return null; runner.enabled = false;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (runner) Object.Destroy(runner.gameObject);
            yield return null; LevelRunner.PlaytestDefinition = null;
        }
        private static LevelDefinition Fixture()
        {
            return new LevelDefinition { schemaVersion=2, id="StationKit_Test", title="Station integration", briefing="", completionText="Complete",
                width=9,height=7,gridSize=1,terrainRows=Enumerable.Repeat(".........",7).ToArray(),
                playerSpawn=new PlayerSpawn{x=1,z=3,facing="E"},
                crates=new[]{new CrateDefinition{id="cargo",x=2,z=3,kind=CrateDefinition.Cargo},new CrateDefinition{id="energy",x=7,z=5,kind=CrateDefinition.Energy}},
                sockets=new[]{new SocketDefinition{id="goal",x=6,z=3,isGoal=true}},gates=Array.Empty<GateDefinition>(),decorations=Array.Empty<DecorationDefinition>() };
        }
        private static bool Lamp(Transform obj) => obj.GetComponentInChildren<Renderer>().sharedMaterials
            .Where(m=>m.name.StartsWith("M_StationStatusEmission",StringComparison.Ordinal)).Any(m=>m.IsKeywordEnabled("_EMISSION") && m.GetColor("_EmissionColor").r+m.GetColor("_EmissionColor").g+m.GetColor("_EmissionColor").b>.001f);
        private static Transform Lower(StationGateView gate) => gate.transform.Find("PowerGateRoot/MovingParts/LowerPanel");
        private static IEnumerator WaitFor(Func<bool> predicate)
        {
            float until=Time.realtimeSinceStartup+4;
            while(!predicate() && Time.realtimeSinceStartup<until) yield return null;
            Assert.That(predicate(),Is.True,"Timed out waiting for presentation state.");
        }
        private IEnumerator Move(Direction d)
        {
            Assert.That(runner.TryMove(d),Is.True); yield return WaitFor(()=>!runner.Presenter.Busy);
        }

        [UnityTest] public IEnumerator CampaignAndAuthoringUseAllElevenAssetsWithOriginalData()
        {
            var seen=new HashSet<string>();
            // Small campaign maps need not contain every decorative floor variant.
            foreach(var level in Resources.Load<CampaignCatalog>("configs/CampaignCatalog").ReadLevels().Concat(new[] { Fixture() }))
            {
                string hash=LevelJson.Hash(level);
                runner.LoadLevel(level); yield return null;
                foreach(var t in runner.Board.GetComponentsInChildren<Transform>(true)) if(t.name.EndsWith("Root",StringComparison.Ordinal)) seen.Add(t.name);
                foreach(var c in level.crates)
                {
                    Assert.That(runner.Board.Crates[c.id].Find((c.IsEnergy?"EnergyCrate":"CargoCrate")+"Root"),Is.Not.Null);
                    Assert.That(runner.Board.Crates[c.id].GetComponentsInChildren<Collider>(),Is.Empty);
                }
                Assert.That(LevelJson.Hash(level),Is.EqualTo(hash));
                foreach (var label in runner.Board.GetComponentsInChildren<TextMesh>())
                    Assert.That(label.GetComponent<Renderer>().sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
            }
            foreach(string id in new[]{"EnergyCrate","CargoCrate","GoalSocket","UtilitySocket","PowerGate","FloorPlain","FloorService","FloorGrate","WallStraight","WallCorner","WallEnd"})
                Assert.That(seen.Contains(id+"Root"),Is.True,id);
        }

        [UnityTest] public IEnumerator MixedOccupancyUsesRealAnyAllAndIndependentSourceIndicators()
        {
            var level=Fixture();
            level.sockets=level.sockets.Concat(new[]{new SocketDefinition{id="a",x=7,z=5},new SocketDefinition{id="b",x=2,z=3},new SocketDefinition{id="c",x=6,z=5}}).ToArray();
            level.gates=new[]{
                new GateDefinition{id="any",x=5,z=3,facing="N",powerMode="Any",sourceSocketIds=new[]{"a","b","c"}},
                new GateDefinition{id="all",x=5,z=4,facing="E",powerMode="All",sourceSocketIds=new[]{"a","b"}},
                new GateDefinition{id="shared",x=5,z=5,facing="S",powerMode="Any",sourceSocketIds=new[]{"a"}}};
            runner.LoadLevel(level); yield return null;
            Assert.That(runner.Board.Gates["any"].IsPowered && runner.Board.Gates["any"].IsOpen,Is.True);
            Assert.That(runner.Board.Gates["all"].IsPowered || runner.Board.Gates["all"].IsOpen,Is.False);
            Assert.That(runner.Board.Gates["shared"].IsPowered,Is.True);
            Assert.That(Lamp(runner.Board.transform.Find("a/UtilitySocketRoot/PowerStatus")),Is.True);
            Assert.That(Lamp(runner.Board.transform.Find("b/UtilitySocketRoot/PowerStatus")),Is.False);
            foreach(var gate in runner.Board.Gates.Values)
            {
                var inputs=gate.transform.Find("PowerGateRoot").Cast<Transform>().Where(t=>t.name.StartsWith("Input ",StringComparison.Ordinal)).ToArray();
                foreach(var input in inputs) Assert.That(Lamp(input.Find("SourceBadgeTemplate_SourcePower")),Is.EqualTo(input.name.StartsWith("Input a ",StringComparison.Ordinal)));
            }
            Assert.That(runner.Board.Gates["any"].transform.Find("PowerGateRoot").Cast<Transform>().Count(t=>t.name.StartsWith("Input ",StringComparison.Ordinal)),Is.EqualTo(6));
            var materials=runner.Board.Crates.Values.SelectMany(t=>t.GetComponentsInChildren<Renderer>()).SelectMany(r=>r.sharedMaterials);
            Assert.That(materials.Any(m=>m.name.Contains("LinkAccent") || m.name.Contains("StatusEmission")),Is.False);
        }

        [UnityTest] public IEnumerator CargoRobotEmptyHandoffPausesAndUndoCancelsClosing()
        {
            var level=Fixture(); level.gates=new[]{new GateDefinition{id="gate",x=2,z=3,facing="E",powerMode="Any",sourceSocketIds=new[]{"goal"}}};
            runner.LoadLevel(level); yield return null; var gate=runner.Board.Gates["gate"];
            Assert.That(gate.IsOpen,Is.True); Assert.That(gate.IsPowered,Is.False);
            yield return Move(Direction.E); Assert.That(gate.IsOpen,Is.True); Assert.That(gate.IsPowered,Is.False);
            Assert.That(runner.TryMove(Direction.N),Is.True); yield return WaitFor(()=>gate.Animating);
            runner.SetPaused(true); float height=Lower(gate).localPosition.y; yield return new WaitForSeconds(.25f);
            Assert.That(Lower(gate).localPosition.y,Is.EqualTo(height).Within(.0001f));
            runner.SetPaused(false); yield return new WaitForSeconds(.04f); runner.Undo();
            yield return new WaitForSeconds(.35f);
            Assert.That(runner.Session.State.Player,Is.EqualTo(new Cell(2,3)));
            Assert.That(Lower(gate).localPosition.y,Is.EqualTo(1.305f).Within(.0001f));
            Assert.That(gate.IsPowered,Is.False); Assert.That(gate.Animating,Is.False);
            yield return Move(Direction.N); yield return new WaitForSeconds(.25f);
            Assert.That(gate.IsOpen,Is.False); Assert.That(Lower(gate).localPosition.y,Is.EqualTo(.29f).Within(.0001f));
            runner.Restart(); Assert.That(gate.IsOpen,Is.True); Assert.That(gate.IsPowered,Is.False);
        }

        [UnityTest] public IEnumerator UndoAndRestartDuringPoweredOpeningRestorePoseAndLamps()
        {
            var level=Fixture(); level.crates[0].kind=CrateDefinition.Energy;
            level.sockets=level.sockets.Concat(new[]{new SocketDefinition{id="source",x=3,z=3}}).ToArray();
            level.gates=new[]{new GateDefinition{id="gate",x=5,z=3,facing="N",powerMode="Any",sourceSocketIds=new[]{"source"}}};
            runner.LoadLevel(level); yield return null; var gate=runner.Board.Gates["gate"];
            for(int i=0;i<2;i++)
            {
                Assert.That(runner.TryMove(Direction.E),Is.True); yield return WaitFor(()=>gate.Animating);
                Assert.That(gate.IsPowered,Is.True);
                if(i==0) runner.Undo(); else runner.Restart();
                yield return new WaitForSeconds(.35f);
                Assert.That(gate.IsOpen || gate.IsPowered || gate.Animating,Is.False);
                Assert.That(Lower(gate).localPosition.y,Is.EqualTo(.29f).Within(.0001f));
                Assert.That(Lamp(runner.Board.transform.Find("source/UtilitySocketRoot/PowerStatus")),Is.False);
            }
        }

        [UnityTest] public IEnumerator TopDownHidesWholeUpperKitButLeavesCameraObstaclesAndBases()
        {
            runner.SelectLevel(2); yield return null;
            var walls=runner.Board.GetComponentsInChildren<Transform>().Where(t=>t.name=="Upper").ToArray();
            var colliders=runner.Board.GetComponentsInChildren<Collider>(); var enabled=colliders.Select(c=>c.enabled).ToArray();
            runner.ToggleCamera(); yield return new WaitForSeconds(.35f);
            Assert.That(walls.SelectMany(t=>t.GetComponentsInChildren<Renderer>()).All(r=>!r.enabled),Is.True);
            foreach(var gate in runner.Board.Gates.Values)
            {
                var root=gate.transform.Find("PowerGateRoot");
                Assert.That(root.Find("UpperStructure").GetComponentsInChildren<Renderer>().All(r=>!r.enabled),Is.True);
                Assert.That(root.Find("MovingParts").GetComponentsInChildren<Renderer>().All(r=>!r.enabled),Is.True);
                Assert.That(root.Find("Base").GetComponentsInChildren<Renderer>().All(r=>r.enabled),Is.True);
            }
            CollectionAssert.AreEqual(enabled,colliders.Select(c=>c.enabled).ToArray());
            runner.ToggleCamera(); yield return new WaitForSeconds(.35f);
            Assert.That(walls.SelectMany(t=>t.GetComponentsInChildren<Renderer>()).All(r=>r.enabled),Is.True);
        }

        [UnityTest] public IEnumerator StableIdentitySurvivesReorderedDataAndDoesNotModifySharedBases()
        {
            var level=LevelJson.Read(Resources.Load<TextAsset>("configs/levels/L06").text); runner.LoadLevel(level); yield return null;
            Func<Dictionary<string,Material>> identity=()=>runner.Definition.sockets.ToDictionary(s=>s.id,s=>runner.Board.transform.Find(s.id).GetComponentsInChildren<Renderer>()
                .SelectMany(r=>r.sharedMaterials).First(m=>m.name.StartsWith("M_StationLinkAccent",StringComparison.Ordinal)));
            var before=identity(); var colors=before.ToDictionary(p=>p.Key,p=>p.Value.GetColor("_BaseColor"));
            level.sockets=level.sockets.Reverse().ToArray(); foreach(var g in level.gates) g.sourceSocketIds=g.sourceSocketIds.Reverse().ToArray();
            runner.LoadLevel(level); yield return null;
            foreach(var pair in identity()) { Assert.That(pair.Value,Is.SameAs(before[pair.Key])); Assert.That(pair.Value.GetColor("_BaseColor"),Is.EqualTo(colors[pair.Key])); }
            runner.Restart(); yield return null;
            Assert.That(Object.FindObjectsOfType<BoardView>().Length,Is.EqualTo(1));
        }
    }
}
