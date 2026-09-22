using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

// A marker sphere, a floor ring at the sound's reach and a name label for the sounds of the story part that is on
// now. While a chapter waits, only its scenes' guide sounds are shown, so the view matches what leads the visitor;
// once a scene is triggered, that scene's own sounds are shown instead. Onboarding and Ending show all their
// sounds.
//
// Each sound gets its own colour, hues spread evenly within its scene in hierarchy order, so a marker and its
// range ring are read as a pair and neighbours never look alike. Only a sound that is playing shows its colour;
// a silent one washes out to white at the dim trigger alpha, so what is sounding stands out at a glance.
// Follows the sources every frame, so it shows their placement and the room calibration, at the range heard.
public class SoundSourceVisualizer : DebugVisualizer
{
    [Tooltip("Transparent unlit, both faces. Same materials as the Trigger Visualizer; each sound gets a tinted copy.")]
    [SerializeField] Material      _playingMaterial;
    [Tooltip("Only its alpha is used, for the faded look of a silent sound.")]
    [SerializeField] Material      _silentMaterial;
    [Tooltip("Colour saturation and brightness of the markers.")]
    [SerializeField, Range(0f, 1f)] float _saturation = 0.8f;
    [SerializeField, Range(0f, 1f)] float _brightness = 1f;
    [Tooltip("How far a silent sound washes out towards white. 1 = plain white, 0 = keeps its own colour.")]
    [SerializeField, Range(0f, 1f)] float _silentWhiteness = 0.85f;
    [SerializeField] TMP_FontAsset _font;
    [Tooltip("Material for _font using the 'TextMeshPro/Distance Field Overlay' shader.")]
    [SerializeField] Material      _textMaterial;
    [Tooltip("Marker sphere across, metres.")]
    [SerializeField, Min(0.01f)] float _markerSize = 0.15f;
    [Tooltip("Range ring line width, metres.")]
    [SerializeField, Min(0.001f)] float _ringWidth = 0.01f;
    [Tooltip("Floor for the range rings. Unset: y = 0.")]
    [SerializeField] Transform _floor;
    [Tooltip("Height above the floor, metres. Keeps the ring from z-fighting it.")]
    [SerializeField, Min(0f)] float _ringLift = 0.005f;
    [Tooltip("Label line height, metres.")]
    [SerializeField, Min(0f)] float _fontSize = 0.05f;
    [Tooltip("Gap between the top of the marker and the label, metres.")]
    [SerializeField, Min(0f)] float _labelGap = 0.05f;

    const int Segments = 48;

    class Entry
    {
        public AudioSource  Source;
        public StoryPart    Part;
        public StoryScene   Scene;   // null for Onboarding / Ending
        public bool         IsGuide;
        public GameObject   Root;
        public MeshRenderer Marker;
        public MeshRenderer Ring;
        public Mesh         RingMesh;
        public TextMeshPro  Label;
        public Material     Material; // this sound's tinted copy, shared by its marker and ring
        public Color        Colour;
        public bool         WasPlaying;
        public bool         HasAuthored;
        public float        AuthoredMin, AuthoredMax;
        public string       LastText;
    }

    readonly List<Entry>   _entries  = new();
    readonly Vector3[]     _vertices = new Vector3[Segments * 2];
    Mesh _sphereMesh;

    protected override void Awake()
    {
        base.Awake();
        _sphereMesh = BuiltinSphere();
    }

    void Start()
    {
        // Room Scaled Ranges caches the authored min / max in its Awake, so it is ready to ask by now.
        var ranges = FindFirstObjectByType<RoomScaledRanges>(FindObjectsInactive.Include);
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var part = source.GetComponentInParent<StoryPart>(includeInactive: true);
            if (part == null) continue;
            var e = Build(source, part, source.GetComponentInParent<StoryScene>(includeInactive: true));
            e.HasAuthored = ranges != null && ranges.TryGetAuthoredRange(source, out e.AuthoredMin, out e.AuthoredMax);
            _entries.Add(e);
        }
        Colourise();
    }

    // Hues spread evenly within each scene (or each part, for Onboarding and Ending), in the order the sounds
    // sit in the hierarchy, so the sounds shown together are always as far apart in hue as they can be.
    void Colourise()
    {
        foreach (var group in _entries.GroupBy(e => e.Scene != null ? (Object)e.Scene : e.Part))
        {
            var members = group.ToList();
            for (int i = 0; i < members.Count; i++)
            {
                var e = members[i];
                e.Colour   = Color.HSVToRGB((float)i / members.Count, _saturation, _brightness);
                e.Material = new Material(_playingMaterial) { name = $"Debug Sound {e.Source.name}" };
                e.Marker.sharedMaterial = e.Material;
                e.Ring.sharedMaterial   = e.Material;
                Tint(e, playing: false);
            }
        }
    }

    // Playing: the sound's own colour. Silent: washed out towards white, so colour alone says what is sounding.
    void Tint(Entry e, bool playing)
    {
        var c = playing ? e.Colour : Color.Lerp(e.Colour, Color.white, _silentWhiteness);
        c.a = playing ? _playingMaterial.color.a : _silentMaterial.color.a;
        e.Material.color = c;
        e.WasPlaying = playing;
    }

    void OnDestroy()
    {
        foreach (var e in _entries)
        {
            if (e.RingMesh != null) Destroy(e.RingMesh);
            if (e.Material != null) Destroy(e.Material);
        }
    }

    void LateUpdate()
    {
        if (!IsVisible) return;
        var cam = Camera.main;
        float floorY = (_floor != null ? _floor.position.y : 0f) + _ringLift;

        foreach (var e in _entries)
        {
            e.Root.SetActive(Shows(e));
            if (!e.Root.activeSelf) continue;

            bool playing = e.Source.isPlaying;
            if (playing != e.WasPlaying) Tint(e, playing);
            var centre = e.Source.transform.position;

            e.Marker.transform.SetPositionAndRotation(centre, Quaternion.identity);
            e.Marker.transform.localScale = Vector3.one * _markerSize; // the built-in sphere is 1 m across

            UpdateRing(e, new Vector3(centre.x, floorY, centre.z), e.Source.maxDistance);

            var text = LabelText(e);
            if (text != e.LastText) { e.Label.text = text; e.LastText = text; }

            var label = e.Label.transform;
            label.position = centre + Vector3.up * (_markerSize * 0.5f + _labelGap);
            if (cam != null) label.rotation = Quaternion.LookRotation(label.position - cam.transform.position);
        }
    }

    // The sound's name, then its range: authored > current where the room has scaled it, plain values where
    // it has not (an uncalibrated VR run, or a room the same size as the design space).
    static string LabelText(Entry e)
    {
        float min = e.Source.minDistance, max = e.Source.maxDistance;
        bool scaled = e.HasAuthored &&
                      (!Mathf.Approximately(min, e.AuthoredMin) || !Mathf.Approximately(max, e.AuthoredMax));
        return scaled
            ? $"{e.Source.name}\nmin {e.AuthoredMin:0.0} > {min:0.0}  max {e.AuthoredMax:0.0} > {max:0.0} m"
            : $"{e.Source.name}\nmin {min:0.0}  max {max:0.0} m";
    }

    // While a scene waits, its guide is the only sound shown; while it plays, all of its sounds are.
    // A part without scenes (Onboarding, Ending) shows everything under it.
    static bool Shows(Entry e)
    {
        if (e.Source == null || e.Part == null || !e.Part.gameObject.activeInHierarchy) return false;
        if (e.Scene == null) return true;
        if (!e.Scene.gameObject.activeInHierarchy) return false;
        return e.Scene.Playing || e.IsGuide;
    }

    // A flat ribbon ring of the sound's max distance: where it fades to silence, after the room scaling.
    void UpdateRing(Entry e, Vector3 centre, float radius)
    {
        float half = _ringWidth * 0.5f;
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            _vertices[i]            = centre + dir * (radius + half); // outer
            _vertices[i + Segments] = centre + dir * Mathf.Max(radius - half, 0f); // inner
        }
        e.RingMesh.vertices = _vertices;
        if (e.RingMesh.triangles.Length == 0) BuildRingTopology(e.RingMesh);
        e.RingMesh.RecalculateBounds();
    }

    // One quad per segment, wound to face up (Unity's front face: Cross(b - a, c - a) points at the viewer).
    void BuildRingTopology(Mesh mesh)
    {
        var triangles = new int[Segments * 6];
        for (int i = 0; i < Segments; i++)
        {
            int j = (i + 1) % Segments;
            int oi = i, oj = j, ii = i + Segments, ij = j + Segments;
            var n = Vector3.Cross(_vertices[oj] - _vertices[oi], _vertices[ij] - _vertices[oi]);
            int[] quad = n.y > 0f ? new[] { oi, oj, ij, oi, ij, ii }
                                  : new[] { oi, ij, oj, oi, ii, ij };
            quad.CopyTo(triangles, i * 6);
        }
        mesh.triangles = triangles;

        var normals = new Vector3[Segments * 2];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
        mesh.normals = normals;
    }

    Entry Build(AudioSource source, StoryPart part, StoryScene scene)
    {
        var e = new Entry
        {
            Source  = source,
            Part    = part,
            Scene   = scene,
            IsGuide = scene != null && scene.Guide == source,
            Root    = new GameObject(source.name) { layer = Geometry.gameObject.layer },
        };
        e.Root.transform.SetParent(Geometry, worldPositionStays: false);

        var marker = AddRenderer("Marker", _silentMaterial);
        marker.sharedMesh = _sphereMesh;
        marker.transform.SetParent(e.Root.transform, worldPositionStays: false);
        e.Marker = marker.GetComponent<MeshRenderer>();

        var ring = AddRenderer("Range Ring", _silentMaterial);
        ring.transform.SetParent(e.Root.transform, worldPositionStays: false);
        e.RingMesh = new Mesh { name = "Debug Sound Range" };
        e.RingMesh.MarkDynamic();
        ring.sharedMesh = e.RingMesh;
        e.Ring = ring.GetComponent<MeshRenderer>();

        var text = new GameObject("Label") { layer = Geometry.gameObject.layer };
        text.transform.SetParent(e.Root.transform, worldPositionStays: false);
        e.Label = text.AddComponent<TextMeshPro>();
        if (_font != null) e.Label.font = _font;
        if (_textMaterial != null) e.Label.fontSharedMaterial = _textMaterial;
        e.Label.fontSize         = _fontSize * 10f; // TextMeshPro world text: fontSize 10 ≈ 1 m line height
        e.Label.alignment        = TextAlignmentOptions.Bottom;
        e.Label.textWrappingMode = TextWrappingModes.NoWrap;
        e.Label.rectTransform.sizeDelta = new Vector2(1f, 0.2f);
        e.Label.rectTransform.pivot     = new Vector2(0.5f, 0f);
        return e; // the text is written every frame, name plus range
    }

    static Mesh BuiltinSphere()
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return mesh;
    }
}
