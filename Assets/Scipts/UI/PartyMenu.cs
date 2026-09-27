using System;
using Game.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Exploration
{
    public class PartyMenu : MonoBehaviour
    {
        [Serializable]
        private class Slot
        {
            public Button button;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI detailsText;
        }

        [SerializeField] private Slot[] slots;
        [SerializeField] private Button styleSourceButton;
        [SerializeField] private Color pendingColor = new Color(1f, 0.85f, 0.4f);
        private Action closed;
        private BattleData data;
        private string[] partyIds;
        private int selected;
        private int pending = -1;
        private bool initialized;
        public bool IsOpen => gameObject.activeSelf;

        public bool Open(Action onClosed)
        {
            try
            {
                if (!initialized)
                {
                    if (slots == null || slots.Length != 3 || styleSourceButton == null)
                        throw new InvalidOperationException("Party menu references are incomplete.");
                    foreach (Slot slot in slots)
                        if (slot == null || slot.button == null || slot.nameText == null)
                            throw new InvalidOperationException("Party slot references are incomplete.");
                    for (int i = 0; i < slots.Length; i++)
                    {
                        int index = i;
                        slots[i].button.navigation = new Navigation { mode = Navigation.Mode.None };
                        slots[i].button.onClick.AddListener(() => Confirm(index));
                    }
                    initialized = true;
                }
                data = BattleData.Load();
                selected = 0;
                pending = -1;
                Refresh();
                closed = onClosed;
                gameObject.SetActive(true);
                MenuInput.Activate(this);
                Focus();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Cannot open party menu: " + exception.Message, this);
                CloseImmediately();
                return false;
            }
        }

        private void Update()
        {
            if (!MenuInput.CanRead(this) || MenuInput.EscapePressed)
                return;
            if (MenuInput.BackPressed)
            {
                MenuInput.BlockFrame();
                if (pending >= 0)
                {
                    pending = -1;
                    Refresh();
                    Focus();
                }
                else
                {
                    Action callback = closed;
                    CloseImmediately();
                    callback?.Invoke();
                }
                return;
            }
            int direction = MenuInput.HorizontalStep;
            if (direction == 0)
                return;
            selected = (selected + direction + partyIds.Length) % partyIds.Length;
            Focus();
        }

        // Z uses the existing EventSystem submit binding, shared with other menus.
        private void Confirm(int index)
        {
            if (!IsOpen || !MenuInput.CanRead(this) || MenuInput.BackPressed ||
                MenuInput.EscapePressed || index >= partyIds.Length)
                return;
            MenuInput.BlockFrame();
            selected = index;
            if (pending < 0)
                pending = index;
            else
            {
                string[] next = (string[])partyIds.Clone();
                string first = next[pending];
                next[pending] = next[index];
                next[index] = first;
                GameRuntimeState.SetActiveParty(next);
                pending = -1;
            }
            Refresh();
            Focus();
        }

        private void Refresh()
        {
            GameSaveData snapshot = GameRuntimeState.Snapshot();
            var party = snapshot.CreateActiveParty(data);
            partyIds = snapshot.activePartyIds;
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                slot.button.interactable = i < party.Count;
                ColorBlock colors = styleSourceButton.colors;
                if (i == pending)
                    colors.normalColor = colors.highlightedColor = colors.selectedColor = pendingColor;
                slot.button.colors = colors;
                slot.nameText.text = i < party.Count ? $"LV{party[i].Level} {party[i].Name}" : "\u7a7a\u4f4d";
                if (slot.detailsText == null)
                    continue;
                if (i >= party.Count)
                {
                    slot.detailsText.text = string.Empty;
                    continue;
                }
                Combatant member = party[i];
                string experience = member.IsMaxLevel
                    ? "MAX (\u5df2\u6ee1\u7ea7)"
                    : $"{member.Experience}/{member.ExperienceRequiredForNextLevel} (Next LV {member.Level + 1})";
                slot.detailsText.richText = true;
                slot.detailsText.text = $"EXP<pos=90>\uFF1A{experience}\n" +
                    $"HP<pos=90>\uFF1A{member.Health:0}/{member.MaxHealth:0}\n" +
                    $"MP<pos=90>\uFF1A{member.Mana:0}/{member.MaxMana:0}";
            }
        }

        private void Focus()
        {
            MenuInput.Focus(slots[selected].button);
        }

        public void CloseImmediately()
        {
            MenuInput.Release(this);
            pending = -1;
            closed = null;
            gameObject.SetActive(false);
        }

        private void OnDisable() => MenuInput.Release(this);
    }
}
