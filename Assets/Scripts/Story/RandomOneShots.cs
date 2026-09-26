using System.Collections;
using UnityEngine;

// While enabled, plays single sounds cut from one recording (e.g. one sigh out of a take of several) at random gaps,
// each with a little random pitch and volume, never the same piece twice in a row. Switch it on and off with an
// Activation track to say when it may sound. Scaled time, so the story's fast-forward shortens the gaps too.
public class RandomOneShots : MonoBehaviour
{
    [Tooltip("Plays the pieces: Loop off, Play On Awake off. Its Volume is the loudest a piece gets.")]
    [SerializeField] AudioSource _source;
    [Tooltip("The recording to cut from. Empty: the source's own clip.")]
    [SerializeField] AudioClip _clip;
    [Tooltip("Each piece's start and end in the recording, seconds (x = start, y = end). Empty: the whole clip.")]
    [SerializeField] Vector2[] _pieces;

    [Tooltip("Silence between the end of one piece and the start of the next, seconds (x = shortest, y = longest).")]
    [SerializeField] Vector2 _gap = new Vector2(1.5f, 5f);
    [Tooltip("Wait before the first piece after switching on, seconds (x = shortest, y = longest).")]
    [SerializeField] Vector2 _firstDelay = new Vector2(0.3f, 1.5f);
    [SerializeField] float _pitchJitter = 0.06f;
    [Tooltip("How much quieter than the source's Volume a piece may be: 0.3 = anywhere from 70% to 100%.")]
    [SerializeField, Range(0f, 1f)] float _volumeJitter = 0.3f;
    [Tooltip("Overall loudness, applied live (also to a piece already playing). Animate it from a Timeline " +
             "Animation track for a swell or a fade.")]
    [SerializeField, Range(0f, 1f)] float _level = 1f;

    float _fullVolume;
    float _pieceVolume = 1f;
    int _lastPiece = -1;
    Coroutine _run;

    void Awake() => _fullVolume = _source.volume;

    void Update() => _source.volume = _fullVolume * _level * _pieceVolume;

    void OnEnable() => _run = StartCoroutine(Run());

    void OnDisable()
    {
        if (_run != null) StopCoroutine(_run);
        _run = null;
        _source.Stop();
        _source.volume = _fullVolume;
        _source.pitch = 1f;
    }

    IEnumerator Run()
    {
        var clip = _clip != null ? _clip : _source.clip;
        if (clip == null) yield break;
        _source.clip = clip;
        // Seeking into a clip only works once its data is loaded (it is not, when Preload Audio Data is off).
        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        yield return new WaitForSeconds(Random.Range(_firstDelay.x, _firstDelay.y));
        while (clip.loadState == AudioDataLoadState.Loading) yield return null;
        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            Debug.LogWarning($"[RandomOneShots] {name}: could not load {clip.name}.", this);
            yield break;
        }

        while (true)
        {
            Vector2 piece = Pick(clip);
            float pitch = 1f + Random.Range(-_pitchJitter, _pitchJitter);
            _source.pitch = pitch;
            _pieceVolume = 1f - Random.Range(0f, _volumeJitter);
            _source.volume = _fullVolume * _level * _pieceVolume;
            _source.time = piece.x;
            _source.Play();

            // A higher pitch plays the piece faster, so it also ends sooner.
            float length = (piece.y - piece.x) / pitch;
            _source.SetScheduledEndTime(AudioSettings.dspTime + length);
            yield return new WaitForSeconds(length + Random.Range(_gap.x, _gap.y));
        }
    }

    Vector2 Pick(AudioClip clip)
    {
        if (_pieces == null || _pieces.Length == 0) return new Vector2(0f, clip.length);
        int i = Random.Range(0, _pieces.Length);
        if (_pieces.Length > 1 && i == _lastPiece) i = (i + 1 + Random.Range(0, _pieces.Length - 1)) % _pieces.Length;
        _lastPiece = i;
        var p = _pieces[i];
        return new Vector2(Mathf.Clamp(p.x, 0f, clip.length), Mathf.Clamp(p.y, p.x, clip.length));
    }
}
