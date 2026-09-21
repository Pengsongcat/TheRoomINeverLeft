using System;
using UnityEngine;

// The chapter's trigger zone. Fires when the visitor's body (Player Body, tag Player) walks in.
[RequireComponent(typeof(Collider))]
public class ChapterTrigger : MonoBehaviour
{
    [SerializeField] string _playerTag = "Player";

    public event Action Entered;

    void Reset() => GetComponent<Collider>().isTrigger = true;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(_playerTag)) Entered?.Invoke();
    }
}
