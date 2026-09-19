using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Sokoban.Art.Editor
{
    public static class StationKitOccupancyEvidence
    {
        [Serializable] public class OccupancySample { public int frame; public string phase; public bool powered; public float openness; public Vector3 cargoPosition,robotPosition; }
        [Serializable] public class OccupancyReport { public bool passed; public string driver="Editor presentation fixture, all supply conditions remain false"; public int fps=12; public OccupancySample[] frames; public EvidenceCheck[] checks; }

        [MenuItem("Tools/Station Kit/Capture Occupancy Sequence")]
        public static void CaptureOccupancy()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before capture.");
            Checks.Clear(); var original=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            var samples=new OccupancySample[72];
            try
            {
                SetupLighting(scene); var camera=Camera(scene); var bay=new GameObject("Occupancy sequence"); SceneManager.MoveGameObjectToScene(bay,scene);
                for(int x=-1;x<=1;x++) for(int z=-1;z<=2;z++) Spawn("FloorPlain",scene,bay.transform,new Vector3(x,0,z));
                var gate=Spawn("PowerGate",scene,bay.transform,Vector3.zero); Gate(gate,true,false,false,false);
                var cargo=Spawn("CargoCrate",scene,bay.transform,Vector3.zero);
                var robot=Spawn("Robot",scene,bay.transform,Vector3.back); var slide=Find(robot,"PushSlide"); var rest=slide.localPosition; slide.localPosition=rest+Vector3.forward*.24f;
                var label=Text("CARGO HOLDS OPEN / POWER OFF",bay.transform,new Vector3(0,-.22f,-1.8f),.12f).GetComponent<TextMesh>();
                Aim(camera,new Vector3(0,.5f,.35f),new Vector3(3,4,-6),2.25f);
                string frames=Path.GetFullPath("Logs/StationKitFrames/unity_occupancy");
                for(int i=0;i<72;i++)
                {
                    float z=0,x=0,openness=1; string phase;
                    if(i<16) phase="CARGO HOLDS OPEN / POWER OFF";
                    else if(i<36) {z=Mathf.SmoothStep(0,1,(i-16)/19f);phase="CARGO TO ROBOT / POWER OFF";}
                    else if(i<48) {z=1;phase="ROBOT HOLDS OPEN / POWER OFF";}
                    else if(i<60) {z=1;x=Mathf.SmoothStep(0,1,(i-48)/11f);phase="ROBOT LEAVES / POWER OFF";}
                    else {z=1;x=1;openness=1-Mathf.SmoothStep(0,1,(i-60)/11f);phase="EMPTY - CLOSE / POWER OFF";}
                    cargo.transform.position=new Vector3(0,0,z); robot.transform.position=new Vector3(x,0,z-1); label.text=phase;
                    if(i>=48){robot.transform.rotation=Quaternion.Euler(0,90,0);slide.localPosition=rest;}
                    Find(gate,"LowerPanel").localPosition=new Vector3(0,Mathf.Lerp(.29f,1.305f,openness),.045f);
                    Find(gate,"UpperPanel").localPosition=new Vector3(0,Mathf.Lerp(.87f,1.305f,openness),-.045f);
                    Find(gate,"OpenMark").gameObject.SetActive(openness>=.5f); Find(gate,"ClosedMark").gameObject.SetActive(openness<.5f);
                    bool powered=Lamp(Find(gate,"PowerStatus").gameObject);
                    Check(!powered && !Lamp(Find(gate,"SourceBadgeTemplate").gameObject) && !Lamp(Find(gate,"SourceBadge02").gameObject),"frame_"+i+"/all_power_lamps_off");
                    if(i<60) Check(openness==1,"frame_"+i+"/occupied_hold_open");
                    samples[i]=new OccupancySample{frame=i,phase=phase,powered=powered,openness=openness,cargoPosition=cargo.transform.position,robotPosition=robot.transform.position};
                    Capture(camera,i.ToString("D4"),960,frames);
                    if(i==0 || i==40 || i==71) Capture(camera,"unity_occupancy_"+i.ToString("D2"),1280);
                }
                Check(Find(gate,"LowerPanel").localPosition.y==.29f,"empty_gate_closed");
                var report=new OccupancyReport{passed=Checks.All(c=>c.passed),frames=samples,checks=Checks.ToArray()};
                File.WriteAllText("Logs/StationKitFrames/unity_occupancy.json",JsonUtility.ToJson(report,true)+"\n");
                if(!report.passed) throw new InvalidOperationException("Occupancy fixture failed.");
                Debug.Log("Station Kit: cargo -> robot -> empty, 72 frames, all power indicators off.");
            }
            finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene,true); }
        }
        private const string Prefabs = "Assets/Resources/prefabs/gameplay/StationKit/";
        private const string RobotPrefab = "Assets/Resources/prefabs/gameplay/player/Robot.prefab";
        private const string Evidence = "Logs/StationKitFrames";
        [Serializable] public class EvidenceCheck { public string name; public bool passed; public string detail; }
        private static readonly List<EvidenceCheck> Checks = new List<EvidenceCheck>();
        private static void Check(bool passed, string name) => Checks.Add(new EvidenceCheck { name = name, passed = passed });
        private static Transform Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        private static string Role(Material material) => material.name.Contains("StatusEmission") ? "StatusEmission" : material.name.Contains("LinkAccent") ? "LinkAccent" : "Other";
        private static Material Variant(string role, string key)
        {
            string path = "Assets/Material/Station/M_Station" + role + "_" + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) throw new InvalidOperationException("Occupancy evidence requires existing material: " + path);
            return material;
        }
        static GameObject Spawn(string id,Scene scene,Transform parent,Vector3 p,float yaw=0)
        {
            string path=id=="Robot"?RobotPrefab:Prefabs+id+".prefab";
            var o=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);
            o.transform.SetParent(parent,false); o.transform.localPosition=p; o.transform.localRotation=Quaternion.Euler(0,yaw,0);
            foreach(var t in o.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=31;
            if(id=="Robot")
            {
                var animator=o.GetComponent<Animator>(); if(animator) animator.enabled=false;
                var idle=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Robot/Meshes/Robot.fbx").OfType<AnimationClip>().First(c=>c.name=="Idle"); idle.SampleAnimation(o,0);
            }
            return o;
        }
        static GameObject Text(string value,Transform parent,Vector3 p,float size)
        {
            var o=new GameObject("Label "+value); o.transform.SetParent(parent,false); o.transform.localPosition=p; o.transform.localRotation=Quaternion.Euler(90,0,0); o.layer=31;
            var t=o.AddComponent<TextMesh>(); t.text=value; t.characterSize=size*10f/64f; t.fontSize=64; t.anchor=TextAnchor.MiddleCenter; t.color=new Color(.80f,.87f,.90f);
            return o;
        }
        static Camera Camera(Scene scene)
        {
            var go=new GameObject("Station Kit Evidence Camera"); SceneManager.MoveGameObjectToScene(go,scene);
            var camera=go.AddComponent<Camera>(); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color32(32,42,53,255);
            camera.nearClipPlane=.02f; camera.farClipPlane=300; camera.cullingMask=1<<31; camera.fieldOfView=55;
            camera.allowHDR=false; camera.allowMSAA=true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            return camera;
        }
        static void Aim(Camera c,Vector3 target,Vector3 offset,float size=0)
        {
            c.transform.position=target+offset; c.transform.LookAt(target,Vector3.up); c.orthographic=size>0; c.orthographicSize=size;
            if(offset.x==0 && offset.z==0) c.transform.rotation=Quaternion.Euler(90,0,0);
        }
        static void Capture(Camera camera,string name,int width=1920,string outputDirectory=null)
        {
            string target=outputDirectory??Evidence; Directory.CreateDirectory(target); int height=width*9/16;
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=4}; var previous=RenderTexture.active;
            var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt; camera.aspect=(float)width/height; camera.Render(); RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,width,height),0,0); tex.Apply();
                File.WriteAllBytes(Path.Combine(target,name+".png"),tex.EncodeToPNG());
            }
            finally { camera.targetTexture=null; RenderTexture.active=previous; Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); }
        }
        static void SetupLighting(Scene scene)
        {
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.22f,.25f,.30f); RenderSettings.ambientIntensity=1;
            var o=new GameObject("Station Key Light"); SceneManager.MoveGameObjectToScene(o,scene); o.transform.rotation=Quaternion.Euler(42,-28,0);
            var l=o.AddComponent<Light>(); l.type=LightType.Directional; l.intensity=.65f; l.shadows=LightShadows.Soft; l.cullingMask=1<<31;
        }
        private static void Style(GameObject obj, string key, bool power)
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
        private static void Gate(GameObject root, bool open, bool powered, bool a = false, bool b = false)
        {
            Find(root, "LowerPanel").localPosition = new Vector3(0, open ? 1.305f : .29f, .045f);
            Find(root, "UpperPanel").localPosition = new Vector3(0, open ? 1.305f : .87f, -.045f);
            Find(root, "OpenMark").gameObject.SetActive(open); Find(root, "ClosedMark").gameObject.SetActive(!open);
            Style(Find(root, "PowerStatus").gameObject, "Blue", powered);
            Style(Find(root, "SourceBadgeTemplate").gameObject, "Blue", a);
            Style(Find(root, "SourceBadge02").gameObject, "Purple", b);
        }
        private static bool Lamp(GameObject obj) => obj.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>Role(m)=="StatusEmission").All(m=>m.name.EndsWith("_On",StringComparison.Ordinal));
    }
}
