using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RL_FullCombined_Final : MonoBehaviour
{
    // ============================================================
    // PUBLIC REFERENCES
    // ============================================================

    [Header("Heart Parent Meshes")]
    public Transform RightAtrium;
    public Transform RightVentricle;
    public Transform LeftAtrium;
    public Transform LeftVentricle;
      // we do NOT recolour this back – stays default

    [Header("Parent Animators")]
    public Animator RA_Animator;
    public Animator RV_Animator;
    public Animator LA_Animator;
    public Animator LV_Animator;
    

    [Header("Blood Particle Systems")]
    public ParticleSystem[] RA_Blood;
    public ParticleSystem[] RV_Blood;
    public ParticleSystem[] LA_Blood;
    public ParticleSystem[] LV_Blood;

    [Header("Needle / Injection Points")]
    public Transform NeedleStartPoint;
    public Transform NeedleTravelPoint;
    public Transform NeedleInjectPoint;

    [Header("Oxygen Mask Anchor (World)")]
    public Transform OxygenMaskAnchor;   // no visual mask now – kept just as unused field

    [Header("Vitals UI (optional)")]
    public Text bpmText;
    public Text spo2Text;

    [Header("Durations (seconds)")]
    public float phase0Duration = 1f;    // baseline normal
    public float phase1Duration = 10f;   // near arrest
    public float phase2Duration = 20f;   // syringe + epi
    public float phase3Duration = 15f;   // shock + recovery

    // ============================================================
    // COLOURS
    // ============================================================

    private Color PURPLE       = new Color(0.35f, 0.12f, 0.35f);  // emergency blood
    private Color GOLD         = new Color(1f, 0.85f, 0.32f);     // abnormal heart
    private Color MAROON       = new Color(0.45f, 0.05f, 0.05f);  // recovered chamber
    private Color NORMAL_HEART = new Color(0.78f, 0.25f, 0.20f);  // heart mesh
    private Color NORMAL_BLOOD = new Color(0.6f, 0.05f, 0.05f);   // blood
    private Color FIRE         = new Color(1.0f, 0.55f, 0.15f);   // shock

    // ============================================================
    // INTERNAL STATE
    // ============================================================

    private Material purpleMat;
    private Material normalBloodMat;
    private GameObject syringeGO;

    private bool RA_boosted, RV_boosted, LA_boosted, LV_boosted;

    private bool uiCreated = false;
    private Text autoBpm, autoSpo2;

    // reward graph canvas (for both graphs)
    private GameObject rewardGraphCanvasGO;

    // bottom text UI
    private Canvas bottomCanvas;
    private CanvasGroup bottomGroup;
    private Text bottomText;

    // vitals canvas root so we can destroy it on reset
    private GameObject vitalsCanvasGO;

    // ============================================================
    // PHASE-0 CAPTURE STATE
    // ============================================================

    private bool phase0Captured = false;

    // heart mesh colours per renderer at phase 0
    private struct MeshRendererState
    {
        public Renderer renderer;
        public Color color;
    }
    private List<MeshRendererState> phase0HeartRenderers = new List<MeshRendererState>();

    // blood particle state at phase 0
    private struct BloodPSState
    {
        public ParticleSystem ps;
        public Material originalMaterial;
        public Color originalMaterialColor;
        public Color originalStartColor;
        public float emissionRate;
    }
    private List<BloodPSState> phase0BloodPS = new List<BloodPSState>();

    // original animator speeds
    private float orig_RA_AnimSpeed;
    private float orig_RV_AnimSpeed;
    private float orig_LA_AnimSpeed;
    private float orig_LV_AnimSpeed;
    private float orig_PA_AnimSpeed;

    // ============================================================
    // PUBLIC ENTRY (BUTTON)
    // ============================================================

    public void StartFullRLDemo()
    {
        // capture phase-0 scene state only once (first run)
        if (!phase0Captured)
        {
            CapturePhase0State();
            phase0Captured = true;
        }

        CreateTopVitalsUI();
        EnsureBottomTextUI();

        // reset flags
        RA_boosted = RV_boosted = LA_boosted = LV_boosted = false;

        StartCoroutine(MainSequence());
    }

    // ============================================================
    // MAIN SEQUENCE
    // ============================================================

    private IEnumerator MainSequence()
    {
        // ---------------- PHASE 0 – BASELINE NORMAL ----------------
        yield return Phase0_Baseline();

        // ---------------- PHASE 1 – NEAR ARREST ----------------
        StartCoroutine(ShowBottomText("Heart near-arrest. Blood almost stopped and vitals falling.", phase1Duration));
        yield return Phase1_NearArrest();

        // ---------------- PHASE 2 – SYRINGE + EPINEPHRINE ----------------
        StartCoroutine(ShowBottomText("Epinephrine injected: Passes through RA → RV → LV → LA and increases BP of each chamber.", phase2Duration ));
        yield return Phase2_InjectionAndBoosts();

        // ---------------- FIRST REWARD GRAPH (3 attempts: -10, 0, +40) -------------
        ShowRewardGraphFirst();
        yield return new WaitForSeconds(3f);

        // ---------------- PHASE 3 – SHOCK + OXYGEN (TEXT ONLY) + RECOVERY ----------
        StartCoroutine(ShowBottomText("Cardioversion shock given. Oxygen support started.Blood slowly gets oxygenated by changing into red colour", phase3Duration));
        yield return Phase3_ShockAndOxygenRecovery();

        // ---------------- SECOND REWARD GRAPH (4 attempts: -10, 0, +40, +60) -------
        ShowRewardGraphFinal();
        yield return new WaitForSeconds(3f);

        // ---------------- FINAL BIG TEXT -------------------------------------------
        yield return ShowFinalSuccessText();
    }

    // ============================================================
    // PHASE 0 — BASELINE NORMAL (NO CHANGE)
    // ============================================================

    private IEnumerator Phase0_Baseline()
    {
        float t = 0f;
        while (t < phase0Duration)
        {
            t += Time.deltaTime;
            // Do NOTHING – heart remains exactly as in the scene
            yield return null;
        }
    }

    // ============================================================
    // PHASE 1 — NEAR ARREST
    // ============================================================

    private IEnumerator Phase1_NearArrest()
    {
        float t = 0f;

        // abnormal gold heart
        ApplyHeartColor(GOLD);

        // almost stopped animators
        SetAnimatorSpeed(RA_Animator, 0.05f);
        SetAnimatorSpeed(RV_Animator, 0.05f);
        SetAnimatorSpeed(LA_Animator, 0.05f);
        SetAnimatorSpeed(LV_Animator, 0.05f);
        

        // very low purple blood in all chambers
        ForcePurpleLow(RA_Blood);
        ForcePurpleLow(RV_Blood);
        ForcePurpleLow(LA_Blood);
        ForcePurpleLow(LV_Blood);

        while (t < phase1Duration)
        {
            t += Time.deltaTime;
            float p = t / phase1Duration;

            int bpm = Mathf.RoundToInt(Mathf.Lerp(40, 10, p));
            int spo = Mathf.RoundToInt(Mathf.Lerp(88, 60, p));

            UpdateVitals(bpm, spo);
            yield return null;
        }
    }

    // ============================================================
    // PHASE 2 — SYRINGE + EPINEPHRINE BOOST
    // (RA → RV → LV → LA, all with fluid purple blood)
    // ============================================================

    private IEnumerator Phase2_InjectionAndBoosts()
    {
        // create & inject giant syringe
        yield return CreateAndInjectLargeSyringe();

        float seg = Mathf.Max(0.8f, (phase2Duration * 0.9f) / 4f);

        // RA then RV then LV then LA
        if (!RA_boosted) { yield return BoostChamber(RA_Blood, RA_Animator, RightAtrium, seg); RA_boosted = true; }
        if (!RV_boosted) { yield return BoostChamber(RV_Blood, RV_Animator, RightVentricle, seg); RV_boosted = true; }
        if (!LV_boosted) { yield return BoostChamber(LV_Blood, LV_Animator, LeftVentricle, seg); LV_boosted = true; }
        if (!LA_boosted) { yield return BoostChamber(LA_Blood, LA_Animator, LeftAtrium, seg); LA_boosted = true; }

        // pulmonary artery animation returns (blood still purple)
        

        UpdateVitals(56, 78);
    }

    private IEnumerator BoostChamber(ParticleSystem[] psArr, Animator anim, Transform mesh, float dur)
    {
        if (psArr == null || psArr.Length == 0)
        {
            yield return new WaitForSeconds(dur);
            yield break;
        }

        float t = 0f;
        float startRate = AvgEmission(psArr);
        float targetRate = 350f; // fluid-like blood

        // ramp emission to fluid-like
        while (t < dur * 0.6f)
        {
            t += Time.deltaTime;
            float s = Mathf.SmoothStep(0f, 1f, t / (dur * 0.6f));
            SetEmission(psArr, Mathf.Lerp(startRate, targetRate, s));
            yield return null;
        }

        // highlight mesh & reactivate beating
        ApplyMeshColor(mesh, MAROON);
        SetAnimatorSpeed(anim, 1f);

        // blood remains purple but with high flow
        ForcePurple(psArr, targetRate);

        yield return new WaitForSeconds(0.4f);
    }

    // ============================================================
    // PHASE 3 — FIRE SHOCK + NORMAL RECOVERY (NO MASK OBJECT)
    // ============================================================

    private IEnumerator Phase3_ShockAndOxygenRecovery()
    {
        // 1) hide first reward graph
        if (rewardGraphCanvasGO != null)
        {
            Destroy(rewardGraphCanvasGO);
            rewardGraphCanvasGO = null;
        }

        // 2) fire shock flicker for ~2 seconds
        float shockDuration = 2f;
        float t = 0f;

        // cache starting colours of RA/RV/LV/LA
        List<Renderer> chamberRenderers = new List<Renderer>();
        List<Color> startCols = new List<Color>();
        CollectChamberRenderers(chamberRenderers, startCols);

        while (t < shockDuration)
        {
            t += Time.deltaTime;
            float flicker = Mathf.PingPong(Time.time * 8f, 1f); // fast flicker

            for (int i = 0; i < chamberRenderers.Count; i++)
            {
                if (chamberRenderers[i] == null) continue;

                Color baseCol = Color.Lerp(GOLD, MAROON, 0.5f);
                Color fireMix = Color.Lerp(baseCol, FIRE, 0.7f);
                Color final = Color.Lerp(startCols[i], fireMix, flicker);
                chamberRenderers[i].material.color = final;
            }

            yield return null;
        }

        // 3) smooth recovery: heart colours & blood colours back to normal
        float recDur = Mathf.Max(3f, phase3Duration);
        t = 0f;

        while (t < recDur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / recDur);

            // RA/RV/LV/LA → NORMAL_HEART
            LerpMeshColor(RightAtrium,     NORMAL_HEART, p);
            LerpMeshColor(RightVentricle,  NORMAL_HEART, p);
            LerpMeshColor(LeftVentricle,   NORMAL_HEART, p);
            LerpMeshColor(LeftAtrium,      NORMAL_HEART, p);
            // PulmonaryArtery left untouched

            // purple → normal blood red
            LerpBloodColour(RA_Blood, p);
            LerpBloodColour(RV_Blood, p);
            LerpBloodColour(LA_Blood, p);
            LerpBloodColour(LV_Blood, p);

            // keep animators at normal speeds
            SetAnimatorSpeed(RA_Animator, 1f);
            SetAnimatorSpeed(RV_Animator, 1f);
            SetAnimatorSpeed(LA_Animator, 1f);
            SetAnimatorSpeed(LV_Animator, 1f);
           

            // vitals to fully normal
            int bpm = Mathf.RoundToInt(Mathf.Lerp(56, 78, p));
            int spo = Mathf.RoundToInt(Mathf.Lerp(78, 98, p));
            UpdateVitals(bpm, spo);

            yield return null;
        }
    }

    // ============================================================
    // SYRINGE CREATION & MOTION
    // ============================================================

    private IEnumerator CreateAndInjectLargeSyringe()
    {
        if (NeedleStartPoint == null || NeedleInjectPoint == null)
            yield break;

        syringeGO = new GameObject("RL_GiantSyringe");
        syringeGO.transform.position = NeedleStartPoint.position;
        syringeGO.transform.rotation = NeedleStartPoint.rotation;

        // BARREL
        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(barrel.GetComponent<Collider>());
        barrel.transform.SetParent(syringeGO.transform, false);
        barrel.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        barrel.transform.localPosition = Vector3.zero;
        barrel.GetComponent<Renderer>().material.color = new Color(0.95f, 0.95f, 0.95f);

        // PLUNGER
        GameObject plunger = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(plunger.GetComponent<Collider>());
        plunger.transform.SetParent(syringeGO.transform, false);
        plunger.transform.localScale = new Vector3(0.2f, 0.8f, 0.2f);
        plunger.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        plunger.GetComponent<Renderer>().material.color = new Color(0.7f, 0.7f, 0.7f);

        // NEEDLE TIP (shorter)
        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(tip.GetComponent<Collider>());
        tip.transform.SetParent(syringeGO.transform, false);
        tip.transform.localScale = new Vector3(0.05f, 0.06f, 0.05f);
        tip.transform.localPosition = new Vector3(0f, -1.2f, 0f);
        tip.GetComponent<Renderer>().material.color = new Color(0.8f, 0.8f, 0.8f);

        // FLUID
        GameObject fluid = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(fluid.GetComponent<Collider>());
        fluid.transform.SetParent(syringeGO.transform, false);
        fluid.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
        fluid.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        fluid.GetComponent<Renderer>().material.color = new Color(0.1f, 0.6f, 1f);

        // make whole syringe big & tilt 225° (downwards)
        syringeGO.transform.localScale = Vector3.one * 4.2f;
        syringeGO.transform.Rotate(225f, 0f, 0f, Space.Self);

        // move to travel point
        if (NeedleTravelPoint != null)
            yield return MoveTransform(syringeGO.transform, NeedleStartPoint.position, NeedleTravelPoint.position, 1.2f);

        // move to inject point
        yield return MoveTransform(syringeGO.transform, syringeGO.transform.position, NeedleInjectPoint.position, 1.2f);

        // plunger press
        Vector3 pStart = plunger.transform.localPosition;
        Vector3 pEnd = pStart + new Vector3(0f, -0.35f, 0f);
        float t = 0f;

        while (t < 0.8f)
        {
            t += Time.deltaTime;
            float s = Mathf.SmoothStep(0f, 1f, t / 0.8f);
            plunger.transform.localPosition = Vector3.Lerp(pStart, pEnd, s);
            yield return null;
        }

        yield return new WaitForSeconds(0.4f);

        if (syringeGO != null)
        {
            Destroy(syringeGO);
            syringeGO = null;
        }
if (bottomGroup != null)
{
    bottomGroup.alpha = 1f;
}
    }

    private IEnumerator MoveTransform(Transform obj, Vector3 from, Vector3 to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float s = Mathf.SmoothStep(0f, 1f, t / dur);
            obj.position = Vector3.Lerp(from, to, s);
            yield return null;
        }
    }

    // ============================================================
    // UI – VITALS TOP
    // ============================================================

    private void CreateTopVitalsUI()
    {
        if (uiCreated) return;
        uiCreated = true;

        GameObject cGO = new GameObject("VitalsCanvas");
        vitalsCanvasGO = cGO;
        cGO.layer = 5;

        Canvas c = cGO.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = cGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        autoBpm = SpawnText(cGO, TextAnchor.UpperRight, new Vector2(-20, -95));
        autoSpo2 = SpawnText(cGO, TextAnchor.UpperRight, new Vector2(-20, -20));
    }

    private Text SpawnText(GameObject parent, TextAnchor anchor, Vector2 pos)
    {
        GameObject g = new GameObject("AutoText");
        g.transform.SetParent(parent.transform);

        Text t = g.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 23;
        t.color = Color.white;
        t.alignment = anchor;

        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = Anchor(anchor);
        rt.pivot = Anchor(anchor);
        rt.anchoredPosition = pos;

        return t;
    }

    private Vector2 Anchor(TextAnchor a)
    {
        if (a == TextAnchor.UpperLeft) return new Vector2(0, 1);
        if (a == TextAnchor.UpperRight) return new Vector2(1, 1);
        return Vector2.zero;
    }

    // ============================================================
    // UI – BOTTOM TEXT
    // ============================================================

    private void EnsureBottomTextUI()
    {
        if (bottomCanvas != null) return;

        GameObject cGO = new GameObject("RL_BottomCanvas");
        cGO.layer = 5;

        bottomCanvas = cGO.AddComponent<Canvas>();
        bottomCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = cGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        bottomGroup = cGO.AddComponent<CanvasGroup>();
        bottomGroup.alpha = 0f;

        GameObject txtGO = new GameObject("BottomText");
        txtGO.transform.SetParent(cGO.transform, false);
        bottomText = txtGO.AddComponent<Text>();

        RectTransform rt = bottomText.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0, 10);
        rt.sizeDelta = new Vector2(950, 120);

        bottomText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bottomText.fontSize = 16;
        bottomText.color = Color.black;
        bottomText.alignment = TextAnchor.MiddleCenter;
    }

    private IEnumerator ShowBottomText(string msg, float holdSeconds)
    {
        if (bottomText == null || bottomGroup == null) yield break;

        bottomText.text = msg;

        float a = 0f;
        while (a < 1f)
        {
            a += Time.deltaTime * 2f;
            bottomGroup.alpha = Mathf.SmoothStep(0f, 1f, a);
            yield return null;
        }

        yield return new WaitForSeconds(holdSeconds);

        while (a > 0f)
        {
            a -= Time.deltaTime * 1.5f;
            bottomGroup.alpha = Mathf.SmoothStep(0f, 1f, a);
            yield return null;
        }
    }

    // ============================================================
    // FINAL BIG SUCCESS TEXT
    // ============================================================

    private IEnumerator ShowFinalSuccessText()
    {
        GameObject cGO = new GameObject("RL_FinalSuccessCanvas");
        cGO.layer = 5;

        Canvas c = cGO.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = cGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        CanvasGroup cg = cGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        Text txt = new GameObject("FinalText").AddComponent<Text>();
        txt.transform.SetParent(cGO.transform, false);

        RectTransform rt = txt.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(900, 200);

        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 40;
        txt.color = Color.black;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text = "AI became successful to treat the patient";

        // fade in
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f;
            cg.alpha = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }

        yield return new WaitForSeconds(3.5f);

        // fade out
        while (t > 0f)
        {
            t -= Time.deltaTime * 1.5f;
            cg.alpha = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }

        Destroy(cGO);
    }

    // ============================================================
    // PARTICLE HELPERS
    // ============================================================

    private void EnsurePurple()
    {
        if (purpleMat == null)
        {
            purpleMat = new Material(Shader.Find("Unlit/Color"));
            purpleMat.color = PURPLE;
        }
    }

    private void EnsureNormalBlood()
    {
        if (normalBloodMat == null)
        {
            normalBloodMat = new Material(Shader.Find("Unlit/Color"));
            normalBloodMat.color = NORMAL_BLOOD;
        }
    }

    private void ForcePurpleLow(ParticleSystem[] arr)
    {
        if (arr == null) return;
        EnsurePurple();

        foreach (var ps in arr)
        {
            if (ps == null) continue;
            ps.GetComponent<Renderer>().material = purpleMat;
            var m = ps.main; m.startColor = PURPLE;
            var e = ps.emission; e.rateOverTime = 0.2f;
            ps.Play();
        }
    }

    private void ForcePurple(ParticleSystem[] arr, float em)
    {
        if (arr == null) return;
        EnsurePurple();

        foreach (var ps in arr)
        {
            if (ps == null) continue;
            ps.GetComponent<Renderer>().material = purpleMat;
            var m = ps.main; m.startColor = PURPLE;
            var e = ps.emission; e.rateOverTime = em;
            ps.Play();
        }
    }

    private void SetEmission(ParticleSystem[] arr, float val)
    {
        if (arr == null) return;
        foreach (var ps in arr)
        {
            if (ps == null) continue;
            var e = ps.emission; e.rateOverTime = val;
            ps.Play();
        }
    }

    private float AvgEmission(ParticleSystem[] arr)
    {
        if (arr == null || arr.Length == 0) return 0f;
        float sum = 0f;
        foreach (var ps in arr)
            sum += ps.emission.rateOverTime.constant;
        return sum / arr.Length;
    }

    private void LerpBloodColour(ParticleSystem[] arr, float t)
    {
        if (arr == null) return;
        EnsureNormalBlood();

        foreach (var ps in arr)
        {
            if (ps == null) continue;
            var m = ps.main;
            Color c = Color.Lerp(PURPLE, NORMAL_BLOOD, t);
            m.startColor = c;

            var r = ps.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = normalBloodMat;
                r.material.color = c;
            }
        }
    }

    // ============================================================
    // MESH COLOUR HELPERS
    // ============================================================

    private void ApplyMeshColor(Transform p, Color c)
    {
        if (p == null) return;
        foreach (var r in p.GetComponentsInChildren<Renderer>())
            r.material.color = c;
    }

    private void ApplyHeartColor(Color c)
    {
        Transform[] parts = { RightAtrium, RightVentricle, LeftAtrium, LeftVentricle };
        foreach (var p in parts)
        {
            if (p == null) continue;
            foreach (var r in p.GetComponentsInChildren<Renderer>())
                r.material.color = c;
        }
    }

    private void CollectChamberRenderers(List<Renderer> list, List<Color> startCols)
    {
        list.Clear();
        startCols.Clear();

        Transform[] parts = { RightAtrium, RightVentricle, LeftAtrium, LeftVentricle };
        foreach (var t in parts)
        {
            if (t == null) continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                list.Add(r);
                startCols.Add(r.material.color);
            }
        }
    }

    private void LerpMeshColor(Transform p, Color target, float t)
    {
        if (p == null) return;
        foreach (var r in p.GetComponentsInChildren<Renderer>())
        {
            Color from = r.material.color;
            r.material.color = Color.Lerp(from, target, t);
        }
    }

    // ============================================================
    // VITALS & ANIMATORS
    // ============================================================

    private void SetAnimatorSpeed(Animator a, float s)
    {
        if (a != null) a.speed = s;
    }

    private void UpdateVitals(int bpm, int spo2)
    {
        if (bpmText != null) bpmText.text = "BPM: " + bpm;
        if (spo2Text != null) spo2Text.text = "SpO₂: " + spo2 + "%";

        if (autoBpm != null) autoBpm.text = "BPM: " + bpm;
        if (autoSpo2 != null) autoSpo2.text = "SpO₂: " + spo2 + "%";
    }

    // ============================================================
    // REWARD GRAPH 1 (3 attempts: -10, 0, +40)
    // ============================================================

    private IEnumerator AnimateBarHeight(RectTransform bar, float targetHeight)
    {
        float duration = 0.6f;
        float t = 0f;

        float startHeight = bar.sizeDelta.y;

        while (t < duration)
        {
            t += Time.deltaTime;
            float s = Mathf.SmoothStep(0f, 1f, t / duration);

            float newH = Mathf.Lerp(startHeight, targetHeight, s);
            Vector2 size = bar.sizeDelta;
            size.y = newH;
            bar.sizeDelta = size;

            yield return null;
        }
    }

    private void ShowRewardGraphFirst()
    {
        if (rewardGraphCanvasGO != null)
        {
            Destroy(rewardGraphCanvasGO);
            rewardGraphCanvasGO = null;
        }
if (bottomGroup != null)
{
    bottomGroup.alpha = 1f;
}

        // Overlay canvas, top-right
        GameObject c = new GameObject("RLRewardGraphCanvas_P1P2");
        c.layer = 5;
        rewardGraphCanvasGO = c;

        Canvas rewardCanvas = c.AddComponent<Canvas>();
        rewardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = c.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // Panel
        GameObject p = new GameObject("RewardPanel");
        p.transform.SetParent(c.transform);

        RectTransform prt = p.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(1f, 1f);
        prt.anchorMax = new Vector2(1f, 1f);
        prt.pivot = new Vector2(1f, 1f);
        prt.anchoredPosition = new Vector2(-40f, -80f);
        prt.sizeDelta = new Vector2(260f, 150f);

        Image panelImage = p.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.15f);

        // reward values: 1st wrong, 2nd still wrong, 3rd correct +40
        float[] rewardValues = { -10f, 0f, 40f };
        RectTransform[] rewardBars = new RectTransform[rewardValues.Length];

        float barWidth = 28f;
        float spacing = 25f;

        float totalWidth = rewardValues.Length * barWidth + (rewardValues.Length - 1) * spacing;
        float startX = -totalWidth / 2f + barWidth / 2f;

        float minHeight = 10f;
        float maxHeight = 100f;

        // bars + numeric labels
        for (int i = 0; i < rewardValues.Length; i++)
        {
            GameObject bar = new GameObject("Bar" + (i + 1));
            bar.transform.SetParent(p.transform);

            RectTransform brt = bar.AddComponent<RectTransform>();
            brt.sizeDelta = new Vector2(barWidth, 10f);
            brt.anchorMin = new Vector2(0.5f, 0f);
            brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(startX + i * (barWidth + spacing), 18f);

            Image bi = bar.AddComponent<Image>();
            bi.color = new Color(0f, 0.6f, 0.3f, 0.9f);

            rewardBars[i] = brt;

            // label
            GameObject lbl = new GameObject("Label" + (i + 1));
            lbl.transform.SetParent(p.transform);

            Text txt = lbl.AddComponent<Text>();
            RectTransform tr = txt.rectTransform;

            tr.anchorMin = new Vector2(0.5f, 0f);
            tr.anchorMax = new Vector2(0.5f, 0f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(startX + i * (barWidth + spacing), 10f - 3f);
            tr.sizeDelta = new Vector2(80f, 40f);

            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 18;
            txt.alignment = TextAnchor.UpperCenter;
            txt.color = Color.black;
            txt.text = rewardValues[i].ToString("F0");
        }

        // title
        GameObject title = new GameObject("RewardTitle");
        title.transform.SetParent(p.transform);

        Text titleText = title.AddComponent<Text>();
        RectTransform trt = titleText.rectTransform;

        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -5f);
        trt.sizeDelta = new Vector2(240f, 40f);

        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 17;
        titleText.color = Color.black;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.text = "Attempts vs Reward";

        // animate bars
        float minR = Mathf.Min(rewardValues);
        float maxR = Mathf.Max(rewardValues);
        float range = Mathf.Max(1f, maxR - minR);

        for (int i = 0; i < rewardValues.Length; i++)
        {
            float norm = (rewardValues[i] - minR) / range;
            float targetHeight = Mathf.Lerp(minHeight, maxHeight, norm);
            StartCoroutine(AnimateBarHeight(rewardBars[i], targetHeight));
        }
    }

    // ============================================================
    // REWARD GRAPH 2 (4 attempts: -10, 0, +40, +60)
    // ============================================================

    private void ShowRewardGraphFinal()
    {
        if (rewardGraphCanvasGO != null)
        {
            Destroy(rewardGraphCanvasGO);
            rewardGraphCanvasGO = null;
        }

        // Overlay canvas, same position top-right
        GameObject c = new GameObject("RLRewardGraphCanvas_Final");
        c.layer = 5;
        rewardGraphCanvasGO = c;

        Canvas rewardCanvas = c.AddComponent<Canvas>();
        rewardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = c.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // Panel
        GameObject p = new GameObject("RewardPanel_Final");
        p.transform.SetParent(c.transform);

        RectTransform prt = p.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(1f, 1f);
        prt.anchorMax = new Vector2(1f, 1f);
        prt.pivot = new Vector2(1f, 1f);
        prt.anchoredPosition = new Vector2(-40f, -80f);
        prt.sizeDelta = new Vector2(280f, 150f);

        Image panelImage = p.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.15f);

        // reward values INCLUDING 4th attempt +60
        float[] rewardValues = { -10f, 0f, 40f, 60f };
        RectTransform[] rewardBars = new RectTransform[rewardValues.Length];

        float barWidth = 24f;
        float spacing = 18f;

        float totalWidth = rewardValues.Length * barWidth + (rewardValues.Length - 1) * spacing;
        float startX = -totalWidth / 2f + barWidth / 2f;

        float minHeight = 10f;
        float maxHeight = 110f;

        // bars + numeric labels
        for (int i = 0; i < rewardValues.Length; i++)
        {
            GameObject bar = new GameObject("Bar_Final_" + (i + 1));
            bar.transform.SetParent(p.transform);

            RectTransform brt = bar.AddComponent<RectTransform>();
            brt.sizeDelta = new Vector2(barWidth, 10f);
            brt.anchorMin = new Vector2(0.5f, 0f);
            brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(startX + i * (barWidth + spacing), 18f);

            Image bi = bar.AddComponent<Image>();
            bi.color = new Color(0f, 0.6f, 0.3f, 0.9f);

            rewardBars[i] = brt;

            // label
            GameObject lbl = new GameObject("Label_Final_" + (i + 1));
            lbl.transform.SetParent(p.transform);

            Text txt = lbl.AddComponent<Text>();
            RectTransform tr = txt.rectTransform;

            tr.anchorMin = new Vector2(0.5f, 0f);
            tr.anchorMax = new Vector2(0.5f, 0f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(startX + i * (barWidth + spacing), 10f - 3f);
            tr.sizeDelta = new Vector2(80f, 40f);

            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 18;
            txt.alignment = TextAnchor.UpperCenter;
            txt.color = Color.black;
            txt.text = rewardValues[i].ToString("F0");
        }

        // title
        GameObject title = new GameObject("RewardTitle_Final");
        title.transform.SetParent(p.transform);

        Text titleText = title.AddComponent<Text>();
        RectTransform trt = titleText.rectTransform;

        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -5f);
        trt.sizeDelta = new Vector2(260f, 40f);

        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 17;
        titleText.color = Color.black;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.text = "Attempts vs Reward";

        // animate bars
        float minR = Mathf.Min(rewardValues);
        float maxR = Mathf.Max(rewardValues);
        float range = Mathf.Max(1f, maxR - minR);

        for (int i = 0; i < rewardValues.Length; i++)
        {
            float norm = (rewardValues[i] - minR) / range;
            float targetHeight = Mathf.Lerp(minHeight, maxHeight, norm);
            StartCoroutine(AnimateBarHeight(rewardBars[i], targetHeight));
        }
    }

    // ============================================================
    // PHASE-0 CAPTURE + RESET
    // ============================================================

    private void CapturePhase0State()
    {
        // heart renderers + colours
        phase0HeartRenderers.Clear();
        CaptureHeartRenderers(RightAtrium);
        CaptureHeartRenderers(RightVentricle);
        CaptureHeartRenderers(LeftAtrium);
        CaptureHeartRenderers(LeftVentricle);

        // animator speeds
        orig_RA_AnimSpeed = RA_Animator != null ? RA_Animator.speed : 1f;
        orig_RV_AnimSpeed = RV_Animator != null ? RV_Animator.speed : 1f;
        orig_LA_AnimSpeed = LA_Animator != null ? LA_Animator.speed : 1f;
        orig_LV_AnimSpeed = LV_Animator != null ? LV_Animator.speed : 1f;
        

        // blood particle systems
        phase0BloodPS.Clear();
        CaptureBloodArray(RA_Blood);
        CaptureBloodArray(RV_Blood);
        CaptureBloodArray(LA_Blood);
        CaptureBloodArray(LV_Blood);
    }

    private void CaptureHeartRenderers(Transform t)
    {
        if (t == null) return;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            MeshRendererState st = new MeshRendererState
            {
                renderer = r,
                color = r.material.color
            };
            phase0HeartRenderers.Add(st);
        }
    }

    private void CaptureBloodArray(ParticleSystem[] arr)
    {
        if (arr == null) return;

        foreach (var ps in arr)
        {
            if (ps == null) continue;

            var main = ps.main;
            Color startCol = main.startColor.color;

            var r = ps.GetComponent<Renderer>();
            Material mat = r != null ? r.sharedMaterial : null;   // original shared material
            Color matColor = mat != null ? mat.color : Color.white;

            var e = ps.emission;
            float rate = e.rateOverTime.constant;

            BloodPSState st = new BloodPSState
            {
                ps = ps,
                originalMaterial = mat,
                originalMaterialColor = matColor,
                originalStartColor = startCol,
                emissionRate = rate
            };

            phase0BloodPS.Add(st);
        }
    }

    // 🟢 PUBLIC – call this from your button to reset everything back to Phase 0
    public void ResetToPhase0()
    {
        // stop any running sequences
        StopAllCoroutines();

        // restore heart colours
        foreach (var st in phase0HeartRenderers)
        {
            if (st.renderer == null) continue;
            st.renderer.material.color = st.color;
        }

        // restore animator speeds
        SetAnimatorSpeed(RA_Animator, orig_RA_AnimSpeed);
        SetAnimatorSpeed(RV_Animator, orig_RV_AnimSpeed);
        SetAnimatorSpeed(LA_Animator, orig_LA_AnimSpeed);
        SetAnimatorSpeed(LV_Animator, orig_LV_AnimSpeed);
        

        // restore blood particle systems (material, colour, emission)
        foreach (var st in phase0BloodPS)
        {
            if (st.ps == null) continue;

            var main = st.ps.main;
            main.startColor = st.originalStartColor;

            var r = st.ps.GetComponent<Renderer>();
            if (r != null && st.originalMaterial != null)
            {
                r.sharedMaterial = st.originalMaterial;
                r.sharedMaterial.color = st.originalMaterialColor;
            }

            var e = st.ps.emission;
            e.rateOverTime = st.emissionRate;

            st.ps.Play();
        }

        // remove syringe if exists
        if (syringeGO != null)
        {
            Destroy(syringeGO);
            syringeGO = null;
        }

        // remove bottom text canvas
        if (bottomCanvas != null)
        {
            Destroy(bottomCanvas.gameObject);
            bottomCanvas = null;
            bottomGroup = null;
            bottomText = null;
        }

        // remove reward graphs
        if (rewardGraphCanvasGO != null)
        {
            Destroy(rewardGraphCanvasGO);
            rewardGraphCanvasGO = null;
        }

        // remove final success canvas (if reset happens while it's visible)
        GameObject finalCanvas = GameObject.Find("RL_FinalSuccessCanvas");
        if (finalCanvas != null)
        {
            Destroy(finalCanvas);
        }

        // remove vitals UI canvas
        if (vitalsCanvasGO != null)
        {
            Destroy(vitalsCanvasGO);
            vitalsCanvasGO = null;
            uiCreated = false;
            autoBpm = null;
            autoSpo2 = null;
        }

        // clear on-screen vitals text (external labels)
        if (bpmText != null) bpmText.text = "";
        if (spo2Text != null) spo2Text.text = "";

        // flags
        RA_boosted = RV_boosted = LA_boosted = LV_boosted = false;
    }


private IEnumerator ShowBottomText_NoFadeOut(string msg, float holdSeconds)
{
    if (bottomText == null || bottomGroup == null) yield break;

    bottomText.text = msg;

    // Fade IN only
    float a = 0f;
    while (a < 1f)
    {
        a += Time.deltaTime * 2f;
        bottomGroup.alpha = Mathf.SmoothStep(0f, 1f, a);
        yield return null;
    }

    // Stay visible for the full duration
    yield return new WaitForSeconds(holdSeconds);

    //  No fade-out here – it stays visible until the next text replaces it
}
}