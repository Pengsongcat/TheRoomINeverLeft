
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class LidarReceiver : MonoBehaviour
{
    [Header("Network")]
    public int port = 5005;

    [Header("Position Mapping")]
    public Transform lidarOrigin;
    public float positionScale = 1f;
    public bool invertX = false;
    public bool invertZ = false;
    public bool useFixedHeight = true;
    public float cameraHeight = 0.8f;

    [Header("Movement")]
    public float smoothing = 8f;
    public float trackingTimeout = 2f;

    [Header("Tracking Debug")]
    public bool isDetected;
    public int humanId = -1;
    public Vector3 rawLidarPosition;
    public Vector3 targetPosition;
    public float humanVelocity;

    private UdpClient udpClient;
    private float lastReceivedTime;

    [Serializable]
    public class HumanData
    {
        public bool detected;
        public int id;
        public float x;
        public float y;
        public float z;
        public float velocity;
    }

    void Start()
    {
        targetPosition = transform.position;

        try
        {
            udpClient = new UdpClient(
                new IPEndPoint(IPAddress.Any, port)
            );

            udpClient.Client.Blocking = false;

            Debug.Log(
                $"LiDAR receiver listening on port {port}"
            );
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
            enabled = false;
        }
    }

    void Update()
    {
        ReceiveData();

        if (Time.unscaledTime - lastReceivedTime
            > trackingTimeout)
        {
            isDetected = false;
        }

        if (isDetected)
        {
            float t = 1f - Mathf.Exp(
                -smoothing * Time.deltaTime
            );

            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                t
            );
        }
    }

    void ReceiveData()
    {
        if (udpClient == null)
            return;

        IPEndPoint endpoint = new IPEndPoint(
            IPAddress.Any, 0
        );

        while (udpClient.Available > 0)
        {
            try
            {
                byte[] bytes = udpClient.Receive(
                    ref endpoint
                );

                string json = Encoding.UTF8.GetString(bytes);

                HumanData data =
                    JsonUtility.FromJson<HumanData>(json);

                if (data == null)
                    continue;

                lastReceivedTime = Time.unscaledTime;
                isDetected = data.detected;

                if (!isDetected)
                    continue;

                humanId = data.id;
                humanVelocity = data.velocity;

                rawLidarPosition = new Vector3(
                    data.x,
                    data.y,
                    data.z
                );

                float x = data.x * (invertX ? -1f : 1f);
                float z = data.y * (invertZ ? -1f : 1f);

                Vector3 mappedPosition = new Vector3(
                    x * positionScale,
                    useFixedHeight
                        ? cameraHeight
                        : data.z * positionScale,
                    z * positionScale
                );

                targetPosition = lidarOrigin != null
                    ? lidarOrigin.TransformPoint(mappedPosition)
                    : mappedPosition;
            }
            catch (SocketException)
            {
                break;
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
            }
        }
    }

    void OnDestroy()
    {
        udpClient?.Close();
        udpClient = null;
    }
}
