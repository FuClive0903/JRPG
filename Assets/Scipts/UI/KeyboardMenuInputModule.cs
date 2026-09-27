using UnityEngine.InputSystem.UI;

public class KeyboardMenuInputModule : InputSystemUIInputModule
{
    public override void Process()
    {
        // Back/Esc wins over submit, even if EventSystem updates before the page.
        if (MenuInput.FrameBlocked || MenuInput.BackPressed || MenuInput.EscapePressed)
            return;
        base.Process();
    }
}
