using TMPro;
using UnityEngine;

// World-space instruction panel for staff during calibration.
// Sits in front of the headset slightly below eye level and follows the head lazily:
// it only glides back once the view has turned or moved far enough, so it is easy to read.
public class CalibrationHud : MonoBehaviour
{
    [Header("Placement")]
    [SerializeField] float _distance       = 1.2f;  // metres in front of the head
    [SerializeField] float _heightOffset   = -0.25f; // metres relative to eye height
    [SerializeField] float _sideOffset     = 0f;    // metres to the right of the view direction
    [SerializeField] float _refollowAngle  = 25f;   // degrees the view may drift before the panel follows
    [SerializeField] float _refollowDistance = 0.4f;
    [SerializeField] float _followSpeed    = 4f;

    [Header("Look")]
    [SerializeField] TMP_FontAsset _font;           // optional; TMP default font when empty
    [SerializeField] float _fontSize       = 0.045f; // world units (metres)
    [SerializeField] float _width          = 0.8f;
    [SerializeField] TextAlignmentOptions _alignment = TextAlignmentOptions.Center;
    [SerializeField] Color _textColor      = Color.white;
    [SerializeField] Color _errorColor     = new Color(1f, 0.35f, 0.3f);
    [SerializeField] Color _backgroundColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] float _errorSeconds   = 4f;

    Transform   _panel;
    TextMeshPro _text;
    Transform   _background;
    string      _message = "";
    string      _error;
    float       _errorUntil;
    Vector3     _targetPosition;
    bool        _following;
    bool        _placed;

    // Built on first use too, so other components may call in from their own Awake.
    void Awake() => Build();

    public void SetMessage(string message)
    {
        Build();
        _message = message ?? "";
        Refresh();
    }

    // Shown in red under the current message for a few seconds.
    public void ShowError(string error)
    {
        _error      = error;
        _errorUntil = Time.unscaledTime + _errorSeconds;
        Refresh();
    }

    public void ClearError()
    {
        _error = null;
        Refresh();
    }

    public void SetVisible(bool visible)
    {
        Build();
        _panel.gameObject.SetActive(visible);
        if (visible) _placed = false; // snap in front of the head when shown again
    }

    void LateUpdate()
    {
        if (_panel == null || !_panel.gameObject.activeSelf) return;

        if (_error != null && Time.unscaledTime > _errorUntil)
        {
            _error = null;
            Refresh();
        }

        var rig = XRPlatformServices.Instance != null ? XRPlatformServices.Instance.Rig : null;
        var head = rig?.CenterEyeAnchor;
        if (head == null) return;

        var forward = head.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) forward = head.up; // looking straight down
        forward.y = 0f;
        forward.Normalize();

        var right   = Vector3.Cross(Vector3.up, forward);
        var desired = head.position + forward * _distance + right * _sideOffset + Vector3.up * _heightOffset;

        if (!_placed)
        {
            _panel.position = desired;
            _targetPosition = desired;
            _placed = true;
        }
        else
        {
            var toPanel   = _panel.position - head.position;
            var toDesired = desired - head.position;
            toPanel.y   = 0f;
            toDesired.y = 0f;
            bool drifted = Vector3.Angle(toDesired, toPanel) > _refollowAngle ||
                           Mathf.Abs(toPanel.magnitude - toDesired.magnitude) > _refollowDistance;
            if (drifted) _following = true;
            if (_following)
            {
                _targetPosition = desired;
                if (Vector3.Distance(_panel.position, desired) < 0.02f) _following = false;
            }
            _panel.position = Vector3.Lerp(_panel.position, _targetPosition, 1f - Mathf.Exp(-_followSpeed * Time.unscaledDeltaTime)); // unscaled: unaffected by DevFastForward
        }

        // Face the head.
        var look = _panel.position - head.position;
        if (look.sqrMagnitude > 1e-4f) _panel.rotation = Quaternion.LookRotation(look, Vector3.up);
    }

    void Refresh()
    {
        if (_text == null) return;
        _text.text = _error == null
            ? _message
            : $"{_message}\n<color=#{ColorUtility.ToHtmlStringRGB(_errorColor)}>{_error}</color>";

        // Fit the background to the text.
        _text.ForceMeshUpdate();
        var size = _text.GetRenderedValues(false);
        const float pad = 0.03f;
        _background.localScale = new Vector3(_width + pad * 2f, Mathf.Max(size.y, _fontSize) + pad * 2f, 1f);
    }

    void Build()
    {
        if (_panel != null) return;
        _panel = new GameObject($"{name}Panel").transform;
        _panel.SetParent(transform, worldPositionStays: false);

        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "Background";
        Destroy(bg.GetComponent<Collider>());
        _background = bg.transform;
        _background.SetParent(_panel, worldPositionStays: false);
        _background.localPosition = new Vector3(0f, 0f, 0.005f); // just behind the text
        var bgRenderer = bg.GetComponent<Renderer>();
        bgRenderer.material = new Material(Shader.Find("Sprites/Default")) { color = _backgroundColor };

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(_panel, worldPositionStays: false);
        _text = textGo.AddComponent<TextMeshPro>();
        if (_font != null) _text.font = _font;
        _text.fontSize         = _fontSize * 10f; // TextMeshPro world text: fontSize 10 ≈ 1 m line height
        _text.color            = _textColor;
        _text.alignment        = _alignment;
        _text.textWrappingMode = TextWrappingModes.Normal;
        _text.rectTransform.sizeDelta = new Vector2(_width, 0.5f);

        Refresh();
    }
}
