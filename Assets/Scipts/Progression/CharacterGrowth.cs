using System;

namespace Game.Battle
{
    [Serializable]
    public class CharacterStatGrowth
    {
        public float maxHealth;
        public float maxMana;
        public float attackPower;
        public float magicPower;
        public float speed;

        public CharacterStats CalculateStats(CharacterDefinition character, int level)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));
            if (level < 1)
                throw new ArgumentOutOfRangeException(nameof(level));

            return new CharacterStats(
                Calculate(character.maxHealth, maxHealth, level, "maxHealth", true),
                Calculate(character.maxMana, maxMana, level, "maxMana"),
                Calculate(character.attackPower, attackPower, level, "attackPower"),
                Calculate(character.magicPower, magicPower, level, "magicPower"),
                Calculate(character.speed, speed, level, "speed", true));
        }

        private static float Calculate(float baseValue, float growth, int level, string stat, bool mustBePositive = false)
        {
            if (float.IsNaN(baseValue) || float.IsInfinity(baseValue) ||
                (mustBePositive ? baseValue <= 0 : baseValue < 0) ||
                float.IsNaN(growth) || float.IsInfinity(growth) || growth < 0)
                throw new InvalidOperationException("Invalid base value or per-level growth for " + stat);

            double result = baseValue + (double)growth * (level - 1);
            if (result > float.MaxValue)
                throw new InvalidOperationException("Level growth exceeds the supported value for " + stat);

            return (float)result;
        }
    }

    public readonly struct CharacterStats
    {
        public float MaxHealth { get; }
        public float MaxMana { get; }
        public float AttackPower { get; }
        public float MagicPower { get; }
        public float Speed { get; }

        public CharacterStats(float maxHealth, float maxMana, float attackPower, float magicPower, float speed)
        {
            MaxHealth = maxHealth;
            MaxMana = maxMana;
            AttackPower = attackPower;
            MagicPower = magicPower;
            Speed = speed;
        }
    }
}
