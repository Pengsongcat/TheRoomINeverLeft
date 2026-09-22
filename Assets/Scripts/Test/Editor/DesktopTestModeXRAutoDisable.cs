using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

// Prevents the Editor from starting OpenXR for a desktop test scene.
// The XR project asset is not saved; its original value is restored after Play Mode.
[InitializeOnLoad]
static class DesktopTestModeXRAutoDisable
{
    const string RestoreKey = "TRINL.DesktopTestMode.RestoreXR";

    static DesktopTestModeXRAutoDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                DisableXRForDesktopTest();
                break;

            case PlayModeStateChange.ExitingPlayMode:
            case PlayModeStateChange.EnteredEditMode:
                RestoreXR();
                break;
        }
    }

    static void DisableXRForDesktopTest()
    {
        if (!HasActiveDesktopTestMode()) return;

        XRGeneralSettings settings = XRGeneralSettingsPerBuildTarget
            .XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        if (settings == null || !settings.InitManagerOnStart) return;

        SessionState.SetBool(RestoreKey, true);
        settings.InitManagerOnStart = false;
        Debug.Log("[Desktop Test Mode] Skipping automatic OpenXR startup for this Play Mode session.");
    }

    static void RestoreXR()
    {
        if (!SessionState.GetBool(RestoreKey, false)) return;

        XRGeneralSettings settings = XRGeneralSettingsPerBuildTarget
            .XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        if (settings != null) settings.InitManagerOnStart = true;

        SessionState.EraseBool(RestoreKey);
        Debug.Log("[Desktop Test Mode] Restored automatic OpenXR startup.");
    }

    static bool HasActiveDesktopTestMode()
    {
        foreach (DesktopTestMode mode in Object.FindObjectsByType<DesktopTestMode>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!mode.enabled || !mode.gameObject.activeInHierarchy) continue;
            if (!mode.gameObject.scene.IsValid() || !mode.gameObject.scene.isLoaded) continue;
            return true;
        }

        return false;
    }
}
