using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Sets the Wwise Game Parameters of this director's "Wwise RTPC" tracks from their clip curves at the director's
// time. The Wwise RTPC clip only sends its Animated Value when Timeline applies the clip's curve, which it doesn't
// here (the value stays 0, muting the sound), so those tracks are muted and this plays their curves instead.
// The curves are copied from the tracks when Play mode starts in the Editor; Bake From Timeline saves them for builds.
[RequireComponent(typeof(PlayableDirector))]
public sealed class WwiseRtpcCurves : MonoBehaviour
{
    [Serializable]
    struct Curve
    {
        public TrackAsset Track;  // its binding is the object the Game Parameter is set on
        public AK.Wwise.RTPC Rtpc;
        public float Start;
        public AnimationCurve Values;
    }

    [SerializeField] List<Curve> _curves = new List<Curve>();

    PlayableDirector _director;
    GameObject[] _targets;
    float _lastTime = float.NaN;

    void Awake()
    {
        _director = GetComponent<PlayableDirector>();
#if UNITY_EDITOR
        Bake();
#endif
        _targets = new GameObject[_curves.Count];
        for (int i = 0; i < _curves.Count; i++)
            _targets[i] = _director.GetGenericBinding(_curves[i].Track) as GameObject;
    }

    // Checks the time rather than the play state: a director driven by a Control track doesn't report Playing.
    void LateUpdate()
    {
        float time = (float)_director.time;
        if (time == _lastTime) return;
        _lastTime = time;
        for (int i = 0; i < _curves.Count; i++)
        {
            var c = _curves[i];
            if (_targets[i] != null && c.Rtpc.IsValid())
                c.Rtpc.SetValue(_targets[i], c.Values.Evaluate(time - c.Start));
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Bake From Timeline")]
    void Bake()
    {
        var director = GetComponent<PlayableDirector>();
        if (!(director.playableAsset is TimelineAsset timeline)) return;
        _curves.Clear();
        foreach (var track in timeline.GetOutputTracks())
        {
            if (!(track is AkTimelineRtpcTrack)) continue;
            foreach (var clip in track.GetClips())
            {
                if (clip.curves == null) continue;
                var values = UnityEditor.AnimationUtility.GetEditorCurve(clip.curves,
                    UnityEditor.EditorCurveBinding.FloatCurve("", typeof(AkTimelineRtpcPlayable), "template.value"));
                if (values == null) continue;
                _curves.Add(new Curve { Track = track, Rtpc = ((AkTimelineRtpcPlayable)clip.asset).RTPC,
                                        Start = (float)clip.start, Values = values });
            }
        }
    }
#endif
}
