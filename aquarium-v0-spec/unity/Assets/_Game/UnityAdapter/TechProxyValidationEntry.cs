using UnityEngine;

namespace Aquarium.UnityAdapter
{
    // Standalone viewing scene. It is not in EditorBuildSettings and does not start a game session.
    public sealed class TechProxyValidationEntry : MonoBehaviour
    {
        void Awake()
        {
            var cameraObject = new GameObject("Technical proxy camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.18f, 0.55f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.06f);
            camera.nearClipPlane = 0.01f;
            camera.transform.LookAt(transform.TransformPoint(new Vector3(0f, 0.08f, 0f)));
            var lightObject = new GameObject("Technical proxy light");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(40f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            TechProxyVisuals.Present(transform, TechProxyVisuals.FishFile, TechProxyVisuals.SwimClip, false, new Vector3(0f, 0.08f, 0f));
            TechProxyVisuals.Present(transform, TechProxyVisuals.AnubiasFile, TechProxyVisuals.SwayClip, true, new Vector3(-0.22f, 0f, 0f));
            TechProxyVisuals.Present(transform, TechProxyVisuals.FernFile, TechProxyVisuals.SwayClip, true, new Vector3(0.22f, 0f, 0f));
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 640f, 120f), GUI.skin.box);
            GUILayout.Label("Technical proxy validation. Generic tetra L0 plus two plant stand-ins.");
            GUILayout.Label("Not ember tetra, neon tetra, or finished species art. Glass and water are not in this scene.");
            GUILayout.Label("turn_left stays in the fish file and is not played here. L1/L2 are not switched. This scene is not in the player build.");
            GUILayout.EndArea();
        }
    }
}
