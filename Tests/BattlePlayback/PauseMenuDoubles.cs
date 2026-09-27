using System;
using UnityEngine;

// Narrow service/UI substitutes for exercising the real pause-menu state transitions.
// These do not simulate native EventSystem dispatch, disk IO or scene activation.
namespace UnityEngine
{
    public class Renderer : Component { public bool forceRenderingOff; }
    public static class Application
    {
        public static bool SceneAvailable = true;
        public static bool CanStreamedLevelBeLoaded(string name) => SceneAvailable;
    }
}
namespace UnityEngine.EventSystems
{
    public class BaseInputModule { public bool enabled = true; }
    public class EventSystem
    {
        public static EventSystem current;
        public bool sendNavigationEvents = true;
        public GameObject currentSelectedGameObject;
        public BaseInputModule currentInputModule = new BaseInputModule();
        public void SetSelectedGameObject(GameObject target) => currentSelectedGameObject = target;
    }
}
namespace UnityEngine.UI
{
    public class Image : Component
    {
        public Color color;
        public RectTransform rectTransform = new RectTransform();
    }
    public class Button : Component
    {
        public bool IsInteractable() => true;
        public void Select() => EventSystems.EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name; }
    public static class SceneManager
    {
        public static string Loaded;
        public static bool ThrowOnLoad;
        public static Scene GetActiveScene() => new Scene { name = "Battle_scene" };
        public static void LoadScene(string name)
        {
            if (ThrowOnLoad) throw new InvalidOperationException("Simulated load failure");
            Loaded = name;
        }
    }
}
public enum SaveSlotMode { Load, Save }
public class SaveSlotMenu : MonoBehaviour
{
    public bool IsOpen;
    public Action Closed;
    public bool Open(SaveSlotMode mode, Action<int> confirm, Action closed)
    {
        IsOpen = true;
        Closed = closed;
        MenuInput.Activate(this);
        return true;
    }
    public void CloseImmediately() { IsOpen = false; MenuInput.Release(this); }
    public void Cancel() { CloseImmediately(); Closed(); }
}
namespace Game.Battle
{
    public class GameSaveData { public ExplorationProgress exploration = new ExplorationProgress(); }
    public class ExplorationProgress { public string sceneName = "Exploration_scene"; }
    public static class GameRuntimeState
    {
        public static void Initialize(GameSaveData save) { }
    }
    public static class GameSaveSystem
    {
        public const int AutoSaveSlotNumber = 0;
        public static GameSaveData LoadAutoSave(BattleData data) => new GameSaveData();
        public static GameSaveData LoadManualSave(BattleData data, int slot) => new GameSaveData();
        public static void ActivateManualSave(BattleData data, int slot) { }
    }
}
