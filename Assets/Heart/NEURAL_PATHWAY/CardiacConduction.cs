using System.Collections;
using UnityEngine;

public class CardiacConduction : MonoBehaviour
{
    [Header("Conduction Lights")]
    public Light[] conductionLights;

    [Header("Heart Rates")]
    public float normalBPM = 70f;
    public float vtachBPM = 160f;

    [Header("Light Intensities")]
    public float normalIntensity = 2f;
    public float vtachIntensity = 4f;

    [Header("ECG System")]
    public ECGScroll ecg;
    public Texture normalECG;
    public Texture vtachECG;

    [Header("UI Display")]
    public StatusDisplay statusUI;

    [Header("Sound System")]
    public AudioSource normalHeartbeat;
    public AudioSource vtachAlarm;
    public AudioSource shockSound;

    private bool sequenceStarted = false;
    private bool moduleActivated = false;

    void Start()
    {
        StopAllCoroutines();
        normalHeartbeat?.Stop();
        vtachAlarm?.Stop();
        shockSound?.Stop();

        if (ecg != null)
        {
            ecg.enabled = false;  // 🔥 stops scrolling
            ecg.SetWave(normalECG, 0f);
            ecg.gameObject.SetActive(false); // 🔥 hides ECG
        }

        foreach (var l in conductionLights)
        {
            l.intensity = 0;
            l.color = new Color(0f, 1f, 0.9f);
        }

        statusUI?.ShowMessage("", new Color(0, 0, 0, 0), 999f);
    }

    public void ActivateModule()
    {
        if (moduleActivated) return;
        moduleActivated = true;

        statusUI?.ShowMessage("💙 Neural Pathways Activated — Normal Rhythm", Color.cyan, 4f);

        ecg.enabled = true;             // 🔥 restart scrolling
        ecg.gameObject.SetActive(true); // 🔥 show ECG
        ecg?.SetWave(normalECG, 0.6f);

        normalHeartbeat?.Play();
        StartCoroutine(Pulse(normalBPM, normalIntensity));
    }

    public void DeactivateModule()
    {
        moduleActivated = false;
        sequenceStarted = false;

        StopAllCoroutines();

        normalHeartbeat?.Stop();
        vtachAlarm?.Stop();
        shockSound?.Stop();

        ecg.enabled = false;                  // 🔥 stop movement
        ecg.gameObject.SetActive(false);      // 🔥 hide ECG completely
        ecg?.SetWave(normalECG, 0f);

        foreach (var l in conductionLights)
            l.intensity = 0;

        statusUI?.ShowMessage("🧊 System Standby — Safe Mode", Color.gray, 3f);
    }

    IEnumerator Pulse(float bpm, float intensity)
    {
        while (moduleActivated)
        {
            float delay = 60f / bpm;
            float stepTime = delay / conductionLights.Length;

            for (int i = 0; i < conductionLights.Length; i++)
            {
                if (!moduleActivated) yield break;
                StartCoroutine(Flash(conductionLights[i], stepTime * 0.5f, intensity));
                yield return new WaitForSeconds(stepTime);
            }
        }
    }

    IEnumerator Flash(Light l, float duration, float maxIntensity)
    {
        float t = 0;

        while (t < duration / 2)
        {
            l.intensity = Mathf.Lerp(0, maxIntensity, t / (duration / 2));
            t += Time.deltaTime;
            yield return null;
        }

        t = 0;
        while (t < duration / 2)
        {
            l.intensity = Mathf.Lerp(maxIntensity, 0, t / (duration / 2));
            t += Time.deltaTime;
            yield return null;
        }

        l.intensity = 0;
    }

    public void RunSequence()
    {
        if (!moduleActivated) return;
        if (!sequenceStarted)
        {
            sequenceStarted = true;
            StopAllCoroutines();
            StartCoroutine(EventSequence());
        }
    }

    IEnumerator EventSequence()
    {
        statusUI?.ShowMessage("⚡ SHOCK APPLIED — SYSTEM OVERRIDING", Color.yellow, 5f);

        normalHeartbeat?.Stop();
        vtachAlarm?.Stop();
        shockSound?.Play();

        foreach (var l in conductionLights)
        {
            l.color = Color.white;
            l.intensity = 7f;
        }

        yield return new WaitForSeconds(5f);

        statusUI?.ShowMessage("🚨 VTACH DETECTED — UNSTABLE RHYTHM", statusUI.alertRed, 5f);

        shockSound?.Stop();
        vtachAlarm?.Play();

        foreach (var l in conductionLights)
            l.color = Color.red;

        ecg?.SetWave(vtachECG, 2.4f);
        StartCoroutine(Pulse(vtachBPM, vtachIntensity));

        yield return new WaitForSeconds(5f);

        statusUI?.ShowMessage("💙 RECOVERY — NORMAL RHYTHM RESTORED", Color.green, 5f);

        vtachAlarm?.Stop();
        normalHeartbeat?.Play();

        StopAllCoroutines();
        StartCoroutine(Pulse(normalBPM, normalIntensity));
        ecg?.SetWave(normalECG, 0.6f);

        foreach (var l in conductionLights)
            l.color = new Color(0f, 1f, 0.9f);

        sequenceStarted = false;
    }
}