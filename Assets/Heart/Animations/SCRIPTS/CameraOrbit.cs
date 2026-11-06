using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    public Transform target;
    public float baseDistance = 3.5f;
    public float rotationSpeed = 70f;
    public float zoomSpeed = 3f;

    float currentX;
    float currentY;

    float scaledDistance;

    void Start()
    {
        // Auto-scale distance based on heart size
        float maxScale = Mathf.Max(target.lossyScale.x, target.lossyScale.y, target.lossyScale.z);
        scaledDistance = baseDistance * maxScale * 0.001f;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Rotate
        if (Input.GetMouseButton(0))
        {
            currentX += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            currentY -= Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
        }

        // Zoom
        scaledDistance -= Input.mouseScrollDelta.y * zoomSpeed;
        scaledDistance = Mathf.Clamp(scaledDistance, 1f, 50f);

        // Position
        Quaternion rot = Quaternion.Euler(currentY, currentX, 0);
        Vector3 dir = new Vector3(0, 0, -scaledDistance);

        transform.position = target.position + rot * dir;
        transform.LookAt(target.position);
    }
}