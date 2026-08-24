using UnityEngine;

[RequireComponent(typeof(BaseSound))]
public class FootstepController : MonoBehaviour
{
    [Header("Tracking Settings")]
    [Tooltip("The target transform to follow (usually the Player).")]
    public Transform playerTransform;
    public bool inactiveOnDeactivate = true;

    [Tooltip("Offset from the player's position (e.g., at the feet).")]
    public Vector3 positionOffset = new Vector3(0, -1f, 0);

    [Header("Movement Logic")]
    [Tooltip("Minimum distance moved per second to trigger footsteps.")]
    public float movementThreshold = 0.1f;

    [Tooltip("Smoothing factor for speed calculation to prevent flickering.")]
    [Range(0.01f, 1f)]
    public float speedSmoothing = 0.5f;

    [Header("Debug")]
    [Tooltip("Check this to print speed and state logs to the Console.")]
    public bool showDebugLogs = true; // 开启 Debug 模式

    private BaseSound baseSound;
    private Vector3 lastPosition;
    private float currentSpeed;

    // Internal states
    private bool isTracking = false;
    private bool isMoving = false;

    private void Awake()
    {
        baseSound = GetComponent<BaseSound>();
        baseSound.inactivateOnFadeOut = false;
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            Debug.LogError($"[{gameObject.name}] Critical Error: Player Transform is missing!");
        }
        else
        {
            lastPosition = playerTransform.position;
        }
    }

    public void ActivateTracking()
    {
        if (isTracking) return;

        isTracking = true;
        if (showDebugLogs) Debug.Log("👣 [Footstep] Tracking ACTIVATED.");
    }

    public void DeactivateTracking()
    {
        if (!isTracking) return;

        isTracking = false;
        if (showDebugLogs) Debug.Log("👣 [Footstep] Tracking DEACTIVATED.");

        if (isMoving)
        {
            isMoving = false;
            baseSound.inactivateOnFadeOut = inactiveOnDeactivate;
            baseSound.FadeOut();
        }
    }

    private void LateUpdate()
    {
        if (playerTransform == null || baseSound == null) return;

        // 1. 一直跟随
        transform.position = playerTransform.position + positionOffset;

        // 2. 如果没被激活，同步坐标后退出
        if (!isTracking)
        {
            lastPosition = playerTransform.position;
            currentSpeed = 0f;
            return;
        }

        // 3. 计算速度
        float distanceMoved = Vector3.Distance(playerTransform.position, lastPosition);
        float instantSpeed = distanceMoved / Time.deltaTime;

        // 平滑速度
        currentSpeed = Mathf.Lerp(currentSpeed, instantSpeed, speedSmoothing);
        lastPosition = playerTransform.position;

        // ======= DEBUG 输出 =======
        if (showDebugLogs && instantSpeed > 0.01f) // 只有产生微小移动时才打印，防止平时刷屏
        {
            Debug.Log($"👣 [Footstep] Instant: {instantSpeed:F3} | Smoothed: {currentSpeed:F3} | Threshold: {movementThreshold}");
        }

        // 4. 声音逻辑
        HandleMovementAudio();
    }

    private void HandleMovementAudio()
    {
        if (currentSpeed > movementThreshold)
        {
            if (!isMoving)
            {
                isMoving = true;
                if (showDebugLogs) Debug.Log("👣 [Footstep] === Started Moving! Triggering FadeIn ===");
                baseSound.FadeIn();
            }
        }
        else
        {
            if (isMoving)
            {
                isMoving = false;
                if (showDebugLogs) Debug.Log("👣 [Footstep] === Stopped Moving! Triggering FadeOut ===");
                baseSound.FadeOut();
            }
        }
    }
}