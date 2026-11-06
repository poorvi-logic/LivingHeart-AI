using UnityEngine;

public class HeartRL_ToggleButton : MonoBehaviour
{
    public RL_FullCombined_Final rl;   // drag the object with RL_FullCombined_Final here

    private bool running = false;

    public void OnButtonPressed()
    {
        if (!running)
        {
            running = true;
            rl.enabled = true;
            rl.StartFullRLDemo();
        }
        else
        {
            running = false;
            rl.ResetToPhase0();
            rl.enabled = false;
        }
    }
}