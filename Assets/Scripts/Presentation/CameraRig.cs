using Cinemachine;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sokoban
{
    [DefaultExecutionOrder(50)] // After actors, before CinemachineBrain (100).
    public sealed class CameraRig : MonoBehaviour
    {
        private Camera output;
        private CinemachineVirtualCamera followCamera, topCamera;
        private Cinemachine3rdPersonFollow follow;
        private Transform orbit;
        private Transform player;
        private LevelDefinition level;
        private BoardView board;
        private float yaw, pitch = 48, topSize, lastFullSize;
        private int sector;
        private bool projectionBeforeBlend = true;
        public bool TopDown { get; private set; }
        public float Yaw => yaw;
        public Direction Forward => TopDown ? Direction.N : (Direction)sector;
        public Camera Output => output;
        public float MouseSensitivity { get; set; } = 1;
        public bool InvertVertical { get; set; }

        private void OnEnable() => CinemachineCore.CameraUpdatedEvent.AddListener(AfterCameraUpdate);
        private void OnDisable() => CinemachineCore.CameraUpdatedEvent.RemoveListener(AfterCameraUpdate);
        private void AfterCameraUpdate(CinemachineBrain brain)
        {
            if (!output || brain.gameObject != output.gameObject) return;
            // Cinemachine 2 switches non-interpolated lens fields at the midpoint. Keep the
            // source projection through the travel, then commit the destination at the end.
            output.orthographic = brain.IsBlending ? projectionBeforeBlend : TopDown;
        }

        [System.Serializable]
        public sealed class ViewSettings
        {
            public bool topDown, invert;
            public float yaw, pitch, distance, size, sensitivity;
        }
        public ViewSettings Capture() => new ViewSettings { topDown = TopDown, yaw = yaw, pitch = pitch,
            distance = follow.CameraDistance, size = topSize, sensitivity = MouseSensitivity, invert = InvertVertical };
        public void Restore(ViewSettings settings)
        {
            if (settings == null) return;
            if (TopDown != settings.topDown) Toggle();
            yaw = settings.yaw; pitch = settings.pitch; follow.CameraDistance = settings.distance;
            topSize = Mathf.Clamp(settings.size, 2, FullSize()); sector = (Mathf.RoundToInt(yaw / 90) % 4 + 4) % 4;
            MouseSensitivity = settings.sensitivity; InvertVertical = settings.invert; Snap();
        }
        public void Snap()
        {
            UpdateTargets(); followCamera.PreviousStateIsValid = false; topCamera.PreviousStateIsValid = false;
        }

        public void Initialize(LevelDefinition definition, Transform target, BoardView view)
        {
            level = definition; player = target; board = view; TopDown = true;
            output = new GameObject("Game Camera", typeof(Camera), typeof(AudioListener), typeof(CinemachineBrain)).GetComponent<Camera>();
            output.transform.SetParent(transform, false); output.tag = "MainCamera";
            output.backgroundColor = new Color(.045f, .065f, .09f); output.clearFlags = CameraClearFlags.SolidColor;
            output.nearClipPlane = .05f; output.farClipPlane = 150;
            output.orthographic = true;
            output.GetComponent<CinemachineBrain>().m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, .25f);
            output.GetComponent<CinemachineBrain>().m_UpdateMethod = CinemachineBrain.UpdateMethod.LateUpdate;
            orbit = new GameObject("Orbit target").transform; orbit.SetParent(transform, false);
            yaw = (int)System.Enum.Parse(typeof(Direction), level.playerSpawn.facing) * 90;
            sector = Mathf.RoundToInt(yaw / 90) % 4;
            followCamera = NewCamera("Third person", 20);
            followCamera.Follow = orbit;
            followCamera.m_Lens.FieldOfView = 55;
            followCamera.m_Lens.NearClipPlane = .05f;
            followCamera.m_Lens.ModeOverride = LensSettings.OverrideModes.Perspective;
            follow = followCamera.AddCinemachineComponent<Cinemachine3rdPersonFollow>();
            follow.ShoulderOffset = Vector3.zero; follow.VerticalArmLength = 0;
            follow.CameraDistance = 4.8f; follow.Damping = new Vector3(.08f, .08f, .08f);
            follow.CameraCollisionFilter = 1; follow.CameraRadius = .12f; follow.DampingIntoCollision = 0; follow.DampingFromCollision = .15f;
            topCamera = NewCamera("North up", 30);
            topCamera.m_Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            topCamera.m_Lens.NearClipPlane = .05f;
            topCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
            topSize = lastFullSize = FullSize(); topCamera.m_Lens.OrthographicSize = topSize;
            UpdateTargets();
        }
        private CinemachineVirtualCamera NewCamera(string label, int priority)
        {
            var camera = new GameObject(label).AddComponent<CinemachineVirtualCamera>();
            camera.transform.SetParent(transform, false); camera.Priority = priority; return camera;
        }
        // Fit inside the HUD's clear centre, including when the player resizes the window.
        private float FullSize() => Mathf.Max((level.height / 2f + .35f) / .62f,
            (level.width / 2f + .35f) / Mathf.Max(.1f, output.aspect * .92f));
        public void Toggle()
        {
            projectionBeforeBlend = output.orthographic;
            TopDown = !TopDown; topCamera.Priority = TopDown ? 30 : 10;
            if (TopDown) topSize = FullSize();
            UpdateTargets();
        }
        public void ReadMouse(bool allow)
        {
            var mouse = Mouse.current;
            if (!allow || mouse == null) return;
            if (!TopDown)
            {
                var delta = mouse.delta.ReadValue();
                yaw += delta.x * .12f * MouseSensitivity;
                pitch = Mathf.Clamp(pitch + delta.y * .12f * MouseSensitivity * (InvertVertical ? 1 : -1), 30, 65);
                follow.CameraDistance = Mathf.Clamp(follow.CameraDistance - mouse.scroll.ReadValue().y / 120f * .2f, 3, 6);
                if (Mathf.Abs(Mathf.DeltaAngle(sector * 90, yaw)) > 53) sector = (Mathf.RoundToInt(yaw / 90) % 4 + 4) % 4;
            }
            else topSize = Mathf.Clamp(topSize - mouse.scroll.ReadValue().y / 120f * .5f, 2, FullSize());
        }
        private void LateUpdate() { if (player) UpdateTargets(); }
        private void UpdateTargets()
        {
            orbit.position = player.position + Vector3.up * .55f; orbit.rotation = Quaternion.Euler(pitch, yaw, 0);
            float fullSize = FullSize();
            topSize = Mathf.Approximately(topSize, lastFullSize) ? fullSize : Mathf.Clamp(topSize, 2, fullSize);
            lastFullSize = fullSize;
            board.PrepareCamera(TopDown, orbit.position - orbit.forward * follow.CameraDistance, player.position);
            float halfX = topSize * output.aspect, halfZ = topSize;
            float x = halfX * 2 >= level.width ? (level.width - 1) / 2f : Mathf.Clamp(player.position.x, halfX - .5f, level.width - .5f - halfX);
            float z = halfZ * 2 >= level.height ? (level.height - 1) / 2f : Mathf.Clamp(player.position.z, halfZ - .5f, level.height - .5f - halfZ);
            topCamera.transform.position = new Vector3(x, 30, z); topCamera.m_Lens.OrthographicSize = topSize;
        }
    }
}
