using UnityEngine;
using UnityEngine.Rendering.Universal;

// Scene-local test harness. Add an active instance to a scene to run it in desktop mode.
// No project settings, shared assets or XR runtime settings are changed.
[DefaultExecutionOrder(-30000)]
public sealed class DesktopTestMode : MonoBehaviour
{
    [SerializeField] Vector3 spawnPosition = new Vector3(0, 0, -2);
    [SerializeField] float eyeHeight = 1.65f;
    [SerializeField] float moveSpeed = 1.5f;
    [SerializeField] float mouseSensitivity = 0.12f;
    StoryDirector story;
    bool usePC;

    void Awake()
    {
        // This component only runs when its GameObject is active. Disable/remove it for VR.
        usePC = true;

        var roots = gameObject.scene.GetRootGameObjects();
        Camera source = null;
        foreach (var root in roots)
        {
            if (story == null) story = root.GetComponentInChildren<StoryDirector>(true);
            var rig = root.GetComponentInChildren<XRPlatformServices>(true);
            if (rig != null)
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera.CompareTag("MainCamera") && camera.enabled) source = camera;
                root.SetActive(false);
            }
            foreach (var manager in root.GetComponentsInChildren<ExperienceManager>(true))
                manager.gameObject.SetActive(false);
            foreach (var calibrator in root.GetComponentsInChildren<RoomCalibrator>(true))
                calibrator.gameObject.SetActive(false);
            foreach (var portal in root.GetComponentsInChildren<MRPortal>(true))
                portal.gameObject.SetActive(false);
        }

        // Prevent a second listener/view from this scene. Other loaded scenes are not touched.
        foreach (var root in roots)
        {
            foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        }

        var player = new GameObject("PC Player (Runtime)") { tag = "Player", layer = 3 };
        player.transform.SetParent(transform, false);
        player.transform.position = spawnPosition;
        var body = player.AddComponent<CapsuleCollider>();
        body.height = 1.8f;
        body.radius = 0.2f;
        body.center = Vector3.up * 0.9f;
        body.isTrigger = true;
        var rigidbody = player.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.isKinematic = true;

        var view = new GameObject("PC Camera") { tag = "MainCamera" };
        view.transform.SetParent(player.transform, false);
        var pcCamera = view.AddComponent<Camera>();
        if (source != null) pcCamera.CopyFrom(source);
        // CopyFrom also copies the source camera's transform. Restore the first-person pose afterwards.
        view.transform.localPosition = Vector3.up * eyeHeight;
        view.transform.localRotation = Quaternion.identity;
        view.transform.localScale = Vector3.one;
        pcCamera.enabled = true;
        pcCamera.stereoTargetEye = StereoTargetEyeMask.None;
        pcCamera.targetTexture = null;
        pcCamera.targetDisplay = 0;
        var data = pcCamera.GetUniversalAdditionalCameraData();
        data.allowXRRendering = false;
        data.renderPostProcessing = true;
        view.AddComponent<AudioListener>();
        var controls = player.AddComponent<DesktopTestFirstPerson>();
        controls.Configure(view.transform, rigidbody, moveSpeed, mouseSensitivity);
        Debug.Log("[Main Test] PC mode: WASD move, mouse look, Shift faster, Escape release cursor, click to capture. Story starts automatically.", this);
    }

    void Start()
    {
        if (!usePC) return;
        // Start runs after StoryDirector.Awake has hidden and registered its parts.
        if (story != null) story.Begin();
        else Debug.LogError("[Main Test] No StoryDirector in this scene.", this);
    }
}
