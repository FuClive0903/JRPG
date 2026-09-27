using System;
using Game.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Exploration
{
    public class ExplorationEncounterController : MonoBehaviour
    {
        [SerializeField] private ExplorationSceneController sceneController;
        [SerializeField] private ExplorationPlayerController playerController;
        [SerializeField] private ExplorationStepCounter stepCounter;
        [SerializeField] private bool allowRandomEncounters = true;
        [SerializeField] private string battleSceneName = "Battle_scene";
        [SerializeField] private GameObject battleEnvironmentPrefab;
        [SerializeField, Min(0)] private int safeSteps;
        [SerializeField, Range(0f, 1f)] private float encounterChancePerStep;
        [SerializeField] private bool encounterTriggered;
        [SerializeField, Min(0)] private int lastCheckedStep;
        [SerializeField] private float lastRoll = -1f;

        private BattleData battleData;

        public bool EncounterTriggered { get { return encounterTriggered; } }
        public event Action EncounterStarted;

        private void Awake()
        {
            if (sceneController == null || playerController == null || stepCounter == null)
            {
                Debug.LogError("Exploration encounter references are incomplete.", this);
                enabled = false;
                return;
            }

            try
            {
                battleData = BattleData.Load();
                ExplorationEncounterRules rules = battleData.encounterRules;
                safeSteps = rules.safeSteps;
                encounterChancePerStep = rules.encounterChancePerStep;
                stepCounter.SetDistancePerStep(rules.distancePerStep);
            }
            catch (Exception exception)
            {
                Debug.LogError("Cannot load exploration encounter rules: " + exception.Message, this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (stepCounter != null)
                stepCounter.StepCompleted += CheckEncounter;
        }

        private void OnDisable()
        {
            if (stepCounter != null)
                stepCounter.StepCompleted -= CheckEncounter;
        }

        public void ResetEncounter()
        {
            encounterTriggered = false;
            lastCheckedStep = 0;
            lastRoll = -1f;
            stepCounter.ResetSteps();
            playerController.SetInputEnabled(true);
        }

        private void CheckEncounter(int completedSteps)
        {
            if (!allowRandomEncounters || encounterTriggered || completedSteps <= safeSteps)
                return;

            lastCheckedStep = completedSteps;
            lastRoll = UnityEngine.Random.value;
            if (lastRoll >= encounterChancePerStep)
                return;

            encounterTriggered = true;
            playerController.SetInputEnabled(false);
            Debug.Log("Random encounter triggered at step " + completedSteps + ".", this);
            EnterBattle();
        }

        private void EnterBattle()
        {
            bool sessionPrepared = false;
            try
            {
                if (string.IsNullOrWhiteSpace(battleSceneName) ||
                    !Application.CanStreamedLevelBeLoaded(battleSceneName))
                {
                    throw new InvalidOperationException(
                        "Battle scene is not available in the build scene list: " + battleSceneName);
                }

                if (!sceneController.SaveCurrentPosition())
                    throw new InvalidOperationException("Cannot save the exploration position.");

                GameSaveData save = GameRuntimeState.Snapshot();
                BattleSession.Prepare(save.CreateActiveParty(battleData), battleData.defaultEnemyParty,
                    SceneManager.GetActiveScene().name, save.items, save.gold, save.CreatePlayers(battleData),
                    battleEnvironmentPrefab);
                sessionPrepared = true;
                stepCounter.ResetSteps();
                EncounterStarted?.Invoke();
                SceneManager.LoadScene(battleSceneName);
            }
            catch (Exception exception)
            {
                if (sessionPrepared)
                    BattleSession.LeaveBattle();
                encounterTriggered = false;
                playerController.SetInputEnabled(true);
                Debug.LogError("Cannot enter random battle: " + exception.Message, this);
            }
        }
    }
}
