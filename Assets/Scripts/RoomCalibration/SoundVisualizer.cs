using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Debug meshes that live in the sound layout itself: each sound's "Visualizer" cylinder, trigger boxes,
// plus extra renderers such as the player's feet / head collider spheres.
// Hidden at startup so visitors never see them; CalibrationFlow shows them with the visualizer toggle.
// Colors are lightened at runtime (debug only, the materials are untouched) and listed on a legend panel.
public class SoundVisualizer : MonoBehaviour
{
    [Serializable]
    public struct LegendLabel
    {
        public Material material;
        public string   label;
    }

    [SerializeField] Transform  _soundLayout;    // every renderer under it
    [SerializeField] Renderer[] _extraRenderers; // e.g. FeetCollider, HeadCollider
    [Tooltip("0 = original material color, 1 = white.")]
    [SerializeField, Range(0f, 1f)] float _lighten = 0.5f;

    [Header("Legend")]
    [SerializeField] CalibrationHud _legend;
    [SerializeField] RoomDebugView  _debugView; // corner, outline and Start Point colors
    [Tooltip("Legend name per material, in legend order. Unlisted materials use their material name.")]
    [SerializeField] LegendLabel[]  _labels;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    readonly List<Renderer>             _renderers      = new();
    readonly Dictionary<Renderer, Color> _rendererColors = new(); // lightened
    readonly List<Material>             _materials      = new(); // distinct, in discovery order
    MaterialPropertyBlock _block;

    public bool MeshesVisible { get; private set; }

    void Awake()
    {
        Collect();
        SetMeshesVisible(false);
        if (_legend != null)
        {
            _legend.SetMessage(BuildLegend());
            _legend.SetVisible(false);
        }
    }

    public void SetMeshesVisible(bool visible)
    {
        MeshesVisible = visible;
        foreach (var r in _renderers)
            if (r != null) r.enabled = visible;
    }

    public void SetLegendVisible(bool visible)
    {
        if (_legend != null) _legend.SetVisible(visible);
    }

    // Lightened visualizer color of a sound object (searched in its children, inactive included).
    public bool TryGetSoundColor(Transform sound, out Color color)
    {
        foreach (var r in sound.GetComponentsInChildren<Renderer>(includeInactive: true))
            if (_rendererColors.TryGetValue(r, out color)) return true;
        color = default;
        return false;
    }

    // ─── Setup ─────────────────────────────────────────────────────────────────

    void Collect()
    {
        _block ??= new MaterialPropertyBlock();

        if (_soundLayout != null)
            foreach (var r in _soundLayout.GetComponentsInChildren<Renderer>(includeInactive: true))
                Add(r);

        if (_extraRenderers != null)
            foreach (var r in _extraRenderers)
                if (r != null) Add(r);
    }

    void Add(Renderer r)
    {
        if (_rendererColors.ContainsKey(r)) return;
        var material = r.sharedMaterial;
        if (material == null) return;

        var color = Lighten(MaterialColor(material));
        _renderers.Add(r);
        _rendererColors[r] = color;
        if (!_materials.Contains(material)) _materials.Add(material);

        r.GetPropertyBlock(_block);
        _block.SetColor(material.HasProperty(BaseColorId) ? BaseColorId : ColorId, color);
        r.SetPropertyBlock(_block);
        _block.Clear();
    }

    string BuildLegend()
    {
        var sb = new StringBuilder("<b>Visualizer</b>  <size=75%>X = hide</size>\n");

        var listed = new HashSet<Material>();
        if (_labels != null)
            foreach (var entry in _labels)
            {
                if (entry.material == null || !_materials.Contains(entry.material) || !listed.Add(entry.material)) continue;
                Line(sb, Lighten(MaterialColor(entry.material)), string.IsNullOrEmpty(entry.label) ? LabelFromName(entry.material) : entry.label);
            }
        foreach (var material in _materials)
            if (listed.Add(material))
                Line(sb, Lighten(MaterialColor(material)), LabelFromName(material));

        if (_debugView != null)
        {
            Line(sb, _debugView.StartPointColor,  "Start Point");
            Line(sb, _debugView.RoomOutlineColor, "Room outline");
            Line(sb, _debugView.RawCornerColor,   "Placed corners");
        }
        sb.Append("<size=75%>Floor ring = trigger reach</size>");
        return sb.ToString();
    }

    static void Line(StringBuilder sb, Color color, string label) =>
        sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append('>').Append(label).Append("</color>\n");

    static string LabelFromName(Material material)
    {
        var n = material.name;
        return n.StartsWith("Unlit ") ? n.Substring("Unlit ".Length) : n;
    }

    static Color MaterialColor(Material material)
    {
        if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
        if (material.HasProperty(ColorId))     return material.GetColor(ColorId);
        return Color.white;
    }

    Color Lighten(Color c)
    {
        var lit = Color.Lerp(c, Color.white, _lighten);
        lit.a = c.a;
        return lit;
    }
}
