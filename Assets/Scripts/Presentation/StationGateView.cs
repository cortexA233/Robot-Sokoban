using System.Collections.Generic;
using DG.Tweening;
using Sokoban.Domain;
using UnityEngine;

namespace Sokoban
{
    /// <summary>Consumes independent logical power/passage signals; owns only visual interpolation.</summary>
    public sealed class StationGateView : MonoBehaviour
    {
        private Transform lower, upper;
        private GameObject openMark, closedMark;
        private Renderer[] upperRenderers;
        private BoxCollider cameraObstacle;
        private Tween motion;
        private float openness, seconds;
        private bool initialized, paused;
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
            cameraObstacle = gameObject.AddComponent<BoxCollider>();
            cameraObstacle.center = new Vector3(0, .575f, 0); cameraObstacle.size = new Vector3(.86f, 1.15f, .16f);

            // Source identities and condition labels are owned by StationCircuitView in both camera modes.
            upperRenderers = hidden.ToArray();
        }

        public void Apply(bool powered, bool open, IReadOnlyDictionary<string, bool> socketPower, bool immediate)
        {
            IsPowered = powered;
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
