using System;
using System.Linq;
using UnityEngine;

namespace Sokoban
{
    /// <summary>Presentation-only asset and identity configuration; never changes level rules.</summary>
    [CreateAssetMenu(menuName = "Robot Sokoban/Station Kit Theme")]
    public sealed class StationKitTheme : ScriptableObject
    {
        [Serializable] public sealed class Asset { public string id; public GameObject prefab; }
        [Serializable] public sealed class LinkStyle { public string key; public Material material; public int symbol; }
        [Serializable] public sealed class SocketBinding { public string levelId, socketId, styleKey, label; }
        public Asset[] assets;
        public LinkStyle[] styles;
        public SocketBinding[] bindings;
        public Mesh[] socketSymbols, badgeSymbols;
        public Material powerOff, powerOn, trackSurface, trackMark, labelMaterial;
        public Material spaceBackground;
        public Font labelFont;
        [Range(.05f, .5f)] public float gateSeconds = .18f;

        public GameObject Prefab(string id)
        {
            var entry = assets.FirstOrDefault(a => a.id == id);
            if (entry == null || !entry.prefab) throw new InvalidOperationException("Station Kit prefab missing: " + id);
            return entry.prefab;
        }
        public LinkStyle Style(string levelId, string socketId, out string label)
        {
            var entry = bindings.FirstOrDefault(b => b.levelId == levelId && b.socketId == socketId);
            if (entry != null)
            {
                label = entry.label;
                return styles.First(s => s.key == entry.styleKey);
            }
            // Stable across enumeration, runtime moves, undo and reload, including unsaved author maps.
            uint hash = 2166136261;
            foreach (char c in levelId + "/" + socketId) hash = unchecked((hash ^ c) * 16777619);
            label = socketId.StartsWith("socket_", StringComparison.Ordinal) ? socketId.Substring(7).ToUpperInvariant() : socketId;
            return styles[hash % styles.Length];
        }
    }
}
