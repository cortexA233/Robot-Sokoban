using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Sokoban.Art.Editor
{
    /// <summary>Asset-only import, measured contracts and isolated presentation fixtures.</summary>
    public static class StationKitAssetTools
    {
        public const string Models = "Assets/Art/StationKit/Meshes/";
        public const string Prefabs = "Assets/Resources/prefabs/gameplay/StationKit/";
        public const string Materials = "Assets/Material/Station/";
        public static readonly string[] Ids = { "EnergyCrate", "CargoCrate", "GoalSocket", "UtilitySocket", "PowerGate", "FloorPlain", "FloorService", "FloorGrate", "WallStraight", "WallCorner", "WallEnd" };
        static readonly string[] MaterialNames = { "Body", "CargoBody", "CargoBrace", "Dark", "Metal", "Floor", "LinkAccent", "StatusEmission", "Symbol", "EnergyWindow" };
        static readonly string[] Colors = { "#D9DFE5", "#796653", "#C4AC81", "#202A35", "#83939F", "#657580", "#FFFFFF", "#273D40", "#F2F5EC", "#99DAE1" };
        static readonly float[] Metallic = { .12f, .05f, .25f, .18f, .65f, .15f, 0, 0, 0, .1f };
        static readonly float[] Roughness = { .48f, .67f, .54f, .62f, .38f, .72f, .5f, .45f, .55f, .3f };
        public static string Source => Path.GetFullPath("ArtSource/StationKit");
        public static string Evidence => Path.Combine(Source, "previews");

        [Serializable] public class Check { public string name; public bool passed; public string detail; }
        [Serializable] public class Slot { public int slot; public string material; public string role; }
        [Serializable] public class MaterialRecord { public string name,path,shader,baseColorSrgb; public float metallic,smoothness; public Color emissionColorLinear; public bool emissionKeyword; }
        [Serializable] public class RendererRecord { public string path; public Slot[] materials; public int triangles; }
        [Serializable] public class AssetRecord
        {
            public string assetId, fbxSha256, rootPath;
            public Vector3 min, max, size, rootPosition, rootRotation, rootScale;
            public int triangles, rendererCount, meshCount;
            public RendererRecord[] renderers;
        }
        [Serializable] public class Report
        {
            public string protocol = "station-kit-v1", unityVersion, checkedAt;
            public bool passed;
            public AssetRecord[] assets;
            public MaterialRecord[] materials;
            public Check[] checks;
            public string stateDriver = "Explicit editor presentation fixtures; no BoardView or rule changes.";
        }
        static readonly List<Check> Checks = new List<Check>();
        static void CheckThat(bool ok, string name, string detail = "") => Checks.Add(new Check { name = name, passed = ok, detail = detail });
        public static Color ColorOf(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        public static string Hash(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        public static void Backup(string path)
        {
            if (!File.Exists(path)) return;
            string target = Path.Combine("Logs/StationKitBackups", Hash(path).Substring(0,12), path);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (!File.Exists(target)) File.Copy(path,target);
        }
        public static void Folder(string path)
        {
            path = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        public static Transform Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        public static string Role(Material material)
        {
            string n = material.name;
            if (n.Contains("LinkAccent")) return "LinkAccent";
            if (n.Contains("StatusEmission")) return "StatusEmission";
            if (n.Contains("EnergyWindow")) return "FixedEnergy";
            if (n.Contains("Symbol")) return "Symbol";
            if (n.Contains("Dark")) return "Dark";
            if (n.Contains("Metal")) return "Metal";
            return "Body";
        }
        static Material SaveMaterial(string name, Color color, float metallic, float roughness, float emission)
        {
            string path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) Backup(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", 1 - roughness);
            // URP's material validator removes _EMISSION when EmissiveIsBlack is set.
            m.globalIlluminationFlags = emission > 0 ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (emission > 0) m.EnableKeyword("_EMISSION"); else m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color.linear * emission); EditorUtility.SetDirty(m);
            return m;
        }
        public static Material Variant(string role, string key)
        {
            string name = "M_Station" + role + "_" + key;
            var existing = AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
            if (existing) return existing;
            string color = key == "Blue" ? "#559CE8" : key == "Purple" ? "#B88CDF" : key == "Lime" ? "#C2D969" : key == "On" ? "#BDF0D8" : "#273D40";
            return SaveMaterial(name, ColorOf(color), 0, .5f, key == "On" ? .65f : 0);
        }
        public static void Style(GameObject obj, string key, bool power)
        {
            foreach (var r in obj.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var role = Role(mats[i]);
                    if (role == "LinkAccent") mats[i] = Variant(role, key);
                    else if (role == "StatusEmission") mats[i] = Variant(role, power ? "On" : "Off");
                }
                r.sharedMaterials = mats;
            }
        }
        public static void Gate(GameObject root, bool open, bool powered, bool a = false, bool b = false)
        {
            Find(root, "LowerPanel").localPosition = new Vector3(0, open ? 1.305f : .29f, .045f);
            Find(root, "UpperPanel").localPosition = new Vector3(0, open ? 1.305f : .87f, -.045f);
            Find(root, "OpenMark").gameObject.SetActive(open); Find(root, "ClosedMark").gameObject.SetActive(!open);
            Style(Find(root, "PowerStatus").gameObject, "Blue", powered);
            Style(Find(root, "SourceBadgeTemplate").gameObject, "Blue", a);
            Style(Find(root, "SourceBadge02").gameObject, "Purple", b);
        }
        public static void TopDown(GameObject obj, bool top)
        {
            foreach (var t in obj.GetComponentsInChildren<Transform>(true))
                if (t.name == "Upper" || t.name == "UpperStructure" || t.name == "MovingParts")
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = !top;
        }
        public static Bounds BoundsOf(GameObject obj)
        {
            var p = obj.GetComponentsInChildren<MeshFilter>(true).SelectMany(m => m.sharedMesh.vertices.Select(v => obj.transform.InverseTransformPoint(m.transform.TransformPoint(v)))).ToArray();
            var b = new Bounds(p[0], Vector3.zero); foreach (var v in p) b.Encapsulate(v); return b;
        }
        static AssetRecord Measure(GameObject obj, string id)
        {
            var bound = BoundsOf(obj); var root = Find(obj, id + "Root");
            var renderers = obj.GetComponentsInChildren<MeshRenderer>(true);
            var record = new AssetRecord { assetId = id, fbxSha256 = Hash(Models + id + ".fbx"), rootPath = AnimationUtility.CalculateTransformPath(root, obj.transform),
                min = bound.min, max = bound.max, size = bound.size, rootPosition = root.localPosition, rootRotation = root.localEulerAngles, rootScale = root.localScale,
                meshCount = obj.GetComponentsInChildren<MeshFilter>(true).Length, rendererCount = renderers.Length,
                triangles = obj.GetComponentsInChildren<MeshFilter>(true).Sum(m => m.sharedMesh.triangles.Length / 3),
                renderers = renderers.Select(r => new RendererRecord { path = AnimationUtility.CalculateTransformPath(r.transform, root), triangles = r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3,
                    materials = r.sharedMaterials.Select((m, i) => new Slot { slot = i, material = m.name, role = Role(m) }).ToArray() }).ToArray() };
            CheckThat(root.localPosition.sqrMagnitude < 1e-8 && Quaternion.Angle(root.localRotation, Quaternion.identity) < .01f && (root.localScale - Vector3.one).sqrMagnitude < 1e-8, id + "/identity_root");
            CheckThat(renderers.All(r => r.sharedMaterials.All(m => m && m.shader.name == "Universal Render Pipeline/Lit")), id + "/urp_materials");
            CheckThat(!obj.GetComponentsInChildren<Collider>(true).Any() && !obj.GetComponentsInChildren<Rigidbody>(true).Any() && !obj.GetComponentsInChildren<Animator>(true).Any(), id + "/no_rule_or_physics_components");
            if (id.EndsWith("Crate"))
            {
                CheckThat((bound.size - Vector3.one * .8f).magnitude < .005f && Mathf.Abs(bound.min.y) < .001f, id + "/cube_dimensions", bound.ToString());
                CheckThat(Find(obj, "PushContact_N").localPosition == new Vector3(0,.4f,.4f) && Find(obj, "PushContact_E").localPosition == new Vector3(.4f,.4f,0), id + "/axes_N_E");
                CheckThat(!renderers.SelectMany(r => r.sharedMaterials).Any(m => Role(m) == "LinkAccent" || Role(m) == "StatusEmission"), id + "/fixed_material_semantics");
            }
            if (id.StartsWith("Floor")) CheckThat((bound.size - new Vector3(.98f,.24f,.98f)).magnitude < .001f && Mathf.Abs(bound.max.y) < .001f, id + "/deck_contract");
            if (id.StartsWith("Wall")) CheckThat((bound.size - new Vector3(.98f,1.54f,.98f)).magnitude < .001f, id + "/wall_contract");
            return record;
        }

        static bool TriangleBox(Vector3 a, Vector3 b, Vector3 c, Bounds bounds)
        {
            a -= bounds.center; b -= bounds.center; c -= bounds.center;
            Vector3[] edges = { b-a, c-b, a-c }, unit = { Vector3.right, Vector3.up, Vector3.forward };
            var axes = new List<Vector3>(unit) { Vector3.Cross(edges[0], edges[1]) };
            foreach (var e in edges) foreach (var u in unit) axes.Add(Vector3.Cross(e,u));
            foreach (var axis in axes)
            {
                if (axis.sqrMagnitude < 1e-14f) continue;
                float p = Vector3.Dot(a,axis), q = Vector3.Dot(b,axis), r = Vector3.Dot(c,axis);
                float radius = Vector3.Dot(new Vector3(Mathf.Abs(axis.x),Mathf.Abs(axis.y),Mathf.Abs(axis.z)),bounds.extents);
                if (Mathf.Min(p,Mathf.Min(q,r)) > radius+1e-6f || Mathf.Max(p,Mathf.Max(q,r)) < -radius-1e-6f) return false;
            }
            return true;
        }
        static int Intersections(GameObject obj, Bounds sweep)
        {
            int hits = 0;
            foreach (var mf in obj.GetComponentsInChildren<MeshFilter>(true))
            {
                var matrix = obj.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var points = mf.sharedMesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray(); var tri = mf.sharedMesh.triangles;
                for (int i = 0; i < tri.Length; i += 3)
                {
                    var a=points[tri[i]]; var b=points[tri[i+1]]; var c=points[tri[i+2]];
                    var mn=Vector3.Min(a,Vector3.Min(b,c)); var mx=Vector3.Max(a,Vector3.Max(b,c));
                    if (mx.x<sweep.min.x || mn.x>sweep.max.x || mx.y<sweep.min.y || mn.y>sweep.max.y || mx.z<sweep.min.z || mn.z>sweep.max.z) continue;
                    if (TriangleBox(a,b,c,sweep)) hits++;
                }
            }
            return hits;
        }
        static void Sweeps(GameObject obj, string id)
        {
            if (id == "PowerGate") Gate(obj, true, false);
            foreach (var subject in new[] { "EnergyCrate", "CargoCrate", "Robot" })
                for (int axis = 0; axis < 2; axis++)
                {
                    float w = subject == "Robot" ? .70f : .8008f, d = subject == "Robot" ? .72f : .8008f;
                    var box = new Bounds(new Vector3(0,.402f,0), new Vector3(axis==0?2.82f:w,.798f,axis==1?2.82f:d));
                    int hits = Intersections(obj,box);
                    CheckThat(hits==0,id+"/"+subject+"_"+(axis==0?"EW":"NS")+"_sweep",hits+" intersecting triangles; 3 mm graphic overlay tolerance");
                }
            if (id == "PowerGate") Gate(obj, false, false);
        }
        static void ConfigureImport()
        {
            Folder(Materials); Folder(Prefabs);
            var mats = new Dictionary<string,Material>();
            for (int i=0;i<MaterialNames.Length;i++)
            {
                string name="M_Station"+MaterialNames[i]; mats[name]=SaveMaterial(name,ColorOf(Colors[i]),Metallic[i],Roughness[i],i==9?.35f:0);
            }
            foreach (var id in Ids)
            {
                string path=Models+id+".fbx"; AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale=1; importer.useFileScale=true; importer.bakeAxisConversion=true; importer.preserveHierarchy=true;
                importer.importAnimation=false; importer.importCameras=false; importer.importLights=false; importer.addCollider=false;
                importer.isReadable=true; importer.meshCompression=ModelImporterMeshCompression.Off;
                foreach(var pair in mats) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);
                importer.SaveAndReimport();
            }
        }

        // [MenuItem("Tools/Station Kit/Import and Validate Assets")]
        public static void ImportAndValidate()
        {
            Checks.Clear(); ConfigureImport(); var records=new List<AssetRecord>(); Scene scene=EditorSceneManager.NewPreviewScene();
            try
            {
                foreach(var id in Ids)
                {
                    var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Models+id+".fbx"),scene);
                    obj.name=id;
                    if(id.EndsWith("Socket") || id=="PowerGate") Sweeps(obj,id);
                    if(id=="PowerGate") Gate(obj,false,false);
                    // Inventory the same default materials and pose that the prefab saves.
                    records.Add(Measure(obj,id));
                    Backup(Prefabs+id+".prefab"); PrefabUtility.SaveAsPrefabAsset(obj,Prefabs+id+".prefab"); Object.DestroyImmediate(obj);
                }
                var a=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"GoalSocket.prefab"),scene);
                var b=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"GoalSocket.prefab"),scene);
                var beforeB=b.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).ToArray();
                var beforeBody=a.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>Role(m)=="Body").ToArray();
                Style(a,"Lime",true);
                CheckThat(beforeB.SequenceEqual(b.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials)),"instance_isolation/B_unchanged");
                CheckThat(beforeBody.SequenceEqual(a.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>Role(m)=="Body")),"instance_isolation/fixed_body_unchanged");
                Object.DestroyImmediate(a); Object.DestroyImmediate(b);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.SaveAssets();
            var materialRecords=AssetDatabase.FindAssets("t:Material",new[]{Materials.TrimEnd('/')}).Select(guid=>
            {
                var path=AssetDatabase.GUIDToAssetPath(guid); var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                return new MaterialRecord{name=m.name,path=path,shader=m.shader.name,baseColorSrgb="#"+ColorUtility.ToHtmlStringRGB(m.GetColor("_BaseColor")),metallic=m.GetFloat("_Metallic"),smoothness=m.GetFloat("_Smoothness"),emissionColorLinear=m.GetColor("_EmissionColor"),emissionKeyword=m.IsKeywordEnabled("_EMISSION")};
            }).ToArray();
            var report=new Report{unityVersion=Application.unityVersion,checkedAt=DateTime.UtcNow.ToString("o"),assets=records.ToArray(),materials=materialRecords,checks=Checks.ToArray(),passed=Checks.All(c=>c.passed)};
            File.WriteAllText(Path.Combine(Source,"validation/unity_validation.json"),JsonUtility.ToJson(report,true)+"\n");
            if(!report.passed) throw new InvalidOperationException("Station kit checks failed: "+string.Join(", ",Checks.Where(c=>!c.passed).Select(c=>c.name)));
            Debug.Log("Station Kit: "+Checks.Count+" import/geometry/material checks passed; 11 prefabs saved.");
        }
    }
}
