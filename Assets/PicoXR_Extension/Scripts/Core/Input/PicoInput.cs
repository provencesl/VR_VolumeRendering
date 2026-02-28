using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR;

/// <summary>
/// Shortcut/Helpers to the Pico Inputs
/// </summary>
public class PicoInput : MonoBehaviour
{
    // => https://hackmd.io/@jcxyisncu1102/steamvr-to-pico/%2FWW0m58UyQM6buo2FrQ42mg
    // Menu	CommonUsages.menuButton
    // Trigger	CommonUsages.TriggerButton
    // Grip	CommonUsages.GripButton
    // Joystick	CommonUsages.primary2DAxisClick
    // X/A	CommonUsages.primaryButton
    // Y/B	CommonUsages.secondaryButton
    
    public enum PicoButton 
    { 
        MenuL, TriggerL, GripL, JoystickL, X, Y, // 0-5
        MenuR, TriggerR, GripR, JoystickR, A, B, // 6-11
    };

    public class ButtonStatus 
    {
        public bool isPressed = false;
        public bool isDown = false;
        public bool isUp = false;
    }

    /// <summary>
    /// (isPressed, isDown, isUp)
    /// </summary>
    private static Dictionary<PicoButton, ButtonStatus> buttonStatus = new Dictionary<PicoButton, ButtonStatus>();

    /* -------------------------------------------------------------------------- */

    private static PicoInput _instance;

    private static void EnsureInstance()
    {
        if(_instance == null)
        {
            PicoInput comp = new GameObject().AddComponent<PicoInput>();
            DontDestroyOnLoad(comp);
            _instance = comp;
            _instance.name = " (Instance) " + typeof(PicoInput);
        }
    }

    /* -------------------------------------------------------------------------- */

    /// <summary>
    /// Awake is called when the script instance is being loaded.
    /// </summary>
    void Awake()
    {        
        for(int i = 0; i < 12; i++)
        { 
            buttonStatus[(PicoButton)i] = new ButtonStatus();
        }
    }

    /// <summary>
    /// Update is called every frame, if the MonoBehaviour is enabled.
    /// </summary>
    void Update()
    {
        for(int i = 0; i < 12; i++)
        {            
            PicoButton key = (PicoButton)i;
            bool current = _GetButton(key);
            if(buttonStatus.TryGetValue(key, out ButtonStatus b))
            {
                b.isUp = b.isPressed && !current;
                b.isDown = !b.isPressed && current;
                b.isPressed = current;
            }
        }
    }

    private bool _GetButton(PicoButton button)
    {
        XRNode node = button < PicoButton.MenuR ? XRNode.LeftHand : XRNode.RightHand;
        bool result;
        switch(button)
        {
            case PicoButton.MenuL:
            case PicoButton.MenuR:
                return InputDevices.GetDeviceAtXRNode(node).TryGetFeatureValue(CommonUsages.menuButton, out result) && result;
            case PicoButton.TriggerL:
            case PicoButton.TriggerR:
                return InputDevices.GetDeviceAtXRNode(node).TryGetFeatureValue(CommonUsages.triggerButton, out result) && result;
            case PicoButton.GripL:
            case PicoButton.GripR:
                return InputDevices.GetDeviceAtXRNode(node).TryGetFeatureValue(CommonUsages.gripButton, out result) && result;
            case PicoButton.JoystickL:
            case PicoButton.JoystickR:
                return InputDevices.GetDeviceAtXRNode(node).TryGetFeatureValue(CommonUsages.primary2DAxisClick, out result) && result;
            case PicoButton.X:
            case PicoButton.A:
                return InputDevices.GetDeviceAtXRNode(node).TryGetFeatureValue(CommonUsages.primaryButton, out result) && result;
            case PicoButton.Y:
            case PicoButton.B:
                return InputDevices.GetDeviceAtXRNode(node).TryGetFeatureValue(CommonUsages.secondaryButton, out result) && result;
        }
        throw new System.NotImplementedException("No such key: (PicoButton)"+button);
    }

    /* -------------------------------------------------------------------------- */

    public static bool GetButton(PicoButton button)
    {
        EnsureInstance();
        return buttonStatus[button].isPressed;
    }
    public static bool GetButtonDown(PicoButton button)
    {
        EnsureInstance();
        return buttonStatus[button].isDown;
    }
    public static bool GetButtonUp(PicoButton button)
    {
        EnsureInstance();
        return buttonStatus[button].isUp;
    }

    /// <summary>
    /// GetJoystickValue
    /// </summary>
    /// <param name="deviceNo">0: Left Hand; 1: Right Hand</param>
    /// <returns>Vector2 capped within (-1,-1) to (1,1)</returns>
    public static Vector2 GetJoystickValue(int deviceNo)
    {
        XRNode node = deviceNo == 0 ? XRNode.LeftHand : XRNode.RightHand;
        if(InputDevices.GetDeviceAtXRNode(node).TryGetFeatureValue(CommonUsages.primary2DAxis, out var result))
            return result;
        return Vector2.zero;
    }
}