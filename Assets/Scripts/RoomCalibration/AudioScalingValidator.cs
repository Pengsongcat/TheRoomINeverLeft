using UnityEngine;
using SteamAudio;

public class AudioScalingValidator : MonoBehaviour
{
    private void Awake()
    {
        var sources = FindObjectsByType<SteamAudioSource>(FindObjectsSortMode.None);
        bool anyPhysicsBased = false;

        foreach (var source in sources)
        {
            if (source.distanceAttenuation && source.distanceAttenuationInput == DistanceAttenuationInput.PhysicsBased)
            {
                Debug.LogError(
                    $"[AudioScalingValidator] '{source.gameObject.name}' uses Physics Based attenuation. " +
                    "Room scaling will not work correctly — Steam Audio Physics Based ignores MinDistance/MaxDistance. " +
                    "Switch to Curve Driven on this SteamAudioSource.",
                    source.gameObject);
                anyPhysicsBased = true;
            }
        }

        if (anyPhysicsBased)
            Debug.Break();

        if (!anyPhysicsBased)
            Debug.Log("[AudioScalingValidator] All SteamAudioSources are Curve Driven. Room scaling is safe.");
    }
}
