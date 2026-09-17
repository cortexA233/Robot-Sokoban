using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using Sokoban.Domain;
using UnityEngine;

namespace Sokoban
{
    /// <summary>Consumes independent logical power/passage signals; owns only visual interpolation.</summary>
    public sealed class StationGateView : MonoBehaviour
    {
        private sealed class Source { public string id; public StationKitRendering.PowerLamps lamps; }
        private readonly List<Source> sources = new List<Source>();
        private Transform lower, upper;
        private GameObject openMark, closedMark;
        private Renderer[] upperRenderers;
        private StationKitRendering.PowerLamps condition;
        private BoxCollider cameraObstacle;
        private Tween motion;
        private float openness, seconds;
        private bool initialized, paused;
        private string[] overflowIds;
        private TextMesh overflow;
        public bool IsOpen { get; private set; }
        public bool IsPowered { get; private set; }
        public bool Animating => motion != null && motion.IsActive();

        public void Initialize(GateDefinition gate, LevelDefinition level, StationKitTheme theme)
        {
            var root = StationKitRendering.Part(transform, "PowerGateRoot");
            lower = StationKitRendering.Part(root, "MovingParts/LowerPanel"); upper = StationKitRendering.Part(root, "MovingParts/UpperPanel");
            openMark = StationKitRendering.Part(root, "TopDownMarkers/OpenMark").gameObject;
            closedMark = StationKitRendering.Part(root, "TopDownMarkers/ClosedMark").gameObject;
            var hidden = new List<Renderer>();
            hidden.AddRange(StationKitRendering.Part(root, "UpperStructure").GetComponentsInChildren<Renderer>());
            hidden.AddRange(StationKitRendering.Part(root, "MovingParts").GetComponentsInChildren<Renderer>());
            seconds = theme.gateSeconds;
            condition = new StationKitRendering.PowerLamps(StationKitRendering.Part(root, "PowerStatus").gameObject, theme);
            cameraObstacle = gameObject.AddComponent<BoxCollider>();
            cameraObstacle.center = new Vector3(0, .575f, 0); cameraObstacle.size = new Vector3(.86f, 1.15f, .16f);

            var template = StationKitRendering.Part(root, "SourceBadgeTemplate").gameObject;
            StationKitRendering.Part(root, "SourceBadge02").gameObject.SetActive(false);
            var badges = new List<GameObject>();
            int count = Mathf.Min(gate.sourceSocketIds.Length, 16);
            // Make pristine copies before adding labels. Up to four on each edge; mirror
            // the usual 1-4 input case so a closed panel cannot conceal every source.
            int copies = count <= 4 ? count * 2 : count;
            for (int i = 0; i < copies; i++) badges.Add(Instantiate(template, root));
            template.SetActive(false);
            for (int i = 0; i < copies; i++)
            {
                int sourceIndex = i % count; string id = gate.sourceSocketIds[sourceIndex];
                var style = theme.Style(level.id, id, out string label);
                var badge = badges[i]; badge.name = "Input " + id + " " + i; badge.SetActive(true);
                int edge = count <= 4 ? i / count * 2 : i / 4;
                int rowCount = count <= 4 ? count : Mathf.Min(4, count - edge * 4);
                int column = count <= 4 ? sourceIndex : i % 4;
                float x = (column - (rowCount - 1) * .5f) * (rowCount <= 3 ? .28f : .20f);
                var turn = Quaternion.Euler(0, edge * 90, 0);
                badge.transform.localPosition = turn * new Vector3(x, -.0065f, -.446f);
                badge.transform.localRotation = turn;
                var identity = badge.transform.Find("SourceBadgeTemplate_Identity").GetComponent<MeshFilter>();
                identity.sharedMesh = theme.badgeSymbols[style.symbol]; StationKitRendering.Identity(badge, style);
                StationKitRendering.Label(badge.transform, label, new Vector3(.027f, .010f, 0), .041f, theme, true);
                sources.Add(new Source { id = id, lamps = new StationKitRendering.PowerLamps(badge, theme) });
            }
            if (gate.sourceSocketIds.Length > 16)
            {
                overflowIds = new string[gate.sourceSocketIds.Length - 16];
                System.Array.Copy(gate.sourceSocketIds, 16, overflowIds, 0, overflowIds.Length);
                overflow = StationKitRendering.Label(root, "", new Vector3(0, 1.8f, 0), .10f, theme);
            }
            StationKitRendering.Label(root, gate.powerMode == "All" ? "ALL" : "ANY", new Vector3(0, .009f, -.19f), .085f, theme);
            for (int side = 0; side < 2; side++)
            {
                var label = StationKitRendering.Label(root.Find("UpperStructure"), gate.powerMode == "All" ? "ALL" : "ANY",
                    new Vector3(0, 1.31f, side == 0 ? -.184f : .184f), .11f, theme, true);
                label.transform.localRotation = Quaternion.Euler(0, side * 180, 0);
                hidden.Add(label.GetComponent<Renderer>());
            }
            upperRenderers = hidden.ToArray();
        }

        public void Apply(bool powered, bool open, IReadOnlyDictionary<string, bool> socketPower, bool immediate)
        {
            IsPowered = powered; condition.Set(powered);
            foreach (var source in sources) source.lamps.Set(socketPower[source.id]);
            if (overflow)
            {
                var text = new StringBuilder("INPUTS\n");
                foreach (string id in overflowIds) text.Append(id).Append(socketPower[id] ? " ON\n" : " OFF\n");
                overflow.text = text.ToString();
            }
            cameraObstacle.enabled = !open; openMark.SetActive(open); closedMark.SetActive(!open);
            bool changed = !initialized || open != IsOpen; IsOpen = open; initialized = true;
            if (immediate) { Cancel(); return; }
            if (!changed) return;
            motion?.Kill(false);
            motion = DOTween.To(() => openness, SetPose, open ? 1 : 0, seconds).SetEase(Ease.InOutSine).SetTarget(this).SetLink(gameObject)
                .OnComplete(() => motion = null);
            if (paused) motion.Pause();
        }
        private void SetPose(float value)
        {
            openness = value;
            lower.localPosition = new Vector3(0, Mathf.Lerp(.29f, 1.305f, value), .045f);
            upper.localPosition = new Vector3(0, Mathf.Lerp(.87f, 1.305f, value), -.045f);
        }
        public void SetTopDown(bool value) { foreach (var renderer in upperRenderers) renderer.enabled = !value; }
        public void SetPaused(bool value) { paused = value; if (value) motion?.Pause(); else motion?.Play(); }
        public void Cancel() { motion?.Kill(false); motion = null; if (lower) SetPose(IsOpen ? 1 : 0); }
        private void OnDestroy() { motion?.Kill(false); motion = null; }
    }
}
