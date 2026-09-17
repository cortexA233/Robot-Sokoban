using UnityEngine;

namespace Sokoban
{
    /// <summary>Two board-owned depth-tested font materials, kept in sync with the dynamic atlas.</summary>
    public sealed class StationKitLabelMaterials : MonoBehaviour
    {
        private Font font;
        private Material light, dark;
        public void Initialize(StationKitTheme theme)
        {
            font = theme.labelFont;
            light = new Material(theme.labelMaterial) { name = "Station label light" };
            dark = new Material(theme.labelMaterial) { name = "Station label dark" };
            light.SetColor("_BaseColor", new Color(.95f, .97f, .94f)); dark.SetColor("_BaseColor", new Color(.06f, .09f, .12f));
            Font.textureRebuilt += Rebuilt; Rebuilt(font);
        }
        private void Rebuilt(Font changed)
        {
            if (changed != font) return;
            var texture = font.material.mainTexture;
            light.SetTexture("_BaseMap", texture); dark.SetTexture("_BaseMap", texture);
        }
        public Material Get(bool useDark) => useDark ? dark : light;
        private void OnDestroy()
        {
            Font.textureRebuilt -= Rebuilt;
            if (light) Destroy(light); if (dark) Destroy(dark);
        }
    }
}
