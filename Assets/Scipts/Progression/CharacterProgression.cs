using System;

namespace Game.Battle
{
    [Serializable]
    public class CharacterLevelRules
    {
        public int maxLevel = 99;
        public int firstLevelExperience = 100;
        public int experienceIncreasePerLevel = 50;
        public bool refillHealthAndManaOnLevelUp;

        public void Validate()
        {
            if (maxLevel < 1 || firstLevelExperience <= 0 || experienceIncreasePerLevel < 0)
                throw new InvalidOperationException("Level rules require maxLevel >= 1, positive EXP cost and non-negative EXP growth.");

            long highestCost = firstLevelExperience + (long)Math.Max(0, maxLevel - 2) * experienceIncreasePerLevel;
            if (highestCost > int.MaxValue)
                throw new InvalidOperationException("Level rules exceed the supported EXP cost.");
        }

        public int GetExperienceRequired(int level)
        {
            if (level < 1 || level > maxLevel)
                throw new ArgumentOutOfRangeException(nameof(level));
            if (level == maxLevel)
                return 0;

            return checked(firstLevelExperience + (level - 1) * experienceIncreasePerLevel);
        }
    }

    public class CharacterProgression
    {
        private readonly CharacterLevelRules rules;

        public int Level { get; private set; }
        // EXP earned toward the next level, not lifetime EXP.
        public int Experience { get; private set; }
        public bool IsMaxLevel { get { return Level == rules.maxLevel; } }
        public int ExperienceRequiredForNextLevel { get { return rules.GetExperienceRequired(Level); } }

        public CharacterProgression(CharacterLevelRules rules, int level = 1, int experience = 0)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            rules.Validate();
            if (level < 1 || level > rules.maxLevel)
                throw new ArgumentOutOfRangeException(nameof(level));

            Level = level;
            if (experience < 0 || (IsMaxLevel ? experience != 0 : experience >= ExperienceRequiredForNextLevel))
                throw new ArgumentOutOfRangeException(nameof(experience), "Stored EXP must be below the next level's cost, or zero at max level.");

            Experience = experience;
        }

        public int GainExperience(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0 || IsMaxLevel)
                return 0;

            int previousLevel = Level;
            long availableExperience = (long)Experience + amount;
            while (!IsMaxLevel && availableExperience >= ExperienceRequiredForNextLevel)
            {
                availableExperience -= ExperienceRequiredForNextLevel;
                Level++;
            }

            Experience = IsMaxLevel ? 0 : (int)availableExperience;
            return Level - previousLevel;
        }
    }
}
