using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HeartAIModeCombinedController
/// - Combines HeartAIModeManager + AorticValveSurgeryController into one script
/// - Sequence: Valve blockage (mild → severe → critical) → automatic aortic surgery
/// - Button: 1st click = play full cinematic; 2nd click = full reset to normal
/// </summary>
public class HeartAIModeCombinedController : MonoBehaviour
{
    // ===============================================================
    // TOGGLE STATE + ORIGINAL TRANSFORMS
    // ===============================================================
    private int state = 0;

    // Parents (top-level meshes)
    private Vector3 mitralOrigPos, mitralOrigScale;
    private Quaternion mitralOrigRot;

    private Vector3 tricuspidOrigPos, tricuspidOrigScale;
    private Quaternion tricuspidOrigRot;

    private Vector3 aorticOriginalPos, aorticOriginalScale;
    private Quaternion aorticOriginalRot;

    private Vector3 lvOrigPos, lvOrigScale;
    private Quaternion lvOrigRot;

    // ===============================================================
    // VALVE BLOCKAGE FIELDS (HeartAIModeManager)
    // ===============================================================
    [Header("Valve Parents")]
    public Transform mitralValve;
    public Transform tricuspidValve;
    public Transform aorticValve;

    [Header("Left Ventricle")]
    public Transform leftVentricle;
    public Animator leftVentricleAnimator;

    [Header("Camera")]
    public Camera mainCam;

    [Header("Blood Flows (All Particle Systems)")]
    public ParticleSystem[] bloodFlows;

    // INTERNAL RENDERERS
    Renderer[] mitralR;
    Renderer[] tricuspidR;
    Renderer[] aorticR;
    Renderer lvR;

    // CAMERA DEFAULTS
    Vector3 camDefaultPos;
    Quaternion camDefaultRot;
    float camDefaultFOV;

    Color normalColor;
    Color darkTurquoise = new Color(0f, 0.45f, 0.45f);

    Coroutine routine;

    // store original particle values for bloodFlows (pre-surgery, fully normal)
    float[] bloodBaseSpeed;
    float[] bloodBaseSize;
    float[] bloodBaseRate;
    float[] bloodBaseNoise;
    bool[]  bloodBaseNoiseEnabled;     // <<< NEW: remember if noise was originally on/off

    // ===============================================================
    // BOTTOM BIOLOGICAL TEXT UI
    // ===============================================================
    Canvas bioCanvas;
    Text bioText;
    CanvasGroup bioGroup;

    void CreateBottomBioUI()
    {
        GameObject c = new GameObject("ValveBlockageBioCanvas");
        c.layer = 5;

        bioCanvas = c.AddComponent<Canvas>();
        bioCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = c.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        bioGroup = c.AddComponent<CanvasGroup>();
        bioGroup.alpha = 0;

        GameObject t = new GameObject("BioText");
        t.transform.SetParent(c.transform);

        bioText = t.AddComponent<Text>();
        RectTransform rt = bioText.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, -35f);
        rt.sizeDelta = new Vector2(950f, 150f);

        bioText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bioText.fontSize = 18;
        bioText.color = Color.white;
        bioText.alignment = TextAnchor.MiddleCenter;
    }

    IEnumerator ShowBioText(string msg, float holdSeconds)
    {
        bioText.text = msg;

        float a = 0f;
        while (a < 1f)
        {
            a += Time.deltaTime * 2f;
            bioGroup.alpha = Mathf.SmoothStep(0f, 1f, a);
            yield return null;
        }

        yield return new WaitForSeconds(holdSeconds);

        while (a > 0f)
        {
            a -= Time.deltaTime * 2f;
            bioGroup.alpha = Mathf.SmoothStep(0f, 1f, a);
            yield return null;
        }
    }

    // ===============================================================
    // SURGERY FIELDS (AorticValveSurgeryController)
    // ===============================================================
    [Header("Extreme Zoom Target during Removal")]
    public Transform ExtremeZoomPoint;

    [Header("Tools")]
    public Transform SurgicalTool_Scalpel;
    public Transform SurgicalTool_Grasper;
    public Transform SurgicalTool_Needle;

    [Header("Heart Meshes")]
    public Renderer[] heartMeshes;

    [Header("Blood Flow Restore (normal flows after surgery)")]
    public ParticleSystem[] bloodFlowsRestore;

    [Header("Timings")]
    public float prepTime = 3f, incisionTime = 4f, exposeTime = 3f, removeTime = 5f,
                 placeTime = 5f, sutureTime = 5f, restoreTime = 3f, exitTime = 2f;

    [Header("Prosthetic Path Points")]
    public Transform ProstheticStartPoint, ProstheticEntryPoint, ProstheticExitPoint, ProstheticEndPoint;

    // Surgery internals
    Renderer[] valveRenderers;
    Color[] valveOriginalColors, heartOriginalColors;

    // child transforms (leaflets) original local transforms
    Vector3[] valveLocalPos;
    Quaternion[] valveLocalRot;
    Vector3[] valveLocalScale;

    Transform prostheticRoot;
    List<GameObject> prostheticParts = new List<GameObject>();
    Vector3 prostheticOriginalScale;

    float[] restoreBaseSpeed;
    float[] restoreBaseRate;
    float[] restoreBaseNoise;

    float lv_DefaultSpeed = 1f;

    Canvas textCanvas;
    Text stepText;
    CanvasGroup textGroup;

    // ===============================================================
    // START / INIT
    // ===============================================================
    void Start()
    {
        // ---------------- VALVE SIDE ----------------
        if (mitralValve != null)    mitralR    = mitralValve.GetComponentsInChildren<Renderer>();
        if (tricuspidValve != null) tricuspidR = tricuspidValve.GetComponentsInChildren<Renderer>();
        if (aorticValve != null)    aorticR    = aorticValve.GetComponentsInChildren<Renderer>();

        if (leftVentricle != null)  lvR = leftVentricle.GetComponentInChildren<Renderer>();

        if (mitralR != null && mitralR.Length > 0)
            normalColor = mitralR[0].material.color;
        else
            normalColor = Color.red;

        // Save parent transforms (top-level)
        if (mitralValve != null)
        {
            mitralOrigPos = mitralValve.localPosition;
            mitralOrigRot = mitralValve.localRotation;
            mitralOrigScale = mitralValve.localScale;
        }

        if (tricuspidValve != null)
        {
            tricuspidOrigPos = tricuspidValve.localPosition;
            tricuspidOrigRot = tricuspidValve.localRotation;
            tricuspidOrigScale = tricuspidValve.localScale;
        }

        if (aorticValve != null)
        {
            aorticOriginalPos = aorticValve.localPosition;
            aorticOriginalRot = aorticValve.localRotation;
            aorticOriginalScale = aorticValve.localScale;
        }

        if (leftVentricle != null)
        {
            lvOrigPos = leftVentricle.localPosition;
            lvOrigRot = leftVentricle.localRotation;
            lvOrigScale = leftVentricle.localScale;
        }

        if (leftVentricleAnimator != null)
            lv_DefaultSpeed = leftVentricleAnimator.speed;

        camDefaultPos = mainCam != null ? mainCam.transform.position : Vector3.zero;
        camDefaultRot = mainCam != null ? mainCam.transform.rotation : Quaternion.identity;
        camDefaultFOV = mainCam != null ? mainCam.fieldOfView : 60f;

        CreateBottomBioUI();

        // ---------------- SURGERY SIDE ----------------
        if (aorticValve != null)
        {
            valveRenderers = aorticValve.GetComponentsInChildren<Renderer>();
            int n = valveRenderers.Length;

            valveOriginalColors = new Color[n];
            valveLocalPos   = new Vector3[n];
            valveLocalRot   = new Quaternion[n];
            valveLocalScale = new Vector3[n];

            for (int i = 0; i < n; i++)
            {
                valveOriginalColors[i] = valveRenderers[i].material.color;
                valveLocalPos[i] = valveRenderers[i].transform.localPosition;
                valveLocalRot[i] = valveRenderers[i].transform.localRotation;
                valveLocalScale[i] = valveRenderers[i].transform.localScale;
            }
        }

        if (heartMeshes != null && heartMeshes.Length > 0)
        {
            heartOriginalColors = new Color[heartMeshes.Length];
            for (int i = 0; i < heartMeshes.Length; i++)
                heartOriginalColors[i] = heartMeshes[i].material.color;
        }

        // Restore particles base (surgery restore array)
        if (bloodFlowsRestore != null)
        {
            int n = bloodFlowsRestore.Length;
            restoreBaseSpeed = new float[n];
            restoreBaseRate = new float[n];
            restoreBaseNoise = new float[n];

            for (int i = 0; i < n; i++)
            {
                var m = bloodFlowsRestore[i].main;
                var e = bloodFlowsRestore[i].emission;
                var no = bloodFlowsRestore[i].noise;

                restoreBaseSpeed[i] = m.startSpeed.constant;
                restoreBaseRate[i] = e.rateOverTime.constant;
                restoreBaseNoise[i] = no.strength.constant;
            }
        }

        // Store NORMAL starting blood values (before any blockage)
        if (bloodFlows != null)
        {
            int n = bloodFlows.Length;
            bloodBaseSpeed = new float[n];
            bloodBaseSize = new float[n];
            bloodBaseRate = new float[n];
            bloodBaseNoise = new float[n];
            bloodBaseNoiseEnabled = new bool[n];           // NEW

            for (int i = 0; i < n; i++)
            {
                var bf = bloodFlows[i];
                var m = bf.main;
                var e = bf.emission;
                var no = bf.noise;

                bloodBaseSpeed[i] = m.startSpeed.constant;
                bloodBaseSize[i]  = m.startSize.constant;
                bloodBaseRate[i]  = e.rateOverTime.constant;
                bloodBaseNoise[i] = no.strength.constant;
                bloodBaseNoiseEnabled[i] = no.enabled;      // remember if noise was on/off
            }
        }

        CreateBottomTextUI();
    }

    // ===============================================================
    // PUBLIC START TRIGGERS
    // ===============================================================
    public void StartValveBlockageCinematic()
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(ValveBlockageSequence());
    }

    public void StartAorticSurgery() => StartCoroutine(DoFullSurgery());

    // ===============================================================
    // MAIN VALVE SEQUENCE
    // ===============================================================
    private IEnumerator ValveBlockageSequence()
    {
        StartCoroutine(ShowBioText(
            "Valve blockage often develops slowly over years.\nCalcium buildup and scarring make the valve leaflets stiff and thick.",
            5f
        ));

        yield return StartCoroutine(Mild_Mitral_Tricuspid_Perfect());
        yield return StartCoroutine(Severe_Aortic_25s());
        yield return StartCoroutine(CriticalAorticMode());

        // Automatically start surgery
        StartAorticSurgery();

        routine = null;
    }

    // ---------------- PHASE 1 – MILD ----------------
    private IEnumerator Mild_Mitral_Tricuspid_Perfect()
    {
        StartCoroutine(ShowBioText(
            "Mitral and tricuspid valves develop mild narrowing.\nBlood must squeeze through slightly tighter openings.",
            5f
        ));

        Vector3 targetPos =
            (mitralValve.position + tricuspidValve.position) * 0.5f +
            new Vector3(0, 0.12f, 0.15f);

        Quaternion targetRot = Quaternion.LookRotation(
            ((mitralValve.position + tricuspidValve.position) * 0.5f) - targetPos
        );

        float t = 0;
        Vector3 startPos = mainCam.transform.position;
        Quaternion startRot = mainCam.transform.rotation;

        while (t < 1f)
        {
            t += Time.deltaTime;
            float s = SmoothStep(t);

            mainCam.transform.position = Vector3.Lerp(startPos, targetPos, s);
            mainCam.transform.rotation = Quaternion.Slerp(startRot, targetRot, s);

            yield return null;
        }

        StartCoroutine(ShowBioText(
            "Flow through the chambers turns slightly irregular.\nPressure changes cause uneven pulses of blood.",
            5f
        ));

        float mildStartSpeed = 0.40f;
        float mildEndSpeed   = 0.33f;

        foreach (var bf in bloodFlows)
        {
            var main = bf.main;
            main.startSpeed = mildStartSpeed;
            main.startSize  = 0.08f;

            var noise = bf.noise;
            noise.enabled = true;
            noise.strength = 0f;
        }

        float elapsed = 0f;
        float total   = 5f;

        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / total;
            float s = SmoothStep(p);

            Color mildColor = Color.Lerp(normalColor, darkTurquoise, s);
            foreach (var r in mitralR)    r.material.color = mildColor;
            foreach (var r in tricuspidR) r.material.color = mildColor;

            foreach (var bf in bloodFlows)
            {
                var main = bf.main;
                main.startSpeed = Mathf.Lerp(mildStartSpeed, mildEndSpeed, s);
                main.startSize  = Mathf.Lerp(0.08f, 0.075f, s);

                var noise = bf.noise;
                noise.strength = Mathf.Lerp(0f, 0.10f, s);
            }

            yield return null;
        }

        foreach (var r in mitralR)    r.material.color = normalColor;
        foreach (var r in tricuspidR) r.material.color = normalColor;
    }

    // ---------------- PHASE 2 – SEVERE AORTIC ----------------
    private IEnumerator Severe_Aortic_25s()
    {
        StartCoroutine(ShowBioText(
            "The aortic valve now shows severe blockage.\nOnly a narrow opening remains for blood to exit the heart.",
            5f
        ));

        Vector3 targetPos =
            aorticValve.position +
            (-aorticValve.forward * 0.17f) +
            (Vector3.up * 0.04f);

        Quaternion targetRot =
            Quaternion.LookRotation(aorticValve.position - targetPos);

        float t = 0;
        Vector3 startPos = mainCam.transform.position;
        Quaternion startRot = mainCam.transform.rotation;

        while (t < 1f)
        {
            t += Time.deltaTime / 1.5f;
            float s = SmoothStep(t);

            mainCam.transform.position = Vector3.Lerp(startPos, targetPos, s);
            mainCam.transform.rotation = Quaternion.Slerp(startRot, targetRot, s);

            yield return null;
        }

        StartCoroutine(ShowBioText(
            "Less blood can pass through each beat.\nForward flow through the aorta gradually falls.",
            5f
        ));

        float elapsed = 0f;
        float total   = 25f;

        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            float s = SmoothStep(elapsed / total);

            foreach (var r in aorticR)
                r.material.color = Color.Lerp(normalColor, Color.black, s);

            aorticValve.localScale =
                Vector3.Lerp(Vector3.one, Vector3.one * 0.35f, s);

            if (lvR)
                lvR.material.color =
                    Color.Lerp(normalColor, new Color(0f, 0.25f, 0.05f), s);

            leftVentricle.localScale =
                Vector3.Lerp(Vector3.one, Vector3.one * 1.18f, s);

            leftVentricleAnimator.speed =
                Mathf.Lerp(1f, 0.25f, s);

            foreach (var bf in bloodFlows)
            {
                var main = bf.main;
                main.startSpeed = Mathf.Lerp(0.40f, 0.12f, s);
                main.startSize  = Mathf.Lerp(0.08f, 0.012f, s);

                var em = bf.emission;
                em.rateOverTime = Mathf.Lerp(65, 8, s);

                var noise = bf.noise;
                noise.strength = Mathf.Lerp(0f, 0.55f, s);
            }

            mainCam.fieldOfView =
                camDefaultFOV + Mathf.Sin(Time.time * 2.5f) * (1.2f + s * 1.5f);

            yield return null;
        }
    }

    // ---------------- PHASE 3 – CRITICAL ----------------
    private IEnumerator CriticalAorticMode()
    {
        StartCoroutine(ShowBioText(
            "The diseased aortic valve becomes tight and rigid.\nIt barely opens and almost stops forward flow.",
            5f
        ));

        StartCoroutine(ShowBioText(
            "Blood flow collapses and circulation fails.\nEmergency valve replacement is needed to save life.",
            5f
        ));

        float elapsed = 0;
        float total   = 7f;

        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            float s = elapsed / total;

            foreach (var r in aorticR)
                r.material.color = Color.black;

            if (lvR)
                lvR.material.color =
                    Color.Lerp(lvR.material.color, new Color(0f, 0.1f, 0.02f), s);

            leftVentricleAnimator.speed =
                Mathf.Lerp(0.25f, 0.1f, s);

            foreach (var bf in bloodFlows)
            {
                var main = bf.main;
                main.startSpeed = 0;
                main.startSize  = 0;

                var em = bf.emission;
                em.rateOverTime = 0;
            }

            mainCam.fieldOfView =
                camDefaultFOV + Mathf.Sin(Time.time * 9f) * 2f;

            yield return null;
        }
    }

    // ===============================================================
    // COMMON SmoothStep
    // ===============================================================
    private float SmoothStep(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // ===============================================================
    // SURGERY SEQUENCE + UI
    // ===============================================================
    private void CreateBottomTextUI()
    {
        GameObject c = new GameObject("AorticValveTextCanvas");
        c.layer = 5;

        textCanvas = c.AddComponent<Canvas>();
        textCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = c.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        textGroup = c.AddComponent<CanvasGroup>();
        textGroup.alpha = 0;

        GameObject t = new GameObject("StepText");
        t.transform.SetParent(c.transform);

        stepText = t.AddComponent<Text>();
        RectTransform rt = stepText.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, -35f);
        rt.sizeDelta = new Vector2(950f, 150f);

        stepText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        stepText.fontSize = 20;
        stepText.color = Color.white;
        stepText.alignment = TextAnchor.MiddleCenter;
    }

    IEnumerator ShowStepText(string msg, float hold = 2.2f)
    {
        stepText.text = msg;

        float a = 0f;
        while (a < 1f)
        {
            a += Time.deltaTime * 2f;
            textGroup.alpha = Mathf.SmoothStep(0, 1, a);
            yield return null;
        }

        yield return new WaitForSeconds(hold);

        while (a > 0f)
        {
            a -= Time.deltaTime * 1.5f;
            textGroup.alpha = Mathf.SmoothStep(0, 1, a);
            yield return null;
        }
    }

    private IEnumerator DoFullSurgery()
    {
        Vector3 prepPos = aorticValve.position +
                          (-aorticValve.forward * 0.20f) +
                          Vector3.up * 0.07f;
        Quaternion prepRot = Quaternion.LookRotation(aorticValve.position - prepPos);

        StartCoroutine(ShowStepText(
            "Surgical field prepared. Aortic valve isolated.",
            2.3f
        ));
        yield return LerpCamera(camDefaultPos, prepPos, camDefaultRot, prepRot, prepTime);

        StartCoroutine(ShowStepText(
            "Incision created to access the diseased valve.",
            2.2f
        ));
        AutoPlaceTool(SurgicalTool_Scalpel, 0.10f, 0.03f, 0);
        SurgicalTool_Scalpel.gameObject.SetActive(true);
        yield return IncisionAnimation(incisionTime);
        SurgicalTool_Scalpel.gameObject.SetActive(false);

        StartCoroutine(ShowStepText(
            "Stenotic aortic valve exposed for removal.",
            2.4f
        ));
        yield return ExposeAnimation(exposeTime);

        StartCoroutine(ShowStepText(
            "Calcified leaflets removed to clear outflow tract.",
            2.6f
        ));
        AutoPlaceTool(SurgicalTool_Grasper, 0.12f, 0.02f, 0);
        SurgicalTool_Grasper.gameObject.SetActive(true);
        yield return RemoveOldValveWithExtremeZoom(removeTime);
        SurgicalTool_Grasper.gameObject.SetActive(false);

        StartCoroutine(ShowStepText(
            "Prosthetic valve guided into the aortic position.",
            2.6f
        ));
        CreateRedProstheticFromOld();
        yield return PlaceProstheticPath(placeTime);

        StartCoroutine(ShowStepText(
            "Valve secured with uniform annular suturing.",
            2.6f
        ));
        AutoPlaceTool(SurgicalTool_Needle, 0.12f, 0.03f, 0.02f);
        SurgicalTool_Needle.gameObject.SetActive(true);
        yield return SutureAnimation(sutureTime);
        SurgicalTool_Needle.gameObject.SetActive(false);

        StartCoroutine(ShowStepText(
            "Forward flow restored. Left ventricle ejects freely.",
            2.7f
        ));
        yield return RestoreHeartAndBlood(); // + blood boost

        StartCoroutine(ShowStepText(
            "Aortic valve replacement successful.",
            2.6f
        ));

        yield return LerpCamera(mainCam.transform.position, camDefaultPos,
                                mainCam.transform.rotation, camDefaultRot,
                                exitTime);
    }

    // ---------------- SURGERY HELPERS (UNCHANGED LOGIC) ----------------
    private IEnumerator IncisionAnimation(float d)
    {
        Vector3 st = SurgicalTool_Scalpel.position + SurgicalTool_Scalpel.right * -0.03f;
        Vector3 en = SurgicalTool_Scalpel.position + SurgicalTool_Scalpel.right * 0.03f;

        float t = 0;
        while (t < d)
        {
            t += Time.deltaTime;
            float s = SmoothStep(t / d);
            SurgicalTool_Scalpel.position = Vector3.Lerp(st, en, s);
            yield return null;
        }
    }

    private IEnumerator ExposeAnimation(float d)
    {
        float t = 0;
        while (t < d)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator RemoveOldValveWithExtremeZoom(float duration)
    {
        Vector3 pullDir = aorticValve.forward;

        Vector3 returnPos = mainCam.transform.position;
        Quaternion returnRot = mainCam.transform.rotation;
        float returnFOV = mainCam.fieldOfView;

        if (ExtremeZoomPoint != null)
        {
            yield return LerpCameraFOV(
                mainCam.transform.position, ExtremeZoomPoint.position,
                mainCam.transform.rotation, ExtremeZoomPoint.rotation,
                returnFOV, 12f, 1.2f);
        }

        float tt = 0;
        while (tt < 1f)
        {
            tt += Time.deltaTime * 1.2f;
            float s = SmoothStep(tt);

            Color black = Color.black;
            Color red = new Color(0.9f, 0.15f, 0.15f);
            Color white = Color.white;

            Color blend = s < 0.5f ?
                          Color.Lerp(black, red, s * 2f) :
                          Color.Lerp(red, white, (s - 0.5f) * 2f);

            foreach (var r in valveRenderers)
                r.material.color = blend;

            yield return null;
        }

        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float s = SmoothStep(t / duration);

            foreach (var r in valveRenderers)
            {
                r.transform.position += pullDir * 0.20f * Time.deltaTime;
                Color c = r.material.color;
                c.a = Mathf.Lerp(1f, 0f, s);
                r.material.color = c;
            }

            yield return null;
        }

        foreach (var r in valveRenderers) r.enabled = false;

        yield return LerpCameraFOV(
            mainCam.transform.position, returnPos,
            mainCam.transform.rotation, returnRot,
            12f, returnFOV,
            1.2f);
    }

    private void CreateRedProstheticFromOld()
    {
        if (prostheticRoot != null)
            Destroy(prostheticRoot.gameObject);

        prostheticRoot = new GameObject("ProstheticValveRoot").transform;
        prostheticRoot.position = ProstheticStartPoint.position;
        prostheticRoot.rotation = ProstheticStartPoint.rotation;

        prostheticParts.Clear();

        foreach (var r in valveRenderers)
        {
            GameObject clone = new GameObject("Prosthetic_Part");
            clone.transform.SetParent(prostheticRoot);

            MeshFilter srcMF = r.GetComponent<MeshFilter>();
            if (srcMF != null)
            {
                MeshFilter mf = clone.AddComponent<MeshFilter>();
                mf.mesh = srcMF.sharedMesh;
            }

            MeshRenderer mr = clone.AddComponent<MeshRenderer>();
            Material mat = new Material(r.sharedMaterial);
            mat.color = new Color(1f, 0.15f, 0.15f, 1f);
            mr.material = mat;

            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;

            clone.transform.localScale = r.transform.lossyScale * 2f;
            prostheticOriginalScale = clone.transform.localScale;

            prostheticParts.Add(clone);
        }
    }

    private IEnumerator PlaceProstheticPath(float time)
    {
        float seg = time / 3f;

        if (ProstheticEntryPoint != null)
            yield return MoveLinear(prostheticRoot, ProstheticEntryPoint.position, ProstheticEntryPoint.rotation, seg);

        if (ProstheticExitPoint != null)
            yield return MoveLinear(prostheticRoot, ProstheticExitPoint.position, ProstheticExitPoint.rotation, seg);

        if (ProstheticEndPoint != null)
            yield return MoveWithBounce(prostheticRoot, ProstheticEndPoint, seg);
    }

    private IEnumerator MoveLinear(Transform obj, Vector3 pos, Quaternion rot, float d)
    {
        Vector3 sPos = obj.position;
        Quaternion sRot = obj.rotation;

        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime / d;
            float k = SmoothStep(t);

            obj.position = Vector3.Lerp(sPos, pos, k);
            obj.rotation = Quaternion.Slerp(sRot, rot, k);

            yield return null;
        }
    }

    private IEnumerator MoveWithBounce(Transform obj, Transform final, float d)
    {
        Vector3 finalPos = final.position;
        Quaternion finalRot = final.rotation;

        Vector3 dir = (finalPos - obj.position).normalized;
        Vector3 over = finalPos + dir * 0.02f;

        float t1 = d * 0.6f;
        float x = 0;
        Vector3 sPos = obj.position;

        while (x < 1)
        {
            x += Time.deltaTime / t1;
            float k = SmoothStep(x);

            obj.position = Vector3.Lerp(sPos, over, k);
            obj.rotation = Quaternion.Slerp(obj.rotation, finalRot, k);

            yield return null;
        }

        float t2 = d * 0.4f;
        x = 0;
        sPos = obj.position;

        while (x < 1)
        {
            x += Time.deltaTime / t2;
            float k = 1f - Mathf.Pow(1f - x, 2f);

            obj.position = Vector3.Lerp(sPos, finalPos, k);
            obj.rotation = Quaternion.Slerp(obj.rotation, finalRot, k);

            yield return null;
        }
    }

    private IEnumerator SutureAnimation(float d)
    {
        Vector3 st = SurgicalTool_Needle.position + SurgicalTool_Needle.right * 0.03f;
        Vector3 en = SurgicalTool_Needle.position + SurgicalTool_Needle.right * -0.03f;

        float t = 0;
        while (t < d)
        {
            t += Time.deltaTime;
            float osc = (Mathf.Sin(t * 4f) + 1f) * 0.5f;

            SurgicalTool_Needle.position = Vector3.Lerp(st, en, osc);

            float pulse = Mathf.Lerp(0.98f, 1.02f, osc);

            foreach (var p in prostheticParts)
                p.transform.localScale = prostheticOriginalScale * pulse;

            yield return null;
        }

        foreach (var p in prostheticParts)
            p.transform.localScale = prostheticOriginalScale;
    }

    private IEnumerator RestoreHeartAndBlood()
    {
        float t = 0;

        int n = bloodFlowsRestore.Length;

        float[] curSpeed = new float[n];
        float[] curRate = new float[n];
        float[] curNoise = new float[n];

        for (int i = 0; i < n; i++)
        {
            var m = bloodFlowsRestore[i].main;
            var e = bloodFlowsRestore[i].emission;
            var no = bloodFlowsRestore[i].noise;

            curSpeed[i] = m.startSpeed.constant;
            curRate[i] = e.rateOverTime.constant;
            curNoise[i] = no.strength.constant;
        }

        leftVentricleAnimator.speed = lv_DefaultSpeed;

        while (t < 1)
        {
            t += Time.deltaTime / restoreTime;
            float s = Mathf.SmoothStep(0, 1, t);

            for (int i = 0; i < heartMeshes.Length; i++)
            {
                heartMeshes[i].material.color =
                    Color.Lerp(heartMeshes[i].material.color, heartOriginalColors[i], s);
            }

            for (int i = 0; i < n; i++)
            {
                var m = bloodFlowsRestore[i].main;
                var e = bloodFlowsRestore[i].emission;
                var no = bloodFlowsRestore[i].noise;

                m.startSpeed = Mathf.Lerp(curSpeed[i], restoreBaseSpeed[i], s);
                e.rateOverTime = Mathf.Lerp(curRate[i], restoreBaseRate[i], s);
                no.strength = Mathf.Lerp(curNoise[i], restoreBaseNoise[i], s);
            }

            yield return null;
        }

        BoostBloodFlow();
    }

    private void BoostBloodFlow()
    {
        foreach (var bf in bloodFlowsRestore)
        {
            if (bf == null) continue;

            var em = bf.emission;
            em.rateOverTime = new ParticleSystem.MinMaxCurve(150f);
        }
    }

    private IEnumerator LerpCamera(Vector3 fp, Vector3 tp, Quaternion fr, Quaternion tr, float d)
    {
        float t = 0;
        while (t < d)
        {
            t += Time.deltaTime;
            float s = SmoothStep(t / d);

            mainCam.transform.position = Vector3.Lerp(fp, tp, s);
            mainCam.transform.rotation = Quaternion.Slerp(fr, tr, s);

            yield return null;
        }
    }

    private IEnumerator LerpCameraFOV(Vector3 fp, Vector3 tp, Quaternion fr, Quaternion tr,
                                      float fFOV, float tFOV, float d)
    {
        float t = 0;
        while (t < d)
        {
            t += Time.deltaTime;
            float s = SmoothStep(t / d);

            mainCam.transform.position = Vector3.Lerp(fp, tp, s);
            mainCam.transform.rotation = Quaternion.Slerp(fr, tr, s);
            mainCam.fieldOfView = Mathf.Lerp(fFOV, tFOV, s);

            yield return null;
        }
    }

    private void AutoPlaceTool(Transform t, float f, float u, float r)
    {
        if (t == null) return;

        Vector3 pos = aorticValve.position +
                      (-aorticValve.forward * f) +
                      (aorticValve.up * u) +
                      (aorticValve.right * r);

        t.position = pos;
        t.rotation = Quaternion.LookRotation(aorticValve.position - pos);
    }

    // ===============================================================
    // TOGGLE BUTTON ENTRY POINT
    // ===============================================================
    public void ToggleHeartScenario()
    {
        if (state == 0)
        {
            // FIRST PRESS → START FULL CINEMATIC
            StartValveBlockageCinematic();
            state = 1;
        }
        else
        {
            // SECOND PRESS → FULL RESET TO NORMAL
            StopAllCoroutines();
            ResetToNormal();
            state = 0;
        }
    }

    // ===============================================================
    // FULL RESET TO NORMAL (BEFORE ANY BLOCKAGE / SURGERY)
    // ===============================================================
    private void ResetToNormal()
    {
        StopAllCoroutines();

        // ---- CAMERA ----
        if (mainCam != null)
        {
            mainCam.transform.position = camDefaultPos;
            mainCam.transform.rotation = camDefaultRot;
            mainCam.fieldOfView = camDefaultFOV;
        }

        // ---- PARENT TRANSFORMS ----
        if (mitralValve != null)
        {
            mitralValve.localPosition = mitralOrigPos;
            mitralValve.localRotation = mitralOrigRot;
            mitralValve.localScale = mitralOrigScale;
        }

        if (tricuspidValve != null)
        {
            tricuspidValve.localPosition = tricuspidOrigPos;
            tricuspidValve.localRotation = tricuspidOrigRot;
            tricuspidValve.localScale = tricuspidOrigScale;
        }

        if (aorticValve != null)
        {
            aorticValve.localPosition = aorticOriginalPos;
            aorticValve.localRotation = aorticOriginalRot;
            aorticValve.localScale = aorticOriginalScale;
        }

        if (leftVentricle != null)
        {
            leftVentricle.localPosition = lvOrigPos;
            leftVentricle.localRotation = lvOrigRot;
            leftVentricle.localScale = lvOrigScale;

    leftVentricleAnimator.speed = lv_DefaultSpeed;         // restore speed
    leftVentricleAnimator.Rebind();                        // reset all states
    leftVentricleAnimator.Update(0f);                      // force refresh

        }

        // ---- AORTIC VALVE CHILDREN (leaflets) ----
        if (valveRenderers != null && valveLocalPos != null)
        {
            for (int i = 0; i < valveRenderers.Length; i++)
            {
                if (valveRenderers[i] == null) continue;

                valveRenderers[i].enabled = true;
                valveRenderers[i].transform.localPosition = valveLocalPos[i];
                valveRenderers[i].transform.localRotation = valveLocalRot[i];
                valveRenderers[i].transform.localScale = valveLocalScale[i];

                if (i < valveOriginalColors.Length)
                    valveRenderers[i].material.color = valveOriginalColors[i];
            }
        }

        // ---- MITRAL & TRICUSPID COLORS ----
        if (mitralR != null)
        {
            foreach (var r in mitralR)
                if (r != null) r.material.color = normalColor;
        }
        if (tricuspidR != null)
        {
            foreach (var r in tricuspidR)
                if (r != null) r.material.color = normalColor;
        }

        // ---- HEART MESHES COLORS ----
        if (heartMeshes != null && heartOriginalColors != null)
        {
            for (int i = 0; i < heartMeshes.Length; i++)
                if (heartMeshes[i] != null) heartMeshes[i].material.color = heartOriginalColors[i];
        }

        // ---- LEFT VENTRICLE animator ----
        if (leftVentricleAnimator != null)
            leftVentricleAnimator.speed = lv_DefaultSpeed;

        // ---- BLOOD FLOWS (MAIN) — restore EXACT normal start values ----
        if (bloodFlows != null && bloodBaseSpeed != null)
        {
            for (int i = 0; i < bloodFlows.Length; i++)
            {
                var bf = bloodFlows[i];
                if (bf == null) continue;

                var m = bf.main;
                var e = bf.emission;
                var no = bf.noise;

                m.startSpeed = bloodBaseSpeed[i];
                m.startSize  = bloodBaseSize[i];
                e.rateOverTime = bloodBaseRate[i];
                no.strength = bloodBaseNoise[i];
                no.enabled  = bloodBaseNoiseEnabled[i];   // <<< reset noise on/off to original

                if (!bf.isPlaying) bf.Play();
            }
        }

        // ---- BLOOD FLOWS RESTORE (after surgery) → back to calm normal ----
        if (bloodFlowsRestore != null && restoreBaseSpeed != null)
        {
            for (int i = 0; i < bloodFlowsRestore.Length; i++)
            {
                var bf = bloodFlowsRestore[i];
                if (bf == null) continue;

                var m = bf.main;
                var e = bf.emission;
                var no = bf.noise;

                m.startSpeed = restoreBaseSpeed[i];
                e.rateOverTime = restoreBaseRate[i];
                no.strength = restoreBaseNoise[i];

                if (!bf.isPlaying) bf.Play();
            }
        }

        // ---- PROSTHETIC CLEANUP ----
        if (prostheticRoot != null)
        {
            Destroy(prostheticRoot.gameObject);
            prostheticRoot = null;
            prostheticParts.Clear();
        }

        // ---- TOOLS OFF ----
        if (SurgicalTool_Scalpel != null) SurgicalTool_Scalpel.gameObject.SetActive(false);
        if (SurgicalTool_Grasper != null) SurgicalTool_Grasper.gameObject.SetActive(false);
        if (SurgicalTool_Needle != null) SurgicalTool_Needle.gameObject.SetActive(false);

        // ---- UI TEXT CLEANUP ----
        if (bioGroup != null) bioGroup.alpha = 0;
        if (textGroup != null) textGroup.alpha = 0;
        if (bioText != null) bioText.text = "";
        if (stepText != null) stepText.text = "";
    }
}