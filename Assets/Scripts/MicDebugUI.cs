using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MicDebugUI : MonoBehaviour
{
    public MicInput mic;
    public TMP_Text debugText;

    void Update()
    {
        if (mic == null || debugText == null) return;
        debugText.text = $"raw:{mic.rawRMS:F4}\nnorm:{mic.normalizedLevel:F3}\nsmoothed:{mic.smoothedLevel:F3}";
    }
}