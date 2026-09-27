using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Battle
{
    public class BattleHud : MonoBehaviour
    {
        private enum MenuMode { Commands, Skills, Items }

        [SerializeField] private BattleController battleController;
        [SerializeField] private TextMeshProUGUI selectedNameText;
        [SerializeField] private TextMeshProUGUI selectedHpText;
        [SerializeField] private TextMeshProUGUI selectedMpText;
        [SerializeField] private Image selectedHpFill;
        [SerializeField] private Image selectedMpFill;
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI actionTimelineText;
        [SerializeField] private Button attackButton;
        [SerializeField] private Button defendButton;
        [SerializeField] private Button skillButton;
        [SerializeField] private Button itemButton;
        [SerializeField] private Button previousTargetButton;
        [SerializeField] private Button nextTargetButton;
        [SerializeField] private Transform statusIconContainer;
        [SerializeField] private StatusIconView statusIconPrefab;

        private Button[] menuButtons;
        private List<BattleActionDefinition> menuActions = new List<BattleActionDefinition>();
        private MenuMode menuMode;
        private BattleState previousState = BattleState.Start;
        private int commandIndex;
        private int skillIndex;
        private int itemIndex;
        private Combatant statusCombatant;
        private string statusSignature;

        private void Start()
        {
            menuButtons = new[] { attackButton, defendButton, skillButton, itemButton };
            for (int i = 0; i < menuButtons.Length; i++)
            {
                int buttonIndex = i;
                menuButtons[i].onClick.AddListener(() => OnMenuButtonPressed(buttonIndex));
            }

            if (previousTargetButton != null)
            {
                previousTargetButton.onClick.AddListener(() =>
                {
                    battleController.OnPreviousTargetButtonPressed();
                    ClearFocus();
                });
            }
            if (nextTargetButton != null)
            {
                nextTargetButton.onClick.AddListener(() =>
                {
                    battleController.OnNextTargetButtonPressed();
                    ClearFocus();
                });
            }

            ShowCommandMenu(-1);
        }

        private void Update()
        {
            if (battleController.IsInputBlocked) return;
            BattleState state = battleController.CurrentState;
            if ((state == BattleState.ResolvingAction || state == BattleState.EnemyTurn) &&
                menuMode != MenuMode.Commands)
                ShowCommandMenu(-1);

            HandleMenuCancel(state);
            RefreshHud();
            RefreshKeyboardFocus(state);
            previousState = state;
        }

        private void OnMenuButtonPressed(int index)
        {
            if (battleController.IsInputBlocked || MenuInput.FrameBlocked || MenuInput.BackPressed || MenuInput.EscapePressed) return;
            if (battleController.CurrentState != BattleState.WaitingForCommand)
                return;

            if (menuMode == MenuMode.Commands)
            {
                commandIndex = index;
                if (index == 0)
                    battleController.OnAttackButtonPressed();
                else if (index == 1)
                    battleController.OnDefendButtonPressed();
                else if (index == 2)
                    ShowActionMenu(MenuMode.Skills, battleController.GetCurrentSkills());
                else
                    ShowActionMenu(MenuMode.Items, battleController.GetItems());
                return;
            }

            if (index == 3)
            {
                ReturnToCommandMenu();
                return;
            }

            if (index >= menuActions.Count)
                return;

            if (menuMode == MenuMode.Skills)
            {
                skillIndex = index;
                battleController.OnSkillSelected(menuActions[index]);
            }
            else
            {
                itemIndex = index;
                battleController.OnItemSelected(menuActions[index]);
            }
        }

        private void ShowCommandMenu(int focusIndex)
        {
            if (focusIndex >= 0) MenuInput.BlockFrame();
            menuMode = MenuMode.Commands;
            menuActions.Clear();
            SetButton(0, "Attack", true);
            SetButton(1, "Defend", true);
            SetButton(2, "Skill", true);
            SetButton(3, "Item", true);
            if (focusIndex >= 0)
            {
                commandIndex = focusIndex;
                MenuInput.Focus(menuButtons[commandIndex]);
            }
        }

        private void ShowActionMenu(MenuMode mode, List<BattleActionDefinition> actions)
        {
            MenuInput.BlockFrame();
            menuMode = mode;
            menuActions = actions;
            RefreshMenuButtons();
            SelectRememberedAction();
        }

        private void RefreshMenuButtons()
        {
            if (menuMode == MenuMode.Commands)
                return;

            for (int i = 0; i < 3; i++)
            {
                if (i >= menuActions.Count)
                {
                    SetButton(i, "-", false);
                    continue;
                }

                BattleActionDefinition action = menuActions[i];
                if (menuMode == MenuMode.Skills)
                {
                    bool usable = battleController.CurrentCombatant.Mana >= action.manaCost;
                    SetButton(i, action.name + " " + action.manaCost + " MP", usable);
                }
                else
                {
                    int count = battleController.GetItemCount(action.id);
                    SetButton(i, action.name + " x" + count, count > 0);
                }
            }
            SetButton(3, "Back", true);
        }

        private void SetButton(int index, string label, bool interactable)
        {
            menuButtons[index].GetComponentInChildren<TextMeshProUGUI>().text = label;
            menuButtons[index].interactable = interactable;
        }

        private void SelectRememberedAction()
        {
            int rememberedIndex = menuMode == MenuMode.Skills ? skillIndex : itemIndex;
            if (rememberedIndex < 3 && rememberedIndex < menuActions.Count &&
                menuButtons[rememberedIndex].interactable)
            {
                MenuInput.Focus(menuButtons[rememberedIndex]);
                return;
            }

            foreach (Button button in menuButtons)
            {
                if (button.interactable)
                {
                    MenuInput.Focus(button);
                    return;
                }
            }
        }

        private void HandleMenuCancel(BattleState state)
        {
            if (state != BattleState.WaitingForCommand || previousState != BattleState.WaitingForCommand ||
                menuMode == MenuMode.Commands)
                return;

            bool cancel = !MenuInput.FrameBlocked && MenuInput.BackPressed;
            if (cancel)
                ReturnToCommandMenu();
        }

        private void ReturnToCommandMenu()
        {
            int parentIndex = menuMode == MenuMode.Skills ? 2 : 3;
            ShowCommandMenu(parentIndex);
        }

        private void RefreshHud()
        {
            RefreshTurnText();
            RefreshSelectedCharacterInfo();
            RefreshCommandButtons();
            RefreshActionTimeline();
            RefreshTargetNavigationButtons();
        }

        private void RefreshTurnText()
        {
            turnText.text = battleController.CurrentCombatant == null
                ? " "
                : battleController.CurrentCombatant.Name + "'s Turn";
        }

        private void RefreshSelectedCharacterInfo()
        {
            Combatant displayCombatant = battleController.CurrentState == BattleState.SelectingTarget
                ? battleController.SelectedTarget
                : battleController.CurrentCombatant;

            if (displayCombatant == null)
            {
                List<Combatant> upcoming = battleController.GetUpcomingCombatants(1);
                if (upcoming.Count > 0)
                    displayCombatant = upcoming[0];
            }

            if (displayCombatant == null)
            {
                selectedNameText.text = "??";
                selectedHpText.text = "??";
                selectedMpText.text = "??";
                SetResourceBar(selectedHpFill, 0f, 1f);
                SetResourceBar(selectedMpFill, 0f, 1f);
                return;
            }

            selectedNameText.text = displayCombatant.Name + "  Lv." + displayCombatant.Level;
            selectedHpText.text = "HP  " + displayCombatant.Health + " / " + displayCombatant.MaxHealth;
            selectedMpText.text = "MP  " + displayCombatant.Mana + " / " + displayCombatant.MaxMana;
            SetResourceBar(selectedHpFill, displayCombatant.Health, displayCombatant.MaxHealth);
            SetResourceBar(selectedMpFill, displayCombatant.Mana, displayCombatant.MaxMana);
            RefreshStatusIcons(displayCombatant);
        }

        private static void SetResourceBar(Image fillImage, float currentValue, float maximumValue)
        {
            if (fillImage == null)
                return;

            fillImage.fillAmount = maximumValue > 0f
                ? Mathf.Clamp01(currentValue / maximumValue)
                : 0f;
        }

        private void RefreshStatusIcons(Combatant combatant)
        {
            string signature = "";
            foreach (ActiveStatus status in combatant.ActiveStatuses)
                signature += status.Definition.id + ":" + status.RemainingTurns + ";";

            if (statusCombatant == combatant && statusSignature == signature)
                return;

            statusCombatant = combatant;
            statusSignature = signature;
            for (int i = statusIconContainer.childCount - 1; i >= 0; i--)
            {
                statusIconContainer.GetChild(i).gameObject.SetActive(false);
                Destroy(statusIconContainer.GetChild(i).gameObject);
            }

            foreach (ActiveStatus status in combatant.ActiveStatuses)
                Instantiate(statusIconPrefab, statusIconContainer).Setup(status);
        }

        private void RefreshCommandButtons()
        {
            bool canChoose = battleController.CurrentState == BattleState.WaitingForCommand;
            if (!canChoose)
            {
                foreach (Button button in menuButtons)
                    button.interactable = false;
                return;
            }

            if (menuMode == MenuMode.Commands)
            {
                foreach (Button button in menuButtons)
                    button.interactable = true;
            }
            else
            {
                RefreshMenuButtons();
            }
        }

        private void RefreshActionTimeline()
        {
            List<Combatant> timeline = battleController.GetUpcomingCombatants(5);
            string text = "";
            for (int i = 0; i < timeline.Count; i++)
                text += (i + 1) + "." + timeline[i].Name + "\n";
            actionTimelineText.text = text;
        }

        private void RefreshTargetNavigationButtons()
        {
            bool selecting = battleController.CurrentState == BattleState.SelectingTarget;
            bool canChangeTarget = selecting && battleController.SelectableTargetCount > 1;
            if (previousTargetButton != null)
            {
                previousTargetButton.gameObject.SetActive(canChangeTarget);
                previousTargetButton.interactable = canChangeTarget;
            }
            if (nextTargetButton != null)
            {
                nextTargetButton.gameObject.SetActive(canChangeTarget);
                nextTargetButton.interactable = canChangeTarget;
            }
        }

        private void RefreshKeyboardFocus(BattleState state)
        {
            if (state == BattleState.WaitingForCommand && previousState != BattleState.WaitingForCommand)
            {
                if (previousState == BattleState.SelectingTarget)
                {
                    if (menuMode == MenuMode.Commands)
                        MenuInput.Focus(menuButtons[commandIndex]);
                    else
                        SelectRememberedAction();
                }
                else
                {
                    MenuInput.Focus(attackButton);
                }
            }
            else if (state == BattleState.WaitingForCommand && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == null)
            {
                if (menuMode == MenuMode.Commands)
                    MenuInput.Focus(menuButtons[commandIndex]);
                else
                    SelectRememberedAction();
            }
            else if (state == BattleState.SelectingTarget && previousState != BattleState.SelectingTarget)
                ClearFocus();
        }

        private void ClearFocus()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
