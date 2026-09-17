using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sokoban
{
    internal static class StationKitRendering
    {
        public static Transform Part(Transform root, string path)
        {
            var part = root.Find(path);
            if (!part) throw new InvalidOperationException("Station Kit node missing: " + root.name + "/" + path);
            return part;
        }
        public static bool HasRole(Material material, string role) => material && material.name.StartsWith("M_Station" + role, StringComparison.Ordinal);

        public static void Identity(GameObject root, StationKitTheme.LinkStyle style)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) if (HasRole(materials[i], "LinkAccent")) materials[i] = style.material;
                renderer.sharedMaterials = materials;
            }
        }
        public static TextMesh Label(Transform parent, string text, Vector3 position, float height, StationKitTheme theme, bool dark = false)
        {
            var obj = new GameObject("Identity label"); obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var label = obj.AddComponent<TextMesh>(); label.font = theme.labelFont; label.fontSize = 64;
            label.characterSize = height * 10 / 64; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.color = dark ? new Color(.06f, .09f, .12f) : new Color(.95f, .97f, .94f); label.text = text;
            var renderer = label.GetComponent<Renderer>();
            renderer.sharedMaterial = parent.GetComponentInParent<StationKitLabelMaterials>(true).Get(dark);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return label;
        }

        /// <summary>Cache both slot arrays once; state changes allocate no materials or slot arrays.</summary>
        public sealed class PowerLamps
        {
            private readonly List<Renderer> renderers = new List<Renderer>();
            private readonly List<Material[]> on = new List<Material[]>(), off = new List<Material[]>();
            private bool? current;
            public PowerLamps(GameObject root, StationKitTheme theme)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var a = renderer.sharedMaterials; var b = (Material[])a.Clone(); bool found = false;
                    for (int i = 0; i < a.Length; i++)
                        if (HasRole(a[i], "StatusEmission")) { a[i] = theme.powerOn; b[i] = theme.powerOff; found = true; }
                    if (found) { renderers.Add(renderer); on.Add(a); off.Add(b); }
                }
            }
            public void Set(bool powered)
            {
                if (current == powered) return;
                current = powered;
                for (int i = 0; i < renderers.Count; i++) renderers[i].sharedMaterials = powered ? on[i] : off[i];
            }
        }
    }
}
