using UnityEngine;

public sealed class PingPongLocalX : MonoBehaviour
{
    [SerializeField] private float minX = 0.5f;
    [SerializeField] private float maxX = 2f;
    [SerializeField, Min(0f)] private float speed = 0.5f;

    private bool movingToMax = true;

    private void OnEnable()
    {
        Vector3 position = transform.localPosition;
        position.x = Mathf.Clamp(position.x, minX, maxX);
        transform.localPosition = position;
    }

    private void Update()
    {
        float targetX = movingToMax ? maxX : minX;
        Vector3 position = transform.localPosition;
        position.x = Mathf.MoveTowards(position.x, targetX, speed * Time.deltaTime);
        transform.localPosition = position;

        if (Mathf.Approximately(position.x, targetX))
        {
            movingToMax = !movingToMax;
        }
    }

    private void OnValidate()
    {
        if (maxX < minX)
        {
            (minX, maxX) = (maxX, minX);
        }
    }
}
