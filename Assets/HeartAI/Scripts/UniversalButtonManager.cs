using UnityEngine;
using UnityEngine.UI;

public class UniversalButtonManager : MonoBehaviour
{
    [System.Serializable]
    public class ScenarioButton
    {
        public Button uiButton;
        public MonoBehaviour[] scenarioScripts; // multiple scripts per button allowed
    }

    public ScenarioButton[] buttons;

    private MonoBehaviour[] currentRunningScripts = null;
    private bool isSwitchingScenario = false;

    void Start()
    {
        foreach (var sb in buttons)
        {
            ScenarioButton localSB = sb;
            if (localSB.uiButton != null)
                localSB.uiButton.onClick.AddListener(() => OnButtonPressed(localSB));
        }
    }

    void OnButtonPressed(ScenarioButton sb)
    {
        if (isSwitchingScenario) return;
        StartCoroutine(HandleButtonPress(sb));
    }

    System.Collections.IEnumerator HandleButtonPress(ScenarioButton sb)
    {
        isSwitchingScenario = true;

        MonoBehaviour[] newScripts = sb.scenarioScripts;

        // Toggle OFF if same group
        if (currentRunningScripts == newScripts)
        {
            CallStopAll(newScripts);
            currentRunningScripts = null;

            // small safety frame
            yield return new WaitForEndOfFrame();
            isSwitchingScenario = false;
            yield break;
        }

        // Stop previous group
        if (currentRunningScripts != null)
        {
            CallStopAll(currentRunningScripts);
            // let one frame pass so scripts can settle
            yield return new WaitForEndOfFrame();
        }

        // Start new group (start each script)
        CallStartAll(newScripts);
        currentRunningScripts = newScripts;

        yield return new WaitForEndOfFrame();
        isSwitchingScenario = false;
    }

    void CallStartAll(MonoBehaviour[] scripts)
    {
        if (scripts == null) return;
        foreach (var script in scripts)
        {
            if (script == null) continue;
            var m = script.GetType().GetMethod("StartScenario");
            if (m != null) m.Invoke(script, null);
            else Debug.LogWarning(script.name + " missing StartScenario()");
        }
    }

    void CallStopAll(MonoBehaviour[] scripts)
    {
        if (scripts == null) return;
        foreach (var script in scripts)
        {
            if (script == null) continue;
            var m = script.GetType().GetMethod("StopScenario");
            if (m != null) m.Invoke(script, null);
            else Debug.LogWarning(script.name + " missing StopScenario()");
        }
    }
}
