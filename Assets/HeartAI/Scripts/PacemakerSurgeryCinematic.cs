using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PacemakerSurgeryCinematic : MonoBehaviour,IScenario
{
    Canvas textCanvas;
    Text stepText;
    CanvasGroup textGroup;

    [Header("Arrhythmia Sync")]
    public Animator leftVentricleAnimator;
    public Animator rightAtriumAnimator;
    public Animator leftAtriumAnimator;

    public ParticleSystem[] allFlows;
    public Renderer rightAtriumRenderer;

    Color defaultRAColor;
    float defaultLVSpeed, defaultRASpeed, defaultLASpeed;

    // control flags
    public bool forceStopped = false;
    bool manuallyStarted = false;

    bool arrhythmiaActive = false;
    Color raDark = new Color(0f, 0.45f, 0.45f);

    [Header("Skin Prep Targets")]
    public Transform SkinSurfacePoint;

    [Header("Needle Path")]
    public Transform SyringeStartPoint;
    public Transform SyringeInjectPoint;

    [Header("Incision Path")]
    public Transform IncisionStartPoint;
    public Transform IncisionEndPoint;

    [Header("Skin Split End Targets")]
    public Transform UpperSkinEndPoint;
    public Transform LowerSkinEndPoint;

    [Header("Heart Path Targets")]
    public Transform PulmonaryVeinEntry;
    public Transform RAEntryTop;
    public Transform RAEntryBottom;

    [Header("Pocket Animation Targets")]
    public Transform PocketSpawnPoint;
    public Transform DeviceAttachPoint;
    public Transform LeadExitPoint;

    [Header("Sizes")]
    public Vector3 PacemakerSize = new Vector3(20f, 10f, 24f);
    public Vector3 PocketSize = new Vector3(26f, 14f, 26f);

    Transform pacemakerDevice, pocket;
    Transform skinUpper, skinLower;
    Transform needle;
    Transform scalpel;

    LineRenderer externalLead, internalLead;

    Coroutine routine;

    void Start()
    {
        AutoCreateObjects();
        AutoCreateTextUI();

        defaultLVSpeed = leftVentricleAnimator != null ? leftVentricleAnimator.speed : 1f;
        defaultRASpeed = rightAtriumAnimator != null ? rightAtriumAnimator.speed : 1f;
        defaultLASpeed = leftAtriumAnimator != null ? leftAtriumAnimator.speed : 1f;

        if (rightAtriumRenderer != null)
            defaultRAColor = rightAtriumRenderer.material.color;
    }

    // Start/Stop wrappers
    public void StartScenario()
    {
        forceStopped = false;
        manuallyStarted = true;
        StartPacemakerSurgery();
    }

    public void StopScenario()
    {
        forceStopped = true;
        manuallyStarted = false;
        ExitToNormalState();
    }

    public void StartPacemakerSurgery()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FullSequence());
    }

    void AutoCreateTextUI()
    {
        GameObject c = new GameObject("SurgeryTextCanvas");
        c.layer = 5;

        textCanvas = c.AddComponent<Canvas>();
        textCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler cs = c.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        textGroup = c.AddComponent<CanvasGroup>();
        textGroup.alpha = 0;

        GameObject t = new GameObject("StepText");
        t.transform.SetParent(c.transform);

        stepText = t.AddComponent<Text>();
        RectTransform rt = stepText.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);

        rt.anchoredPosition = new Vector2(0, 5);
        rt.sizeDelta = new Vector2(950, 140);

        stepText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        stepText.fontSize = 30;
        stepText.color = Color.black;
        stepText.alignment = TextAnchor.MiddleCenter;
    }

    IEnumerator ShowStepText(string msg, float hold = 2.2f)
    {
        if (forceStopped) yield break;

        stepText.text = msg;

        float t = 0;
        while (t < 1f)
        {
            if (forceStopped) yield break;
            t += Time.deltaTime * 1.8f;
            textGroup.alpha = Mathf.SmoothStep(0, 1, t);
            yield return null;
        }

        float timer = 0;
        while (timer < hold)
        {
            if (forceStopped) yield break;
            timer += Time.deltaTime;
            yield return null;
        }

        while (t > 0f)
        {
            if (forceStopped) yield break;
            t -= Time.deltaTime * 1.8f;
            textGroup.alpha = Mathf.SmoothStep(0, 1, t);
            yield return null;
        }
    }

    void AutoCreateObjects()
    {
        // Skin pieces
        skinUpper = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        skinUpper.localScale = new Vector3(0.25f, 4f, 3.5f);
        skinUpper.GetComponent<MeshRenderer>().material.color = new Color(1, .8f, .7f);
        skinUpper.gameObject.SetActive(false);

        skinLower = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        skinLower.localScale = new Vector3(0.25f, 4f, 3.5f);
        skinLower.GetComponent<MeshRenderer>().material.color = new Color(1, .8f, .7f);
        skinLower.gameObject.SetActive(false);

        // Needle (local anesthesia)
        needle = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
        needle.localScale = new Vector3(.06f, 1.5f, .06f);
        needle.rotation = Quaternion.Euler(90, 0, 0);
        needle.GetComponent<MeshRenderer>().material.color = Color.gray;
        needle.gameObject.SetActive(false);

        // Scalpel
        scalpel = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        scalpel.localScale = new Vector3(.22f, .22f, 2.6f);
        scalpel.GetComponent<MeshRenderer>().material.color = Color.gray;
        scalpel.gameObject.SetActive(false);

        // Pacemaker & pocket
        pacemakerDevice = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
        pacemakerDevice.localScale = PacemakerSize;
        pacemakerDevice.rotation = Quaternion.Euler(0, 90, 0);
        pacemakerDevice.gameObject.SetActive(false);

        pocket = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
        pocket.localScale = PocketSize;
        pocket.rotation = Quaternion.Euler(0, 90, 0);
        pocket.gameObject.SetActive(false);

        // Leads
        externalLead = new GameObject("ExternalLead").AddComponent<LineRenderer>();
        externalLead.startWidth = .12f;
        externalLead.endWidth = .12f;
        externalLead.material = new Material(Shader.Find("Sprites/Default"));
        externalLead.startColor = Color.yellow;
        externalLead.endColor = Color.yellow;
        externalLead.positionCount = 25;
        externalLead.enabled = false;

        internalLead = new GameObject("InternalLead").AddComponent<LineRenderer>();
        internalLead.startWidth = .12f;
        internalLead.endWidth = .12f;
        internalLead.material = new Material(Shader.Find("Sprites/Default"));
        internalLead.startColor = Color.yellow;
        internalLead.endColor = Color.yellow;
        internalLead.positionCount = 18;
        internalLead.enabled = false;
    }

    void StartArrhythmiaFX()
    {
        arrhythmiaActive = true;
        StartCoroutine(ArrhythmiaFXLoop());
    }

    IEnumerator ArrhythmiaFXLoop()
    {
        while (arrhythmiaActive && !forceStopped)
        {
            float baseLV = defaultLVSpeed;
            float jitter = 0.65f * Mathf.Sin(Time.time * 12f);

            leftVentricleAnimator.speed = Mathf.Clamp(baseLV + jitter, 0.4f, 1.9f);

            if (rightAtriumRenderer != null)
                rightAtriumRenderer.material.color = raDark * (1f + Mathf.Sin(Time.time * 22f) * 0.48f);

            foreach (var bf in allFlows)
            {
                if (bf == null) continue;
                var em = bf.emission;
                float thick = Mathf.Lerp(140, 200, Mathf.Sin(Time.time * 7f + bf.GetInstanceID()) * 0.5f + 0.5f);
                float thin = Mathf.Lerp(10, 20, Mathf.Sin(Time.time * 9f + bf.GetInstanceID()) * 0.5f + 0.5f);
                em.rateOverTime = Mathf.Sin(Time.time * 6f + bf.GetInstanceID()) > 0.2f ? thick : thin;
            }
            yield return null;
        }
    }

    void RestoreNormalHeart()
    {
        arrhythmiaActive = false;

        if (rightAtriumRenderer != null)
            rightAtriumRenderer.material.color = defaultRAColor;

        leftVentricleAnimator.speed = defaultLVSpeed;
        rightAtriumAnimator.speed = defaultRASpeed;
        leftAtriumAnimator.speed = defaultLASpeed;

        foreach (var bf in allFlows)
        {
            if (bf == null) continue;
            var em = bf.emission;
            em.rateOverTime = 450;
        }
    }

    IEnumerator FullSequence()
    {
        if (forceStopped) yield break;

        StartArrhythmiaFX();

        yield return SkinSequence();
        if (forceStopped) yield break;

        yield return PacemakerSequence();
        if (forceStopped) yield break;

        yield return ShowStepText(
            "Pacemaker senses irregular rhythm\nand sends timed electrical pulses to restore stable heartbeat.",
            2.6f
        );
        if (forceStopped) yield break;

        RestoreNormalHeart();

        yield return ShowStepText("Pacemaker Implantation Complete ✔", 2.6f);
    }

    IEnumerator SkinSequence()
    {
        if (forceStopped) yield break;

        yield return ShowStepText("Administering Local Anesthesia…", 2f);
        if (forceStopped) yield break;

        Vector3 pos = SkinSurfacePoint.position;

        skinUpper.position = pos + new Vector3(0, .1f, 0);
        skinLower.position = pos - new Vector3(0, .1f, 0);
        skinUpper.rotation = Quaternion.Euler(0, 90, 0);
        skinLower.rotation = Quaternion.Euler(0, 90, 0);

        skinUpper.gameObject.SetActive(true);
        skinLower.gameObject.SetActive(true);

        Material mu = skinUpper.GetComponent<MeshRenderer>().material;
        Material ml = skinLower.GetComponent<MeshRenderer>().material;

        float a = 0;
        while (a < 1)
        {
            if (forceStopped) yield break;
            a += Time.deltaTime * .8f;
            mu.color = new Color(1, .8f, .7f, a);
            ml.color = new Color(1, .8f, .7f, a);
            yield return null;
        }

        if (forceStopped) yield break;

        needle.position = SyringeStartPoint.position;
        needle.gameObject.SetActive(true);

        Vector3 needleStop = SyringeInjectPoint.position - needle.right * .3f;
        yield return MoveSmooth(needle, needleStop, 1.6f);
        if (forceStopped) yield break;

        yield return MoveSmooth(needle, SyringeStartPoint.position, 1.3f);
        if (forceStopped) yield break;

        needle.gameObject.SetActive(false);

        yield return ShowStepText("Making Incision…", 2f);
        if (forceStopped) yield break;

        Vector3 start = IncisionStartPoint.position;
        Vector3 end = IncisionEndPoint.position;

        scalpel.position = start;
        scalpel.gameObject.SetActive(true);

        float t2 = 0;
        while (t2 < 1)
        {
            if (forceStopped) yield break;
            t2 += Time.deltaTime * 1.1f;
            scalpel.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, t2));
            yield return null;
        }

        yield return new WaitForSeconds(.4f);
        if (forceStopped) yield break;

        yield return ShowStepText("Opening Skin Flaps…", 2.2f);
        if (forceStopped) yield break;

        float s = 0;
        Vector3 uStart = skinUpper.position;
        Vector3 lStart = skinLower.position;

        while (s < 1)
        {
            if (forceStopped) yield break;
            s += Time.deltaTime * 1.1f;
            float smooth = Mathf.SmoothStep(0, 1, s);
            skinUpper.position = Vector3.Lerp(uStart, UpperSkinEndPoint.position, smooth);
            skinLower.position = Vector3.Lerp(lStart, LowerSkinEndPoint.position, smooth);
            yield return null;
        }

        float f = 1;
        while (f > 0)
        {
            if (forceStopped) yield break;
            f -= Time.deltaTime * 1.1f;
            mu.color = new Color(1, .8f, .7f, f);
            ml.color = new Color(1, .8f, .7f, f);
            yield return null;
        }

        skinUpper.gameObject.SetActive(false);
        skinLower.gameObject.SetActive(false);
        scalpel.gameObject.SetActive(false);
    }

    IEnumerator PacemakerSequence()
    {
        if (forceStopped) yield break;

        yield return ShowStepText("Positioning Pacemaker Device…", 2f);
        if (forceStopped) yield break;

        pacemakerDevice.position = DeviceAttachPoint.position;
        pacemakerDevice.gameObject.SetActive(true);

        pocket.position = PocketSpawnPoint.position;
        pocket.gameObject.SetActive(true);

        yield return MoveSmooth(pocket, DeviceAttachPoint.position, 2.2f);
        if (forceStopped) yield break;

        yield return ShowStepText("Guiding Pacemaker Lead…", 2f);
        if (forceStopped) yield break;

        externalLead.enabled = true;

        yield return AnimateLeadBezier(
            externalLead,
            LeadExitPoint.position,
            PulmonaryVeinEntry.position,
            1.6f
        );
        if (forceStopped) yield break;

        yield return new WaitForSeconds(0.6f);
        if (forceStopped) yield break;

        yield return ShowStepText("Locating Right Atrium using X-Rays…", 2f);
        if (forceStopped) yield break;

        internalLead.enabled = true;

        yield return AnimateLeadStraight(
            internalLead,
            RAEntryTop.position,
            RAEntryBottom.position,
            1.4f
        );
    }

    IEnumerator AnimateLeadBezier(LineRenderer lr, Vector3 start, Vector3 end, float duration)
    {
        float t = 0;
        while (t < 1)
        {
            if (forceStopped) yield break;
            t += Time.deltaTime / duration;
            float prog = Mathf.SmoothStep(0, 1, t);
            Vector3 mid = (start + end) / 2 + Vector3.up * 0.4f;
            for (int i = 0; i < lr.positionCount; i++)
            {
                float p = (i / (float)(lr.positionCount - 1)) * prog;
                Vector3 p1 = Vector3.Lerp(start, mid, p);
                Vector3 p2 = Vector3.Lerp(mid, end, p);
                lr.SetPosition(i, Vector3.Lerp(p1, p2, p));
            }
            yield return null;
        }
    }

    IEnumerator AnimateLeadStraight(LineRenderer lr, Vector3 start, Vector3 end, float duration)
    {
        float t = 0;
        while (t < 1)
        {
            if (forceStopped) yield break;
            t += Time.deltaTime / duration;
            float prog = Mathf.SmoothStep(0, 1, t);
            for (int i = 0; i < lr.positionCount; i++)
            {
                float p = i / (float)(lr.positionCount - 1);
                lr.SetPosition(i, Vector3.Lerp(start, end, p * prog));
            }
            yield return null;
        }
    }

    IEnumerator MoveSmooth(Transform obj, Vector3 target, float duration)
    {
        Vector3 start = obj.position;
        float t = 0;
        while (t < 1)
        {
            if (forceStopped) yield break;
            t += Time.deltaTime / duration;
            obj.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
    }

    public void ExitToNormalState()
    {
        if (routine != null)
            StopCoroutine(routine);

        StopAllCoroutines();

        forceStopped = true;
        arrhythmiaActive = false;

        if (rightAtriumRenderer != null)
            rightAtriumRenderer.material.color = defaultRAColor;

        leftVentricleAnimator.speed = defaultLVSpeed;
        rightAtriumAnimator.speed = defaultRASpeed;
        leftAtriumAnimator.speed = defaultLASpeed;

        foreach (var bf in allFlows)
        {
            if (bf == null) continue;
            var em = bf.emission;
            em.rateOverTime = 180;
        }

        if (textGroup != null)
            textGroup.alpha = 0;

        if (externalLead != null) externalLead.enabled = false;
        if (internalLead != null) internalLead.enabled = false;

        if (pacemakerDevice != null) pacemakerDevice.gameObject.SetActive(false);
        if (pocket != null) pocket.gameObject.SetActive(false);
        if (needle != null) needle.gameObject.SetActive(false);
        if (scalpel != null) scalpel.gameObject.SetActive(false);

        if (skinUpper != null) skinUpper.gameObject.SetActive(false);
        if (skinLower != null) skinLower.gameObject.SetActive(false);

        Debug.Log("Pacemaker reset to normal state.");
    }
}