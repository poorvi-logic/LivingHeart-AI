using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(AudioSource))]
public class MicInput : MonoBehaviour
{
    [Header("Device")]
    public string micDevice = null; // leave null for default microphone
    public int sampleRate = 44100;
    public int sampleWindow = 128;

    [Header("Sensitivity & Thresholds")]
    [Tooltip("Multiplies the raw RMS value to make input more/less sensitive.")]
    [Range(0.1f, 20f)] public float sensitivity = 1f;

    [Tooltip("Minimum RMS value considered as audible (below this -> treated as silence).")]
    [Range(0f, 0.5f)] public float minThreshold = 0.01f;

    [Tooltip("RMS value which maps to 'maximum' (useful for normalizing).")]
    [Range(0.01f, 1f)] public float maxThreshold = 0.4f;

    [Header("Smoothing & Clap Detection")]
    [Range(0f, 1f)] public float smoothing = 0.2f;
    [Tooltip("Multiplier above current smoothed level to consider a spike/clap.")]
    public float spikeMultiplier = 3.5f;
    public float spikeCooldown = 0.4f; // seconds between spike detections

    [Header("Outputs (read-only at runtime)")]
    public float rawRMS = 0f;
    public float normalizedLevel = 0f;
    public float smoothedLevel = 0f;

    // ⭐ ADD THIS (used by Waveform)
    [Header("Waveform Output")]
    public float currentLoudness = 0f;

    [Header("Unity Events")]
    public UnityEvent OnSpike; // invoke for claps/peaks

    AudioSource audioSource;
    float lastSpikeTime = -10f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.mute = true; // we don't want to hear mic output
    }
    
    void OnEnable()
    {
        StartMic();
    }

    void OnDisable()
    {
        StopMic();
    }

    public void StartMic()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("MicInput: No microphone devices found.");
            return;
        }

        if (string.IsNullOrEmpty(micDevice))
            micDevice = Microphone.devices[0];

        if (Microphone.IsRecording(micDevice))
            Microphone.End(micDevice);

        audioSource.clip = Microphone.Start(micDevice, true, 1, sampleRate);
        while (!(Microphone.GetPosition(micDevice) > 0)) { }
        audioSource.Play();
    }

    public void StopMic()
    {
        if (audioSource.isPlaying)
            audioSource.Stop();
        if (!string.IsNullOrEmpty(micDevice) && Microphone.IsRecording(micDevice))
            Microphone.End(micDevice);
    }

    void Update()
    {
        rawRMS = GetRMS();

        float scaled = rawRMS * sensitivity;
        float clamped = Mathf.InverseLerp(minThreshold, maxThreshold, scaled);
        normalizedLevel = Mathf.Clamp01(clamped);

        smoothedLevel = Mathf.Lerp(
            smoothedLevel,
            normalizedLevel, 
            1f - Mathf.Exp(-smoothing * Time.deltaTime * 60f)
        );

        // ⭐ UPDATE CURRENT LOUDNESS
        currentLoudness = smoothedLevel;   // ← waveform reads this!

        // Spike detection
        if (Time.time - lastSpikeTime > spikeCooldown)
        {
            if (normalizedLevel > Mathf.Max(smoothedLevel * spikeMultiplier, 0.35f))
            {
                lastSpikeTime = Time.time;
                OnSpike?.Invoke();
                Debug.Log("MicInput: Spike detected!");
            }
        }
    }

    float GetRMS()
    {
        if (audioSource.clip == null) return 0f;

        float[] samples = new float[sampleWindow];
        int micPosition = Microphone.GetPosition(micDevice) - sampleWindow + 1;
        if (micPosition < 0) micPosition = 0;

        audioSource.clip.GetData(samples, micPosition);

        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];

        return Mathf.Sqrt(sum / samples.Length);
    }

    // UI Setters
    public void SetSensitivity(float v) => sensitivity = v;
    public void SetMinThreshold(float v) => minThreshold = v;
    public void SetMaxThreshold(float v) => maxThreshold = v;
    public void SetSmoothing(float v) => smoothing = v;
    public void SetSpikeMultiplier(float v) => spikeMultiplier = v;
}
