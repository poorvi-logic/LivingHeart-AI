using UnityEngine;

public class CameraWOWOrbit : MonoBehaviour
{
    public Transform target;

    [Header("Orbit Settings")]
    public float rotationSpeed = 100f;    
    public float smoothTime = 0.08f;
    public float inertia = 0.92f;        // movement keeps flowing — WOW effect!
    public float distance = 40f;

    private float yaw;                   
    private float pitch;
    private float yawVelocity;
    private float pitchVelocity;

    private float inertiaYaw;
    private float inertiaPitch;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    void Update()
    {
        bool isDragging = Input.GetMouseButton(1);

        if (isDragging)
        {
            // Mouse input
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;

            // Add rotation
            yaw += mouseX;
            pitch -= mouseY;

            pitch = Mathf.Clamp(pitch, -70f, 70f);

            // Store inertia
            inertiaYaw = mouseX;
            inertiaPitch = mouseY;
        }
        else
        {
            // Apply cinematic floating inertia
            yaw += inertiaYaw;
            pitch -= inertiaPitch;

            inertiaYaw *= inertia;       // gradually slow down
            inertiaPitch *= inertia;
        }

        // Smooth rotation
        float smoothYaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, yaw, ref yawVelocity, smoothTime);
        float smoothPitch = Mathf.SmoothDampAngle(transform.eulerAngles.x, pitch, ref pitchVelocity, smoothTime);

        // Apply rotation
        Quaternion rotation = Quaternion.Euler(smoothPitch, smoothYaw, 0f);
        transform.rotation = rotation;

        // Keep camera at distance
        transform.position = target.position - transform.forward * distance;
    }
}