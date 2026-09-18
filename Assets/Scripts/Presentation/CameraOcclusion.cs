using UnityEngine;

namespace Sokoban
{
    /// <summary>Hides the upper shell only in top view; third person always keeps the complete model.</summary>
    internal sealed class CameraOcclusion
    {
        private readonly Renderer[] upper;
        private readonly BoxCollider obstacle;
        private readonly Vector3 originalCenter, originalSize;
        private bool? hidden;

        public CameraOcclusion(Renderer[] renderers, BoxCollider collider)
        {
            upper = renderers; obstacle = collider;
            originalCenter = collider.center; originalSize = collider.size;
        }

        public void Prepare(bool topDown) => SetHidden(topDown);

        private void SetHidden(bool value)
        {
            if (hidden == value) return;
            hidden = value;
            foreach (var renderer in upper) renderer.enabled = !value;
            // Only top view uses the short base. Restore full camera collision in third person.
            // Gate enabled state is still controlled independently by StationGateView.Apply.
            obstacle.center = value ? new Vector3(originalCenter.x, .12f, originalCenter.z) : originalCenter;
            obstacle.size = value ? new Vector3(originalSize.x, .24f, originalSize.z) : originalSize;
        }
    }
}
