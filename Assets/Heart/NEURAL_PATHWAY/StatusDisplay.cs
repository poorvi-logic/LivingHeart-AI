using UnityEngine;
using TMPro;
using System.Collections;

public class StatusDisplay : MonoBehaviour
{
    public TextMeshProUGUI statusText;

    public Color alertRed = new Color(0.8f, 0f, 0f);

    public void ShowMessage(string message, Color color, float duration = 3f)
    {
        statusText.alpha = 1f;
        StopAllCoroutines();
        StartCoroutine(DisplayRoutine(message, color, duration));
    }

    IEnumerator DisplayRoutine(string msg, Color color, float duration)
    {
        statusText.text = msg;
        statusText.color = color;
        statusText.alpha = 1f;

        yield return new WaitForSeconds(duration);

        float fadeTime = 1f;
        float t = 0f;

        while (t < fadeTime)
        {
            statusText.alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
            t += Time.deltaTime;
            yield return null;
        }

        statusText.alpha = 1f;
        statusText.text = "";
    }
}