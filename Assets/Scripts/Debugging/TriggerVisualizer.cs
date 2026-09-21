using System.Collections.Generic;
using TMPro;
using UnityEngine;

// A see-through sphere with the chapter's name for every chapter trigger in the scene that is on now. Armed
// triggers (the visitor can walk into them) are bright; the rest of that scene's triggers (playing, played, or
// turned off while a sibling plays) are dim. Follows the colliders every frame, so it shows their placement and
// the room calibration, at the size that actually fires.
public class TriggerVisualizer : DebugVisualizer
{
    [Tooltip("Transparent unlit, both faces (visible from inside).")]
    [SerializeField] Material      _armedMaterial;
    [SerializeField] Material      _offMaterial;
    [SerializeField] TMP_FontAsset _font;
    [Tooltip("Material for _font using the 'TextMeshPro/Distance Field Overlay' shader.")]
    [SerializeField] Material      _textMaterial;
    [Tooltip("Label line height, metres.")]
    [SerializeField, Min(0f)] float _fontSize = 0.05f;
    [Tooltip("Gap between the top of the sphere and the label, metres.")]
    [SerializeField, Min(0f)] float _labelGap = 0.05f;

    class Entry
    {
        public ChapterTrigger Trigger;
        public Collider       Collider;
        public StoryScene     Scene;
        public GameObject     Root;
        public MeshRenderer   Sphere;
        public TextMeshPro    Label;
    }

    readonly List<Entry> _entries = new();
    Mesh _sphereMesh;

    protected override void Awake()
    {
        base.Awake();
        _sphereMesh = BuiltinSphere();
    }

    void Start()
    {
        foreach (var trigger in FindObjectsByType<ChapterTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            _entries.Add(Build(trigger));
    }

    void LateUpdate()
    {
        if (!IsVisible) return;
        var cam = Camera.main;

        foreach (var e in _entries)
        {
            bool show = e.Trigger != null && e.Scene != null && e.Scene.gameObject.activeInHierarchy;
            e.Root.SetActive(show);
            if (!show) continue;

            WorldSphere(e.Collider, out var centre, out float radius);
            var sphere = e.Sphere.transform;
            sphere.SetPositionAndRotation(centre, Quaternion.identity);
            sphere.localScale = Vector3.one * (radius * 2f); // the built-in sphere is 1 m across

            e.Sphere.sharedMaterial = e.Trigger.gameObject.activeInHierarchy ? _armedMaterial : _offMaterial;

            var label = e.Label.transform;
            label.position = centre + Vector3.up * (radius + _labelGap);
            if (cam != null) label.rotation = Quaternion.LookRotation(label.position - cam.transform.position);
        }
    }

    Entry Build(ChapterTrigger trigger)
    {
        var chapter = trigger.GetComponentInParent<Chapter>(includeInactive: true);
        var e = new Entry
        {
            Trigger  = trigger,
            Collider = trigger.GetComponent<Collider>(),
            Scene    = trigger.GetComponentInParent<StoryScene>(includeInactive: true),
            Root     = new GameObject(chapter != null ? chapter.name : trigger.name) { layer = Geometry.gameObject.layer },
        };
        e.Root.transform.SetParent(Geometry, worldPositionStays: false);

        var sphere = AddRenderer("Sphere", _offMaterial);
        sphere.sharedMesh = _sphereMesh;
        sphere.transform.SetParent(e.Root.transform, worldPositionStays: false);
        e.Sphere = sphere.GetComponent<MeshRenderer>();

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
        e.Label.text = e.Root.name;
        return e;
    }

    // The world-space sphere a collider fires at. A sphere collider takes its radius from the largest scale axis,
    // so under the room's (s, 1, s) scale it never shrinks below its authored radius. Other shapes: bounding sphere.
    static void WorldSphere(Collider collider, out Vector3 centre, out float radius)
    {
        var t = collider.transform;
        if (collider is SphereCollider s)
        {
            var scale = t.lossyScale;
            centre = t.TransformPoint(s.center);
            radius = s.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return;
        }
        var b = collider.bounds; // empty while the collider is off, so fall back to the transform
        centre = b.size == Vector3.zero ? t.position : b.center;
        radius = b.extents.magnitude;
    }

    static Mesh BuiltinSphere()
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return mesh;
    }
}
