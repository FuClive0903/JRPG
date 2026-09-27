using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using Game.Battle;

public class TitleController : MonoBehaviour
{
    [SerializeField] private Canvas titleCanvas;
    [SerializeField] private VideoClip introVideo;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private GameObject exitConfirmationPanel;
    [SerializeField] private Button exitNoButton;
    [SerializeField] private SaveSlotMenu saveSlotMenu;

    private VideoPlayer videoPlayer;
    private bool isStarting;
    private bool isLoadingScene;
    private bool isExitConfirmationOpen;
    private int introStartFrame;
    private string destinationScene;

    private void Update()
    {
        if (MenuInput.FrameBlocked)
            return;
        // Ignore the confirm press that started the game, regardless of Update order.
        if (isStarting && Time.frameCount > introStartFrame &&
            MenuInput.ConfirmPressed)
        {
            EnterExploration();
            return;
        }

        if (isStarting)
            return;

        if (!MenuInput.CanRead(this))
            return;

        if (MenuInput.BackPressed)
        {
            if (isExitConfirmationOpen)
                CloseExitConfirmation();
            else
                ShowExitConfirmation();
            return;
        }

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            FocusButton(isExitConfirmationOpen ? exitNoButton : newGameButton);
    }

    private void Start()
    {
        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.clip = introVideo;
        videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
        videoPlayer.targetCamera = GetComponent<Camera>();
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.loopPointReached += OnIntroFinished;
        videoPlayer.errorReceived += OnVideoError;
        exitConfirmationPanel.SetActive(false);
        if (saveSlotMenu != null)
            saveSlotMenu.CloseImmediately();
        SetMainMenuInteractable(true);
        FocusButton(newGameButton);
    }

    public void PlayIntro()
    {
        StartGame(false);
    }

    public void LoadGame()
    {
        if (saveSlotMenu != null && saveSlotMenu.Open(
            SaveSlotMode.Load, LoadSaveSlot, CloseSaveSlotMenu))
        {
            SetMainMenuInteractable(false);
            return;
        }

        StartGame(true, GameSaveSystem.AutoSaveSlotNumber);
    }

    public void LoadSaveSlot(int slotNumber)
    {
        StartGame(true, slotNumber);
    }

    public void CloseSaveSlotMenu()
    {
        if (isStarting)
            return;

        SetMainMenuInteractable(true);
        FocusButton(loadGameButton);
    }

    private void StartGame(bool loadSavedGame, int slotNumber = GameSaveSystem.AutoSaveSlotNumber)
    {
        if (isStarting)
            return;

        try
        {
            BattleData data = BattleData.Load();
            GameSaveData loadedSave = null;
            bool activateManualSave = loadSavedGame &&
                slotNumber != GameSaveSystem.AutoSaveSlotNumber;

            if (loadSavedGame)
            {
                GameSaveData save = !activateManualSave
                    ? GameSaveSystem.LoadAutoSave(data)
                    : GameSaveSystem.LoadManualSave(data, slotNumber);
                destinationScene = save.exploration.sceneName;
                loadedSave = save;
            }
            else
            {
                GameSaveSystem.SaveNewGame(
                    data.CreateParty(data.defaultPlayerParty, true), data.defaultInventory);
                destinationScene = "Battle_scene";
            }

            if (string.IsNullOrWhiteSpace(destinationScene) ||
                !Application.CanStreamedLevelBeLoaded(destinationScene))
                throw new System.InvalidOperationException(
                    "Destination scene is not available in the build scene list: " + destinationScene);

            if (activateManualSave)
                GameSaveSystem.ActivateManualSave(data, slotNumber);
            else if (loadedSave != null)
                GameRuntimeState.Initialize(loadedSave);
            if (!loadSavedGame)
            {
                BattleSession.LeaveBattle();
                BattleSession.ClearResult();
            }
        }
        catch (System.Exception exception)
        {
            destinationScene = null;
            Debug.LogError("Cannot start game: " + exception.Message, this);
            loadGameButton.interactable = GameSaveSystem.HasAnySave;
            return;
        }

        isStarting = true;
        MenuInput.Release(this);
        if (saveSlotMenu != null)
            saveSlotMenu.CloseImmediately();
        MenuInput.BlockFrame();
        introStartFrame = Time.frameCount;
        titleCanvas.gameObject.SetActive(false);
        GetComponent<Camera>().backgroundColor = Color.black;
        if (introVideo == null)
        {
            EnterExploration();
            return;
        }
        videoPlayer.Play();
    }

    public void ShowExitConfirmation()
    {
        if (isStarting || isExitConfirmationOpen)
            return;

        isExitConfirmationOpen = true;
        SetMainMenuInteractable(false);
        exitConfirmationPanel.SetActive(true);
        exitConfirmationPanel.transform.SetAsLastSibling();
        FocusButton(exitNoButton);
    }

    public void CloseExitConfirmation()
    {
        if (!isExitConfirmationOpen)
            return;

        isExitConfirmationOpen = false;
        exitConfirmationPanel.SetActive(false);
        SetMainMenuInteractable(true);
        FocusButton(newGameButton);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnIntroFinished(VideoPlayer player)
    {
        EnterExploration();
    }

    private void OnVideoError(VideoPlayer player, string message)
    {
        Debug.LogError("Intro video failed: " + message, this);
        EnterExploration();
    }

    private void EnterExploration()
    {
        if (isLoadingScene)
            return;

        isLoadingScene = true;
        videoPlayer.Stop();
        SceneManager.LoadScene(destinationScene);
    }

    private void SetMainMenuInteractable(bool interactable)
    {
        newGameButton.interactable = interactable;
        loadGameButton.interactable = interactable && GameSaveSystem.HasAnySave;
        settingsButton.interactable = interactable;
        quitButton.interactable = interactable;
    }

    private void OnDisable()
    {
        MenuInput.Release(this);
    }

    private void FocusButton(Button button)
    {
        MenuInput.Activate(this, button);
    }
}
