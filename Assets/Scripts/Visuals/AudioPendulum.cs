using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Swings its object like a pendulum, steadily, while Source's clip is playing on its Timeline:
// angle = amplitude × sin(2π t / Period) around Axis, from its resting aim. When the clip starts the swing grows in
// over Fade seconds; when it ends it dies back down to rest the same way.
// "Playing" is taken from the Timeline, not from the sound, so distance does not matter: at start it finds the
// Audio Tracks bound to Source in the directors above it (a scene's Sound prefab) and swings whenever that director
// is playing inside one of their clips. Retiming a clip in the Timeline moves the swing with it.
// For Scene2.3's clock lights: on each spot light, with that light's clock as Source; Audio Flicker Light on the
// same light sets its brightness.
public class AudioPendulum : MonoBehaviour
{
    [SerializeField] AudioSource _source;

    [Header("Swing")]
    [Tooltip("Largest angle either side of the resting aim, degrees.")]
    [Range(0f, 90f)] [SerializeField] float _amplitude = 35f;
    [Tooltip("Axis of the swing in this object's own space. For a spot light: X or Y swings the beam sideways.")]
    [SerializeField] Vector3 _axis = Vector3.right;
    [Tooltip("Seconds for a full swing there and back.")]
    [Min(0.1f)] [SerializeField] float _period = 2f;
    [Tooltip("Seconds for the swing to grow in when the clip starts, and to die down when it ends.")]
    [Min(0.01f)] [SerializeField] float _fadeSeconds = 1f;

    // Where Source plays: a director and the time ranges of its clips on tracks bound to Source.
    readonly List<(PlayableDirector director, double start, double end)> _clips = new();

    Quaternion _rest;
    float _phase; // 0..1 through a full swing
    float _swing; // 0..1, how much of the amplitude is in use

    void Start()
    {
        if (_source == null) return;
        foreach (var director in _source.GetComponentsInParent<PlayableDirector>(includeInactive: true))
        {
            if (director.playableAsset is not TimelineAsset timeline) continue;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is not AudioTrack || track.muted) continue;
                if (director.GetGenericBinding(track) as AudioSource != _source) continue;
                foreach (var clip in track.GetClips())
                    _clips.Add((director, clip.start, clip.end));
            }
        }
        if (_clips.Count == 0)
            Debug.LogWarning($"[AudioPendulum] {name}: no Timeline Audio Track plays {_source.name}, so it never swings.", this);
    }

    void OnEnable()
    {
        _rest = transform.localRotation;
        _phase = 0f;
        _swing = 0f;
    }

    void OnDisable() => transform.localRotation = _rest;

    void Update()
    {
        float dt = Time.deltaTime;
        _swing = Mathf.MoveTowards(_swing, Playing() ? 1f : 0f, dt / _fadeSeconds);
        if (_swing > 0f) _phase = Mathf.Repeat(_phase + dt / _period, 1f);
        else _phase = 0f; // at rest: start the next swing from the middle

        float angle = _amplitude * Mathf.SmoothStep(0f, 1f, _swing) * Mathf.Sin(2f * Mathf.PI * _phase);
        transform.localRotation = _rest * Quaternion.AngleAxis(angle, _axis);
    }

    // True while a director that plays Source is running inside one of Source's clips.
    bool Playing()
    {
        foreach (var (director, start, end) in _clips)
            if (director != null && director.state == PlayState.Playing && director.time >= start && director.time < end)
                return true;
        return false;
    }
}
