using UnityEngine;

namespace Sokoban
{
    /// <summary>Hides only the upper visual shell. The grid remains the sole movement authority.</summary>
    internal sealed class CameraOcclusion
    {
        private readonly Renderer[] upper;
        private readonly BoxCollider obstacle;
        private readonly Vector3 originalCenter, originalSize;
        private readonly Bounds fullBounds;
        private bool? hidden;
        private float revealAfter;

        public CameraOcclusion(Renderer[] renderers, BoxCollider collider)
        {
            upper = renderers; obstacle = collider;
            originalCenter = collider.center; originalSize = collider.size;
            // A stable bound, independent of the currently hidden/collapsed collider and gate animation.
            fullBounds = new Bounds(collider.transform.TransformPoint(originalCenter), Vector3.zero);
            foreach (var renderer in upper) fullBounds.Encapsulate(renderer.bounds);
            fullBounds.Encapsulate(collider.transform.position + Vector3.up * .3f);
        }

        public void Prepare(bool topDown, Vector3 eye, Vector3[] targets, int count)
        {
            bool occluded = false;
            if (!topDown)
            {
                var bounds = fullBounds;
                bounds.Expand(hidden == true ? .42f : .24f);
                for (int i = 0; i < count && !occluded; i++)
                {
                    var delta = targets[i] - eye;
                    occluded = bounds.Contains(eye) || (bounds.IntersectRay(new Ray(eye, delta.normalized), out float distance)
                        && distance < delta.magnitude);
                }
                if (occluded) revealAfter = Time.unscaledTime + .18f;
            }
            SetHidden(topDown || occluded || Time.unscaledTime < revealAfter);
        }

        private void SetHidden(bool value)
        {
            if (hidden == value) return;
            hidden = value;
            foreach (var renderer in upper) renderer.enabled = !value;
            // Match camera collision to the visible base instead of pushing the lens into the robot.
            // Gate enabled state is still controlled independently by StationGateView.Apply.
            obstacle.center = value ? new Vector3(originalCenter.x, .12f, originalCenter.z) : originalCenter;
            obstacle.size = value ? new Vector3(originalSize.x, .24f, originalSize.z) : originalSize;
        }
    }
}
