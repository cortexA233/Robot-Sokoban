using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Sokoban.Tests
{
    public sealed class ArtRefinementTests
    {
        private LevelRunner runner;
        [UnitySetUp] public IEnumerator SetUp()
        {
            LevelRunner.PlaytestDefinition = null;
            runner = new GameObject("Art refinement test").AddComponent<LevelRunner>();
            runner.Progress = new PlayerProgress(() => null, _ => { });
            yield return null; runner.enabled = false;
            runner.LoadLevel(LevelJson.Read(Resources.Load<TextAsset>("configs/levels/L12").text));
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (runner) Object.Destroy(runner.gameObject);
            LevelRunner.PlaytestDefinition = null; yield return null;
        }

        [UnityTest] public IEnumerator AuthoredTransportMeshesKeepUnitScaleAndUnobstructedCells()
        {
            var theme = Resources.Load<StationKitTheme>("configs/StationKitTheme");
            foreach (string id in new[] { "RedirectorPlate", "LowFrictionDeck" })
            {
                var prefab = theme.Prefab(id);
                Assert.That(prefab.GetComponentsInChildren<Collider>(), Is.Empty);
                Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(prefab.transform.Find(id + "Root").localRotation, Is.EqualTo(Quaternion.identity));
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
                {
                    Assert.That(filter.sharedMesh.vertexCount, Is.GreaterThan(0));
                    Assert.That(filter.sharedMesh.uv.Length, Is.EqualTo(filter.sharedMesh.vertexCount));
                    Assert.That(filter.sharedMesh.tangents.Length, Is.EqualTo(filter.sharedMesh.vertexCount));
                }
            }
            Assert.That(runner.Board.GetComponentsInChildren<Transform>().Any(t => t.name == "LowFrictionDeckRoot"), Is.True);
            foreach (var view in runner.Board.Redirectors.Values)
            {
                Assert.That(view.GetComponentsInChildren<Collider>(), Is.Empty);
                Assert.That(view.transform.Find("Directional plate/RedirectorPlateRoot"), Is.Not.Null);
                var arrows = view.transform.Find("Direction and edge arrows").GetComponent<MeshFilter>().sharedMesh;
                Assert.That(arrows.bounds.max.y, Is.LessThan(.008f));
                Assert.That(arrows.bounds.size.x, Is.GreaterThan(.8f), "Box-covered edge arrows must remain visible.");
            }
            yield return null;
        }

        [UnityTest] public IEnumerator SocketHousingRemainsVisibleWhileLegacySymbolsStayHidden()
        {
            foreach (var socket in runner.Definition.sockets)
            {
                string root = socket.isGoal ? "GoalSocketRoot" : "UtilitySocketRoot";
                var model = runner.Board.transform.Find(socket.id + "/" + root);
                Assert.That(model.Find("Geometry").GetComponentsInChildren<Renderer>().All(r => r.enabled), Is.True);
                foreach (string part in new[] { "TypeMarker", "LinkMarkers", "PowerStatus" })
                    Assert.That(model.Find(part).GetComponentsInChildren<Renderer>().All(r => !r.enabled), Is.True);
                Assert.That(runner.Board.transform.Find(socket.id + "/Circuit socket/Socket state fill"), Is.Not.Null);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator ArtEffectsAreOwnedByOneLevelAndDoNotChangeSceneLightingSettings()
        {
            var skybox = RenderSettings.skybox;
            var ambientMode = RenderSettings.ambientMode;
            var profile = Resources.Load<StationKitTheme>("configs/StationKitTheme").artPostProcessing;
            for (int i = 0; i < 3; i++)
            {
                runner.SelectLevel(i); yield return null;
                Assert.That(Object.FindObjectsOfType<Volume>().Count(v => v.name == "Station art volume"), Is.EqualTo(1));
                Assert.That(Object.FindObjectsOfType<Light>().Count(l => l.name == "Station soft fill"), Is.EqualTo(1));
                var volume = runner.Cameras.GetComponentInChildren<Volume>();
                Assert.That(volume.sharedProfile, Is.SameAs(profile));
                Assert.That(volume.HasInstantiatedProfile(), Is.False);
                var data = runner.Cameras.Output.GetUniversalAdditionalCameraData();
                Assert.That(data.renderPostProcessing, Is.True);
                Assert.That(data.antialiasing, Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
                foreach (var camera in Object.FindObjectsOfType<Camera>().Where(c => c.name == "Menu background camera"))
                    Assert.That(camera.GetUniversalAdditionalCameraData().volumeLayerMask.value & (1 << volume.gameObject.layer), Is.Zero);
                Assert.That(RenderSettings.skybox, Is.SameAs(skybox));
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(ambientMode));
            }
            Object.Destroy(runner.gameObject); runner = null; yield return null;
            Assert.That(Object.FindObjectsOfType<Volume>().Any(v => v.name == "Station art volume"), Is.False);
            Assert.That(Object.FindObjectsOfType<Light>().Any(l => l.name == "Station soft fill"), Is.False);
        }
    }
}
