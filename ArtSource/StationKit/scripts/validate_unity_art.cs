// Asset acceptance through CoplayDev execute_code. Prefabs open in isolated scenes.
var checks = new System.Collections.Generic.List<object>();
bool passed = true;
System.Action<bool, string, object> check = (ok, name, detail) => {
    checks.Add(new { name = name, passed = ok, detail = detail }); passed &= ok;
};
System.Func<string, string> hash = path => {
    using (var sha = System.Security.Cryptography.SHA256.Create())
        return System.BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
};
var assets = new System.Collections.Generic.List<object>();
var theme = UnityEngine.Resources.Load<Sokoban.StationKitTheme>("configs/StationKitTheme");
var ids = new[] { "EnergyCrate", "CargoCrate", "GoalSocket", "UtilitySocket", "PowerGate",
    "FloorPlain", "FloorService", "FloorGrate", "WallStraight", "WallCorner", "WallEnd", "RedirectorPlate", "LowFrictionDeck", "Robot" };
foreach (string id in ids)
{
    string prefabPath = id == "Robot" ? "Assets/Resources/prefabs/gameplay/player/Robot.prefab" : "Assets/Resources/prefabs/gameplay/StationKit/" + id + ".prefab";
    string fbxPath = "Assets/Art/" + (id == "Robot" ? "Robot" : "StationKit") + "/Meshes/" + id + ".fbx";
    var instance = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
    try
    {
        var root = instance.transform.Find(id + "Root");
        check(root != null, id + "/semantic_root", id + "Root");
        if (!root) continue;
        check(root.localPosition.sqrMagnitude < .0000001f && UnityEngine.Quaternion.Angle(root.localRotation, UnityEngine.Quaternion.identity) < .001f && (root.localScale - UnityEngine.Vector3.one).sqrMagnitude < .0000001f,
            id + "/identity_root", null);
        var renderers = instance.GetComponentsInChildren<UnityEngine.Renderer>(true);
        var filters = instance.GetComponentsInChildren<UnityEngine.MeshFilter>(true);
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        int triangles = filters.Sum(f => (int)Enumerable.Range(0, f.sharedMesh.subMeshCount).Sum(i => (long)f.sharedMesh.GetIndexCount(i)) / 3);
        check(instance.GetComponentsInChildren<UnityEngine.Collider>(true).Length == 0, id + "/no_physics", null);
        check(renderers.All(r => r.sharedMaterials.All(m => m && m.shader && m.shader.name == "Universal Render Pipeline/Lit" && m.shader.isSupported)), id + "/urp_materials", null);
        if (id.EndsWith("Crate")) check((bounds.size - UnityEngine.Vector3.one * .8f).magnitude < .006f, id + "/crate_envelope", bounds.size.ToString());
        if (id == "PowerGate") check(bounds.size.x <= .981f && bounds.size.z <= .982f, id + "/gate_footprint", bounds.size.ToString());
        if (id == "Robot") check((bounds.size - new UnityEngine.Vector3(.7f, .8f, .6805f)).magnitude < .001f, id + "/robot_envelope", bounds.size.ToString());
        var mappings = renderers.Select(r => new {
            path = UnityEditor.AnimationUtility.CalculateTransformPath(r.transform, root),
            materials = r.sharedMaterials.Select(m => new {
                name = m.name, path = UnityEditor.AssetDatabase.GetAssetPath(m),
                metallicMask = m.HasProperty("_MetallicGlossMap") ? UnityEditor.AssetDatabase.GetAssetPath(m.GetTexture("_MetallicGlossMap")) : "",
                normalMap = m.HasProperty("_BumpMap") ? UnityEditor.AssetDatabase.GetAssetPath(m.GetTexture("_BumpMap")) : ""
            }).ToArray()
        }).ToArray();
        assets.Add(new { assetId=id, rootPath=id+"Root", fbx=fbxPath, fbxSha256=hash(fbxPath), prefab=prefabPath, prefabSha256=hash(prefabPath),
            triangles=triangles, rendererCount=renderers.Length, bounds=new { min=new[]{bounds.min.x,bounds.min.y,bounds.min.z}, max=new[]{bounds.max.x,bounds.max.y,bounds.max.z}, size=new[]{bounds.size.x,bounds.size.y,bounds.size.z} }, renderers=mappings });
        if (id != "Robot") continue;
        var clips = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<UnityEngine.AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        check(clips.Length == 3, "Robot/three_clips", clips.Select(c=>c.name).ToArray());
        foreach (var expected in new[] { new {name="Idle",length=2f,loop=true}, new {name="Move",length=1f,loop=true}, new {name="Push",length=.6f,loop=false} })
        {
            var clip = clips.Single(c=>c.name==expected.name);
            check(UnityEngine.Mathf.Abs(clip.length-expected.length)<.001f && UnityEditor.AnimationUtility.GetAnimationClipSettings(clip).loopTime==expected.loop,
                "Robot/clip_"+expected.name, clip.length);
        }
        var push = clips.Single(c=>c.name=="Push"); var move = clips.Single(c=>c.name=="Move");
        var contact = root.Find("PushSlide/PushContact"); var anchor = root.Find("EmotionAnchor");
        float maxContactError=0;
        for(int frame=0; frame<=18; frame++)
        {
            push.SampleAnimation(instance, frame/30f);
            var localContact=instance.transform.InverseTransformPoint(contact.position);
            if(frame>=3 && frame<=15) maxContactError=UnityEngine.Mathf.Max(maxContactError,(localContact-new UnityEngine.Vector3(0,.4f,.6f)).magnitude);
            if(frame==0 || frame==18) maxContactError=UnityEngine.Mathf.Max(maxContactError,(localContact-new UnityEngine.Vector3(0,.4f,.36f)).magnitude);
            check((instance.transform.InverseTransformPoint(anchor.position)-new UnityEngine.Vector3(0,1.05f,0)).magnitude<.0001f && root.localPosition.sqrMagnitude<.0000001f,
                "Robot/static_anchor_and_root/"+frame,null);
        }
        check(maxContactError<.01f,"Robot/push_contact",maxContactError);
        foreach(string wheel in new[]{"Wheel_LF_Pivot","Wheel_LB_Pivot","Wheel_RF_Pivot","Wheel_RB_Pivot"})
        {
            var pivot=root.Find(wheel); float angle=0; move.SampleAnimation(instance,0); var previous=pivot.localRotation;
            for(int frame=1;frame<=30;frame++){move.SampleAnimation(instance,frame/30f);angle+=UnityEngine.Quaternion.Angle(previous,pivot.localRotation);previous=pivot.localRotation;}
            check(UnityEngine.Mathf.Abs(angle-360)<.1f,"Robot/visible_wheel_revolution/"+wheel,angle);
        }
    }
    finally { UnityEditor.PrefabUtility.UnloadPrefabContents(instance); }
}
check(theme.assets.Any(a=>a.id=="RedirectorPlate") && theme.assets.Any(a=>a.id=="LowFrictionDeck"),"theme/transport_prefabs",null);
check(theme.artPostProcessing && UnityEditor.AssetDatabase.GetDependencies("Assets/Resources/configs/StationKitTheme.asset",true).Contains("Assets/Settings/StationArtProfile.asset"),"theme/profile_build_dependency",null);
foreach(var guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Art/StationKit/Textures"}))
{
    string path=UnityEditor.AssetDatabase.GUIDToAssetPath(guid); var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
    check(!importer.sRGBTexture && importer.mipmapEnabled && importer.wrapMode==UnityEngine.TextureWrapMode.Repeat,"texture/linear_mipmapped_repeat",path);
}
var result = new { version="v0.11.0", checkedAt=System.DateTime.UtcNow.ToString("o"), unityVersion=UnityEngine.Application.unityVersion, passed=passed, assets=assets, checks=checks };
System.IO.Directory.CreateDirectory("Docs/Versions/V0.11.0Validation");
System.IO.File.WriteAllText("Docs/Versions/V0.11.0Validation/UnityAssets.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
return new {passed=passed,assets=assets.Count,checks=checks.Count};
