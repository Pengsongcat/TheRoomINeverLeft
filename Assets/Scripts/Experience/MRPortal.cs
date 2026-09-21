using System;
using System.Collections;
using UnityEngine;

// A door the visitor walks through to go from MR into the VR world, like entering a room.
// Its front opening shows the VR world (see 'TRINL/Portal' and PassthroughMask). When the visitor's head passes
// through the doorway from the front (local −Z), just before the door surface so the opening is never clipped by
// the camera's near plane, the portal raises Entered and turns on its back opening, which keeps the real room
// visible through the doorway behind them. Once they are _dissolveDistance past the door it dissolves from the
// top down and raises Closed.
public class MRPortal : MonoBehaviour
{
    static readonly int CutYId = Shader.PropertyToID("_CutY");

    [Tooltip("Every renderer that dissolves (openings and frame).")]
    [SerializeField] Renderer[] _parts;
    [Tooltip("Back-facing opening that shows the real room; turned on when the visitor enters.")]
    [SerializeField] GameObject _backOpening;
    [Tooltip("Doorway size, metres (width × height).")]
    [SerializeField] Vector2 _doorway = new Vector2(1f, 2.1f);
    [Tooltip("Height of the top of the frame above the portal's base, metres.")]
    [SerializeField, Min(0f)] float _height = 2.15f;
    [Tooltip("The switch happens when the head is this far in front of the door surface, metres.")]
    [SerializeField, Min(0f)] float _switchDistance = 0.15f;
    [Tooltip("The dissolve starts once the head is this far past the door surface, metres.")]
    [SerializeField, Min(0f)] float _dissolveDistance = 1f;
    [SerializeField, Min(0.01f)] float _dissolveSeconds = 3f;

    public event Action Entered;
    public event Action Closed;

    MaterialPropertyBlock _block;
    Transform _head;
    float     _lastZ;
    bool      _entered;

    void Awake() => _block = new MaterialPropertyBlock();

    void OnEnable()
    {
        _entered = false;
        _lastZ = float.NaN;
        if (_backOpening != null) _backOpening.SetActive(false);
        SetCut(10000f);
    }

    void Update()
    {
        if (_entered) return;
        if (_head == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            _head = cam.transform;
        }

        var p = transform.InverseTransformPoint(_head.position);
        float line = -_switchDistance;
        bool inDoorway = Mathf.Abs(p.x) < _doorway.x * 0.5f && p.y > 0f && p.y < _doorway.y;
        bool crossed   = !float.IsNaN(_lastZ) && _lastZ < line && p.z >= line;
        _lastZ = p.z;
        if (!crossed || !inDoorway) return;

        _entered = true;
        if (_backOpening != null) _backOpening.SetActive(true);
        Debug.Log("[MRPortal] Visitor entered through the portal.");
        Entered?.Invoke();
        StartCoroutine(Dissolve());
    }

    IEnumerator Dissolve()
    {
        // Keep the doorway open until the visitor has walked far enough into the room.
        while (transform.InverseTransformPoint(_head.position).z < _dissolveDistance)
            yield return null;

        float bottom = transform.position.y;
        float top    = bottom + _height;
        for (float t = 0f; t < _dissolveSeconds; t += Time.deltaTime)
        {
            SetCut(Mathf.Lerp(top, bottom, t / _dissolveSeconds));
            yield return null;
        }
        SetCut(bottom - 1f);
        Closed?.Invoke();
        gameObject.SetActive(false);
    }

    void SetCut(float worldY)
    {
        _block.SetFloat(CutYId, worldY);
        foreach (var r in _parts)
            if (r != null) r.SetPropertyBlock(_block);
    }
}
