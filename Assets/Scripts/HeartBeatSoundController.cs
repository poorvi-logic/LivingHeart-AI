using UnityEngine;
using TMPro;

public class HeartBeatSoundController : MonoBehaviour
{
    [Header("Mic Input Reference")]
    public MicInput mic;

    [Header("Animators for Heart Parts")]
    public Animator[] heartAnimators;

    [Header("Heart Mesh Renderers (for color change)")]
    public MeshRenderer[] heartRenderers;

    [Header("Particle Systems")]
    public ParticleSystem[] bloodFlowParticles;

    [Header("Heart Colors")]
    public Color calmColor = new Color(0.3f, 0.6f, 1f);
    public Color mediumColor = new Color(1f, 0.5f, 0.5f);
    public Color strongColor = new Color(1f, 0.1f, 0.1f);

    [Header("Particle Rate")]
    public float minParticles = 5f;
    public float maxParticles = 70f;

    [Header("Heartbeat Status Text")]
    public TMP_Text statusText;   // <---- NEW TMP FIELD

    // ORIGINAL MATERIAL + PARTICLE DATA
    Material originalMaterial;
    float[] originalEmissionRates;
    Color[] originalParticleColors;

    void Start()
    {
        // Save original material
        if (heartRenderers != null && heartRenderers.Length > 0)
            originalMaterial = heartRenderers[0].sharedMaterial;

        // Save original particle settings
        if (bloodFlowParticles != null)
        {
            originalEmissionRates = new float[bloodFlowParticles.Length];
            originalParticleColors = new Color[bloodFlowParticles.Length];

            for (int i = 0; i < bloodFlowParticles.Length; i++)
            {
                var ps = bloodFlowParticles[i];
                originalEmissionRates[i] = ps.emission.rateOverTime.constant;
                originalParticleColors[i] = ps.main.startColor.color;
            }
        }
    }

    void Update()
    {
        if (mic == null) return;

        float intensity = Mathf.Clamp01(mic.smoothedLevel);

        UpdateHeartbeatAnimation(intensity);
        UpdateColor(intensity);
        UpdateParticles(intensity);
        UpdateStatusText(intensity);   // <---- NEW
    }

    // ----------------------------------------------------------
    // 🔥 HEARTBEAT ANIMATOR UPDATE
    // ----------------------------------------------------------
    void UpdateHeartbeatAnimation(float intensity)
    {
        if (heartAnimators == null) return;

        foreach (Animator anim in heartAnimators)
        {
            if (anim != null)
            {
                anim.SetFloat("HeartBeatIntensity", intensity);
            }
        }
    }

    // ----------------------------------------------------------
    // 🎨 COLOR UPDATE
    // ----------------------------------------------------------
    void UpdateColor(float intensity)
    {
        Color target;

        if (intensity < 0.1f) target = calmColor;
        else if (intensity < 0.90f) target = mediumColor;
        else target = strongColor;

        foreach (var r in heartRenderers)
        {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);

            mpb.SetColor("_BaseColor", target);
            mpb.SetColor("_Color", target);

            r.SetPropertyBlock(mpb);
        }
    }

    // ----------------------------------------------------------
    // 💧 PARTICLE UPDATE
    // ----------------------------------------------------------
    void UpdateParticles(float intensity)
    {
        float rate = Mathf.Lerp(minParticles, maxParticles, intensity);

        foreach (var ps in bloodFlowParticles)
        {
            var emission = ps.emission;
            emission.rateOverTime = rate;
        }
    }

    // ----------------------------------------------------------
    // 📝 NEW — UPDATE HEART STATUS TEXT
    // ----------------------------------------------------------
    void UpdateStatusText(float intensity)
    {
        if (statusText == null) return;

        if (intensity < 0.1f)
        {
            statusText.text = "Calm";
            statusText.color = calmColor;
        }
        else if (intensity < 0.9f)
        {
            statusText.text = "Normal";
            statusText.color = mediumColor;
        }
        else
        {
            statusText.text = "Stressed";
            statusText.color = strongColor;
        }
    }

    // ----------------------------------------------------------
    // 🔄 RESET EVERYTHING
    // ----------------------------------------------------------
    public void ResetHeart()
    {
        foreach (var anim in heartAnimators)
        {
            if (anim != null)
                anim.SetFloat("HeartBeatIntensity", 0f);
        }

        foreach (var r in heartRenderers)
        {
            if (r != null)
            {
                r.SetPropertyBlock(null);
                r.sharedMaterial = originalMaterial;
            }
        }

        for (int i = 0; i < bloodFlowParticles.Length; i++)
        {
            var ps = bloodFlowParticles[i];
            var emission = ps.emission;
            emission.rateOverTime = originalEmissionRates[i];

            var main = ps.main;
            main.startColor = originalParticleColors[i];
        }

        if (statusText != null)
        {
            statusText.text = "Calm";
            statusText.color = calmColor;
        }
    }
}
