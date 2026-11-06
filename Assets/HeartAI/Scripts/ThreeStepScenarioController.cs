using UnityEngine;

public class ThreeStepScenarioController : MonoBehaviour
{
    public ArrhythmiaCinematicController arrhythmia;
    public PacemakerSurgeryCinematic pacemaker;

    int clickState = 0;   // 0 = nothing, 1 = arrhythmia, 2 = pacemaker

    public void OnMainButtonPressed()
    {
        clickState++;

        // Loop back after 3
        if (clickState > 3)
            clickState = 1;

        if (clickState == 1)
        {
            // STEP 1 → Arrhythmia
            StopAll();
            arrhythmia.StartScenario();
            Debug.Log("STEP 1: ARRHYTHMIA STARTED");
        }
        else if (clickState == 2)
        {
            // STEP 2 → Pacemaker
            StopAll();
            pacemaker.StartScenario();
            Debug.Log("STEP 2: PACEMAKER STARTED");
        }
        else if (clickState == 3)
        {
            // STEP 3 → EXIT / RESET
            StopAll();
            Debug.Log("STEP 3: EVERYTHING RESET");
        }
    }

    void StopAll()
    {
        if (arrhythmia != null) arrhythmia.StopScenario();
        if (pacemaker != null) pacemaker.StopScenario();
    }
}
