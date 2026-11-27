
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;

public class HeadTrackingReceiver : MonoBehaviour
{
    [Header("Network")]
    public int port = 4242;

    [Header("Rotation")]
    public float smoothing = 20f;
    public bool invertYaw;
    public bool invertPitch;
    public bool invertRoll;

    [Header("Debug")]
    public float yaw;
    public float pitch;
    public float roll;
    public bool receiving;

    private UdpClient udp;
    private Quaternion targetRotation;
    private Quaternion neutralRotation;
    private float lastReceived;

    void OnEnable()
    {
        neutralRotation = transform.rotation;
        targetRotation = transform.rotation;

        udp = new UdpClient(
            new IPEndPoint(IPAddress.Loopback, port)
        );

        udp.Client.Blocking = false;

        Debug.Log($"Head tracking listening on {port}");
    }

    void Update()
    {
        if (udp == null)
            return;

        IPEndPoint endpoint = new IPEndPoint(
            IPAddress.Any, 0
        );

        while (udp.Available > 0)
        {
            try
            {
                byte[] data = udp.Receive(ref endpoint);

                if (data.Length != 48)
                    continue;

                yaw = (float)BitConverter.ToDouble(data, 24);
                pitch = (float)BitConverter.ToDouble(data, 32);
                roll = (float)BitConverter.ToDouble(data, 40);

                float y = yaw * (invertYaw ? -1 : 1);
                float p = pitch * (invertPitch ? -1 : 1);
                float r = roll * (invertRoll ? -1 : 1);

                targetRotation = neutralRotation *
                    Quaternion.Euler(p, y, r);

                lastReceived = Time.unscaledTime;
                receiving = true;
            }
            catch (SocketException)
            {
                break;
            }
        }

        if (Time.unscaledTime - lastReceived > 1f)
            receiving = false;

        float t = 1f - Mathf.Exp(
            -smoothing * Time.deltaTime
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            t
        );
    }

    void OnDisable()
    {
        udp?.Close();
        udp = null;
        receiving = false;
    }
}
