// Execute through CoplayDev execute_code after compilation. No scene is saved.
var textureFolder = "Assets/Art/StationKit/Textures/";
foreach (string name in new[] { "Coating", "Metal", "Polymer", "Rubber", "Brace", "MicroNormal" })
{
    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(textureFolder + name + ".png");
    importer.textureType = name == "MicroNormal" ? UnityEditor.TextureImporterType.NormalMap : UnityEditor.TextureImporterType.Default;
    importer.sRGBTexture = false; importer.mipmapEnabled = true; importer.maxTextureSize = 256;
    importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
    importer.wrapMode = UnityEngine.TextureWrapMode.Repeat; importer.filterMode = UnityEngine.FilterMode.Trilinear;
    importer.alphaSource = UnityEditor.TextureImporterAlphaSource.FromInput;
    importer.SaveAndReimport();
}
var profiles = new System.Collections.Generic.Dictionary<string, string> {
    {"M_RobotBody", "Coating"}, {"M_RobotDark", "Rubber"},
    {"M_StationBody", "Coating"}, {"M_StationDark", "Polymer"},
    {"M_StationMetal", "Metal"}, {"M_StationCargoBody", "Polymer"}, {"M_StationCargoBrace", "Brace"}
};
var normal = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(textureFolder + "MicroNormal.png");
foreach (var pair in profiles)
{
    string folder = pair.Key.StartsWith("M_Robot") ? "Assets/Art/Robot/Materials/" : "Assets/Material/Station/";
    var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(folder + pair.Key + ".mat");
    material.SetTexture("_MetallicGlossMap", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(textureFolder + pair.Value + ".png"));
    material.SetFloat("_Smoothness", 1); material.SetFloat("_SmoothnessTextureChannel", 0);
    material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", .25f);
    material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_NORMALMAP");
    UnityEditor.EditorUtility.SetDirty(material);
}
foreach (string name in new[] { "M_RobotEmission", "M_StationEnergyWindow" })
{
    string folder = name.StartsWith("M_Robot") ? "Assets/Art/Robot/Materials/" : "Assets/Material/Station/";
    var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(folder + name + ".mat");
    material.SetFloat("_Smoothness", .79f); material.SetFloat("_Metallic", .16f);
    // Existing emission/colour and fixed energy identity are preserved.
    UnityEditor.EditorUtility.SetDirty(material);
}
var theme = UnityEngine.Resources.Load<Sokoban.StationKitTheme>("configs/StationKitTheme");
foreach (var tint in new[] { new[] { "M_StationCargoBrace", "#A3A69A" }, new[] { "M_StationCargoBody", "#74675B" } })
{
    UnityEngine.Color color; UnityEngine.ColorUtility.TryParseHtmlString(tint[1], out color);
    var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Material/Station/" + tint[0] + ".mat");
    material.SetColor("_BaseColor", color); UnityEditor.EditorUtility.SetDirty(material);
}
theme.trackSurface.SetFloat("_Metallic", .55f); theme.trackSurface.SetFloat("_Smoothness", .68f);
theme.trackSurface.SetTexture("_BumpMap", normal); theme.trackSurface.SetFloat("_BumpScale", .16f);
theme.trackSurface.EnableKeyword("_NORMALMAP"); UnityEditor.EditorUtility.SetDirty(theme.trackSurface);
var stationMaterials = UnityEditor.AssetDatabase.FindAssets("t:Material", new[] { "Assets/Material/Station" })
    .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
    .ToDictionary(m => m.name, m => m);
foreach (string aid in new[] { "RedirectorPlate", "LowFrictionDeck" })
{
    string modelPath = "Assets/Art/StationKit/Meshes/" + aid + ".fbx";
    var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(modelPath);
    importer.globalScale = 1; importer.bakeAxisConversion = true; importer.importAnimation = false;
    importer.preserveHierarchy = true; importer.isReadable = true;
    importer.animationType = UnityEditor.ModelImporterAnimationType.None;
    importer.materialImportMode = UnityEditor.ModelImporterMaterialImportMode.ImportStandard;
    importer.importNormals = UnityEditor.ModelImporterNormals.Import;
    importer.importTangents = UnityEditor.ModelImporterTangents.CalculateMikk;
    foreach (var material in stationMaterials.Values)
        importer.AddRemap(new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), material.name), material);
    importer.SaveAndReimport();
    var source = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(modelPath);
    var instance = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(source);
    try
    {
        instance.name = aid;
        var surface = instance.GetComponentsInChildren<UnityEngine.Renderer>().Single(r => r.name == "Surface");
        surface.sharedMaterial = theme.trackSurface;
        string prefabPath = "Assets/Resources/prefabs/gameplay/StationKit/" + aid + ".prefab";
        var prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        var list = theme.assets.ToList(); var entry = list.FirstOrDefault(a => a.id == aid);
        if (entry == null) list.Add(new Sokoban.StationKitTheme.Asset { id = aid, prefab = prefab });
        else entry.prefab = prefab;
        theme.assets = list.ToArray();
    }
    finally { UnityEngine.Object.DestroyImmediate(instance); }
}
string profilePath = "Assets/Settings/StationArtProfile.asset";
var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(profilePath);
if (!profile)
{
    profile = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
    UnityEditor.AssetDatabase.CreateAsset(profile, profilePath);
    var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
    bloom.threshold.value = 1.1f; bloom.intensity.value = .12f; bloom.scatter.value = .55f;
    var tone = profile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
    tone.mode.value = UnityEngine.Rendering.Universal.TonemappingMode.Neutral;
    foreach (var effect in profile.components) UnityEditor.AssetDatabase.AddObjectToAsset(effect, profile);
}
theme.artPostProcessing = profile; theme.artFillColor = new UnityEngine.Color(.72f, .82f, 1);
theme.artFillIntensity = .28f;
UnityEditor.EditorUtility.SetDirty(profile); UnityEditor.EditorUtility.SetDirty(theme);
UnityEditor.AssetDatabase.SaveAssets();
return new { added = new[] { "RedirectorPlate", "LowFrictionDeck" }, materials = profiles.Count, profile = profilePath };
