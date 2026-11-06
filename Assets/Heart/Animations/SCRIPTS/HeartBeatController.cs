using UnityEngine;

public class HeartBeatController : MonoBehaviour
{
    [Header("Assign ONLY the inner Heart mesh here")]
    public Transform heartMesh;

    [Header("Heartbeat Settings")]
    public float beatSpeed = 2f;
    public float beatIntensity = 0.03f;  // safe value

    private Vector3 baseScale;

    void Start()
    {
        if (heartMesh == null)
        {
            Debug.LogError("HeartBeatController: No heart mesh assigned!");
            enabled = false;
            return;
        }

        // IMPORTANT: baseline scale (should be 1,1,1 always)
        baseScale = heartMesh.localScale;
    }

    void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * beatSpeed) * beatIntensity;

        // Animate ONLY the mesh, not the parent
        heartMesh.localScale = baseScale * pulse;
    }
}