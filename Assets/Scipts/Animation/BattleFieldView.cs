using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle
{
    public class BattleFieldView : MonoBehaviour
    {
        [SerializeField] private Transform[] playerSlots;
        [SerializeField] private Transform[] enemySlots;
        [SerializeField] private BattleFloatingNumberView floatingNumbers;
        private readonly Dictionary<Combatant, BattleActorView> actorViews =
            new Dictionary<Combatant, BattleActorView>();

        public void Setup(List<Combatant> players, List<Combatant> enemies,
            System.Action<Combatant> onActorClicked)
        {
            ResetPresentation();
            actorViews.Clear();
            for ( int i=0; i< playerSlots.Length; i++)
            {
                playerSlots[i].gameObject.SetActive(i < players.Count);
                if ( i >= players.Count)
                {
                    continue;
                }

                BattleActorView actorView = playerSlots[i].GetComponentInChildren<BattleActorView>();

                if ( actorView == null)
                {
                    continue;
                }
                
                actorView.Setup(players[i], onActorClicked);
                actorViews[players[i]] = actorView;
            }
            for (int i = 0; i < enemySlots.Length; i++)
            {
                if ( i >= enemies.Count)
                {
                    break;
                }

                BattleActorView actorView = enemySlots[i].GetComponentInChildren<BattleActorView>();

                if ( actorView == null)
                {
                    continue;
                }
                
                actorView.Setup(enemies[i], onActorClicked);
                actorViews[enemies[i]] = actorView;
            }
        }

        public IEnumerator PlayAction(Combatant actor, BattleCommandType command,
            BattleActionDefinition action)
        {
            if (actorViews.TryGetValue(actor, out BattleActorView view) && view != null &&
                view.isActiveAndEnabled)
                yield return view.PlayAction(command, action);
            else
                yield return null;
        }

        public IEnumerator PlayActionRecovery(Combatant actor)
        {
            if (actorViews.TryGetValue(actor, out BattleActorView view) && view != null &&
                view.isActiveAndEnabled)
                yield return view.PlayActionRecovery();
        }

        public IEnumerator PlayResults(IReadOnlyList<Combatant> targets,
            IReadOnlyList<float> healthBefore, BattleNumberStyle numberStyle = BattleNumberStyle.Damage)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Combatant target = targets[i];
                if (!actorViews.TryGetValue(target, out BattleActorView view) || view == null ||
                    !view.isActiveAndEnabled)
                    continue;

                float healthChange = target.Health - healthBefore[i];
                Coroutine number = floatingNumbers == null ? null
                    : floatingNumbers.Show(view.transform, healthChange, numberStyle);
                bool wasDefeated = healthBefore[i] > 0f && !target.IsAlive();
                if (wasDefeated)
                    yield return view.PlayDefeat();
                else
                    yield return view.PlayReaction(healthChange, numberStyle);
                if (number != null)
                    yield return number;
            }
        }

        public void ResetPresentation()
        {
            foreach (BattleActorView view in actorViews.Values)
            {
                if (view != null)
                {
                    view.ResetPresentation();
                    view.SetSelected(false);
                }
            }
        }

        public void SetSelectedTarget(Combatant target)
        {
            foreach (var entry in actorViews)
                if (entry.Value != null)
                    entry.Value.SetSelected(entry.Key == target);
        }
    }
}
