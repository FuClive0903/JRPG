using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle
{
    public enum BattleTargetType
    {
        EnemyOne,
        AllyOne,
        Self,
        AllEnemies,
        AllAllies,
        DeadAlly
    }

    public enum BattleEffectType
    {
        Damage,
        Heal,
        Revive,
        ApplyStatus
    }

    [Serializable]
    public class BattleDropDefinition
    {
        public string itemId;
        public float chance = 1f;
        public int minCount = 1;
        public int maxCount = 1;
    }

    [Serializable]
    public class CharacterDefinition
    {
        public string id;
        public string name;
        public string battleAppearance;
        public float maxHealth;
        public float maxMana;
        public float attackPower;
        public float magicPower;
        public float speed;
        public string[] skillIds;
        public int startingLevel = 1;
        public int experienceReward;
        public BattleDropDefinition[] drops;
        public CharacterStatGrowth growthPerLevel = new CharacterStatGrowth();
    }

    [Serializable]
    public class BattleActionDefinition
    {
        public string id;
        public string name;
        public string effect;
        public string target;
        public float power;
        public float manaCost;
        public string statusId;
        public string category;
        public bool usableInBattle;

        public BattleTargetType GetTargetType()
        {
            if (Enum.TryParse(target, out BattleTargetType result))
                return result;
            throw new InvalidOperationException("BattleData: invalid target type for " + id);
        }

        public BattleEffectType GetEffectType()
        {
            if (Enum.TryParse(effect, out BattleEffectType result))
                return result;
            throw new InvalidOperationException("BattleData: invalid effect type for " + id);
        }
    }

    [Serializable]
    public class BattleItemAmount
    {
        public string itemId;
        public int count;
    }

    [Serializable]
    public class ExplorationEncounterRules
    {
        public float distancePerStep = 1f;
        public int safeSteps = 20;
        public float encounterChancePerStep = 0.5f;
    }

    [Serializable]
    public class StatusDefinition
    {
        public string id;
        public string name;
        public string effect;
        public string timing;
        public float value;
        public int durationTurns;
        public int priority;
        public string category;
        public string iconText;

        public StatusEffectType GetEffectType()
        {
            if (Enum.TryParse(effect, out StatusEffectType result))
                return result;
            throw new InvalidOperationException("BattleData: invalid status effect for " + id);
        }
    }

    public enum StatusEffectType
    {
        Heal,
        Damage,
        SkipTurn,
        SpeedMultiplier
    }

    [Serializable]
    public class BattleData
    {
        public CharacterDefinition[] characters;
        public BattleActionDefinition[] skills;
        public BattleActionDefinition[] items;
        public StatusDefinition[] statuses;
        public string[] defaultPlayerParty;
        public string[] defaultEnemyParty;
        public BattleItemAmount[] defaultInventory;
        public CharacterLevelRules levelRules = new CharacterLevelRules();
        public ExplorationEncounterRules encounterRules = new ExplorationEncounterRules();

        public static BattleData Load()
        {
            TextAsset asset = Resources.Load<TextAsset>("Data/BattleData");
            if (asset == null)
                throw new InvalidOperationException("Missing Resources/Data/BattleData.json.");

            BattleData data = JsonUtility.FromJson<BattleData>(asset.text);
            if (data == null || data.characters == null || data.characters.Length == 0)
                throw new InvalidOperationException("BattleData: character table is empty.");
            if (data.encounterRules == null || data.encounterRules.distancePerStep <= 0f ||
                data.encounterRules.safeSteps < 0 ||
                data.encounterRules.encounterChancePerStep < 0f ||
                data.encounterRules.encounterChancePerStep > 1f)
            {
                throw new InvalidOperationException("BattleData: invalid exploration encounter rules.");
            }

            return data;
        }

        public CharacterDefinition FindCharacter(string id)
        {
            return Array.Find(characters, entry => entry != null && entry.id == id);
        }

        public BattleActionDefinition FindSkill(string id)
        {
            if (skills == null)
                return null;
            return Array.Find(skills, entry => entry != null && entry.id == id);
        }

        public BattleActionDefinition FindItem(string id)
        {
            if (items == null)
                return null;
            return Array.Find(items, entry => entry != null && entry.id == id);
        }

        public StatusDefinition FindStatus(string id)
        {
            if (statuses == null)
                return null;
            return Array.Find(statuses, entry => entry != null && entry.id == id);
        }

        public List<BattleActionDefinition> GetSkills(string characterId)
        {
            List<BattleActionDefinition> result = new List<BattleActionDefinition>();
            CharacterDefinition character = FindCharacter(characterId);
            if (character == null || character.skillIds == null)
                return result;

            foreach (string skillId in character.skillIds)
            {
                BattleActionDefinition skill = FindSkill(skillId);
                if (skill != null)
                    result.Add(skill);
            }
            return result;
        }

        public List<Combatant> CreateParty(string[] characterIds, bool isPlayer)
        {
            if (characterIds == null || characterIds.Length == 0)
                throw new InvalidOperationException("BattleData: party cannot be empty.");

            List<Combatant> party = new List<Combatant>();
            foreach (string id in characterIds)
                party.Add(CreateCharacter(id, isPlayer));

            return party;
        }

        public Combatant CreateCharacter(string characterId, bool isPlayer, int? level = null, int experience = 0)
        {
            CharacterDefinition character = FindCharacter(characterId);
            if (character == null)
                throw new InvalidOperationException("BattleData: unknown character ID: " + characterId);

            return new Combatant(character, levelRules, isPlayer, level, experience);
        }
    }
}
