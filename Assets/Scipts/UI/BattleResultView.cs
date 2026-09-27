using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Battle
{
    public class BattleResultView : MonoBehaviour
    {
        [SerializeField] private BattleController battleController;
        [SerializeField] private BattleTransitionView transitionView;
        [SerializeField] private Image settlementBackground;
        [SerializeField] private GameObject settlementPanel;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button returnMenuButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private GameObject expPage;
        [SerializeField] private GameObject itemPage;
        [SerializeField] private TextMeshProUGUI expClaimText;
        [SerializeField] private ScrollRect experienceScrollRect;
        [SerializeField] private TextMeshProUGUI characterExperiencePrefab;
        [SerializeField, Min(0)] private float experienceAnimationSeconds = 2f;
        [SerializeField] private TextMeshProUGUI itemClaimText;
        [SerializeField] private TextMeshProUGUI goldReceivedText;
        [SerializeField] private RectTransform itemContent;
        [SerializeField] private GameObject itemRewardPrefab;
        [SerializeField] private string victoryTitle = "Victory";
        [SerializeField] private string defeatTitle = "Defeat";
        [SerializeField] private string menuSceneName = "Title_scene";

        private BattleState battleResult;
        private bool hasResult;
        private bool isLeaving;
        private bool showingOutcome;
        private bool transitionBusy;
        private Color settlementBackgroundColor;
        private Coroutine experienceAnimation;
        private BattleData settlementData;
        private readonly List<GameObject> itemRows = new List<GameObject>();
        private readonly List<TextMeshProUGUI> experienceRows = new List<TextMeshProUGUI>();
        private Navigation nextButtonNavigation;

        private void Awake()
        {
            nextButtonNavigation = nextButton.navigation;
            if (settlementBackground != null)
                settlementBackgroundColor = settlementBackground.color;
            settlementPanel.SetActive(false);
            resultPanel.SetActive(false);
            expPage.SetActive(false);
            itemPage.SetActive(false);

            nextButton.onClick.AddListener(ShowNextSettlementPage);
            retryButton.onClick.AddListener(RetryBattle);
            returnMenuButton.onClick.AddListener(ReturnToMenu);
            continueButton.onClick.AddListener(Continue);
        }

        private void OnEnable()
        {
            battleController.BattleEnded += ShowSettlement;
            bool resumeOutcome = hasResult && showingOutcome;
            ShowSettlement(battleController.CurrentState);
            if (hasResult)
                MenuInput.Activate(this, resultPanel.activeSelf
                    ? (battleResult == BattleState.Victory ? continueButton : retryButton)
                    : nextButton);
            if (resumeOutcome)
                BeginOutcomeIntro();
            else if (hasResult && !showingOutcome && transitionView != null)
                transitionView.ShowDocked(battleResult == BattleState.Victory ? victoryTitle : defeatTitle);
            if (hasResult && expPage.activeSelf && settlementData != null && experienceAnimation == null)
            {
                SetExperienceNavigation();
                UpdateExperienceDisplay(GetReceivedExperience());
            }
        }

        private void OnDisable()
        {
            if (nextButton != null)
                nextButton.navigation = nextButtonNavigation;
            MenuInput.Release(this);
            if (battleController != null)
                battleController.BattleEnded -= ShowSettlement;
            StopAllCoroutines();
            experienceAnimation = null;
            transitionBusy = false;
            if (transitionView != null)
                transitionView.Hide();
        }

        private void OnDestroy()
        {
            if (nextButton != null) nextButton.onClick.RemoveListener(ShowNextSettlementPage);
            if (retryButton != null) retryButton.onClick.RemoveListener(RetryBattle);
            if (returnMenuButton != null) returnMenuButton.onClick.RemoveListener(ReturnToMenu);
            if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
        }

        private void ShowSettlement(BattleState result)
        {
            if (hasResult || (result != BattleState.Victory && result != BattleState.Defeat))
            {
                return;
            }

            hasResult = true;
            MenuInput.Activate(this);
            battleResult = result;
            if (result == BattleState.Victory)
            {
                try
                {
                    GameSaveSystem.SaveVictory(BattleSession.Result);
                }
                catch (System.Exception exception)
                {
                    Debug.LogError("Cannot save victory progress: " + exception.Message, this);
                }
            }

            resultPanel.SetActive(false);
            settlementPanel.transform.SetAsLastSibling();
            settlementPanel.SetActive(true);
            if (transitionView != null)
            {
                BeginOutcomeIntro();
                return;
            }
            ShowExperiencePage();
            FocusButton(nextButton);
        }

        private void ShowNextSettlementPage()
        {
            if (!MenuInput.CanRead(this) || !hasResult || !settlementPanel.activeInHierarchy || isLeaving || transitionBusy)
                return;
            MenuInput.BlockFrame();

            if (showingOutcome)
            {
                transitionBusy = true;
                nextButton.interactable = false;
                StartCoroutine(OpenSettlement());
                return;
            }

            if (expPage.activeSelf)
            {
                if (experienceAnimation != null)
                {
                    StopCoroutine(experienceAnimation);
                    experienceAnimation = null;
                    UpdateExperienceDisplay(GetReceivedExperience());
                    return;
                }
                ShowItemPage();
            }
            else
                ShowResult();
        }

        private void BeginOutcomeIntro()
        {
            showingOutcome = true;
            transitionBusy = true;
            expPage.SetActive(false);
            itemPage.SetActive(false);
            nextButton.interactable = false;
            if (settlementBackground != null)
            {
                Color color = settlementBackgroundColor;
                color.a = 0f;
                settlementBackground.color = color;
            }
            StartCoroutine(RevealOutcome());
        }

        private IEnumerator RevealOutcome()
        {
            yield return transitionView.PlayEnd(battleResult == BattleState.Victory ? victoryTitle : defeatTitle);
            transitionBusy = false;
            nextButton.interactable = true;
            FocusButton(nextButton);
        }

        private IEnumerator OpenSettlement()
        {
            yield return transitionView.MoveToDock();
            if (settlementBackground != null)
                settlementBackground.color = settlementBackgroundColor;
            showingOutcome = false;
            transitionBusy = false;
            nextButton.interactable = true;
            if (battleResult == BattleState.Victory)
            {
                ShowExperiencePage();
                FocusButton(nextButton);
            }
            else
                ShowResult();
        }

        private void ShowExperiencePage()
        {
            BattleOutcome outcome = BattleSession.Result;
            int experience = outcome != null && outcome.rewards != null
                ? outcome.rewards.experiencePerCharacter
                : 0;
            expClaimText.text = "EXP received: " + experience;

            foreach (TextMeshProUGUI row in experienceRows)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            experienceRows.Clear();

            if (outcome != null && outcome.players != null)
            {
                settlementData = BattleData.Load();
                for (int i = 0; i < outcome.players.Length; i++)
                {
                    TextMeshProUGUI row = Instantiate(characterExperiencePrefab, experienceScrollRect.content);
                    row.richText = true;
                    experienceRows.Add(row);
                }
            }

            itemPage.SetActive(false);
            expPage.SetActive(true);
            SetExperienceNavigation();
            Canvas.ForceUpdateCanvases();
            experienceScrollRect.StopMovement();
            experienceScrollRect.verticalNormalizedPosition = 1f;
            UpdateExperienceDisplay(experience);
            if (experience > 0 && experienceAnimationSeconds > 0)
                experienceAnimation = StartCoroutine(AnimateExperience());
        }

        private int GetReceivedExperience()
        {
            return BattleSession.Result?.rewards?.experiencePerCharacter ?? 0;
        }

        private IEnumerator AnimateExperience()
        {
            int total = GetReceivedExperience();
            float elapsed = 0;
            UpdateExperienceDisplay(0);
            while (elapsed < experienceAnimationSeconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                int shown = (int)(total * (double)Mathf.Clamp01(elapsed / experienceAnimationSeconds));
                UpdateExperienceDisplay(shown);
            }
            UpdateExperienceDisplay(total);
            experienceAnimation = null;
        }

        private void UpdateExperienceDisplay(int received)
        {
            BattleOutcome outcome = BattleSession.Result;
            expClaimText.text = "EXP received: " + received;
            if (outcome?.players == null || settlementData == null)
                return;

            int rows = Mathf.Min(outcome.players.Length, experienceRows.Count);
            for (int i = 0; i < rows; i++)
            {
                BattlePartyMember player = outcome.players[i];
                CharacterExperienceReward change = System.Array.Find(
                    outcome.rewards?.characters ?? new CharacterExperienceReward[0],
                    entry => entry != null && entry.characterId == player.characterId);
                experienceRows[i].text = CharacterExperienceText.Format(settlementData, player, change, received);
            }
        }

        private void SetExperienceNavigation()
        {
            nextButton.navigation = new Navigation { mode = Navigation.Mode.None };
            if (experienceScrollRect.verticalScrollbar != null)
                experienceScrollRect.verticalScrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        private void Update()
        {
            if (!hasResult || isLeaving || transitionBusy || showingOutcome || !expPage.activeInHierarchy ||
                !MenuInput.CanRead(this) || MenuInput.BackPressed || MenuInput.EscapePressed)
                return;
            int direction = MenuInput.VerticalStep;
            if (direction == 0 || experienceRows.Count == 0)
                return;
            float overflow = experienceScrollRect.content.rect.height - experienceScrollRect.viewport.rect.height;
            if (overflow <= 0f)
                return;
            float step = experienceRows[0].rectTransform.rect.height;
            var layout = experienceScrollRect.content.GetComponent<VerticalLayoutGroup>();
            if (layout != null) step += layout.spacing;
            experienceScrollRect.StopMovement();
            experienceScrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                experienceScrollRect.verticalNormalizedPosition - direction * step / overflow);
        }

        private void ShowItemPage()
        {
            nextButton.navigation = nextButtonNavigation;
            expPage.SetActive(false);
            itemPage.SetActive(true);

            foreach (GameObject row in itemRows)
                Destroy(row);
            itemRows.Clear();

            BattleRewardResult rewards = BattleSession.Result == null
                ? null
                : BattleSession.Result.rewards;
            BattleItemAmount[] items = rewards == null ? null : rewards.items;
            int totalCount = 0;
            if (items != null)
            {
                BattleData data = BattleData.Load();
                foreach (BattleItemAmount item in items)
                {
                    if (item == null || item.count <= 0)
                        continue;

                    BattleActionDefinition definition = data.FindItem(item.itemId);
                    GameObject row = Instantiate(itemRewardPrefab, itemContent);
                    row.transform.Find("ItemNameText").GetComponent<TextMeshProUGUI>().text =
                        definition == null ? item.itemId : definition.name;
                    row.transform.Find("AmountText").GetComponent<TextMeshProUGUI>().text =
                        "X " + item.count.ToString("00");
                    itemRows.Add(row);
                    totalCount += item.count;
                }
            }

            itemClaimText.text = "Items received: " + totalCount;
            goldReceivedText.text = "Gold received: " + (rewards == null ? 0 : rewards.gold);
        }

        private void ShowResult()
        {
            if (!hasResult || !settlementPanel.activeInHierarchy || isLeaving)
            {
                return;
            }

            bool isVictory = battleResult == BattleState.Victory;
            nextButton.navigation = nextButtonNavigation;
            resultTitleText.text = isVictory ? victoryTitle : defeatTitle;
            resultTitleText.gameObject.SetActive(transitionView == null);
            retryButton.gameObject.SetActive(!isVictory);
            returnMenuButton.gameObject.SetActive(!isVictory);
            continueButton.gameObject.SetActive(isVictory);
            continueButton.interactable = !string.IsNullOrWhiteSpace(battleController.ReturnScene);

            settlementPanel.SetActive(false);
            resultPanel.transform.SetAsLastSibling();
            resultPanel.SetActive(true);
            if (transitionView != null)
                transitionView.BringToFront();
            FocusButton(isVictory ? continueButton : retryButton);
        }

        private void RetryBattle()
        {
            if (battleResult == BattleState.Defeat)
            {
                LoadScene(SceneManager.GetActiveScene().name, true);
            }
        }

        private void ReturnToMenu()
        {
            if (battleResult == BattleState.Defeat)
            {
                LoadScene(menuSceneName);
            }
        }

        private void Continue()
        {
            if (battleResult == BattleState.Victory && continueButton.interactable)
            {
                LoadScene(battleController.ReturnScene);
            }
        }

        private void LoadScene(string sceneName, bool retry = false)
        {
            if (!hasResult || !resultPanel.activeInHierarchy || isLeaving)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("Result scene is not available in the build scene list: " + sceneName, this);
                return;
            }

            isLeaving = true;
            MenuInput.Release(this);
            if (!retry)
                BattleSession.LeaveBattle();
            SceneManager.LoadScene(sceneName);
        }

        private void FocusButton(Button button)
        {
            if (!isLeaving)
                MenuInput.Activate(this, button);
        }
    }
}
