using UnityEngine;
using UnityEngine.InputSystem;

// Horizontal rehearsal movement, with a trigger body; deliberately no wall collision or gravity.
public sealed class DesktopTestFirstPerson : MonoBehaviour
{
    Transform view;
    Rigidbody body;
    float speed, sensitivity, pitch;
    Vector2 movement;
    bool captured;
    CursorLockMode previousLock;
    bool previousVisible;

    public void Configure(Transform cameraTransform, Rigidbody playerBody, float moveSpeed, float mouseSensitivity)
    {
        view = cameraTransform;
        body = playerBody;
        speed = moveSpeed;
        sensitivity = mouseSensitivity;
        previousLock = Cursor.lockState;
        previousVisible = Cursor.visible;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        movement = Vector2.zero;
        if (!Application.isFocused || keyboard == null || view == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame) ReleaseCursor();
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            captured = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (!captured || Cursor.lockState != CursorLockMode.Locked) return;
        movement = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
            (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
        movement = Vector2.ClampMagnitude(movement, 1) * (keyboard.leftShiftKey.isPressed ? 2 : 1);
        if (mouse == null) return;
        var delta = mouse.delta.ReadValue() * sensitivity;
        transform.Rotate(0, delta.x, 0);
        pitch = Mathf.Clamp(pitch - delta.y, -85, 85);
        view.localRotation = Quaternion.Euler(pitch, 0, 0);
    }

    void FixedUpdate()
    {
        if (body == null) return;
        var delta = transform.right * movement.x + transform.forward * movement.y;
        body.MovePosition(body.position + delta * (speed * Time.fixedDeltaTime));
    }

    void OnApplicationFocus(bool focused) { if (!focused) ReleaseCursor(); }
    void ReleaseCursor()
    {
        movement = Vector2.zero;
        if (!captured) return;
        Cursor.lockState = previousLock;
        Cursor.visible = previousVisible;
        captured = false;
    }
    void OnDisable() => ReleaseCursor();
}
