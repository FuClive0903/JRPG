using System;
using System.Collections.Generic;
using Game.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum SaveSlotMode
{
    Load,
    Save
}

public class SaveSlotMenu : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Button styleSourceButton;

    private readonly List<Button> slotButtons = new List<Button>();
    private readonly List<TextMeshProUGUI> slotTexts = new List<TextMeshProUGUI>();
    private readonly List<int> slotNumbers = new List<int>();
    private Action<int> slotConfirmed;
    private Action closed;
    private SaveSlotMode mode;
    private int selectedIndex = -1;
    private bool initialized;

    public bool IsOpen { get { return gameObject.activeSelf; } }

    private void Update()
    {
        if (!MenuInput.CanRead(this) || MenuInput.EscapePressed)
            return;
        if (MenuInput.BackPressed)
            Close();
        else if (MenuInput.VerticalStep != 0)
            MoveSelection(MenuInput.VerticalStep);
    }

    public bool Open(SaveSlotMode openMode, Action<int> onSlotConfirmed, Action onClosed,
        int preferredSlotNumber = -1)
    {
        if (!EnsureInitialized() || onSlotConfirmed == null)
            return false;

        mode = openMode;
        slotConfirmed = onSlotConfirmed;
        closed = onClosed;
        titleText.text = mode == SaveSlotMode.Load ? "读取存档" : "保存游戏";
        gameObject.SetActive(true);
        MenuInput.Activate(this);
        RefreshSlots();
        SelectInitialSlot(preferredSlotNumber);
        return true;
    }

    public void CloseImmediately()
    {
        MenuInput.Release(this);
        slotConfirmed = null;
        closed = null;
        gameObject.SetActive(false);
    }

    private void OnDisable() => MenuInput.Release(this);

    private void Close()
    {
        Action closeCallback = closed;
        CloseImmediately();
        closeCallback?.Invoke();
    }

    private bool EnsureInitialized()
    {
        if (initialized)
            return true;
        if (titleText == null || scrollRect == null || scrollRect.content == null ||
            styleSourceButton == null)
        {
            Debug.LogError("Save slot menu references are incomplete.", this);
            return false;
        }

        var buttons = new List<Button>();
        var texts = new List<TextMeshProUGUI>();
        var numbers = new List<int>();
        if (!FindSlot("AutoSave", GameSaveSystem.AutoSaveSlotNumber, buttons, texts, numbers))
            return false;

        for (int slotNumber = 1; slotNumber <= GameSaveSystem.ManualSaveSlotCount; slotNumber++)
        {
            if (!FindSlot("SaveSlot_" + slotNumber.ToString("00"), slotNumber,
                buttons, texts, numbers))
                return false;
        }

        slotButtons.AddRange(buttons);
        slotTexts.AddRange(texts);
        slotNumbers.AddRange(numbers);
        for (int i = 0; i < slotButtons.Count; i++)
        {
            int capturedSlotNumber = slotNumbers[i];
            Navigation navigation = slotButtons[i].navigation;
            navigation.mode = Navigation.Mode.None;
            slotButtons[i].navigation = navigation;
            slotButtons[i].colors = styleSourceButton.colors;
            slotButtons[i].onClick.AddListener(() => ConfirmSlot(capturedSlotNumber));
        }

        initialized = true;
        return true;
    }

    private bool FindSlot(string objectName, int slotNumber, List<Button> buttons,
        List<TextMeshProUGUI> texts, List<int> numbers)
    {
        Transform slot = scrollRect.content.Find(objectName);
        Button button = slot == null ? null : slot.GetComponent<Button>();
        Transform textTransform = slot == null ? null : slot.Find("SlotText");
        TextMeshProUGUI slotText = textTransform == null
            ? null
            : textTransform.GetComponent<TextMeshProUGUI>();
        if (button == null || slotText == null)
        {
            Debug.LogError("Save slot UI is incomplete: " + objectName, this);
            return false;
        }

        buttons.Add(button);
        texts.Add(slotText);
        numbers.Add(slotNumber);
        return true;
    }

    private void RefreshSlots()
    {
        BattleData data;
        try
        {
            data = BattleData.Load();
        }
        catch (Exception exception)
        {
            Debug.LogError("Cannot load save slot data: " + exception.Message, this);
            SetAllSlotsUnavailable();
            return;
        }

        RefreshSlot(0, GameSaveSystem.HasAutoSave, data);
        for (int i = 1; i < slotButtons.Count; i++)
            RefreshSlot(i, GameSaveSystem.HasManualSave(slotNumbers[i]), data);
    }

    private void RefreshSlot(int index, bool exists, BattleData data)
    {
        int slotNumber = slotNumbers[index];
        string title = GetSlotTitle(slotNumber);
        bool canWriteSlot = mode == SaveSlotMode.Save &&
            slotNumber != GameSaveSystem.AutoSaveSlotNumber;

        if (!exists)
        {
            slotButtons[index].interactable = canWriteSlot;
            slotTexts[index].text = FormatEmptySlot(title, "空");
            return;
        }

        try
        {
            GameSaveData save = slotNumber == GameSaveSystem.AutoSaveSlotNumber
                ? GameSaveSystem.LoadAutoSave(data)
                : GameSaveSystem.LoadManualSave(data, slotNumber);
            slotButtons[index].interactable = mode == SaveSlotMode.Load || canWriteSlot;
            slotTexts[index].text = FormatOccupiedSlot(title, save);
        }
        catch (Exception exception)
        {
            slotButtons[index].interactable = canWriteSlot;
            slotTexts[index].text = FormatEmptySlot(title, "无法读取");
            Debug.LogWarning("Cannot read " + title + ": " + exception.Message, this);
        }
    }

    private void SetAllSlotsUnavailable()
    {
        for (int i = 0; i < slotButtons.Count; i++)
        {
            slotButtons[i].interactable = false;
            slotTexts[i].text = FormatEmptySlot(GetSlotTitle(slotNumbers[i]), "无法读取");
        }
    }

    private static string FormatOccupiedSlot(string title, GameSaveData save)
    {
        DateTime savedAt = GameSaveSystem.GetSavedAtLocal(save);
        BattlePartyMember mainCharacter = save.players[0];
        return "<size=32>" + title + "</size>\n<size=24>" +
            savedAt.ToString("yyyy/MM/dd HH:mm") + "　主角 Lv." + mainCharacter.level +
            "　" + save.exploration.sceneName + "</size>";
    }

    private static string FormatEmptySlot(string title, string state)
    {
        return "<size=32>" + title + "</size>\n<size=24>" + state + "</size>";
    }

    private static string GetSlotTitle(int slotNumber)
    {
        return slotNumber == GameSaveSystem.AutoSaveSlotNumber
            ? "Auto_Save"
            : "Save_" + slotNumber.ToString("00");
    }

    private void SelectInitialSlot(int preferredSlotNumber)
    {
        int preferredIndex = slotNumbers.IndexOf(preferredSlotNumber);
        if (preferredIndex >= 0 && slotButtons[preferredIndex].IsInteractable())
        {
            SelectSlot(preferredIndex);
            return;
        }

        selectedIndex = -1;
        MoveSelection(1);
    }

    private void MoveSelection(int direction)
    {
        if (slotButtons.Count == 0)
            return;

        for (int step = 1; step <= slotButtons.Count; step++)
        {
            int nextIndex = (selectedIndex + direction * step) % slotButtons.Count;
            if (nextIndex < 0)
                nextIndex += slotButtons.Count;
            if (!slotButtons[nextIndex].IsInteractable())
                continue;

            SelectSlot(nextIndex);
            return;
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void SelectSlot(int index)
    {
        selectedIndex = index;
        MenuInput.Focus(slotButtons[index]);
        Canvas.ForceUpdateCanvases();
        KeepVisible((RectTransform)slotButtons[index].transform);
    }

    private void KeepVisible(RectTransform selected)
    {
        RectTransform viewport = scrollRect.viewport;
        if (viewport == null)
            return;

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected);
        Rect viewRect = viewport.rect;
        Vector2 position = scrollRect.content.anchoredPosition;
        if (bounds.max.y > viewRect.yMax)
            position.y -= bounds.max.y - viewRect.yMax;
        else if (bounds.min.y < viewRect.yMin)
            position.y -= bounds.min.y - viewRect.yMin;

        scrollRect.StopMovement();
        scrollRect.content.anchoredPosition = position;
    }

    private void ConfirmSlot(int slotNumber)
    {
        if (!MenuInput.CanRead(this) || MenuInput.BackPressed || MenuInput.EscapePressed)
            return;
        MenuInput.BlockFrame();
        int index = slotNumbers.IndexOf(slotNumber);
        if (index < 0 || !slotButtons[index].IsInteractable())
            return;

        slotConfirmed?.Invoke(slotNumber);
    }
}
