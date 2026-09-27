using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Battle
{
    [DefaultExecutionOrder(-10000)]
    public class BattlePauseMenu : MonoBehaviour
    {
        private const string TitleSceneName = "Title_scene";

        [SerializeField] private BattleController battleController;
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private Transform battleFieldRoot;
        [SerializeField] private RectTransform cursor;
        [SerializeField] private Image[] rows;
        [SerializeField] private TextMeshProUGUI[] labels;
        [SerializeField] private SaveSlotMenu saveSlotMenu;
        [SerializeField] private Color normalColor = new Color(0.025f, 0.08f, 0.24f);
        [SerializeField] private Color selectedColor = new Color(0.035f, 0.27f, 0.72f);
        [SerializeField] private Color disabledTextColor = new Color(0.52f, 0.59f, 0.7f);

        private bool isOpen;
        private bool isLeaving;
        private float savedTimeScale;
        private GameObject savedFocus;
        private EventSystem eventSystem;
        private BaseInputModule inputModule;
        private bool savedModuleEnabled;
        private bool savedNavigation;
        private int restoreInputFrame = -1;
        private int selectedIndex;
        private int heldDirection;
        private float repeatRemaining;
        private Renderer[] hiddenRenderers;
        private bool[] savedRenderingOff;
        private readonly List<GameObject> hiddenMenuObjects = new List<GameObject>();

        private void Awake()
        {
            menuRoot.SetActive(false);
            if (saveSlotMenu != null) saveSlotMenu.CloseImmediately();
        }

        private void Update()
        {
            if (isLeaving) return;
            if (restoreInputFrame >= 0 && Time.frameCount > restoreInputFrame)
                RestoreInput();

            if (isOpen && (battleController == null || !battleController.isActiveAndEnabled))
            {
                Close();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || MenuInput.FrameBlocked) return;
            if (MenuInput.EscapePressed)
            {
                if (isOpen) Close();
                else if (CanOpen()) Open();
                return;
            }
            if (!isOpen || !MenuInput.CanRead(this)) return;

            if (MenuInput.BackPressed)
            {
                Close();
                return;
            }
            if (MenuInput.ConfirmPressed)
            {
                if (selectedIndex == 0) Close();
                else if (selectedIndex == 1) OpenLoadSlots();
                else if (selectedIndex == 3) RestartBattle();
                else if (selectedIndex == 4) ReturnToTitle();
                return;
            }

            int direction = (keyboard.downArrowKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                - (keyboard.upArrowKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            if (direction == 0) { heldDirection = 0; return; }
            repeatRemaining -= Time.unscaledDeltaTime;
            if (direction != heldDirection || repeatRemaining <= 0f)
            {
                selectedIndex = (selectedIndex + direction + rows.Length) % rows.Length;
                RefreshSelection();
                repeatRemaining = direction != heldDirection ? 0.35f : 0.12f;
                heldDirection = direction;
            }
        }

        private bool CanOpen()
        {
            return battleController != null && battleController.isActiveAndEnabled &&
                battleController.CurrentState != BattleState.Victory &&
                battleController.CurrentState != BattleState.Defeat &&
                battleController.CurrentState != BattleState.Paused;
        }

        private void Open()
        {
            savedTimeScale = Time.timeScale;
            eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                savedFocus = eventSystem.currentSelectedGameObject;
                savedNavigation = eventSystem.sendNavigationEvents;
                inputModule = eventSystem.currentInputModule;
                savedModuleEnabled = inputModule != null && inputModule.enabled;
                // The menu owns keyboard input; don't let the UI module submit behind it.
                if (inputModule != null) inputModule.enabled = false;
                eventSystem.sendNavigationEvents = false;
                eventSystem.SetSelectedGameObject(null);
            }
            battleController.SetMenuPaused(true);
            Time.timeScale = 0f;
            HideBattleField();
            isOpen = true;
            MenuInput.Activate(this);
            selectedIndex = 0;
            heldDirection = 0;
            menuRoot.SetActive(true);
            menuRoot.transform.SetAsLastSibling();
            RefreshSelection();
        }

        private void HideBattleField()
        {
            if (battleFieldRoot == null) return;
            // Hide rendering only: disabling actor objects would interrupt their playback.
            hiddenRenderers = battleFieldRoot.GetComponentsInChildren<Renderer>(true);
            savedRenderingOff = new bool[hiddenRenderers.Length];
            for (int i = 0; i < hiddenRenderers.Length; i++)
            {
                savedRenderingOff[i] = hiddenRenderers[i].forceRenderingOff;
                hiddenRenderers[i].forceRenderingOff = true;
            }
        }

        private void RestoreBattleField()
        {
            if (hiddenRenderers == null) return;
            for (int i = 0; i < hiddenRenderers.Length; i++)
            {
                if (hiddenRenderers[i] != null)
                    hiddenRenderers[i].forceRenderingOff = savedRenderingOff[i];
            }
            hiddenRenderers = null;
            savedRenderingOff = null;
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].color = i == selectedIndex ? selectedColor : normalColor;
                bool available = i == 0 || i == 4 || (i == 1 && saveSlotMenu != null) ||
                    (i == 3 && BattleSession.Entry != null);
                labels[i].color = available ? Color.white : disabledTextColor;
            }
            RectTransform row = rows[selectedIndex].rectTransform;
            cursor.anchorMin = new Vector2(row.anchorMin.x - 0.055f, row.anchorMin.y);
            cursor.anchorMax = new Vector2(row.anchorMin.x - 0.012f, row.anchorMax.y);
            cursor.anchoredPosition = Vector2.zero;
            cursor.sizeDelta = Vector2.zero;
        }

        private void Close()
        {
            if (!isOpen) return;
            if (saveSlotMenu != null) saveSlotMenu.CloseImmediately();
            SetSlotInput(false);
            RestoreMenuOptions();
            isOpen = false;
            MenuInput.Release(this);
            MenuInput.BlockFrame();
            menuRoot.SetActive(false);
            Time.timeScale = savedTimeScale;
            RestoreBattleField();
            if (battleController != null) battleController.SetMenuPaused(false);
            // Re-enable native UI next frame so Z/X cannot also trigger a battle command.
            restoreInputFrame = Time.frameCount;
        }

        private void OpenLoadSlots()
        {
            if (saveSlotMenu == null || saveSlotMenu.IsOpen) return;
            // Only the shared slot page uses EventSystem submit; the battle stays paused.
            SetSlotInput(true);
            if (!saveSlotMenu.Open(SaveSlotMode.Load, LoadSlot, CloseLoadSlots))
            {
                CloseLoadSlots();
                return;
            }
            foreach (Transform child in menuRoot.transform)
            {
                if (!child.gameObject.activeSelf) continue;
                hiddenMenuObjects.Add(child.gameObject);
                child.gameObject.SetActive(false);
            }
            saveSlotMenu.transform.SetAsLastSibling();
        }

        private void CloseLoadSlots()
        {
            SetSlotInput(false);
            RestoreMenuOptions();
            heldDirection = 0;
            repeatRemaining = 0f;
            MenuInput.Activate(this);
            RefreshSelection();
        }

        private void RestoreMenuOptions()
        {
            foreach (GameObject child in hiddenMenuObjects)
                if (child != null) child.SetActive(true);
            hiddenMenuObjects.Clear();
        }

        private void SetSlotInput(bool enabled)
        {
            if (inputModule != null) inputModule.enabled = enabled;
            if (eventSystem != null) eventSystem.sendNavigationEvents = enabled;
        }

        private void RestartBattle()
        {
            if (BattleSession.Entry == null) return;
            string sceneName = SceneManager.GetActiveScene().name;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("Battle scene is not available in the build scene list: " + sceneName, this);
                return;
            }
            PrepareSceneChange();
            // Do not prepare a new entry: it is the original party, inventory and environment.
            SceneManager.LoadScene(sceneName);
        }

        private void ReturnToTitle()
        {
            if (!isOpen || isLeaving) return;
            if (!Application.CanStreamedLevelBeLoaded(TitleSceneName))
            {
                Debug.LogError("Title scene is not available in the build scene list: " + TitleSceneName, this);
                return;
            }
            PrepareSceneChange();
            BattleSession.LeaveBattle();
            BattleSession.ClearResult();
            SceneManager.LoadScene(TitleSceneName);
        }

        private void LoadSlot(int slotNumber)
        {
            if (!isOpen || isLeaving) return;
            string destination;
            try
            {
                BattleData data = BattleData.Load();
                GameSaveData save = slotNumber == GameSaveSystem.AutoSaveSlotNumber
                    ? GameSaveSystem.LoadAutoSave(data)
                    : GameSaveSystem.LoadManualSave(data, slotNumber);
                destination = save.exploration.sceneName;
                if (string.IsNullOrWhiteSpace(destination) ||
                    !Application.CanStreamedLevelBeLoaded(destination))
                    throw new System.InvalidOperationException(
                        "Exploration scene is not available in the build scene list: " + destination);

                if (slotNumber == GameSaveSystem.AutoSaveSlotNumber)
                    GameRuntimeState.Initialize(save);
                else
                    GameSaveSystem.ActivateManualSave(data, slotNumber);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("Cannot load save slot: " + exception.Message, this);
                return;
            }
            PrepareSceneChange();
            BattleSession.LeaveBattle();
            BattleSession.ClearResult();
            SceneManager.LoadScene(destination);
        }

        private void PrepareSceneChange()
        {
            isLeaving = true;
            Close();
            // Global time belongs to the next scene; keep old playback frozen until unload.
            if (battleController != null) battleController.SetMenuPaused(true);
            RestoreInput();
        }

        private void RestoreInput()
        {
            if (eventSystem != null)
            {
                eventSystem.sendNavigationEvents = savedNavigation;
                // A resumed battle may already have moved to another page this frame.
                if (eventSystem.currentSelectedGameObject == null && CanOpen() &&
                    savedFocus != null && savedFocus.activeInHierarchy)
                    eventSystem.SetSelectedGameObject(savedFocus);
            }
            if (inputModule != null) inputModule.enabled = savedModuleEnabled;
            restoreInputFrame = -1;
            savedFocus = null;
            inputModule = null;
            eventSystem = null;
        }

        private void OnDisable()
        {
            Close();
            if (restoreInputFrame >= 0) RestoreInput();
        }
    }
}
