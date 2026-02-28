using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using static PicoInput;

public class Demo_PicoControllerPanel : MonoBehaviour
{
    [SerializeField] Text _eventsText;
    [SerializeField] Text _statusText;

    /// <summary>
    /// Update is called every frame, if the MonoBehaviour is enabled.
    /// </summary>
    void Update()
    {
        // Events
        if(GetButtonDown(PicoButton.TriggerL) || GetButtonDown(PicoButton.TriggerR))
        {
            _eventsText.text = $"{System.DateTime.Now} 你剛<color=lime>按下</color>了扳機鍵 GetButtonDown(PicoButton.Trigger)\n{_eventsText.text}";
        }
        if(GetButtonUp(PicoButton.TriggerL) || GetButtonUp(PicoButton.TriggerR))
        {
            _eventsText.text = $"{System.DateTime.Now} 你剛<color=yellow>放開</color>了扳機鍵 GetButtonUp(PicoButton.TriggerL)\n{_eventsText.text}";
        }

        // status
        _statusText.text = "";
        for(int i = 0; i < 12; i++) // all pico buttons
        { 
            PicoButton b = (PicoButton)i;
            _statusText.text += b + ": " + (PicoInput.GetButton(b) ? "<color=lime>O</color>" : "<color=red>X</color>") + "\n";
        }
        _statusText.text += $"JoystickL: ${PicoInput.GetJoystickValue(0)}\n";
        _statusText.text += $"JoystickR: ${PicoInput.GetJoystickValue(1)}\n";
    }
}