using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Sokoban.Art.Editor
{
    /// <summary>Import and measure this asset only; never changes the user's open scene.</summary>
    [InitializeOnLoad]
    public static class RobotAssetTools
    {
        const string Folder = "Assets/_Game/Art/Robot";
        const string ModelPath = Folder + "/Robot.fbx";
        static string Source => Path.GetFullPath("ArtSource/Robot");
        static string RequestPath => Path.Combine(Source, ".validate-unity");

        static RobotAssetTools()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(RequestPath);
                ImportAndValidate();
            };
        }

        [Serializable] public class Check { public string name; public bool passed; public string detail; }
        [Serializable] public class ClipInfo { public string name; public float duration; public bool loop; public int curves; }
        [Serializable] public class Report
        {
            public string unityVersion;
            public string fbxSha256;
            public bool passed;
            public Vector3 dimensions;
            public Vector3 rootEuler;
            public Vector3 rootScale;
            public Vector3 retractedContact;
            public Vector3 extendedContact;
            public float maxContactError;
            public int triangles;
            public string rootPath;
            public ClipInfo[] clips;
            public Check[] checks;
        }

        static ModelImporterClipAnimation Clip(string name, float first, float last, bool loop, string take)
        {
            return new ModelImporterClipAnimation
            {
                name = name, takeName = take, firstFrame = first, lastFrame = last,
                loopTime = loop, loopPose = loop, keepOriginalOrientation = true,
                keepOriginalPositionXZ = true, keepOriginalPositionY = true,
                lockRootRotation = true, lockRootPositionXZ = true, lockRootHeightY = true
            };
        }

        static void ConfigureImport()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.preserveHierarchy = true;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;
            string take = importer.defaultClipAnimations.First().takeName;
            importer.clipAnimations = new[]
            {
                Clip("Idle", 0, 60, true, take), Clip("Move", 70, 100, true, take),
                Clip("Push", 110, 128, false, take)
            };
            string[] names = { "M_RobotBody", "M_RobotDark", "M_RobotAccent", "M_RobotEmission" };
            string[] colors = { "#D9DFE5", "#202A35", "#F29D38", "#63DCEB" };
            float[] metallic = { .1f, .2f, 0, 0 };
            float[] roughness = { .55f, .65f, .5f, .4f };
            if (!AssetDatabase.IsValidFolder(Folder + "/Materials")) AssetDatabase.CreateFolder(Folder, "Materials");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is unavailable");
            for (int i = 0; i < names.Length; i++)
            {
                string path = Folder + "/Materials/" + names[i] + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                ColorUtility.TryParseHtmlString(colors[i], out Color color);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Metallic", metallic[i]);
                mat.SetFloat("_Smoothness", 1 - roughness[i]);
                if (i == 3)
                {
                    // Modest linear emission preserves cyan in both the game's
                    // URP lighting and editor previews without a tonemapper.
                    mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color.linear * .6f);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                EditorUtility.SetDirty(mat);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), names[i]), mat);
            }
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
        }

        static Transform Find(GameObject model, string name)
        {
            return model.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        }

        static Bounds BoundsOf(GameObject obj, Transform frame)
        {
            var points = obj.GetComponentsInChildren<MeshFilter>().SelectMany(m =>
                m.sharedMesh.vertices.Select(v => frame.InverseTransformPoint(m.transform.TransformPoint(v)))).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);
            return bounds;
        }

        static string Hash(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        [MenuItem("Tools/Robot/Import and Validate")]
        public static void ImportAndValidate()
        {
            var report = new Report { unityVersion = Application.unityVersion, fbxSha256 = Hash(ModelPath) };
            var checks = new List<Check>();
            void Check(bool passed, string name, string detail = "") => checks.Add(new Check { name = name, passed = passed, detail = detail });
            Scene preview = default;
            GameObject model = null;
            try
            {
                ConfigureImport();
                var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
                report.clips = clips.Select(c => new ClipInfo { name = c.name, duration = c.length,
                    loop = c.isLooping, curves = AnimationUtility.GetCurveBindings(c).Length }).ToArray();
                Check(clips.Select(c => c.name).OrderBy(n => n).SequenceEqual(new[] { "Idle", "Move", "Push" }), "three_named_clips");
                var idle = clips.Single(c => c.name == "Idle");
                var move = clips.Single(c => c.name == "Move");
                var push = clips.Single(c => c.name == "Push");
                Check(Mathf.Abs(idle.length - 2) < .001f && Mathf.Abs(move.length - 1) < .001f && Mathf.Abs(push.length - .6f) < .001f, "clip_durations");
                Check(idle.isLooping && move.isLooping && !push.isLooping, "clip_loop_settings");
                preview = EditorSceneManager.NewPreviewScene();
                model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), preview);
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                model.transform.localScale = Vector3.one;
                Transform root = Find(model, "RobotRoot");
                Transform contact = Find(model, "PushContact");
                Transform emotion = Find(model, "EmotionAnchor");
                report.rootPath = AnimationUtility.CalculateTransformPath(root, model.transform);
                report.rootEuler = root.localEulerAngles;
                report.rootScale = root.localScale;
                idle.SampleAnimation(model, 0);
                var bounds = BoundsOf(model, model.transform);
                report.dimensions = bounds.size;
                Check(Mathf.Abs(bounds.size.x-.70f)<.02f && Mathf.Abs(bounds.size.y-.80f)<.02f && Mathf.Abs(bounds.size.z-.68f)<.02f, "dimensions_metres");
                Check(Mathf.Abs(bounds.min.y)<.005f, "ground_origin");
                Check(root.localPosition.sqrMagnitude < 1e-8f && Quaternion.Angle(root.localRotation, Quaternion.identity)<.01f && (root.localScale-Vector3.one).sqrMagnitude<1e-8f, "robot_root_identity");
                Check(emotion.parent == root && contact.parent.name == "PushSlide" && contact.parent.parent == root, "anchor_hierarchy");
                report.retractedContact = contact.position;
                Check(Vector3.Distance(contact.position,new Vector3(0,.4f,.36f))<.001f,"retracted_face_forward_positive_z");
                Check(Vector3.Distance(emotion.position,new Vector3(0,1.05f,0))<.001f,"emotion_anchor");
                Check(Find(model,"Wheel_LF_Pivot").position.x<0 && Find(model,"Wheel_RF_Pivot").position.x>0 && Find(model,"Wheel_LF_Pivot").position.z>0 && Find(model,"Wheel_LB_Pivot").position.z<0,"named_left_right_front_back_axes");
                var transforms = model.GetComponentsInChildren<Transform>();
                var neutral = transforms.Select(t=>(t.localPosition,t.localRotation,t.localScale)).ToArray();
                void ResetPose()
                {
                    for (int i=0;i<transforms.Length;i++)
                    { transforms[i].localPosition=neutral[i].localPosition; transforms[i].localRotation=neutral[i].localRotation; transforms[i].localScale=neutral[i].localScale; }
                }
                foreach (var clip in clips)
                {
                    ResetPose();
                    float maxRootError=0, maxAnchorError=0;
                    for (int i=0;i<=Mathf.RoundToInt(clip.length*30);i++)
                    {
                        clip.SampleAnimation(model,i/30f);
                        maxRootError=Mathf.Max(maxRootError,root.position.magnitude,Quaternion.Angle(root.rotation,Quaternion.identity));
                        maxAnchorError=Mathf.Max(maxAnchorError,Vector3.Distance(emotion.position,new Vector3(0,1.05f,0)));
                    }
                    Check(maxRootError<.001f && maxAnchorError<.001f,"stationary_root_and_emotion/"+clip.name);
                }
                foreach (var clip in new[] { idle, move })
                {
                    ResetPose(); clip.SampleAnimation(model,0);
                    var first=transforms.Select(t=>(t.localPosition,t.localRotation)).ToArray();
                    clip.SampleAnimation(model,clip.length);
                    Check(transforms.Select((t,i)=>Vector3.Distance(t.localPosition,first[i].localPosition)<.0001f && Quaternion.Angle(t.localRotation,first[i].localRotation)<.02f).All(v=>v),"loop_endpoint_pose/"+clip.name);
                }
                foreach (string wheelName in new[] { "Wheel_LF_Pivot", "Wheel_LB_Pivot", "Wheel_RF_Pivot", "Wheel_RB_Pivot" })
                {
                    Transform wheel=Find(model,wheelName);
                    foreach (var clip in new[] { move,push })
                    {
                        ResetPose(); float start=clip==push?.1f:0, end=clip==push?.5f:1;
                        clip.SampleAnimation(model,start); Quaternion prior=wheel.localRotation; float sum=0;
                        int steps=Mathf.RoundToInt((end-start)*30);
                        for(int i=1;i<=steps;i++)
                        { clip.SampleAnimation(model,start+i/30f); sum+=Quaternion.Angle(prior,wheel.localRotation); prior=wheel.localRotation; }
                        Check(Mathf.Abs(sum-360)<.15f,"visible_revolution/"+clip.name+"/"+wheelName,sum.ToString("F4"));
                    }
                }
                ResetPose();
                for(int i=3;i<=15;i++)
                {
                    push.SampleAnimation(model,i/30f);
                    report.extendedContact=contact.position;
                    report.maxContactError=Mathf.Max(report.maxContactError,Vector3.Distance(contact.position,new Vector3(0,.4f,.6f)));
                    var physicalFace=BoundsOf(Find(model,"PushPlate").gameObject,model.transform);
                    report.maxContactError=Mathf.Max(report.maxContactError,Mathf.Abs(physicalFace.max.z-.6f));
                    // Move both root and reference box by the same SmoothStep progress.
                    float t=(i-3)/12f; float progress=t*t*(3-2*t);
                    float faceZ=contact.position.z+progress;
                    float boxRearZ=1+progress-.4f;
                    report.maxContactError=Mathf.Max(report.maxContactError,Mathf.Abs(faceZ-boxRearZ));
                }
                Check(report.maxContactError<=.01f,"push_contact_and_synchronized_one_metre_move");
                ResetPose(); push.SampleAnimation(model,push.length);
                Check(Vector3.Distance(contact.position,new Vector3(0,.4f,.36f))<.001f && Quaternion.Angle(Find(model,"BodyPivot").localRotation,neutral[Array.IndexOf(transforms,Find(model,"BodyPivot"))].localRotation)<.01f,"push_returns_neutral");
                ResetPose(); idle.SampleAnimation(model,0);
                Check(model.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m && m.shader.name=="Universal Render Pipeline/Lit")),"urp_materials");
                report.triangles=model.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3);
                Check(report.triangles<=10000,"triangle_budget");
                Check(model.GetComponentsInChildren<Camera>().Length==0 && model.GetComponentsInChildren<Light>().Length==0,"references_excluded");
                report.passed=checks.All(c=>c.passed);
                if (report.passed) { SavePrefab(model, clips); RenderPreview(model); }
            }
            catch(Exception error)
            {
                Check(false,"exception",error.ToString());
                Debug.LogException(error);
            }
            finally
            {
                report.checks=checks.ToArray(); report.passed=checks.Count>0 && checks.All(c=>c.passed);
                Directory.CreateDirectory(Source);
                File.WriteAllText(Path.Combine(Source,"unity_validation.json"),JsonUtility.ToJson(report,true)+"\n");
                if(model) Object.DestroyImmediate(model);
                if(preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                Debug.Log("Robot asset validation: "+(report.passed?"PASS":"FAIL")+"; "+Path.Combine(Source,"unity_validation.json"));
            }
        }

        static void SavePrefab(GameObject model, AnimationClip[] clips)
        {
            string path=Folder+"/Robot.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine=controller.layers[0].stateMachine;
            foreach (var clip in clips)
            {
                var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==clip.name) ?? machine.AddState(clip.name);
                state.motion=clip;
                if(clip.name=="Idle") machine.defaultState=state;
            }
            var animator=model.GetComponent<Animator>();
            if (!animator) animator=model.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller;
            animator.applyRootMotion=false;
            PrefabUtility.SaveAsPrefabAsset(model,Folder+"/Robot.prefab");
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        static void RenderPreview(GameObject model)
        {
            var utility=new PreviewRenderUtility();
            Texture2D image=null;
            try
            {
                utility.BeginStaticPreview(new Rect(0,0,720,720));
                utility.AddSingleGO(Object.Instantiate(model));
                utility.camera.orthographic=true; utility.camera.orthographicSize=.66f;
                utility.camera.nearClipPlane=.01f; utility.camera.farClipPlane=20;
                utility.camera.transform.position=new Vector3(1.4f,1.1f,2);
                utility.camera.transform.LookAt(new Vector3(0,.4f,0));
                utility.camera.clearFlags=CameraClearFlags.SolidColor;
                utility.camera.backgroundColor=new Color(.14f,.18f,.23f);
                utility.lights[0].intensity=1.3f;
                utility.lights[0].transform.rotation=Quaternion.Euler(40,210,0);
                utility.lights[1].intensity=.7f;
                utility.ambientColor=new Color(.5f,.5f,.5f);
                utility.Render(true);
                image=utility.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(Source,"previews/unity.png"),image.EncodeToPNG());
            }
            finally
            {
                if(image) Object.DestroyImmediate(image);
                utility.Cleanup();
            }
        }
    }
}
