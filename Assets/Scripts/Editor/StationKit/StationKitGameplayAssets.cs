using System;
using System.Collections.Generic;
using System.Linq;
using Sokoban.Domain;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sokoban.Art.Editor
{
    public static class StationKitGameplayAssets
    {
        const string ThemePath = "Assets/Resources/configs/StationKitTheme.asset";
        const string MaterialFolder = "Assets/Material/StationGameplay/";
        const string MeshFolder = "Assets/Art/StationKit/Gameplay/";

        [MenuItem("Tools/Station Kit/Prepare Gameplay Integration")]
        public static void Prepare()
        {
            RepairEmissionFlags();
            StationKitAssetTools.Folder(MaterialFolder); StationKitAssetTools.Folder(MeshFolder);
            var existingTheme=AssetDatabase.LoadAssetAtPath<StationKitTheme>(ThemePath);
            if (existingTheme)
            {
                if (!existingTheme.labelMaterial) { existingTheme.labelMaterial=LabelMaterial(); EditorUtility.SetDirty(existingTheme); AssetDatabase.SaveAssets(); }
                return; // Preserve edited palette, source bindings and prefab assignments.
            }
            var theme = ScriptableObject.CreateInstance<StationKitTheme>();
            theme.assets = StationKitAssetTools.Ids.Select(id => new StationKitTheme.Asset { id = id,
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StationKitAssetTools.Prefabs + id + ".prefab") }).ToArray();
            Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(StationKitAssetTools.Materials + "M_Station" + name + ".mat");
            theme.styles = new[]
            {
                new StationKitTheme.LinkStyle { key="blue", material=Mat("LinkAccent_Blue"), symbol=0 },
                new StationKitTheme.LinkStyle { key="purple", material=Mat("LinkAccent_Purple"), symbol=1 },
                new StationKitTheme.LinkStyle { key="lime", material=Mat("LinkAccent_Lime"), symbol=2 },
                new StationKitTheme.LinkStyle { key="teal", material=Tint(Mat("LinkAccent"),"M_StationLinkAccent_Teal","#49BCAC"), symbol=0 },
                new StationKitTheme.LinkStyle { key="gold", material=Tint(Mat("LinkAccent"),"M_StationLinkAccent_Gold","#D8B85D"), symbol=1 }
            };
            var bindings = new List<StationKitTheme.SocketBinding>();
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Resources/configs/levels" }))
            {
                var level = LevelJson.Read(AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid)).text);
                int next = 0;
                foreach (var socket in level.sockets.OrderBy(s => s.id, StringComparer.Ordinal))
                {
                    string label = socket.id.StartsWith("socket_",StringComparison.Ordinal) ? socket.id.Substring(7).ToUpperInvariant() : socket.id;
                    int index = label == "A" ? 0 : label == "B" ? 1 : label == "C" ? 2 : label == "S" ? 3 : label == "T" ? 4 : next++ % theme.styles.Length;
                    bindings.Add(new StationKitTheme.SocketBinding { levelId=level.id, socketId=socket.id, label=label, styleKey=theme.styles[index].key });
                }
            }
            theme.bindings = bindings.ToArray();
            var goal = theme.Prefab("GoalSocket");
            var gate = theme.Prefab("PowerGate");
            theme.socketSymbols = new[] { goal.transform.Find("GoalSocketRoot/LinkMarkers/IdentityBands").GetComponent<MeshFilter>().sharedMesh,
                AssetDatabase.LoadAssetAtPath<Mesh>(StationKitAssetTools.Models+"SocketIdentityTriangle.asset"),
                AssetDatabase.LoadAssetAtPath<Mesh>(StationKitAssetTools.Models+"SocketIdentityCircle.asset") };
            var identity = gate.transform.Find("PowerGateRoot/SourceBadgeTemplate/SourceBadgeTemplate_Identity").GetComponent<MeshFilter>();
            theme.badgeSymbols = new[] { identity.sharedMesh, BadgeSymbol(identity,3,"Triangle"), BadgeSymbol(identity,20,"Circle") };
            theme.powerOff=Mat("StatusEmission_Off"); theme.powerOn=Mat("StatusEmission_On");
            theme.trackSurface=Tint(Mat("Floor"),"M_StationTrackSurface","#699BAE"); theme.trackMark=Mat("Dark");
            theme.labelFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NotoSansSC-Regular.otf");
            theme.labelMaterial=LabelMaterial();
            if (!theme.labelFont || theme.assets.Any(a=>!a.prefab) || theme.socketSymbols.Any(m=>!m))
                throw new InvalidOperationException("Station theme dependency is missing.");
            AssetDatabase.CreateAsset(theme,ThemePath); AssetDatabase.SaveAssets();
            Debug.Log("Station gameplay theme created: 11 prefabs, stable per-source identities, independent power materials.");
        }
        static Material LabelMaterial()
        {
            string path=MaterialFolder+"M_StationWorldLabel.mat"; var existing=AssetDatabase.LoadAssetAtPath<Material>(path); if(existing) return existing;
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="M_StationWorldLabel",renderQueue=2450};
            material.SetFloat("_AlphaClip",1); material.SetFloat("_Cutoff",.2f); material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull",0); material.SetFloat("_ZWrite",0); material.SetOverrideTag("RenderType","TransparentCutout");
            AssetDatabase.CreateAsset(material,path); return material;
        }
        private static void RepairEmissionFlags()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Material/Station"}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(!material.name.Contains("EnergyWindow") && !material.name.Contains("StatusEmission")) continue;
                var color=material.GetColor("_EmissionColor"); bool lit=color.r+color.g+color.b>0;
                material.globalIlluminationFlags=lit?MaterialGlobalIlluminationFlags.BakedEmissive:MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                if(lit) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }
        static Material Tint(Material original,string name,string hex)
        {
            string path=MaterialFolder+name+".mat"; var existing=AssetDatabase.LoadAssetAtPath<Material>(path); if(existing) return existing;
            var m=new Material(original){name=name}; ColorUtility.TryParseHtmlString(hex,out var color); m.SetColor("_BaseColor",color);
            AssetDatabase.CreateAsset(m,path); return m;
        }
        static Mesh BadgeSymbol(MeshFilter source,int sides,string label)
        {
            string path=MeshFolder+"InputBadge"+label+".asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(existing) return existing;
            var mesh=Object.Instantiate(source.sharedMesh); mesh.name="InputBadge"+label; var vertices=mesh.vertices.ToList();
            int slot=Array.FindIndex(source.GetComponent<Renderer>().sharedMaterials,m=>m.name.StartsWith("M_StationDark",StringComparison.Ordinal));
            var original=mesh.GetTriangles(slot); var triangles=new List<int>();
            for(int i=0;i<original.Length;i+=3)
            {
                // The existing raised glyph sits above the separate bezel and identity strip.
                if(vertices[original[i]].y>.0081f && vertices[original[i+1]].y>.0081f && vertices[original[i+2]].y>.0081f) continue;
                triangles.Add(original[i]); triangles.Add(original[i+1]); triangles.Add(original[i+2]);
            }
            int first=vertices.Count;
            for(int i=0;i<sides;i++)
            {
                float a=Mathf.PI/2+2*Mathf.PI*i/sides;
                vertices.Add(new Vector3(-.025f+Mathf.Cos(a)*.0215f,.0085f,Mathf.Sin(a)*.0215f));
            }
            for(int i=1;i<sides-1;i++){triangles.Add(first);triangles.Add(first+i+1);triangles.Add(first+i);}
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,slot); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path); return mesh;
        }
    }
}
