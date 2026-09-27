using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal static class PauseMenuChecks
{
    private static void Set(object o, string field, object value) =>
        o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
    private static void Call(object o, string method) =>
        o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, null);
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private sealed class Fixture
    {
        public readonly BattlePauseMenu Menu = new BattlePauseMenu();
        public readonly BattleController Battle = new BattleController();
        public readonly GameObject Root = new GameObject();
        public readonly GameObject Focus = new GameObject();
        public readonly SaveSlotMenu Slots = new SaveSlotMenu();
        public readonly Renderer Visible = new Renderer();
        public readonly Renderer Hidden = new Renderer { forceRenderingOff = true };
        public Fixture()
        {
            Time.frameCount += 10;
            Time.timeScale = 0.75f;
            Keyboard.current = null;
            Application.SceneAvailable = true;
            SceneManager.ThrowOnLoad = false;
            SceneManager.Loaded = null;
            EventSystem.current = new EventSystem { currentSelectedGameObject = Focus };
            Battle.ChangeState(BattleState.WaitingForCommand);
            Set(Menu, "battleController", Battle);
            Set(Menu, "menuRoot", Root);
            Set(Menu, "battleFieldRoot", new Transform { Children = new Component[] { Visible, Hidden } });
            Set(Menu, "cursor", new RectTransform());
            Set(Menu, "rows", new[] { new Image(), new Image(), new Image(), new Image(), new Image() });
            Set(Menu, "labels", new[] { new TextMeshProUGUI(), new TextMeshProUGUI(),
                new TextMeshProUGUI(), new TextMeshProUGUI(), new TextMeshProUGUI() });
            Set(Menu, "saveSlotMenu", Slots);
            Call(Menu, "Awake");
            Call(Menu, "Open");
        }
        public void NextFrame() { Time.frameCount++; Call(Menu, "Update"); }
    }

    public static void Run()
    {
        Action[] tests = { ContinueRestoresState, SlotCancelKeepsBattlePaused,
            DisableFromSlotsRestoresState, RestartKeepsEntry, ReturnClearsEntry,
            MissingSceneKeepsPause, NewPageFocusIsNotOverwritten,
            EscapeClosesSlotsAndBattleMenu, BackWinsOverRestartConfirm,
            OriginallyDisabledInputStaysDisabled, LoadFailureKeepsSlotsOpen,
            SceneChangeKeepsOldBattleFrozen };
        foreach (var test in tests) { test(); Console.WriteLine("PASS PauseMenu." + test.Method.Name); }
        Console.WriteLine(tests.Length + " pause-menu managed checks passed; native UI/scene tests still required.");
    }

    private static void ContinueRestoresState()
    {
        var f = new Fixture();
        Check(Time.timeScale == 0 && f.Battle.IsMenuPaused && f.Visible.forceRenderingOff, "Open must pause/hide.");
        Call(f.Menu, "Close");
        Check(Time.timeScale == 0.75f && !f.Battle.IsMenuPaused && !f.Visible.forceRenderingOff && f.Hidden.forceRenderingOff,
            "Close must restore original time and renderer flags.");
        Check(!EventSystem.current.currentInputModule.enabled && MenuInput.FrameBlocked, "Close frame must be blocked.");
        f.NextFrame();
        Check(EventSystem.current.currentInputModule.enabled && EventSystem.current.currentSelectedGameObject == f.Focus,
            "Next frame must restore module and original focus.");
    }
    private static void SlotCancelKeepsBattlePaused()
    {
        var f = new Fixture();
        Set(f.Menu, "selectedIndex", 1);
        Call(f.Menu, "OpenLoadSlots");
        Check(f.Slots.IsOpen && f.Battle.IsMenuPaused && EventSystem.current.currentInputModule.enabled, "Slots need submit while paused.");
        f.Slots.Cancel();
        Check(Time.timeScale == 0 && f.Battle.IsMenuPaused && !EventSystem.current.currentInputModule.enabled,
            "Cancel must stay paused on root.");
        Call(f.Menu, "OnDisable");
    }
    private static void DisableFromSlotsRestoresState()
    {
        var f = new Fixture();
        Call(f.Menu, "OpenLoadSlots");
        Call(f.Menu, "OnDisable");
        Check(!f.Slots.IsOpen && Time.timeScale == 0.75f && !f.Battle.IsMenuPaused && EventSystem.current.currentInputModule.enabled,
            "Disable from subpage must restore time/input.");
    }
    private static BattleEntry PrepareEntry()
    {
        BattleSession.Prepare(new List<Combatant> { new Combatant("P", 100, 10, 5, 0, 10, true, "p") },
            new[] { "goblin" }, "Exploration_scene", environmentPrefab: new GameObject());
        return BattleSession.Entry;
    }
    private static void RestartKeepsEntry()
    {
        var entry = PrepareEntry();
        var background = BattleSession.EnvironmentPrefab;
        var f = new Fixture();
        Call(f.Menu, "RestartBattle");
        Check(SceneManager.Loaded == "Battle_scene" && ReferenceEquals(entry, BattleSession.Entry) &&
            ReferenceEquals(background, BattleSession.EnvironmentPrefab) && Time.timeScale == 0.75f,
            "Restart must preserve snapshot and restore time.");
    }
    private static void ReturnClearsEntry()
    {
        PrepareEntry();
        var f = new Fixture();
        Call(f.Menu, "ReturnToTitle");
        Check(SceneManager.Loaded == "Title_scene" && BattleSession.Entry == null && BattleSession.Result == null &&
            BattleSession.EnvironmentPrefab == null && Time.timeScale == 0.75f,
            "Return must clear session and restore time.");
    }
    private static void MissingSceneKeepsPause()
    {
        var entry = PrepareEntry();
        var f = new Fixture();
        Application.SceneAvailable = false;
        Call(f.Menu, "ReturnToTitle");
        Check(SceneManager.Loaded == null && ReferenceEquals(entry, BattleSession.Entry) && f.Battle.IsMenuPaused,
            "Missing destination must preserve paused battle.");
        Call(f.Menu, "OnDisable");
    }
    private static void NewPageFocusIsNotOverwritten()
    {
        var f = new Fixture();
        Call(f.Menu, "Close");
        var newFocus = new GameObject();
        EventSystem.current.SetSelectedGameObject(newFocus);
        f.NextFrame();
        Check(EventSystem.current.currentSelectedGameObject == newFocus,
            "Deferred restore must not overwrite focus assigned by the next battle/result page.");
    }

    private static Keyboard Keys() => new Keyboard
    {
        xKey = new KeyControl(), zKey = new KeyControl(), escapeKey = new KeyControl(),
        leftArrowKey = new KeyControl(), rightArrowKey = new KeyControl(),
        upArrowKey = new KeyControl(), downArrowKey = new KeyControl()
    };

    private static void EscapeClosesSlotsAndBattleMenu()
    {
        var f = new Fixture();
        Call(f.Menu, "OpenLoadSlots");
        Keyboard.current = Keys();
        Keyboard.current.escapeKey.wasPressedThisFrame = true;
        f.NextFrame();
        Check(!f.Slots.IsOpen && !f.Root.activeSelf && !f.Battle.IsMenuPaused && Time.timeScale == 0.75f,
            "Esc from slots must close everything and restore time.");
        Keyboard.current = null;
        f.NextFrame();
    }

    private static void BackWinsOverRestartConfirm()
    {
        PrepareEntry();
        var f = new Fixture();
        Set(f.Menu, "selectedIndex", 3);
        Keyboard.current = Keys();
        Keyboard.current.xKey.wasPressedThisFrame = true;
        Keyboard.current.zKey.wasPressedThisFrame = true;
        f.NextFrame();
        Check(SceneManager.Loaded == null && !f.Battle.IsMenuPaused, "X must win over restart Z.");
        Keyboard.current = null;
        f.NextFrame();
    }

    private static void OriginallyDisabledInputStaysDisabled()
    {
        var f = new Fixture();
        Call(f.Menu, "OnDisable");
        EventSystem.current.currentInputModule.enabled = false;
        EventSystem.current.sendNavigationEvents = false;
        Call(f.Menu, "Open");
        Call(f.Menu, "OpenLoadSlots");
        Call(f.Menu, "OnDisable");
        Check(!EventSystem.current.currentInputModule.enabled && !EventSystem.current.sendNavigationEvents,
            "Cleanup must respect original disabled input settings.");
    }

    private static void LoadFailureKeepsSlotsOpen()
    {
        var entry = PrepareEntry();
        var f = new Fixture();
        Call(f.Menu, "OpenLoadSlots");
        Resources.Clear(); // Missing data forces the real LoadSlot catch path before any save activation.
        typeof(BattlePauseMenu).GetMethod("LoadSlot", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(f.Menu, new object[] { 0 });
        Check(f.Slots.IsOpen && f.Battle.IsMenuPaused && Time.timeScale == 0 &&
            ReferenceEquals(entry, BattleSession.Entry) && SceneManager.Loaded == null,
            "Load error must preserve paused battle and permit cancellation.");
        f.Slots.Cancel();
        Call(f.Menu, "OnDisable");
    }

    private static void SceneChangeKeepsOldBattleFrozen()
    {
        PrepareEntry();
        var f = new Fixture();
        Call(f.Menu, "ReturnToTitle");
        Check(f.Battle.IsMenuPaused && Time.timeScale == 0.75f,
            "Pending scene unload must not resume old attack/reward coroutines after restoring global time.");
    }
}
