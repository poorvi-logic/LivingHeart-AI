using UnityEngine;

public class HeartChamberAnimator : MonoBehaviour
{
    [Header("Contraction Settings")]
    [Range(0f, 0.4f)]
    public float contractionAmount = 0.15f;

    [Range(0.5f, 3f)]
    public float heartbeatSpeed = 1.5f;

    [Range(0f, 3f)]
    public float phaseOffset = 0f; 

    private Vector3 originalScale;
    private Vector3 inwardScale;

    void Start()
    {
        originalScale = transform.localScale;

        // inward-only shrink (does not distort shape)
        inwardScale = new Vector3(
            originalScale.x * 0.98f,
            originalScale.y * 0.92f,
            originalScale.z * 0.98f
        );
    }

    void Update()
    {
        float t = (Mathf.Sin((Time.time * heartbeatSpeed) + phaseOffset) + 1f) / 2f;

        Vector3 final = Vector3.Lerp(originalScale, inwardScale, t * contractionAmount);

        transform.localScale = final;
    }
}