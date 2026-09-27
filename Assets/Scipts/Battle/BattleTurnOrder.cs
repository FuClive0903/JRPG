using System.Collections.Generic;

namespace Game.Battle
{
    public class BattleTurnOrder
    {
        public void InitializeTurnTimes(List<Combatant> combatants)
        {
            foreach (Combatant combatant in combatants)
            {
                combatant.NextTurnTime = GetTurnDelay(combatant);
            }
        }

        public Combatant GetNextCombatant(List<Combatant> combatants)
        {
            Combatant nextCombatant = null;

            foreach (Combatant combatant in combatants)
            {
                if (!combatant.IsAlive())
                {
                    continue;
                }

                if (nextCombatant == null || combatant.NextTurnTime < nextCombatant.NextTurnTime)
                {
                    nextCombatant = combatant;
                }
            }

            return nextCombatant;
        }

        public void AdvanceTurn(Combatant combatant)
        {
            if (combatant == null)
            {
                return;
            }

            combatant.NextTurnTime += GetTurnDelay(combatant);
        }

        public List<Combatant> GetUpcomingCombatants(List<Combatant> combatants, int count)
        {
            List<Combatant> aliveCombatants = combatants.FindAll(combatant => combatant.IsAlive());
            List<float> previewTimes = aliveCombatants.ConvertAll(combatant => combatant.NextTurnTime);
            List<Combatant> result = new List<Combatant>();

            if (count <= 0 || aliveCombatants.Count == 0)
            {
                return result;
            }

            for (int i = 0; i < count; i++)
            {
                int nextIndex = 0;

                for (int j = 1; j < previewTimes.Count; j++)
                {
                    if (previewTimes[j] < previewTimes[nextIndex])
                    {
                        nextIndex = j;
                    }
                }

                Combatant nextCombatant = aliveCombatants[nextIndex];
                result.Add(nextCombatant);

                previewTimes[nextIndex] += GetTurnDelay(nextCombatant);
            }

            return result;
        }

        private float GetTurnDelay(Combatant combatant)
        {
            return 100f / combatant.GetEffectiveSpeed();
        }
    }
}
