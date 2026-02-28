using System.Collections;
using System.Collections.Generic;
using TalesFromTheRift;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hide native keyboard, and patch all InputFields in the scene with the opener of our custom keyboard.
/// Call Patch() again if new InputFields has created.
/// </summary>
public class InputFieldPatcher : MonoBehaviour
{
    // On-screen keyboard
    TouchScreenKeyboard _keyboard;

    void Start()
    {
        Patch();
        CanvasKeyboard.Close();
        _keyboard = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default, false, false, false);
    }

    void LateUpdate()
    {
        // always hide native keyboard before render
        _keyboard.active = false;
    }

    /// <summary>
    /// Patch all input fields.
    /// </summary>
    public static void Patch()
    {
        InputField[] ifs = Resources.FindObjectsOfTypeAll<InputField>();
        int i = 0;
        for(; i < ifs.Length; i++)
        {
            EventTrigger trigger = ifs[i].gameObject.AddComponent<EventTrigger>();
            InputField inputField = ifs[i];
            var eventOpen = new EventTrigger.Entry{eventID = EventTriggerType.Select};
            eventOpen.callback.AddListener((_)=>CanvasKeyboard.Open(inputField));
            trigger.triggers.Add(eventOpen);
        }
        if(i > 0)
        {
            Debug.Log($"[InputFieldPatcher] Patched {i} InputFields");
        }
    }
}

