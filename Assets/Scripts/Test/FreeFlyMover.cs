using UnityEngine;
using UnityEngine.InputSystem;

public class FreeFlyMover : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float verticalSpeed = 3f;

    [Header("Rotation")]
    public float rotationSpeed = 120f;

    [Header("Height Clamp")]
    public float minHeight = 0.2f;
    public float maxHeight = 1.8f;

    void Update()
    {
        Move();
        Rotate();
    }

    void Move()
    {
        Vector3 move = Vector3.zero;

        if (Keyboard.current.wKey.isPressed)
            move.z += 1f;

        if (Keyboard.current.sKey.isPressed)
            move.z -= 1f;

        if (Keyboard.current.aKey.isPressed)
            move.x -= 1f;

        if (Keyboard.current.dKey.isPressed)
            move.x += 1f;

        if (Keyboard.current.upArrowKey.isPressed)
            move.y += 1f;

        if (Keyboard.current.downArrowKey.isPressed)
            move.y -= 1f;

        if (move.magnitude > 1f)
            move.Normalize();

        Vector3 newPosition = transform.position + new Vector3(
            move.x * moveSpeed,
            move.y * verticalSpeed,
            move.z * moveSpeed
        ) * Time.deltaTime;

        // Clamp Y
        newPosition.y = Mathf.Clamp(newPosition.y, minHeight, maxHeight);

        transform.position = newPosition;
    }

    void Rotate()
    {
        float rotate = 0f;

        if (Keyboard.current.leftArrowKey.isPressed)
            rotate = -1f;

        if (Keyboard.current.rightArrowKey.isPressed)
            rotate = 1f;

        transform.Rotate(
            0f,
            rotate * rotationSpeed * Time.deltaTime,
            0f,
            Space.Self
        );
    }
}