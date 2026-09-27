using Game.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Exploration
{
    public class ExplorationMenuController : MonoBehaviour
    {
        private const string TitleSceneName = "Title_scene";

        [SerializeField] private ExplorationSceneController sceneController;
        [SerializeField] private ExplorationPlayerController playerController;
        [SerializeField] private GameObject explorationMenu;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button partyButton;
        [SerializeField] private PartyMenu partyMenu;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button returnTitleButton;
        [SerializeField] private GameObject exitConfirmationPanel;
        [SerializeField] private Button exitYesButton;
        [SerializeField] private Button exitNoButton;
        [SerializeField] private SaveSlotMenu saveSlotMenu;
        [SerializeField] private GameObject saveConfirmationPanel;
        [SerializeField] private Button saveYesButton;
        [SerializeField] private Button saveNoButton;

        private bool menuOpen;
        private bool exitConfirmationOpen;
        private bool saveConfirmationOpen;
        private bool playerInputWasEnabled;
        private float previousTimeScale = 1f;
        private int pendingSaveSlot = -1;
        private SaveSlotMode slotMode;

        private void Awake()
        {
            if (!HasRequiredReferences())
            {
                Debug.LogError("Exploration menu references are incomplete.", this);
                enabled = false;
                return;
            }

            resumeButton.onClick.AddListener(CloseMenu);
            partyButton.onClick.AddListener(OpenParty);
            loadButton.onClick.AddListener(OpenLoadSlots);
            saveButton.onClick.AddListener(OpenSaveSlots);
            returnTitleButton.onClick.AddListener(OpenExitConfirmation);
            exitYesButton.onClick.AddListener(ReturnToTitle);
            exitNoButton.onClick.AddListener(CloseExitConfirmation);
            saveYesButton.onClick.AddListener(ConfirmOverwrite);
            saveNoButton.onClick.AddListener(CancelOverwrite);

            partyButton.interactable = partyMenu != null;
            if (partyMenu != null)
                partyMenu.CloseImmediately();
            settingsButton.interactable = false;
            explorationMenu.SetActive(false);
            exitConfirmationPanel.SetActive(false);
            saveConfirmationPanel.SetActive(false);
            saveSlotMenu.CloseImmediately();
        }

        private void Update()
        {
            if (MenuInput.FrameBlocked)
                return;

            if (MenuInput.EscapePressed)
            {
                if (menuOpen)
                    CloseMenu();
                else
                    OpenMenu();
                return;
            }

            if (!menuOpen || !MenuInput.CanRead(this))
                return;

            if (MenuInput.BackPressed)
            {
                if (saveConfirmationOpen)
                    CancelOverwrite();
                else if (exitConfirmationOpen)
                    CloseExitConfirmation();
                else
                    CloseMenu();
                return;
            }

            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == null)
            {
                if (saveConfirmationOpen)
                    FocusButton(saveNoButton);
                else if (exitConfirmationOpen)
                    FocusButton(exitNoButton);
                else
                    FocusButton(resumeButton);
            }
        }

        private void OnDisable()
        {
            MenuInput.Release(this);
            if (saveSlotMenu != null)
                saveSlotMenu.CloseImmediately();
            if (partyMenu != null)
                partyMenu.CloseImmediately();
            if (menuOpen)
                CloseMenu();
        }

        public void OpenMenu()
        {
            if (menuOpen)
                return;

            menuOpen = true;
            previousTimeScale = Time.timeScale;
            playerInputWasEnabled = playerController.InputEnabled;
            playerController.SetInputEnabled(false);
            Time.timeScale = 0f;
            explorationMenu.SetActive(true);
            SetRootButtonsInteractable(true);
            FocusButton(resumeButton);
        }

        public void CloseMenu()
        {
            if (!menuOpen)
                return;

            saveSlotMenu.CloseImmediately();
            if (partyMenu != null)
                partyMenu.CloseImmediately();
            exitConfirmationOpen = false;
            saveConfirmationOpen = false;
            pendingSaveSlot = -1;
            exitConfirmationPanel.SetActive(false);
            saveConfirmationPanel.SetActive(false);
            explorationMenu.SetActive(false);
            RestoreGameplay();
        }

        private void OpenParty()
        {
            if (!menuOpen || partyMenu == null || partyMenu.IsOpen)
                return;
            SetRootButtonsInteractable(false);
            if (!partyMenu.Open(CloseParty))
                CloseParty();
        }

        private void CloseParty()
        {
            SetRootButtonsInteractable(true);
            FocusButton(partyButton);
        }

        private void OpenLoadSlots()
        {
            OpenSlotPage(SaveSlotMode.Load, LoadSlot);
        }

        private void OpenSaveSlots()
        {
            OpenSlotPage(SaveSlotMode.Save, SelectSaveSlot);
        }

        private void OpenSlotPage(SaveSlotMode openMode, System.Action<int> onConfirmed,
            int preferredSlot = -1)
        {
            slotMode = openMode;
            SetRootButtonsInteractable(false);
            if (saveSlotMenu.Open(openMode, onConfirmed, CloseSlotPage, preferredSlot))
                return;

            SetRootButtonsInteractable(true);
            FocusButton(openMode == SaveSlotMode.Load ? loadButton : saveButton);
        }

        private void CloseSlotPage()
        {
            SetRootButtonsInteractable(true);
            FocusButton(slotMode == SaveSlotMode.Load ? loadButton : saveButton);
        }

        private void SelectSaveSlot(int slotNumber)
        {
            if (GameSaveSystem.HasManualSave(slotNumber))
            {
                pendingSaveSlot = slotNumber;
                saveSlotMenu.CloseImmediately();
                saveConfirmationOpen = true;
                saveConfirmationPanel.SetActive(true);
                saveConfirmationPanel.transform.SetAsLastSibling();
                FocusButton(saveNoButton);
                return;
            }

            SaveToSlot(slotNumber);
        }

        private void ConfirmOverwrite()
        {
            if (!saveConfirmationOpen || pendingSaveSlot < 1)
                return;

            int slotNumber = pendingSaveSlot;
            CloseSaveConfirmation();
            SaveToSlot(slotNumber);
        }

        private void CancelOverwrite()
        {
            if (!saveConfirmationOpen)
                return;

            int slotNumber = pendingSaveSlot;
            CloseSaveConfirmation();
            OpenSlotPage(SaveSlotMode.Save, SelectSaveSlot, slotNumber);
        }

        private void CloseSaveConfirmation()
        {
            saveConfirmationOpen = false;
            saveConfirmationPanel.SetActive(false);
            pendingSaveSlot = -1;
        }

        private void SaveToSlot(int slotNumber)
        {
            sceneController.SaveToManualSlot(slotNumber);
            OpenSlotPage(SaveSlotMode.Save, SelectSaveSlot, slotNumber);
        }

        private void LoadSlot(int slotNumber)
        {
            try
            {
                BattleData data = BattleData.Load();
                GameSaveData save = slotNumber == GameSaveSystem.AutoSaveSlotNumber
                    ? GameSaveSystem.LoadAutoSave(data)
                    : GameSaveSystem.LoadManualSave(data, slotNumber);
                string destinationScene = save.exploration.sceneName;
                if (string.IsNullOrWhiteSpace(destinationScene) ||
                    !Application.CanStreamedLevelBeLoaded(destinationScene))
                {
                    throw new System.InvalidOperationException(
                        "Exploration scene is not available in the build scene list: " +
                        destinationScene);
                }

                if (slotNumber != GameSaveSystem.AutoSaveSlotNumber)
                    GameSaveSystem.ActivateManualSave(data, slotNumber);
                else
                    GameRuntimeState.Initialize(save);

                PrepareSceneChange();
                SceneManager.LoadScene(destinationScene);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("Cannot load save slot: " + exception.Message, this);
            }
        }

        private void OpenExitConfirmation()
        {
            if (exitConfirmationOpen)
                return;

            exitConfirmationOpen = true;
            SetRootButtonsInteractable(false);
            exitConfirmationPanel.SetActive(true);
            exitConfirmationPanel.transform.SetAsLastSibling();
            FocusButton(exitNoButton);
        }

        private void CloseExitConfirmation()
        {
            if (!exitConfirmationOpen)
                return;

            exitConfirmationOpen = false;
            exitConfirmationPanel.SetActive(false);
            SetRootButtonsInteractable(true);
            FocusButton(returnTitleButton);
        }

        private void ReturnToTitle()
        {
            sceneController.SaveCurrentPosition();
            PrepareSceneChange();
            SceneManager.LoadScene(TitleSceneName);
        }

        private void PrepareSceneChange()
        {
            if (partyMenu != null)
                partyMenu.CloseImmediately();
            saveSlotMenu.CloseImmediately();
            exitConfirmationPanel.SetActive(false);
            saveConfirmationPanel.SetActive(false);
            explorationMenu.SetActive(false);
            if (menuOpen)
                RestoreGameplay();
        }

        private void RestoreGameplay()
        {
            MenuInput.Release(this);
            MenuInput.BlockFrame();
            menuOpen = false;
            Time.timeScale = previousTimeScale;
            playerController.SetInputEnabled(playerInputWasEnabled);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void SetRootButtonsInteractable(bool interactable)
        {
            resumeButton.interactable = interactable;
            loadButton.interactable = interactable && GameSaveSystem.HasAnySave;
            saveButton.interactable = interactable;
            returnTitleButton.interactable = interactable;
            partyButton.interactable = interactable && partyMenu != null;
            settingsButton.interactable = false;
        }

        private bool HasRequiredReferences()
        {
            return sceneController != null && playerController != null &&
                explorationMenu != null && resumeButton != null && partyButton != null &&
                loadButton != null && saveButton != null && settingsButton != null &&
                returnTitleButton != null && exitConfirmationPanel != null &&
                exitYesButton != null && exitNoButton != null && saveSlotMenu != null &&
                saveConfirmationPanel != null && saveYesButton != null && saveNoButton != null;
        }

        private void FocusButton(Button button)
        {
            MenuInput.Activate(this, button);
        }
    }
}
