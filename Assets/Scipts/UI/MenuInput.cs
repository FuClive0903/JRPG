using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public static class MenuInput
{
    private static MenuInputState state = new MenuInputState();
    public static bool FrameBlocked => state.IsBlocked(Time.frameCount);
    public static bool BackPressed => Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame;
    public static bool EscapePressed => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    public static bool ConfirmPressed => Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => state = new MenuInputState();

    public static bool CanRead(Object owner) => state.CanRead(owner, Time.frameCount);
    public static void BlockFrame() => state.Block(Time.frameCount);
    public static void Release(Object owner) => state.Release(owner, Time.frameCount);

    public static void Activate(Object owner, Button focus = null)
    {
        state.Activate(owner, Time.frameCount);
        Focus(focus);
    }

    public static void Focus(Button button)
    {
        if (EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        if (button != null && button.gameObject.activeInHierarchy && button.IsInteractable())
            button.Select();
    }

    public static int HorizontalStep => Keyboard.current == null ? 0 :
        Keyboard.current.leftArrowKey.wasPressedThisFrame ? -1 :
        Keyboard.current.rightArrowKey.wasPressedThisFrame ? 1 : 0;

    public static int VerticalStep => Keyboard.current == null ? 0 :
        Keyboard.current.upArrowKey.wasPressedThisFrame ? -1 :
        Keyboard.current.downArrowKey.wasPressedThisFrame ? 1 : 0;
}
