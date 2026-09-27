using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Battle
{
    public class BattleController : MonoBehaviour
    {
        private Combatant currentCombatant;
        private Combatant selectedTarget;
        private int selectedTargetIndex = -1;
        private BattleCommandType selectedCommand = BattleCommandType.None;
        private BattleActionDefinition selectedAction;
        private List<Combatant> selectableTargets = new List<Combatant>();
        private List<Combatant> playerParty = new List<Combatant>();
        private List<Combatant> enemyParty = new List<Combatant>();
        private BattleTurnOrder turnOrder = new BattleTurnOrder();
        private BattleData battleData;
        private int targetSelectionStartedFrame = -1;
        private int menuClosedFrame = -1;

        public bool IsMenuPaused { get; private set; }
        public bool IsInputBlocked => IsMenuPaused || Time.frameCount == menuClosedFrame;

        public void SetMenuPaused(bool paused)
        {
            IsMenuPaused = paused;
            if (!paused) menuClosedFrame = Time.frameCount;
        }

        // Drive nested playback one step at a time so even frame-only waits pause.
        private IEnumerator RunPlayback(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    if (IsMenuPaused) { yield return null; continue; }
                    IEnumerator current = stack.Peek();
                    if (!current.MoveNext())
                    {
                        (stack.Pop() as System.IDisposable)?.Dispose();
                        continue;
                    }
                    if (current.Current is IEnumerator nested)
                        stack.Push(nested);
                    else
                        yield return current.Current;
                }
            }
            finally
            {
                while (stack.Count > 0)
                    (stack.Pop() as System.IDisposable)?.Dispose();
            }
        }

        [SerializeField] private BattleFieldView battleFieldView;
        [SerializeField] private BattleTransitionView transitionView;

        public BattleState CurrentState { get; private set; }
        public Combatant CurrentCombatant { get { return currentCombatant; } }
        public Combatant SelectedTarget { get { return selectedTarget; } }
        public int SelectedTargetIndex { get { return selectedTargetIndex; } }
        public int SelectableTargetCount { get { return selectableTargets.Count; } }
        public string ReturnScene { get; private set; }
        public event System.Action<BattleState> BattleEnded;

        private void Start()
        {
            ChangeState(BattleState.Start);
            InitializeBattle();
        }

        private void Update()
        {
            if (IsInputBlocked) return;
            if (battleFieldView != null)
                battleFieldView.SetSelectedTarget(CurrentState == BattleState.SelectingTarget ? selectedTarget : null);

            if (CurrentState != BattleState.SelectingTarget)
                return;

            if (Keyboard.current != null && Keyboard.current.leftArrowKey.wasPressedThisFrame)
                SelectPreviousTarget();

            if (Keyboard.current != null && Keyboard.current.rightArrowKey.wasPressedThisFrame)
                SelectNextTarget();

            if (Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame &&
                Time.frameCount > targetSelectionStartedFrame)
                ConfirmSelectedTarget();

            if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
                CancelTargetSelection();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (transitionView != null)
                transitionView.Hide();
            ClearSelection();
            if (battleFieldView != null)
                battleFieldView.ResetPresentation();
            if (CurrentState != BattleState.Victory && CurrentState != BattleState.Defeat)
                ChangeState(BattleState.Paused);
        }

        public void ChangeState(BattleState newState)
        {
            if (CurrentState == newState || CurrentState == BattleState.Victory || CurrentState == BattleState.Defeat)
                return;

            CurrentState = newState;
            if (newState == BattleState.Victory || newState == BattleState.Defeat)
            {
                BattleRewardResult rewards = null;
                List<Combatant> resultParty = playerParty;
                if (newState == BattleState.Victory)
                {
                    resultParty = BattleSession.Entry.CreateRewardParty(battleData, playerParty);
                    rewards = BattleRewardCalculator.Calculate(battleData, enemyParty);
                    BattleRewardCalculator.GrantExperience(rewards, resultParty);
                }

                BattleSession.RecordResult(newState, resultParty, rewards);
                BattleEnded?.Invoke(newState);
            }
        }

        private void InitializeBattle()
        {
            try
            {
                battleData = BattleData.Load();
                if (BattleSession.Entry == null)
                {
                    BattleSession.Prepare(battleData.CreateParty(battleData.defaultPlayerParty, true),
                        battleData.defaultEnemyParty, "Title_scene", battleData.defaultInventory);
                }

                BattleEntry entry = BattleSession.Entry;
                playerParty = entry.CreatePlayers(battleData);
                enemyParty = battleData.CreateParty(entry.enemies, false);
                ReturnScene = entry.returnScene;
                BattleSession.BeginBattle();
                BattleSession.ClearResult();
            }
            catch (System.Exception exception)
            {
                ChangeState(BattleState.Paused);
                Debug.LogError("Battle initialization failed: " + exception.Message, this);
                enabled = false;
                return;
            }

            battleFieldView.Setup(playerParty, enemyParty, OnActorClicked);
            turnOrder.InitializeTurnTimes(GetAllCombatants());
            StartCoroutine(RunPlayback(BeginBattle()));
        }

        private IEnumerator BeginBattle()
        {
            if (transitionView != null)
                yield return transitionView.PlayStart();
            CheckBattleEnd();
            if (CurrentState != BattleState.Victory && CurrentState != BattleState.Defeat)
                BeginNextTurn();
        }

        private List<Combatant> GetAllCombatants()
        {
            List<Combatant> allCombatants = new List<Combatant>();
            allCombatants.AddRange(playerParty);
            allCombatants.AddRange(enemyParty);
            return allCombatants;
        }

        private void BeginNextTurn()
        {
            StartCoroutine(RunPlayback(ResolveTurnStart()));
        }

        private IEnumerator ResolveTurnStart()
        {
            ChangeState(BattleState.ResolvingAction);
            while (true)
            {
                // Yield between skipped turns to avoid recursion and same-frame input reuse.
                yield return null;
                currentCombatant = turnOrder.GetNextCombatant(GetAllCombatants());
                if (currentCombatant == null)
                    yield break;

                Debug.Log("Next turn: " + currentCombatant.Name);
                float healthBefore = currentCombatant.Health;
                bool hadPoison = currentCombatant.GetStatusTurns("poison") > 0;
                bool skipTurn = currentCombatant.ResolveTurnStartStatuses();
                if (currentCombatant.Health != healthBefore)
                {
                    BattleNumberStyle numberStyle = currentCombatant.Health > healthBefore
                        ? BattleNumberStyle.Healing
                        : hadPoison ? BattleNumberStyle.Poison : BattleNumberStyle.Damage;
                    yield return battleFieldView.PlayResults(new[] { currentCombatant },
                        new[] { healthBefore }, numberStyle);
                }
                else if (skipTurn)
                    yield return battleFieldView.PlayAction(currentCombatant, BattleCommandType.None, null);

                CheckBattleEnd();
                if (CurrentState == BattleState.Victory || CurrentState == BattleState.Defeat)
                    yield break;
                if (!currentCombatant.IsAlive())
                    continue;
                if (skipTurn)
                {
                    Debug.Log(currentCombatant.Name + " skips the turn.");
                    turnOrder.AdvanceTurn(currentCombatant);
                    currentCombatant.FinishStatusTurn();
                    continue;
                }

                if (currentCombatant.IsPlayer)
                    ChangeState(BattleState.WaitingForCommand);
                else
                {
                    ChangeState(BattleState.EnemyTurn);
                    HandleEnemyTurn();
                }
                yield break;
            }
        }

        private void EndCurrentTurn()
        {
            turnOrder.AdvanceTurn(currentCombatant);
            currentCombatant.FinishStatusTurn();
            BeginNextTurn();
        }

        private Combatant GetFirstAliveCombatant(List<Combatant> party)
        {
            return party.Find(character => character.IsAlive());
        }

        public void OnAttackButtonPressed()
        {
            if (IsInputBlocked) return;
            if (CurrentState != BattleState.WaitingForCommand)
                return;

            BeginTargetSelection(BattleCommandType.Attack, null,
                enemyParty.FindAll(character => character.IsAlive()));
        }

        public void OnDefendButtonPressed()
        {
            if (IsInputBlocked) return;
            if (CurrentState == BattleState.WaitingForCommand)
                StartAction(BattleCommandType.Defend, null, new List<Combatant> { currentCombatant });
        }

        public void OnSkillSelected(BattleActionDefinition skill)
        {
            if (IsInputBlocked) return;
            if (CurrentState != BattleState.WaitingForCommand || skill == null ||
                currentCombatant.Mana < skill.manaCost)
                return;

            ChooseActionTargets(BattleCommandType.Skill, skill);
        }

        public void OnItemSelected(BattleActionDefinition item)
        {
            if (IsInputBlocked) return;
            if (CurrentState != BattleState.WaitingForCommand || item == null ||
                BattleSession.GetItemCount(item.id) <= 0)
                return;

            ChooseActionTargets(BattleCommandType.Item, item);
        }

        private void ChooseActionTargets(BattleCommandType command, BattleActionDefinition action)
        {
            BattleTargetType targetType = action.GetTargetType();
            List<Combatant> targets = GetValidTargets(targetType);

            if (targetType == BattleTargetType.EnemyOne ||
                targetType == BattleTargetType.AllyOne ||
                targetType == BattleTargetType.Self ||
                targetType == BattleTargetType.DeadAlly)
            {
                BeginTargetSelection(command, action, targets);
            }
            else
            {
                StartAction(command, action, targets);
            }
        }

        private List<Combatant> GetValidTargets(BattleTargetType targetType)
        {
            if (targetType == BattleTargetType.Self)
                return new List<Combatant> { currentCombatant };
            if (targetType == BattleTargetType.EnemyOne || targetType == BattleTargetType.AllEnemies)
                return enemyParty.FindAll(character => character.IsAlive());
            if (targetType == BattleTargetType.DeadAlly)
                return playerParty.FindAll(character => !character.IsAlive());

            return playerParty.FindAll(character => character.IsAlive());
        }

        private void BeginTargetSelection(BattleCommandType command,
            BattleActionDefinition action, List<Combatant> targets)
        {
            if (targets.Count == 0)
                return;

            selectedCommand = command;
            selectedAction = action;
            selectableTargets = targets;
            selectedTargetIndex = 0;
            selectedTarget = selectableTargets[0];
            targetSelectionStartedFrame = Time.frameCount;
            ChangeState(BattleState.SelectingTarget);
        }

        private void StartAction(BattleCommandType command,
            BattleActionDefinition action, List<Combatant> targets)
        {
            if (CurrentState != BattleState.WaitingForCommand &&
                CurrentState != BattleState.SelectingTarget &&
                CurrentState != BattleState.EnemyTurn)
                return;
            if (currentCombatant == null || !currentCombatant.IsAlive() || targets.Count == 0)
                return;
            if (command == BattleCommandType.Skill && currentCombatant.Mana < action.manaCost)
                return;
            if (command == BattleCommandType.Item && BattleSession.GetItemCount(action.id) <= 0)
                return;

            ChangeState(BattleState.ResolvingAction);
            // Selection owns its list and clears it below; playback keeps its own targets.
            targets = new List<Combatant>(targets);
            ClearSelection();
            StartCoroutine(RunPlayback(ResolveAction(currentCombatant, command, action, targets)));
        }

        private IEnumerator ResolveAction(Combatant actor, BattleCommandType command,
            BattleActionDefinition action, List<Combatant> targets)
        {
            yield return null;

            if (command == BattleCommandType.Skill && !actor.SpendMana(action.manaCost))
            {
                ChangeState(BattleState.WaitingForCommand);
                yield break;
            }
            if (command == BattleCommandType.Item && !BattleSession.ConsumeItem(action.id))
            {
                ChangeState(BattleState.WaitingForCommand);
                yield break;
            }

            yield return battleFieldView.PlayAction(actor, command, action);
            List<float> healthBefore = targets.ConvertAll(target => target.Health);

            if (command == BattleCommandType.Attack)
            {
                float damage = targets[0].TakeDamage(actor.AttackPower);
                Debug.Log(actor.Name + " attacks " + targets[0].Name + " for " + damage);
            }
            else if (command == BattleCommandType.Defend)
            {
                actor.Defend();
                Debug.Log(actor.Name + " is defending.");
            }
            else
            {
                ApplyAction(actor, command, action, targets);
            }

            bool recoveryComplete = false;
            StartCoroutine(PlayActionRecovery(actor, () => recoveryComplete = true));
            yield return battleFieldView.PlayResults(targets, healthBefore,
                GetNumberStyle(command, action));
            while (!recoveryComplete)
                yield return null;

            CheckBattleEnd();
            if (CurrentState == BattleState.Victory || CurrentState == BattleState.Defeat)
                yield break;

            EndCurrentTurn();
        }

        private IEnumerator PlayActionRecovery(Combatant actor, System.Action onComplete)
        {
            yield return battleFieldView.PlayActionRecovery(actor);
            onComplete?.Invoke();
        }

        private void ApplyAction(Combatant actor, BattleCommandType command,
            BattleActionDefinition action, List<Combatant> targets)
        {
            BattleEffectType effect = action.GetEffectType();
            if (effect == BattleEffectType.ApplyStatus)
            {
                StatusDefinition status = battleData.FindStatus(action.statusId);
                if (status == null)
                    throw new System.InvalidOperationException("Unknown status: " + action.statusId);

                foreach (Combatant target in targets)
                    target.ApplyStatus(status, target == actor);

                Debug.Log(actor.Name + " uses " + action.name);
                return;
            }

            float amount = action.power;
            if (command == BattleCommandType.Skill)
                amount += actor.MagicPower;

            foreach (Combatant target in targets)
            {
                if (effect == BattleEffectType.Damage)
                    target.TakeDamage(amount);
                else if (effect == BattleEffectType.Heal)
                    target.RestoreHealth(amount);
                else if (effect == BattleEffectType.Revive)
                    target.Revive(amount);
            }

            Debug.Log(actor.Name + " uses " + action.name);
        }

        private BattleNumberStyle GetNumberStyle(BattleCommandType command,
            BattleActionDefinition action)
        {
            if ((command == BattleCommandType.Skill || command == BattleCommandType.Item) &&
                action != null)
            {
                BattleEffectType effect = action.GetEffectType();
                if (effect == BattleEffectType.Heal || effect == BattleEffectType.Revive)
                    return BattleNumberStyle.Healing;
            }
            return BattleNumberStyle.Damage;
        }

        private void HandleEnemyTurn()
        {
            List<Combatant> alivePlayers = playerParty.FindAll(character => character.IsAlive());
            if (alivePlayers.Count == 0)
            {
                CheckBattleEnd();
                return;
            }

            int targetIndex = UnityEngine.Random.Range(0, alivePlayers.Count);
            StartAction(BattleCommandType.Attack, null,
                new List<Combatant> { alivePlayers[targetIndex] });
        }

        private void CheckBattleEnd()
        {
            if (GetFirstAliveCombatant(enemyParty) == null)
            {
                ChangeState(BattleState.Victory);
                Debug.Log("Victory!");
            }
            else if (GetFirstAliveCombatant(playerParty) == null)
            {
                ChangeState(BattleState.Defeat);
                Debug.Log("Defeat...");
            }
        }

        public void OnPreviousTargetButtonPressed()
        {
            SelectPreviousTarget();
        }

        public void OnNextTargetButtonPressed()
        {
            SelectNextTarget();
        }

        private void SelectNextTarget()
        {
            if (IsInputBlocked) return;
            if (CurrentState != BattleState.SelectingTarget || selectableTargets.Count == 0)
                return;

            selectedTargetIndex = (selectedTargetIndex + 1) % selectableTargets.Count;
            selectedTarget = selectableTargets[selectedTargetIndex];
        }

        private void SelectPreviousTarget()
        {
            if (IsInputBlocked) return;
            if (CurrentState != BattleState.SelectingTarget || selectableTargets.Count == 0)
                return;

            selectedTargetIndex--;
            if (selectedTargetIndex < 0)
                selectedTargetIndex = selectableTargets.Count - 1;
            selectedTarget = selectableTargets[selectedTargetIndex];
        }

        private void ConfirmSelectedTarget()
        {
            if (CurrentState != BattleState.SelectingTarget || selectedTarget == null)
                return;

            StartAction(selectedCommand, selectedAction,
                new List<Combatant> { selectedTarget });
        }

        public void OnActorClicked(Combatant combatant)
        {
            if (IsInputBlocked) return;
            if (CurrentState != BattleState.SelectingTarget)
                return;

            int index = selectableTargets.IndexOf(combatant);
            if (index < 0)
                return;

            selectedTargetIndex = index;
            selectedTarget = combatant;
            ConfirmSelectedTarget();
        }

        private void CancelTargetSelection()
        {
            if (CurrentState != BattleState.SelectingTarget)
                return;

            ClearSelection();
            ChangeState(BattleState.WaitingForCommand);
        }

        private void ClearSelection()
        {
            selectedTarget = null;
            selectedTargetIndex = -1;
            selectedCommand = BattleCommandType.None;
            selectedAction = null;
            selectableTargets.Clear();
        }

        public List<Combatant> GetUpcomingCombatants(int count)
        {
            return turnOrder.GetUpcomingCombatants(GetAllCombatants(), count);
        }

        public List<BattleActionDefinition> GetCurrentSkills()
        {
            return battleData == null || currentCombatant == null
                ? new List<BattleActionDefinition>()
                : battleData.GetSkills(currentCombatant.CharacterId);
        }

        public List<BattleActionDefinition> GetItems()
        {
            if (battleData == null || battleData.items == null)
                return new List<BattleActionDefinition>();

            return new List<BattleActionDefinition>(battleData.items).FindAll(item =>
                item != null && item.usableInBattle);
        }

        public int GetItemCount(string itemId)
        {
            return BattleSession.GetItemCount(itemId);
        }
    }
}
