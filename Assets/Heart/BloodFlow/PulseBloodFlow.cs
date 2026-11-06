using UnityEngine;

public class PulseBloodFlow : MonoBehaviour
{
    public ParticleSystem ps;
    public float minRate = 8f;
    public float maxRate = 35f;
    public float pulseSpeed = 2f;

    void Start()
    {
        if (ps == null)
            ps = GetComponent<ParticleSystem>();
    }

    void Update()
    {
        if (ps == null) return;

        var emission = ps.emission;

        float rate = Mathf.Lerp(
            minRate,
            maxRate,
            (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f
        );

        emission.rateOverTime = rate;
    }
}