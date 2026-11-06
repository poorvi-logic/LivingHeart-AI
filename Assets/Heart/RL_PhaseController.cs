// RL_PhaseController.cs
using System.Collections;
using UnityEngine;
using TMPro;

public class RL_PhaseController : MonoBehaviour
{
    [Header("Core UI / Scene")]
    public TextMeshProUGUI vitalsText;
    public Light phaseLight;
    public Transform heartModel;

    [Header("Blood (assign all shuriken particle systems)")]
    public ParticleSystem[] bloodParticles;

    [Header("Arc points (equal length): arcSources[i] -> arcTargets[i]")]
    public Transform[] arcSources;
    public Transform[] arcTargets;

    [Header("Shockwave settings")]
    public int shockRings = 5;
    public float shockRingDuration = 5.5f;
    public float shockRingMaxRadius = 2.6f;

    private Vector3 baseHeartScale;
    private Color baseHeartColor = Color.white;

    private bool running = false;
    private Coroutine flowCoroutine;

    // Lighting & phase colors
    private Color normalLightColor = Color.white;
    private Color phase2Color = new Color(1f, 0.85f, 0.2f);
    private float phase2Intensity = 1.1f;
    private Color phase3Color = new Color(0.8f, 0.4f, 0.1f);
    private Color arrestColor = new Color(0.25f, 0f, 0.02f);

    // Fog
    private Color fogColor = new Color(0.15f, 0f, 0f);
    private float fogDensityTarget = 0.03f;

    // Original blood settings
    private float[] originalEmissionRates;
    private float[] originalStartSpeeds;

    void Awake()
    {
        baseHeartScale = heartModel.localScale;

        if (bloodParticles != null)
        {
            originalEmissionRates = new float[bloodParticles.Length];
            originalStartSpeeds = new float[bloodParticles.Length];

            for (int i = 0; i < bloodParticles.Length; i++)
            {
                var em = bloodParticles[i].emission;
                originalEmissionRates[i] = em.rateOverTime.constant;

                var main = bloodParticles[i].main;
                originalStartSpeeds[i] = main.startSpeedMultiplier;
            }
        }
    }

    void Start()
    {
        vitalsText.gameObject.SetActive(false);
        phaseLight.gameObject.SetActive(false);

        RenderSettings.fog = false;
        RenderSettings.fogDensity = 0f;
    }

    // -----------------------------------------------------
    // PUBLIC START BUTTON
    // -----------------------------------------------------
    public void StartRLModule()
    {
        if (running) return;

        running = true;
        vitalsText.gameObject.SetActive(true);

        RestoreBlood();

        flowCoroutine = StartCoroutine(MainFlow());
    }

    // -----------------------------------------------------
    // PUBLIC STOP BUTTON — FULL INSTANT RESET
    // -----------------------------------------------------
    public void StopRLModule()
    {
        if (!running) return;

        running = false;

        // Stop main coroutine
        if (flowCoroutine != null)
            StopCoroutine(flowCoroutine);

        // Kill all remaining arcs & shockwaves instantly
        KillRuntimeObjects();

        // Reset UI
        vitalsText.gameObject.SetActive(false);

        // Reset fog
        RenderSettings.fog = false;
        RenderSettings.fogDensity = 0f;

        // Reset heart
        heartModel.localScale = baseHeartScale;
        var r = heartModel.GetComponentInChildren<Renderer>();
        if (r != null) r.material.color = baseHeartColor;

        // Reset light
        phaseLight.gameObject.SetActive(false);
        phaseLight.color = normalLightColor;
        phaseLight.intensity = 1f;

        // Reset blood
        RestoreBlood();
    }

    // -----------------------------------------------------
    // DESTROY all runtime arcs + shockwaves instantly
    // -----------------------------------------------------
    void KillRuntimeObjects()
    {
        GameObject[] waves = GameObject.FindGameObjectsWithTag("Untagged");

        // Destroy only objects created by script
        foreach (GameObject obj in waves)
        {
            if (obj.name.Contains("Shockwave") || obj.name.Contains("Arc"))
            {
                Destroy(obj);
            }
        }
    }

    // -----------------------------------------------------
    // MAIN RL FLOW (unchanged)
    // -----------------------------------------------------
    IEnumerator MainFlow()
    {
        float t = 0;

        // ---------- PHASE 1 ----------
        while (t < 5f && running)
        {
            float hr = 72f + Mathf.Sin(Time.time * 0.8f) * 2f;
            float spo = 98f + Mathf.Sin(Time.time * 0.6f) * 0.3f;

            vitalsText.text =
                $"STATUS: STABLE\nHR: {Mathf.RoundToInt(hr)} | SpO₂: {spo:F1}% | Perfusion: Normal";

            t += Time.deltaTime;
            yield return null;
        }

        if (!running) yield break;

        // ---------- PHASE 2 ----------
        phaseLight.gameObject.SetActive(true);
        phaseLight.color = phase2Color;
        phaseLight.intensity = phase2Intensity;

        t = 0;
        while (t < 5f && running)
        {
            float hr = Mathf.Lerp(72f, 58f, t / 5f);
            float spo = Mathf.Lerp(98f, 78f, t / 5f);

            vitalsText.text =
                $"⚠ VITALS DETERIORATING\nHR: {Mathf.RoundToInt(hr)} ↓ | SpO₂: {spo:F1}% ↓ | Perfusion: Low";

            heartModel.localScale = baseHeartScale + Vector3.one * 0.03f;

            t += Time.deltaTime;
            yield return null;
        }

        if (!running) yield break;

        // ---------- PHASE 3 ----------
        StartCoroutine(FogBurst());

        t = 0;
        while (t < 5f && running)
        {
            float hr = Mathf.Lerp(58f, 11f, t / 5f);
            float spo = Mathf.Lerp(78f, 52f, t / 5f);

            vitalsText.text =
                $"🚨 CARDIAC ARREST\nHR: {Mathf.RoundToInt(hr)} | SpO₂: {spo:F1}% | Perfusion: Collapse";

            phaseLight.color = Color.Lerp(phase2Color, phase3Color, t / 5f);

            var r = heartModel.GetComponentInChildren<Renderer>();
            if (r != null)
                r.material.color = Color.Lerp(baseHeartColor, arrestColor, t / 5f);

            t += Time.deltaTime;
            yield return null;
        }

        if (!running) yield break;

        yield return StartCoroutine(FailedAttempt1());
    }

    // -----------------------------------------------------
    // FOG + FAILED ATTEMPT FUNCTIONS (unchanged)
    // -----------------------------------------------------
    IEnumerator FogBurst()
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = fogColor;

        float t = 0;
        while (t < 1f)
        {
            RenderSettings.fogDensity = Mathf.Lerp(0f, fogDensityTarget, t);
            t += Time.deltaTime * 1.6f;
            yield return null;
        }

        yield return new WaitForSeconds(2f);

        t = 0;
        while (t < 1f)
        {
            RenderSettings.fogDensity = Mathf.Lerp(fogDensityTarget, 0, t);
            t += Time.deltaTime * 1.2f;
            yield return null;
        }

        RenderSettings.fog = false;
    }

    IEnumerator FailedAttempt1()
    {
        vitalsText.text = "🤖 AI Attempting Recovery...";
        yield return new WaitForSeconds(3f);

        vitalsText.text = "⚡ Intervention: Electrical Pulse Applied";

        StartCoroutine(WhiteShockFlash(5.5f));

        for (int i = 0; i < Mathf.Min(arcSources.Length, arcTargets.Length); i++)
        {
            StartCoroutine(ArcTravelRuntime(arcSources[i], arcTargets[i], 1.5f));
            yield return new WaitForSeconds(0.15f);
        }

        for (int i = 0; i < shockRings; i++)
        {
            StartCoroutine(GenerateShockwave(0.18f + i * 0.06f, shockRingMaxRadius, 5.5f));
            yield return new WaitForSeconds(0.2f);
        }

        CollapseBlood();
        yield return new WaitForSeconds(5.5f);

        vitalsText.text = "❌ Attempt Failed — Reward: -10 (AI Penalty)";
        running = false;
    }

    IEnumerator WhiteShockFlash(float dur)
    {
        if (phaseLight == null) yield break;

        float origIntensity = phaseLight.intensity;

        phaseLight.color = Color.white;
        phaseLight.intensity = 3f;

        yield return new WaitForSeconds(dur);

        phaseLight.color = phase3Color;
        phaseLight.intensity = origIntensity * 0.5f;
    }

    IEnumerator ArcTravelRuntime(Transform from, Transform to, float duration)
    {
        if (from == null || to == null) yield break;

        GameObject arc = new GameObject("Arc");
        LineRenderer lr = arc.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.widthMultiplier = 0.08f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = Color.cyan;

        float t = 0;
        while (t < duration)
        {
            Vector3 mid = Vector3.Lerp(from.position, to.position, t / duration);
            lr.SetPosition(0, from.position);
            lr.SetPosition(1, mid);
            t += Time.deltaTime;
            yield return null;
        }

        lr.SetPosition(1, to.position);
        yield return new WaitForSeconds(0.2f);

        Destroy(arc);
    }

    IEnumerator GenerateShockwave(float startRadius, float maxRadius, float duration)
    {
        GameObject wave = new GameObject("Shockwave");
        LineRenderer lr = wave.AddComponent<LineRenderer>();

        int points = 128;
        lr.positionCount = points;
        lr.loop = true;
        lr.widthMultiplier = 0.04f;
        lr.material = new Material(Shader.Find("Sprites/Default"));

        float t = 0;
        while (t < duration)
        {
            float radius = Mathf.Lerp(startRadius, maxRadius, t / duration);

            for (int i = 0; i < points; i++)
            {
                float angle = i * Mathf.PI * 2f / points;
                Vector3 pos = heartModel.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                lr.SetPosition(i, pos);
            }

            Color c = Color.white;
            c.a = 1f - (t / duration);
            lr.startColor = lr.endColor = c;

            t += Time.deltaTime;
            yield return null;
        }

        Destroy(wave);
    }

    void CollapseBlood()
    {
        for (int i = 0; i < bloodParticles.Length; i++)
        {
            var em = bloodParticles[i].emission;
            em.rateOverTime = new ParticleSystem.MinMaxCurve(originalEmissionRates[i] * 0.10f);

            var main = bloodParticles[i].main;
            main.startSpeedMultiplier = originalStartSpeeds[i] * 0.25f;
        }
    }

    void RestoreBlood()
    {
        for (int i = 0; i < bloodParticles.Length; i++)
        {
            var em = bloodParticles[i].emission;
            em.rateOverTime = new ParticleSystem.MinMaxCurve(originalEmissionRates[i]);

            var main = bloodParticles[i].main;
            main.startSpeedMultiplier = originalStartSpeeds[i];
        }
    }
}