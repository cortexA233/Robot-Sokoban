using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using A = Sokoban.Art.Editor.StationKitAssetTools;

namespace Sokoban.Art.Editor
{
    public static partial class StationKitGallery
    {
        [Serializable] public class OccupancySample { public int frame; public string phase; public bool powered; public float openness; public Vector3 cargoPosition,robotPosition; }
        [Serializable] public class OccupancyReport { public bool passed; public string driver="Editor presentation fixture, all supply conditions remain false"; public int fps=12; public OccupancySample[] frames; public A.Check[] checks; }

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
                var gate=Spawn("PowerGate",scene,bay.transform,Vector3.zero); A.Gate(gate,true,false,false,false);
                var cargo=Spawn("CargoCrate",scene,bay.transform,Vector3.zero);
                var robot=Spawn("Robot",scene,bay.transform,Vector3.back); var slide=A.Find(robot,"PushSlide"); var rest=slide.localPosition; slide.localPosition=rest+Vector3.forward*.24f;
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
                    A.Find(gate,"LowerPanel").localPosition=new Vector3(0,Mathf.Lerp(.29f,1.305f,openness),.045f);
                    A.Find(gate,"UpperPanel").localPosition=new Vector3(0,Mathf.Lerp(.87f,1.305f,openness),-.045f);
                    A.Find(gate,"OpenMark").gameObject.SetActive(openness>=.5f); A.Find(gate,"ClosedMark").gameObject.SetActive(openness<.5f);
                    bool powered=Lamp(A.Find(gate,"PowerStatus").gameObject);
                    Check(!powered && !Lamp(A.Find(gate,"SourceBadgeTemplate").gameObject) && !Lamp(A.Find(gate,"SourceBadge02").gameObject),"frame_"+i+"/all_power_lamps_off");
                    if(i<60) Check(openness==1,"frame_"+i+"/occupied_hold_open");
                    samples[i]=new OccupancySample{frame=i,phase=phase,powered=powered,openness=openness,cargoPosition=cargo.transform.position,robotPosition=robot.transform.position};
                    Capture(camera,i.ToString("D4"),960,false,frames);
                    if(i==0 || i==40 || i==71) Capture(camera,"unity_occupancy_"+i.ToString("D2"),1280);
                }
                Check(A.Find(gate,"LowerPanel").localPosition.y==.29f,"empty_gate_closed");
                var report=new OccupancyReport{passed=Checks.All(c=>c.passed),frames=samples,checks=Checks.ToArray()};
                File.WriteAllText(Path.Combine(A.Source,"validation/unity_occupancy.json"),JsonUtility.ToJson(report,true)+"\n");
                if(!report.passed) throw new InvalidOperationException("Occupancy fixture failed.");
                Debug.Log("Station Kit: cargo -> robot -> empty, 72 frames, all power indicators off.");
            }
            finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene,true); }
        }
    }
}
