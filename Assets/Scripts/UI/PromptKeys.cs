using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Keyboard helper for runtime dialogs (new Input System): Escape closes the dialog it sits on.</summary>
public class PromptKeys : MonoBehaviour
{
    public Action onEscape;

    public static bool EnterPressed
    {
        get
        {
            var k = Keyboard.current;
            return k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
        }
    }

    public static bool EscapePressed
    {
        get
        {
            var k = Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
        }
    }

    void Update()
    {
        if (EscapePressed) onEscape?.Invoke();
    }
}
