using System;
using System.Collections.Generic;

namespace Game.Battle
{
    public static class BattleRewardCalculator
    {
        public static BattleRewardResult Calculate(BattleData data, List<Combatant> enemies)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (enemies == null)
                throw new ArgumentNullException(nameof(enemies));

            int experience = 0;
            int gold = 0;
            List<BattleItemAmount> items = new List<BattleItemAmount>();
            foreach (Combatant enemy in enemies)
            {
                CharacterDefinition definition = data.FindCharacter(enemy.CharacterId);
                if (definition == null)
                    throw new InvalidOperationException("Unknown reward enemy: " + enemy.CharacterId);

                experience = checked(experience + definition.experienceReward);
                gold = checked(gold + enemy.Level * 100 + UnityEngine.Random.Range(-100, 101));
                AddDrops(data, definition, items);
            }

            return new BattleRewardResult
            {
                experiencePerCharacter = experience,
                gold = gold,
                items = items.ToArray()
            };
        }

        private static void AddDrops(BattleData data, CharacterDefinition enemy,
            List<BattleItemAmount> result)
        {
            if (enemy.drops == null)
                return;

            foreach (BattleDropDefinition drop in enemy.drops)
            {
                if (drop == null || data.FindItem(drop.itemId) == null)
                    throw new InvalidOperationException("Invalid item drop on enemy: " + enemy.id);
                if (drop.chance < 0 || drop.chance > 1 || drop.minCount < 1 ||
                    drop.maxCount < drop.minCount)
                    throw new InvalidOperationException("Invalid drop settings on enemy: " + enemy.id);
                if (UnityEngine.Random.value > drop.chance)
                    continue;

                int count = UnityEngine.Random.Range(drop.minCount, drop.maxCount + 1);
                BattleItemAmount existing = result.Find(item => item.itemId == drop.itemId);
                if (existing == null)
                    result.Add(new BattleItemAmount { itemId = drop.itemId, count = count });
                else
                    existing.count = checked(existing.count + count);
            }
        }

        public static void GrantExperience(BattleRewardResult reward, List<Combatant> players)
        {
            if (reward == null)
                throw new ArgumentNullException(nameof(reward));
            if (players == null)
                throw new ArgumentNullException(nameof(players));

            reward.characters = new CharacterExperienceReward[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                Combatant player = players[i];
                var change = new CharacterExperienceReward
                {
                    characterId = player.CharacterId,
                    beforeLevel = player.Level,
                    beforeExperience = player.Experience
                };
                player.GainExperience(reward.experiencePerCharacter);
                change.afterLevel = player.Level;
                change.afterExperience = player.Experience;
                reward.characters[i] = change;
            }
        }
    }
}
