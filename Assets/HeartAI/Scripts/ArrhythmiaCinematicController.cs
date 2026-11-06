using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ArrhythmiaCinematicController : MonoBehaviour,IScenario
{
    [Header("Chambers")]
    public Transform leftAtrium;
    public Transform rightAtrium;
    public Transform leftVentricle;

    [Header("Animators")]
    public Animator leftVentricleAnimator;
    public Animator leftAtriumAnimator;
    public Animator rightAtriumAnimator;

    [Header("Special Node")]
    public Transform SANode;

    [Header("Camera")]
    public Camera mainCam;

    [Header("Blood Flows")]
    public ParticleSystem[] allFlows;

    [Header("Pacemaker Surgery Link")]
    public PacemakerSurgeryCinematic pacemakerSurgery;

    Renderer laR, raR, lvR, saR;
    Color laDefaultColor, raDefaultColor, lvDefaultColor, saDefaultColor;

    Vector3 laDefaultScale, raDefaultScale, lvDefaultScale;

    float defaultLVSpeed, defaultLASpeed, defaultRASpeed;

    Vector3 camDefaultPos;
    Quaternion camDefaultRot;
    float camDefaultFOV;

    Color raLight = new Color(0f, 0.85f, 0.85f);
    Color raDark = new Color(0f, 0.45f, 0.45f);

    Coroutine routine;

    Canvas textCanvas;
    Text stepText;
    CanvasGroup textGroup;

    // control flags
  public bool forceStopped = false;     // set when StopScenario/Exit called
    bool manuallyStarted = false;  // set when StartScenario called

    void Start()
    {
        // DO NOT auto-start anything here. Scene should remain idle until button pressed.
        CreateBottomTextUI();

        laR = leftAtrium.GetComponentInChildren<Renderer>();
        raR = rightAtrium.GetComponentInChildren<Renderer>();
        lvR = leftVentricle.GetComponentInChildren<Renderer>();

        if (SANode != null)
            saR = SANode.GetComponent<MeshRenderer>();

        laDefaultColor = laR.material.color;
        raDefaultColor = raR.material.color;
        lvDefaultColor = lvR.material.color;

        if (SANode != null)
            saDefaultColor = saR.material.color;

        laDefaultScale = leftAtrium.localScale;
        raDefaultScale = rightAtrium.localScale;
        lvDefaultScale = leftVentricle.localScale;

        defaultLVSpeed = leftVentricleAnimator != null ? leftVentricleAnimator.speed : 1f;
        defaultLASpeed = leftAtriumAnimator != null ? leftAtriumAnimator.speed : 1f;
        defaultRASpeed = rightAtriumAnimator != null ? rightAtriumAnimator.speed : 1f;

        camDefaultPos = mainCam.transform.position;
        camDefaultRot = mainCam.transform.rotation;
        camDefaultFOV = mainCam.fieldOfView;
    }

    void CreateBottomTextUI()
    {
        GameObject c = new GameObject("ArrhythmiaTextCanvas");
        c.layer = 5;

        textCanvas = c.AddComponent<Canvas>();
        textCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = c.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        textGroup = c.AddComponent<CanvasGroup>();
        textGroup.alpha = 0;

        GameObject t = new GameObject("ArrhythmiaStepText");
        t.transform.SetParent(c.transform);

        stepText = t.AddComponent<Text>();
        RectTransform rt = stepText.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);

        rt.anchoredPosition = new Vector2(0, 5);
        rt.sizeDelta = new Vector2(950, 160);

        stepText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        stepText.fontSize = 30;
        stepText.color = Color.black;
        stepText.alignment = TextAnchor.MiddleCenter;
    }

    IEnumerator ShowStepText(string msg, float hold = 1.6f)
    {
        if (forceStopped) yield break;

        stepText.text = msg;

        float a = 0;
        while (a < 1)
        {
            if (forceStopped) yield break;
            a += Time.deltaTime * 2f;
            textGroup.alpha = Mathf.SmoothStep(0, 1, a);
            yield return null;
        }

        float timer = 0;
        while (timer < hold)
        {
            if (forceStopped) yield break;
            timer += Time.deltaTime;
            yield return null;
        }

        while (a > 0)
        {
            if (forceStopped) yield break;
            a -= Time.deltaTime * 2f;
            textGroup.alpha = Mathf.SmoothStep(0, 1, a);
            yield return null;
        }
    }

    // Start/Stop wrappers for manager
    public void StartScenario()
    {
        forceStopped = false;
        manuallyStarted = true;
        StartArrhythmiaCinematic();
    }

    public void StopScenario()
    {
        // instant cut
        forceStopped = true;
        manuallyStarted = false;
        ExitToNormalState();
    }

    public void StartArrhythmiaCinematic()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ArrhythmiaSequence());
    }

    IEnumerator ArrhythmiaSequence()
    {
        yield return Phase1_Mild();
        if (forceStopped) yield break;

        yield return Phase2_Moderate();
        if (forceStopped) yield break;

        yield return Phase3_Critical();
        if (forceStopped) yield break;

        yield return Phase4_ExplanationPacemaker();
        if (forceStopped) yield break;

        if (!forceStopped)
            ResetHeart();
    }

    IEnumerator Phase1_Mild()
    {
        if (forceStopped) yield break;

        yield return ShowStepText("Detecting mild irregularity in RIGHT ATRIUM…", 1.2f);
        if (forceStopped) yield break;

        yield return StartCoroutine(HighlightSA_Node());
        if (forceStopped) yield break;

        yield return ShowStepText(
            "SA node misfires → atrial muscle loses coordination.\nAtrial kick weakens, reducing stable blood flow.",
            1.6f
        );
        if (forceStopped) yield break;

        Vector3 center = (leftAtrium.position + rightAtrium.position) / 2;
        Vector3 targetPos = center + (-mainCam.transform.forward * 0.22f) + Vector3.up * 0.05f;
        Quaternion targetRot = Quaternion.LookRotation(center - targetPos);

        Vector3 sPos = mainCam.transform.position;
        Quaternion sRot = mainCam.transform.rotation;

        float t = 0;
        while (t < 1)
        {
            if (forceStopped) yield break;
            t += Time.deltaTime / 1.2f;
            float s = SmoothStep(t);

            mainCam.transform.position = Vector3.Lerp(sPos, targetPos, s);
            mainCam.transform.rotation = Quaternion.Slerp(sRot, targetRot, s);

            raR.material.color = Color.Lerp(raDefaultColor, raLight, s * 0.6f);
            yield return null;
        }
    }

    IEnumerator HighlightSA_Node()
    {
        if (forceStopped) yield break;
        if (SANode == null || saR == null) yield break;

        float t = 0;
        Color glow = new Color(1f, 0.95f, 0.2f);

        while (t < 0.8f)
        {
            if (forceStopped) yield break;
            t += Time.deltaTime;
            float s = Mathf.Sin(Time.time * 12f) * 0.4f + 0.6f;

            saR.material.color = Color.Lerp(saDefaultColor, glow, s);
            leftVentricleAnimator.speed = defaultLVSpeed * (1f + Mathf.Sin(Time.time * 8f) * 0.08f);
            yield return null;
        }

        float b = 1;
        while (b > 0)
        {
            if (forceStopped) yield break;
            b -= Time.deltaTime * 1.4f;
            saR.material.color = Color.Lerp(saDefaultColor, glow, b);
            yield return null;
        }
    }

    IEnumerator Phase2_Moderate()
    {
        if (forceStopped) yield break;
        yield return ShowStepText("Right atrial contractions becoming unstable…", 1.2f);
        if (forceStopped) yield break;

        yield return ShowStepText(
            "Erratic electrical loops inside right atrium.\nOut-of-sync firing → thick & thin blood pulses.",
            2f
        );
        if (forceStopped) yield break;

        float e = 0;
        while (e < 3.5f)
        {
            if (forceStopped) yield break;
            e += Time.deltaTime;
            float p = SmoothStep(e / 3.5f);

            leftVentricleAnimator.speed = Mathf.Lerp(1.3f, 1.7f, p);
            rightAtriumAnimator.speed = 1.7f + Mathf.Sin(Time.time * 6f) * 0.20f;

            foreach (var bf in allFlows)
            {
                if (bf == null) continue;
                var emission = bf.emission;
                float thick = Mathf.Lerp(80, 140,
                    Mathf.Sin(Time.time * 4f + bf.GetInstanceID()) * 0.5f + 0.5f);
                float thin = Mathf.Lerp(12, 25,
                    Mathf.Sin(Time.time * 5f + bf.GetInstanceID()) * 0.5f + 0.5f);
                emission.rateOverTime = Mathf.Sin(Time.time * 3f) > 0 ? thick : thin;
            }
            yield return null;
        }
    }

    IEnumerator Phase3_Critical()
    {
        if (forceStopped) yield break;
        yield return ShowStepText("Critical atrial fibrillation in RIGHT ATRIUM…", 1.2f);
        if (forceStopped) yield break;

        yield return ShowStepText(
            "Right atrium quivers instead of pumping.\nChaotic impulses destabilize ventricles → ↓ oxygen.",
            2f
        );
        if (forceStopped) yield break;

        float e = 0;
        while (e < 4f)
        {
            if (forceStopped) yield break;
            e += Time.deltaTime;
            float p = SmoothStep(e / 4f);

            float baseLV = Mathf.Lerp(1.4f, 0.8f, p);
            float jitter = 0.65f * Mathf.Sin(Time.time * 12f);

            leftVentricleAnimator.speed = Mathf.Clamp(baseLV + jitter, 0.4f, 1.9f);
            raR.material.color = raDark * (1f + Mathf.Sin(Time.time * 22f) * 0.48f);

            foreach (var bf in allFlows)
            {
                if (bf == null) continue;
                var emission = bf.emission;
                float thick = Mathf.Lerp(140, 200,
                    Mathf.Sin(Time.time * 7f + bf.GetInstanceID()) * 0.5f + 0.5f);
                float thin = Mathf.Lerp(10, 20,
                    Mathf.Sin(Time.time * 9f + bf.GetInstanceID()) * 0.5f + 0.5f);
                emission.rateOverTime = Mathf.Sin(Time.time * 6f + bf.GetInstanceID()) > 0.2f ? thick : thin;
            }
            yield return null;
        }
    }

    IEnumerator Phase4_ExplanationPacemaker()
    {
        if (forceStopped) yield break;
        yield return ShowStepText("Right atrium unable to maintain electrical rhythm.", 1.4f);
        if (forceStopped) yield break;

        yield return ShowStepText(
            "SA node & atrial pathways cannot reset.\nPacemaker required to override chaos.",
            1.8f
        );
        if (forceStopped) yield break;

        yield return ShowStepText("Handing over to Pacemaker Surgery…", 1.2f);
        if (forceStopped) yield break;

        // Only hand over if this cinematic was manually started and not force-stopped.
        if (manuallyStarted && !forceStopped && pacemakerSurgery != null)
            pacemakerSurgery.StartScenario();
    }

    void ResetHeart()
    {
        if (SANode != null)
            saR.material.color = saDefaultColor;

        laR.material.color = laDefaultColor;
        raR.material.color = raDefaultColor;
        lvR.material.color = lvDefaultColor;

        leftVentricleAnimator.speed = defaultLVSpeed;
        leftAtriumAnimator.speed = defaultLASpeed;
        rightAtriumAnimator.speed = defaultRASpeed;

        mainCam.transform.position = camDefaultPos;
        mainCam.transform.rotation = camDefaultRot;
        mainCam.fieldOfView = camDefaultFOV;
    }

    float SmoothStep(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // instant cut exit — NO cross-stop call to pacemaker (manager will handle group stop)
    public void ExitToNormalState()
    {
        if (routine != null)
            StopCoroutine(routine);

        StopAllCoroutines();
        forceStopped = true;
        manuallyStarted = false;

        laR.material.color = laDefaultColor;
        raR.material.color = raDefaultColor;
        lvR.material.color = lvDefaultColor;

        if (SANode != null && saR != null)
            saR.material.color = saDefaultColor;

        leftVentricleAnimator.speed = defaultLVSpeed;
        leftAtriumAnimator.speed = defaultLASpeed;
        rightAtriumAnimator.speed = defaultRASpeed;

        foreach (var bf in allFlows)
        {
            if (bf == null) continue;
            var em = bf.emission;
            em.rateOverTime = 200f;
        }

        mainCam.transform.position = camDefaultPos;
        mainCam.transform.rotation = camDefaultRot;
        mainCam.fieldOfView = camDefaultFOV;

        if (textGroup != null)
            textGroup.alpha = 0;

        Debug.Log("Arrhythmia scenario reset to normal state.");
    }
}