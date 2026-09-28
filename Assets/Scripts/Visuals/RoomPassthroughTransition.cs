using System;
using System.Collections;
using UnityEngine;

// Put on Room. Started by the Door Exit Dissolve when the door starts to dissolve, with the door room's opacity
// at that moment. The door room is its colour (Door Portal's colour × Interior Base Brightness, on maskDoor) at that
// opacity over passthrough; this first tints the whole passthrough to look the same (darker or brighter to match
// that colour, flatter, paler), then fades the door room out while the passthrough turns dark grey and
// semi-transparent, so the door room melts into the real one. Once the room has faded and the door has
// dissolved, the MR Portal is turned off.
public class RoomPassthroughTransition : MonoBehaviour
{
    [Tooltip("Turned off once the room has faded and the door has dissolved (Room/MR Portal).")]
    [SerializeField] GameObject _mrPortal;
    [SerializeField, Min(0.01f)] float _duration = 3f;
    [Tooltip("Shape of the fade. 1 = linear; higher = the room lingers, then goes quicker.")]
    [SerializeField, Range(0.25f, 4f)] float _curve = 1f;

    [Header("Match the door room")]
    [Tooltip("How strongly the passthrough is tinted to match the door room at the start: brightness toward the " +
             "room colour (dark → −, light → +), contrast and saturation −, each by room opacity × this. Tune in " +
             "the headset until the hand-off is invisible.")]
    [SerializeField, Range(0f, 1f)] float _matchStrength = 1f;

    [Header("End look of the passthrough")]
    [SerializeField, Range(-1f, 1f)] float _endBrightness = -0.5f;
    [SerializeField, Range(-1f, 1f)] float _endContrast = 0f;
    [Tooltip("-1 = grey.")]
    [SerializeField, Range(-1f, 1f)] float _endSaturation = -1f;
    [Tooltip("Passthrough opacity at the end; below 1 it is semi-transparent over black.")]
    [SerializeField, Range(0f, 1f)] float _endOpacity = 0.5f;

    public bool Running { get; private set; }
    public bool Done    { get; private set; }

    // Raised once the MR Portal is off. The Experience Manager then hides the Passthrough Mask, which would
    // otherwise turn the whole view into passthrough now that nothing marks the portal.
    public event Action Finished;

    // Called by the Door Exit Dissolve as its dissolve begins.
    public void Begin(DoorPortal portal, float roomOpacity, DoorExitDissolve door)
    {
        if (Running || Done) return;
        StartCoroutine(Run(portal, Mathf.Clamp01(roomOpacity), door));
    }

    IEnumerator Run(DoorPortal portal, float roomOpacity, DoorExitDissolve door)
    {
        Running = true;
        Debug.Log($"[RoomPassthroughTransition] Started; door room opacity {roomOpacity:0.00}, " +
                  $"colour brightness {RoomBrightness(portal):0.00}.", this);
        var passthrough = XRPlatformServices.Instance != null ? XRPlatformServices.Instance.PassthroughService : null;

        float match = roomOpacity * _matchStrength;
        float roomBrightness = RoomBrightness(portal);
        float startOpacity = passthrough != null ? passthrough.Opacity : 1f;

        for (float t = 0f; ; t += Time.deltaTime)
        {
            float k = Mathf.Pow(Mathf.Clamp01(t / _duration), _curve);
            if (portal != null) portal.insideOpacityOverride = Mathf.Lerp(roomOpacity, 0f, k);
            if (passthrough != null)
            {
                passthrough.SetColorAdjustment(
                    Mathf.Lerp(match * roomBrightness, _endBrightness, k),
                    Mathf.Lerp(-match, _endContrast, k),
                    Mathf.Lerp(-match, _endSaturation, k));
                passthrough.Opacity = Mathf.Lerp(startOpacity, _endOpacity, k);
            }
            if (t >= _duration) break;
            yield return null;
        }

        // The door may still be dissolving; turning the MR Portal off earlier would cut it short.
        while (door != null && door.isActiveAndEnabled && !door.IsComplete) yield return null;

        if (portal != null) portal.insideOpacityOverride = -1f;
        if (_mrPortal != null) _mrPortal.SetActive(false);
        Running = false;
        Done = true;
        Debug.Log("[RoomPassthroughTransition] Room faded; MR Portal turned off.", this);
        Finished?.Invoke();
    }

    // The door room's colour as a passthrough brightness: black → −1, mid grey → 0, white (or brighter) → 1.
    // Its interior light is left out; the Door Exit Dissolve fades it with the door.
    static float RoomBrightness(DoorPortal portal)
    {
        if (portal == null) return 1f;
        var c = portal.whiteEmission;
        float luminance = (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) * portal.interiorBaseBrightness;
        return Mathf.Clamp(luminance * 2f - 1f, -1f, 1f);
    }
}
