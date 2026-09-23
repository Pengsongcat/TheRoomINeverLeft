using System;
using UnityEngine;

// The scene's trigger zone. Fires when the visitor's body (Player Body, tag Player) has stayed inside for Stay
// Duration; leaving before that resets the timer, so walking through does not count. Scaled time, so the story's
// fast-forward shortens it too.
[RequireComponent(typeof(Collider))]
public class SceneTrigger : MonoBehaviour
{
    [SerializeField] string _playerTag = "Player";
    [Tooltip("Seconds the visitor must stay inside before the scene triggers. 0 = on entering.")]
    [SerializeField, Min(0f)] float _stayDuration = 0.5f;

    public event Action Entered;

    bool  _inside;
    bool  _fired;
    float _stayed;

    void Reset() => GetComponent<Collider>().isTrigger = true;

    // Turned off and on by its scene; start counting afresh each time it is armed.
    void OnDisable() { _inside = false; _fired = false; _stayed = 0f; }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(_playerTag)) return;
        _inside = true;
        _stayed = 0f;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(_playerTag)) _inside = false;
    }

    void Update()
    {
        if (!_inside || _fired) return;
        _stayed += Time.deltaTime;
        if (_stayed < _stayDuration) return;
        _fired = true;
        Entered?.Invoke();
    }
}
