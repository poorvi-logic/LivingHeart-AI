using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT
using TMPro;
#endif

/// <summary>
/// Simple UI helper to show short messages.  
/// Supports TextMeshProUGUI if assigned, otherwise falls back to legacy Text.
/// Assign either 'tmpText' (recommended) or 'legacyText'.
/// </summary>
public class UI_InfoText : MonoBehaviour
{
    #if TMP_PRESENT
    public TextMeshProUGUI tmpText;
    #endif
    public Text legacyText;            // UnityEngine.UI.Text fallback
    public CanvasGroup canvasGroup;    // optional fade in/out

    public float defaultShowTime = 3f;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            // create a canvas group if none assigned (optional)
            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void Show(string message, float duration = -1f)
    {
        if (duration <= 0) duration = defaultShowTime;
        StopAllCoroutines();
        if (
            #if TMP_PRESENT
            tmpText != null
            #else
            false
            #endif
            )
        {
            #if TMP_PRESENT
            tmpText.text = message;
            #endif
        }
        else if (legacyText != null)
        {
            legacyText.text = message;
        }
        // simple fade in/out coroutine
        StartCoroutine(ShowRoutine(duration));
    }

    private System.Collections.IEnumerator ShowRoutine(float duration)
    {
        // fade in
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 6f;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(duration);

        // fade out
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 6f;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        // clear text
        if (
            #if TMP_PRESENT
            tmpText != null
            #else
            false
            #endif
            )
        {
            #if TMP_PRESENT
            tmpText.text = "";
            #endif
        }
        else if (legacyText != null)
        {
            legacyText.text = "";
        }
    }
}