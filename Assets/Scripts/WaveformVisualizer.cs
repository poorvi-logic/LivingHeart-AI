using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WaveformVisualizer : MonoBehaviour
{
    public MicInput mic;                 // reference to your mic script
    public RectTransform[] bars;         // all bar prefabs
    public float heightMultiplier = 200; // controls height
    public float smoothing = 10f;

    void Update()
    {
        float loudness = mic.currentLoudness;
        float targetHeight = loudness * heightMultiplier;

        foreach (var bar in bars)
        {
            float h = Mathf.Lerp(bar.sizeDelta.y, targetHeight, Time.deltaTime * smoothing);
            bar.sizeDelta = new Vector2(bar.sizeDelta.x, h);
        }
    }
}

