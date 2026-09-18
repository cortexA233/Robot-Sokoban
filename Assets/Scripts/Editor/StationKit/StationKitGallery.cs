using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using A = Sokoban.Art.Editor.StationKitAssetTools;

namespace Sokoban.Art.Editor
{
    /// <summary>Reproducible presentation fixtures, not a second gameplay controller.</summary>
    public static partial class StationKitGallery
    {
        const string ScenePath="Assets/Scenes/StationKitPreview.unity";
        [Serializable] public class Item { public string assetId,color; public Vector3 position; public float yaw; public bool powered,open,a,b,top,extraBadge,push; public int shape; }
        [Serializable] public class Label { public string text; public Vector3 position; public float size; }
        [Serializable] public class Sheet { public string name; public Item[] objects; public Label[] labels; }
        [Serializable] public class Layout { public Sheet[] sheets; }
        [Serializable] public class PresentationReport { public string driver="Explicit demonstration states, no formal game integration"; public bool passed; public A.Check[] checks; public string[] captures; }
        static readonly List<A.Check> Checks=new List<A.Check>();
        static readonly List<string> Captures=new List<string>();
        static void Check(bool yes,string name,string detail="") => Checks.Add(new A.Check{name=name,passed=yes,detail=detail});
        static string RobotPrefab="Assets/Resources/prefabs/gameplay/player/Robot.prefab";

        static GameObject Spawn(string id,Scene scene,Transform parent,Vector3 p,float yaw=0)
        {
            string path=id=="Robot"?RobotPrefab:A.Prefabs+id+".prefab";
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
        static void Shape(GameObject socket,int shape)
        {
            if(shape==0) return;
            var mf=A.Find(socket,"IdentityBands").GetComponent<MeshFilter>(); var original=mf.sharedMesh;
            string path=A.Models+"SocketIdentity"+(shape==1?"Triangle":"Circle")+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh)
            {
                mesh=Object.Instantiate(original); mesh.name="SocketIdentity"+(shape==1?"Triangle":"Circle");
                var vertices=mesh.vertices.ToList(); var indices=new List<int>();
                for(int side=0;side<4;side++)
                {
                    var rot=Quaternion.Euler(0,side*90,0); var center=new Vector3(-.345f,.002f,.438f); int count=shape==1?3:20;
                    int first=vertices.Count;
                    for(int i=0;i<count;i++)
                    {
                        float angle=2*Mathf.PI*i/count+Mathf.PI/2;
                        vertices.Add(rot*(center+new Vector3(Mathf.Cos(angle)*.017f,0,Mathf.Sin(angle)*.017f)));
                    }
                    for(int i=1;i<count-1;i++){indices.Add(first);indices.Add(first+i+1);indices.Add(first+i);}
                }
                mesh.SetVertices(vertices); int dark=Array.FindIndex(mf.GetComponent<Renderer>().sharedMaterials,m=>A.Role(m)=="Dark");
                mesh.SetTriangles(indices,dark); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh,path);
            }
            mf.sharedMesh=mesh;
        }
        static bool Lamp(GameObject obj) => obj.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>A.Role(m)=="StatusEmission").All(m=>m.name.EndsWith("_On",StringComparison.Ordinal));
        static void ExtraBadge(GameObject gate)
        {
            var template=A.Find(gate,"SourceBadgeTemplate");
            var extra=Object.Instantiate(template.gameObject,template.parent); extra.name="SourceBadge03";
            extra.transform.localPosition=A.Find(gate,"SourceBadgeAnchor").localPosition; A.Style(extra,"Lime",false);
            // The extra identity is a circle; reuse the circle glyph on a separate readable label.
            Text("C",extra.transform,new Vector3(0,.010f,0),.043f);
            Check(extra.GetComponentsInChildren<Renderer>().Any() && extra!=template.gameObject,"A06/extra_independent_badge");
        }
        static Camera Camera(Scene scene)
        {
            var go=new GameObject("Station Kit Evidence Camera"); SceneManager.MoveGameObjectToScene(go,scene);
            var camera=go.AddComponent<Camera>(); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=A.ColorOf("#202A35");
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
        static void Capture(Camera camera,string name,int width=1920,bool gray=false,string outputDirectory=null)
        {
            string target=outputDirectory??A.Evidence; Directory.CreateDirectory(target); int height=width*9/16;
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=4}; var previous=RenderTexture.active;
            var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt; camera.aspect=(float)width/height; camera.Render(); RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,width,height),0,0); tex.Apply();
                if(gray) { var pixels=tex.GetPixels(); for(int i=0;i<pixels.Length;i++) {float g=pixels[i].grayscale; pixels[i]=new Color(g,g,g,1);} tex.SetPixels(pixels);tex.Apply(); }
                File.WriteAllBytes(Path.Combine(target,name+".png"),tex.EncodeToPNG()); if(outputDirectory==null) Captures.Add("ArtSource/StationKit/previews/"+name+".png");
            }
            finally { camera.targetTexture=null; RenderTexture.active=previous; Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); }
        }
        static void SetupLighting(Scene scene)
        {
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.22f,.25f,.30f); RenderSettings.ambientIntensity=1;
            var o=new GameObject("Station Key Light"); SceneManager.MoveGameObjectToScene(o,scene); o.transform.rotation=Quaternion.Euler(42,-28,0);
            var l=o.AddComponent<Light>(); l.type=LightType.Directional; l.intensity=.65f; l.shadows=LightShadows.Soft; l.cullingMask=1<<31;
        }
        static void FixtureChecks(Transform bay,Sheet sheet)
        {
            var roots=bay.Cast<Transform>().Where(t=>!t.name.StartsWith("Label ",StringComparison.Ordinal)).ToArray();
            if(sheet.name=="Sockets")
            {
                var sockets=roots.Where(t=>t.name.EndsWith("Socket",StringComparison.Ordinal)).ToArray();
                Check(sockets.Length==6,"A04/six_occupancy_fixtures");
                for(int i=0;i<sockets.Length;i++) Check(Lamp(sockets[i].gameObject)==(i%3==1),"A04/socket_"+i+"_only_energy_powers");
            }
            if(sheet.name=="Connections")
            {
                var gates=roots.Where(t=>t.name=="PowerGate").ToArray();
                for(int i=0;i<gates.Length;i++)
                {
                    var g=gates[i].gameObject;
                    Check(Lamp(A.Find(g,"SourceBadgeTemplate").gameObject) && !Lamp(A.Find(g,"SourceBadge02").gameObject),"A06/gate_"+i+"_A_on_B_cargo_off");
                    Check((A.Find(g,"LowerPanel").localPosition.y>1)==(i!=1),"A06/Any_All_"+i);
                }
            }
            if(sheet.name=="Corridor")
            {
                var upper=bay.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="FrameAndCassette" || r.name=="WallBody" || r.name.EndsWith("PanelMesh",StringComparison.Ordinal)).ToArray();
                Check(upper.All(r=>!r.enabled),"A08/all_upper_renderers_hidden");
                Check(bay.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Plinth" || r.name=="CornerFeet").All(r=>r.enabled),"A08/low_boundaries_remain");
            }
        }
        // [MenuItem("Tools/Station Kit/Build and Capture Gallery")]
        public static void BuildAndCapture()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before asset gallery validation.");
            Checks.Clear(); Captures.Clear(); var original=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            try
            {
                SetupLighting(scene); var camera=Camera(scene);
                var layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Path.Combine(A.Source,"gallery_layout.json")));
                int index=0;
                foreach(var sheet in layout.sheets)
                {
                    var bay=new GameObject(sheet.name); SceneManager.MoveGameObjectToScene(bay,scene); bay.transform.position=Vector3.right*(index++*25);
                    foreach(var item in sheet.objects)
                    {
                        var o=Spawn(item.assetId,scene,bay.transform,item.position,item.yaw);
                        if(item.assetId.EndsWith("Socket")) { A.Style(o,item.color,item.powered); Shape(o,item.shape); }
                        if(item.assetId=="PowerGate")
                        {
                            A.Gate(o,item.open,item.powered,item.a,item.b); if(item.extraBadge) ExtraBadge(o);
                            var anchor=A.Find(o,"PowerModeAnchor"); Text(sheet.name=="Connections" && !item.open?"ALL":"ANY",anchor,Vector3.up*.003f,.045f);
                        }
                        if(item.assetId=="Robot" && item.push) A.Find(o,"PushSlide").localPosition+=Vector3.forward*.24f;
                        if(item.top) A.TopDown(o,true);
                    }
                    foreach(var label in sheet.labels)
                    {
                        var position=label.position;
                        if(!label.text.StartsWith("STATION",StringComparison.Ordinal)) position.z-=sheet.name=="Crates"?2.5f:1.32f;
                        Text(label.text,bay.transform,position,label.size);
                    }
                    FixtureChecks(bay.transform,sheet);
                    var center=bay.transform.position;
                    float size=sheet.name=="Overview"?3.15f:sheet.name=="Joins"?3.7f:sheet.name=="Corridor"?3.2f:2.4f;
                    var target=center+new Vector3(sheet.name=="Joins"?1:0,.3f,sheet.name=="Connections"?.5f:0);
                    Aim(camera,target,new Vector3(4,7,-8),size);
                    if(sheet.name=="Corridor") Aim(camera,center+new Vector3(.5f,0,0),Vector3.up*10,3.1f);
                    Capture(camera,"unity_"+sheet.name.ToLowerInvariant()+"_1080");
                    if(sheet.name=="Overview" || sheet.name=="Corridor" || sheet.name=="Sockets")
                    {
                        Capture(camera,"unity_"+sheet.name.ToLowerInvariant()+"_720",1280);
                        Capture(camera,"unity_"+sheet.name.ToLowerInvariant()+"_gray_720",1280,true);
                    }
                    if(sheet.name=="Overview")
                    {
                        foreach(Transform r in bay.transform) A.TopDown(r.gameObject,true);
                        Aim(camera,center+new Vector3(0,0,-.2f),Vector3.up*10,3.3f); Capture(camera,"unity_overview_top_1080");
                        foreach(Transform r in bay.transform) A.TopDown(r.gameObject,false);
                    }
                    if(sheet.name=="Crates")
                    {
                        for(int side=0;side<4;side++)
                        {
                            var rotation=Quaternion.Euler(25,side*90,0);
                            var point=center+new Vector3(0,.55f,-.35f);
                            Aim(camera,point,rotation*Vector3.back*3.2f); Capture(camera,"unity_crates_view_"+side+"_720",1280,true);
                        }
                    }
                    if(sheet.name=="Gates")
                    {
                        Aim(camera,center+Vector3.up*.8f,Vector3.back*8,1.8f); Capture(camera,"unity_gates_front_1080");
                        foreach(Transform r in bay.transform) A.TopDown(r.gameObject,true);
                        Aim(camera,center,Vector3.up*10,2.0f); Capture(camera,"unity_gates_top_1080");
                        foreach(Transform r in bay.transform) A.TopDown(r.gameObject,false);
                    }
                    if(sheet.name=="Corridor")
                    {
                        foreach(Transform r in bay.transform) A.TopDown(r.gameObject,false);
                        var point=center+new Vector3(0,.55f,-1);
                        Aim(camera,point,Quaternion.Euler(25,0,0)*Vector3.back*3.2f); Capture(camera,"unity_third_person_1080"); Capture(camera,"unity_third_person_gray_720",1280,true);
                        foreach(Transform r in bay.transform) A.TopDown(r.gameObject,true);
                    }
                }
                // Saved scene opens on the overview, all other fixture bays remain independently inspectable.
                var overview=scene.GetRootGameObjects().Single(o=>o.name=="Overview");
                Aim(camera,overview.transform.position+new Vector3(0,.3f,0),new Vector3(4,7,-8),3.15f);
                AssetDatabase.SaveAssets(); A.Backup(ScenePath); EditorSceneManager.SaveScene(scene,ScenePath);
                Check(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath),"U02/not_in_formal_build");
                var report=new PresentationReport{passed=Checks.All(c=>c.passed),checks=Checks.ToArray(),captures=Captures.ToArray()};
                File.WriteAllText(Path.Combine(A.Source,"validation/unity_presentation.json"),JsonUtility.ToJson(report,true)+"\n");
                if(!report.passed) throw new InvalidOperationException("Station gallery checks failed: "+string.Join(", ",Checks.Where(c=>!c.passed).Select(c=>c.name)));
                Debug.Log("Station Kit gallery: "+Checks.Count+" checks passed; "+Captures.Count+" actual Unity captures.");
            }
            finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene,true); }
        }
    }
}
