using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BiofeedbackModeManager : MonoBehaviour
{
    public GameObject activateButton;
    public GameObject exitButton;
    public GameObject overlay;
    public GameObject[] modulesToHide;

    public void ActivateMode()
    {
        // Hide all modules
        foreach (var module in modulesToHide)
            module.SetActive(false);

        // Show overlay
        overlay.SetActive(true);

        // Switch buttons
        activateButton.SetActive(false);
        exitButton.SetActive(true);

        Debug.Log("Biofeedback Mode Activated");
    }

    public void ExitMode()
    {
        // Show all modules
        foreach (var module in modulesToHide)
            module.SetActive(true);

        // Hide overlay
        overlay.SetActive(false);

        // Switch back
        activateButton.SetActive(true);
        exitButton.SetActive(false);

        Debug.Log("Biofeedback Mode Exited");
    }
}

