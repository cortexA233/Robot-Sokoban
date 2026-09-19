using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sokoban
{
    /// <summary>Level-owned art lighting and camera effects; released with the camera rig.</summary>
    internal static class StationArtPresentation
    {
        public static void Configure(Camera camera, Transform owner, StationKitTheme theme)
        {
            if (!theme || !theme.artPostProcessing) return;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            // Built-in Ignore Raycast layer isolates this profile from the menu camera's
            // default volume mask. This object has no collider, renderer or physics role.
            const int volumeLayer = 2;
            data.volumeLayerMask = 1 << volumeLayer;
            var effects = new GameObject("Station art volume", typeof(Volume));
            effects.layer = volumeLayer; effects.transform.SetParent(owner, false);
            var volume = effects.GetComponent<Volume>();
            volume.isGlobal = true; volume.sharedProfile = theme.artPostProcessing;
            var fill = new GameObject("Station soft fill", typeof(Light)).GetComponent<Light>();
            fill.transform.SetParent(owner, false);
            fill.transform.rotation = Quaternion.Euler(32, -130, 0);
            fill.type = LightType.Directional; fill.shadows = LightShadows.None;
            fill.color = theme.artFillColor; fill.intensity = theme.artFillIntensity;
        }
    }
}
