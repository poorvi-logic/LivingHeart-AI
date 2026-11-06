using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AcuteRVFailureCinematic : MonoBehaviour
{
    // ----------------------------------------------------------
    // TOGGLE STATE
    // ----------------------------------------------------------
    private int state = 0;   // 0 = idle, 1 = running

    // ----------------------------------------------------------
    // USER FIELDS
    // ----------------------------------------------------------

    [Header("Heart Parents")]
    public Transform RightVentricle;
    public Transform LeftVentricle;

    [Header("Optional Animators")]
    public Animator rightVentricleAnimator;
    public Animator leftVentricleAnimator;

    [Header("Blood Flows (ALL)")]
    public ParticleSystem[] allBloodFlows;

    [Header("Blood Flow Density Multiplier")]
    public float bloodDensityMultiplier = 3f;

    [Header("Camera (Optional)")]
    public Camera mainCam;

    [Header("Phase Durations")]
    public float phase1Duration = 3f;
    public float phase2Duration = 4f;
    public float phase3Duration = 5f;
    public float phase4Duration = 5f;
    public float phase5Duration = 6f;
    public float phase6Duration = 6f;

    // ----------------------------------------------------------
    // INTERNAL BASELINES
    // ----------------------------------------------------------

    private List<Renderer> rvRenderers = new List<Renderer>();
    private List<Color> rvBaseColors = new List<Color>();

    private List<Renderer> lvRenderers = new List<Renderer>();
    private List<Color> lvBaseColors = new List<Color>();

    private float rvBaseScale = 1f;
    private float lvBaseScale = 1f;

    private float rvAnimBaseSpeed = 1f;
    private float lvAnimBaseSpeed = 1f;

    private float[] bloodBaseSpeed;
    private float[] bloodBaseRate;
    private float[] bloodBaseNoise;

    // Septum
    private Transform septum;
    private Renderer septumRenderer;
    private Color septumBaseColor;

    // Camera baseline
    private Vector3 camBasePos;
    private Quaternion camBaseRot;
    private float camBaseFOV;

    // UI Text
    private Canvas textCanvas;
    private Text stepText;
    private CanvasGroup textGroup;

    // ----------------------------------------------------------
    // START → CACHE ALL BASELINES
    // ----------------------------------------------------------
    void Start()
    {
        CacheChamberBaseline();
        CacheAnimatorBaseline();
        CacheBloodBaseline();
        CacheCameraBaseline();
        AutoCreateSeptum();
        CreateBottomTextUI();
    }

    // ----------------------------------------------------------
    // CACHE RV & LV CHAMBER COLORS + SCALE
    // ----------------------------------------------------------
    void CacheChamberBaseline()
    {
        if (RightVentricle != null)
        {
            rvRenderers.Clear();
            rvBaseColors.Clear();

            var rs = RightVentricle.GetComponentsInChildren<Renderer>();
            foreach (var r in rs)
            {
                rvRenderers.Add(r);
                rvBaseColors.Add(r.material.color);
            }

            rvBaseScale = RightVentricle.localScale.x;
        }

        if (LeftVentricle != null)
        {
            lvRenderers.Clear();
            lvBaseColors.Clear();

            var rs = LeftVentricle.GetComponentsInChildren<Renderer>();
            foreach (var r in rs)
            {
                lvRenderers.Add(r);
                lvBaseColors.Add(r.material.color);
            }

            lvBaseScale = LeftVentricle.localScale.x;
        }
    }

    // ----------------------------------------------------------
    // CACHE ANIMATOR SPEEDS
    // ----------------------------------------------------------
    void CacheAnimatorBaseline()
    {
        if (rightVentricleAnimator != null)
            rvAnimBaseSpeed = rightVentricleAnimator.speed;

        if (leftVentricleAnimator != null)
            lvAnimBaseSpeed = leftVentricleAnimator.speed;
    }

    // ----------------------------------------------------------
    // CACHE BLOOD FLOW BASELINES
    // ----------------------------------------------------------
    void CacheBloodBaseline()
    {
        int n = (allBloodFlows != null) ? allBloodFlows.Length : 0;

        bloodBaseSpeed = new float[n];
        bloodBaseRate = new float[n];
        bloodBaseNoise = new float[n];

        for (int i = 0; i < n; i++)
        {
            var ps = allBloodFlows[i];

            var m = ps.main;
            var e = ps.emission;
            var no = ps.noise;

            bloodBaseSpeed[i] = m.startSpeed.constant;
            bloodBaseRate[i] = e.rateOverTime.constant;
            bloodBaseNoise[i] = no.strength.constant;

            // Allow dense “fluid-like” look
            int originalMax = m.maxParticles;
            int targetMax = Mathf.RoundToInt(originalMax * Mathf.Max(1f, bloodDensityMultiplier * 2f));
            m.maxParticles = Mathf.Max(originalMax, targetMax);
        }
    }

    // ----------------------------------------------------------
    // CACHE CAMERA BASELINE
    // ----------------------------------------------------------
    void CacheCameraBaseline()
    {
        if (mainCam != null)
        {
            camBasePos = mainCam.transform.position;
            camBaseRot = mainCam.transform.rotation;
            camBaseFOV = mainCam.fieldOfView;
        }
    }

    // ----------------------------------------------------------
    // CREATE SEPTUM BETWEEN RV & LV
    // ----------------------------------------------------------
    void AutoCreateSeptum()
    {
        if (RightVentricle == null || LeftVentricle == null)
            return;

        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
        s.name = "ProceduralSeptum";
        Destroy(s.GetComponent<Collider>());

        septum = s.transform;
        Vector3 mid = (RightVentricle.position + LeftVentricle.position) * 0.5f;
        septum.position = mid;

        Vector3 dir = (RightVentricle.position - LeftVentricle.position).normalized;
        septum.rotation = Quaternion.LookRotation(dir, Vector3.up);

        septum.localScale = new Vector3(0.02f, 0.5f, 0.8f);

        septumRenderer = s.GetComponent<Renderer>();
        septumBaseColor = new Color(0.9f, 0.8f, 0.8f);
        septumRenderer.material.color = septumBaseColor;
    }

    // ----------------------------------------------------------
    // UI (BOTTOM TEXT)
    // ----------------------------------------------------------
    void CreateBottomTextUI()
    {
        GameObject c = new GameObject("RVFailureTextCanvas");
        c.layer = 5;

        textCanvas = c.AddComponent<Canvas>();
        textCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = c.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        textGroup = c.AddComponent<CanvasGroup>();
        textGroup.alpha = 0f;

        GameObject t = new GameObject("StepText");
        t.transform.SetParent(c.transform);

        stepText = t.AddComponent<Text>();
        RectTransform rt = stepText.rectTransform;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0, -35f);
        rt.sizeDelta = new Vector2(950f, 150f);

        stepText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        stepText.fontSize = 18;
        stepText.color = Color.white;
        stepText.alignment = TextAnchor.MiddleCenter;
    }

    IEnumerator ShowStepText(string msg, float holdSeconds)
    {
        if (stepText == null) yield break;

        stepText.text = msg;

        float a = 0f;
        while (a < 1f)
        {
            a += Time.deltaTime * 2f;
            textGroup.alpha = Mathf.SmoothStep(0f, 1f, a);
            yield return null;
        }

        yield return new WaitForSeconds(holdSeconds);

        while (a > 0f)
        {
            a -= Time.deltaTime * 1.5f;
            textGroup.alpha = Mathf.SmoothStep(0f, 1f, a);
            yield return null;
        }
    }

    // ----------------------------------------------------------
    // PUBLIC TOGGLE BUTTON
    // ----------------------------------------------------------
    public void ToggleRVCollapse()
    {
        if (state == 0)
        {
            ResetRVCollapseToBaseline();
            StartRVCollapseCinematic();
            state = 1;
        }
        else
        {
            ResetRVCollapseToBaseline();
            state = 0;
        }
    }

    // ----------------------------------------------------------
    // ENTRY
    // ----------------------------------------------------------
    public void StartRVCollapseCinematic()
    {
        StartCoroutine(RVCollapseSequence());
    }

    // ----------------------------------------------------------
    // MASTER RESET — RESTORE EVERYTHING TO NORMAL
    // ----------------------------------------------------------
    public void ResetRVCollapseToBaseline()
    {
        StopAllCoroutines();

        // RV colours
        for (int i = 0; i < rvRenderers.Count; i++)
            rvRenderers[i].material.color = rvBaseColors[i];

        // LV colours
        for (int i = 0; i < lvRenderers.Count; i++)
            lvRenderers[i].material.color = lvBaseColors[i];

        // RV scale
        if (RightVentricle != null)
            RightVentricle.localScale = Vector3.one * rvBaseScale;

        // LV scale
        if (LeftVentricle != null)
            LeftVentricle.localScale = Vector3.one * lvBaseScale;

        // Animators
        if (rightVentricleAnimator != null)
            rightVentricleAnimator.speed = rvAnimBaseSpeed;

        if (leftVentricleAnimator != null)
            leftVentricleAnimator.speed = lvAnimBaseSpeed;

        // Blood flows
        for (int i = 0; i < allBloodFlows.Length; i++)
        {
            var ps = allBloodFlows[i];
            var m = ps.main;
            var e = ps.emission;
            var no = ps.noise;

            m.startSpeed = bloodBaseSpeed[i];
            e.rateOverTime = bloodBaseRate[i] * bloodDensityMultiplier;
            no.strength = bloodBaseNoise[i];

            ps.Clear();
            ps.Play();
        }

        // Camera
        if (mainCam != null)
        {
            mainCam.transform.position = camBasePos;
            mainCam.transform.rotation = camBaseRot;
            mainCam.fieldOfView = camBaseFOV;
        }

        // Septum
        if (septum != null)
        {
            septum.position = (RightVentricle.position + LeftVentricle.position) * 0.5f;
            septumRenderer.material.color = septumBaseColor;
        }

        // Hide text UI
        if (textCanvas != null)
            textCanvas.gameObject.SetActive(true);

        textGroup.alpha = 0f;
    }

    // ----------------------------------------------------------
    // MAIN SEQUENCE — UNTOUCHED
    // ----------------------------------------------------------
    IEnumerator RVCollapseSequence()
    {
        // PHASE 1
        StartCoroutine(ShowStepText("Right ventricle baseline pumping.", 5f));
        yield return Phase1_Baseline();

        // PHASE 2
        StartCoroutine(ShowStepText("Pulmonary pressure spike.", 5f));
        yield return Phase2_PressureSpike();

        // PHASE 3
        StartCoroutine(ShowStepText("RV dilation begins.", 5f));
        yield return Phase3_RVDilation();

        // PHASE 4
        StartCoroutine(ShowStepText("Septum shifts into LV.", 5f));
        yield return Phase4_SeptalShift();

        // PHASE 5
        StartCoroutine(ShowStepText("Cyanotic wobbling failure.", 5f));
        yield return Phase5_WobblingFailure();

        // PHASE 6
        StartCoroutine(ShowStepText("Acute RV collapse.", 5f));
        yield return Phase6_Collapse();
    }

    // ----------------------------------------------------------
    // PHASE LOGIC (unchanged)
    // ----------------------------------------------------------

    IEnumerator Phase1_Baseline()
    {
        float t = 0f, dur = Mathf.Max(1f, phase1Duration);

        while (t < dur)
        {
            t += Time.deltaTime;
            float s = SmoothStep(t / dur);

            // Colors
            for (int i = 0; i < rvRenderers.Count; i++)
                rvRenderers[i].material.color = Color.Lerp(rvRenderers[i].material.color, rvBaseColors[i], s);

            for (int i = 0; i < lvRenderers.Count; i++)
                lvRenderers[i].material.color = Color.Lerp(lvRenderers[i].material.color, lvBaseColors[i], s);

            // Scales
            if (RightVentricle != null)
                RightVentricle.localScale = Vector3.Lerp(RightVentricle.localScale, Vector3.one * rvBaseScale, s);

            if (LeftVentricle != null)
                LeftVentricle.localScale = Vector3.Lerp(LeftVentricle.localScale, Vector3.one * lvBaseScale, s);

            // Animators
            if (rightVentricleAnimator != null)
                rightVentricleAnimator.speed = Mathf.Lerp(rightVentricleAnimator.speed, rvAnimBaseSpeed, s);

            if (leftVentricleAnimator != null)
                leftVentricleAnimator.speed = Mathf.Lerp(leftVentricleAnimator.speed, lvAnimBaseSpeed, s);

            // Blood
            for (int i = 0; i < allBloodFlows.Length; i++)
            {
                var ps = allBloodFlows[i];
                var m = ps.main;
                var e = ps.emission;
                var no = ps.noise;

                m.startSpeed = Mathf.Lerp(m.startSpeed.constant, bloodBaseSpeed[i], s);
                e.rateOverTime = Mathf.Lerp(e.rateOverTime.constant, bloodBaseRate[i] * bloodDensityMultiplier, s);
                no.strength = Mathf.Lerp(no.strength.constant, bloodBaseNoise[i], s);
            }

            yield return null;
        }
    }

    IEnumerator Phase2_PressureSpike()
    {
        float t = 0f, dur = Mathf.Max(1f, phase2Duration);

        while (t < dur)
        {
            t += Time.deltaTime;
            float s = SmoothStep(t / dur);

            for (int i = 0; i < rvRenderers.Count; i++)
            {
                Color baseCol = rvBaseColors[i];
                Color darker = new Color(baseCol.r * 0.7f, baseCol.g * 0.7f, baseCol.b * 0.9f);
                rvRenderers[i].material.color = Color.Lerp(baseCol, darker, s);
            }

            if (rightVentricleAnimator != null)
                rightVentricleAnimator.speed = Mathf.Lerp(rvAnimBaseSpeed, rvAnimBaseSpeed * 0.8f, s);

            for (int i = 0; i < allBloodFlows.Length; i++)
            {
                var ps = allBloodFlows[i];
                var m = ps.main;
                var e = ps.emission;
                var no = ps.noise;

                m.startSpeed = Mathf.Lerp(m.startSpeed.constant, bloodBaseSpeed[i] * 0.6f, s);
                e.rateOverTime = Mathf.Lerp(e.rateOverTime.constant, bloodBaseRate[i] * 0.7f * bloodDensityMultiplier, s);
                no.strength = Mathf.Lerp(no.strength.constant, 0.3f, s);
            }

            yield return null;
        }
    }

    IEnumerator Phase3_RVDilation()
    {
        float t = 0f, dur = Mathf.Max(1f, phase3Duration);

        Vector3 rvStart = RightVentricle.localScale;
        Vector3 rvEnd = rvStart * 1.2f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float s = SmoothStep(t / dur);

            RightVentricle.localScale = Vector3.Lerp(rvStart, rvEnd, s);
            float wob = 1f + 0.03f * Mathf.Sin(Time.time * 6f);
            RightVentricle.localScale *= wob;

            for (int i = 0; i < rvRenderers.Count; i++)
            {
                Color from = rvRenderers[i].material.color;
                Color cyan = new Color(0.4f, 0.25f, 0.5f);
                rvRenderers[i].material.color = Color.Lerp(from, cyan, s * 0.5f);
            }

            for (int i = 0; i < allBloodFlows.Length; i++)
            {
                var ps = allBloodFlows[i];
                var m = ps.main;
                var e = ps.emission;
                var no = ps.noise;

                m.startSpeed = Mathf.Lerp(m.startSpeed.constant, bloodBaseSpeed[i] * 0.5f, s);
                e.rateOverTime = Mathf.Lerp(e.rateOverTime.constant, bloodBaseRate[i] * 1.3f * bloodDensityMultiplier, s);
                no.strength = Mathf.Lerp(no.strength.constant, 0.25f, s);
            }

            yield return null;
        }
    }

    IEnumerator Phase4_SeptalShift()
    {
        float t = 0f, dur = Mathf.Max(1f, phase4Duration);

        Vector3 septStart = septum.position;
        Vector3 septEnd = septStart + (LeftVentricle.position - RightVentricle.position).normalized * 0.03f;

        Vector3 lvStart = LeftVentricle.localScale;
        Vector3 lvEnd = lvStart * 0.85f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float s = SmoothStep(t / dur);

            septum.position = Vector3.Lerp(septStart, septEnd, s);
            LeftVentricle.localScale = Vector3.Lerp(lvStart, lvEnd, s);

            for (int i = 0; i < lvRenderers.Count; i++)
            {
                Color from = lvRenderers[i].material.color;
                Color pale = new Color(from.r * 1.1f, from.g * 1.05f, from.b * 1.05f);
                lvRenderers[i].material.color = Color.Lerp(from, pale, s);
            }

            for (int i = 0; i < allBloodFlows.Length; i++)
            {
                var ps = allBloodFlows[i];
                var m = ps.main;
                var e = ps.emission;

                m.startSpeed = Mathf.Lerp(m.startSpeed.constant, bloodBaseSpeed[i] * 0.4f, s);
                e.rateOverTime = Mathf.Lerp(e.rateOverTime.constant, bloodBaseRate[i] * 0.5f * bloodDensityMultiplier, s);
            }

            yield return null;
        }
    }

    IEnumerator Phase5_WobblingFailure()
    {
        float t = 0f, dur = Mathf.Max(1f, phase5Duration);

        while (t < dur)
        {
            t += Time.deltaTime;
            float s = SmoothStep(t / dur);

            float wobScale = 1f + 0.06f * Mathf.Sin(Time.time * 9f);
            RightVentricle.localScale = Vector3.one * (rvBaseScale * 1.4f) * wobScale;

            for (int i = 0; i < rvRenderers.Count; i++)
            {
                Color from = rvRenderers[i].material.color;
                Color deep = new Color(0.2f, 0.08f, 0.3f);
                rvRenderers[i].material.color = Color.Lerp(from, deep, s * 0.3f);
            }

            for (int i = 0; i < allBloodFlows.Length; i++)
            {
                var ps = allBloodFlows[i];
                var m = ps.main;
                var e = ps.emission;
                var no = ps.noise;

                m.startSpeed = Mathf.Lerp(m.startSpeed.constant, bloodBaseSpeed[i] * 0.15f, s);
                e.rateOverTime = Mathf.Lerp(e.rateOverTime.constant, bloodBaseRate[i] * 0.2f * bloodDensityMultiplier, s);
                no.strength = Mathf.Lerp(no.strength.constant, 0.4f, s);
            }

            yield return null;
        }
    }

    IEnumerator Phase6_Collapse()
    {
        float t = 0f, dur = Mathf.Max(1f, phase6Duration);

        while (t < dur)
        {
            t += Time.deltaTime;
            float s = SmoothStep(t / dur);

            if (rightVentricleAnimator != null)
                rightVentricleAnimator.speed = Mathf.Lerp(rightVentricleAnimator.speed, rvAnimBaseSpeed * 0.05f, s);

            for (int i = 0; i < allBloodFlows.Length; i++)
            {
                var ps = allBloodFlows[i];
                var m = ps.main;
                var e = ps.emission;
                var no = ps.noise;

                m.startSpeed = Mathf.Lerp(m.startSpeed.constant, 0f, s);
                e.rateOverTime = Mathf.Lerp(e.rateOverTime.constant, 0f, s);
                no.strength = Mathf.Lerp(no.strength.constant, 0.5f, s);
            }

            for (int i = 0; i < rvRenderers.Count; i++)
            {
                Color from = rvRenderers[i].material.color;
                Color deep = new Color(0.08f, 0.03f, 0.15f);
                rvRenderers[i].material.color = Color.Lerp(from, deep, s);
            }

            yield return null;
        }
    }

    // ----------------------------------------------------------
    // Small interpolation helper
    // ----------------------------------------------------------
    float SmoothStep(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}