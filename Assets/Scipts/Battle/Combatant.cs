namespace Game.Battle
{
    public class ActiveStatus
    {
        public StatusDefinition Definition;
        public int RemainingTurns;
        public bool SkipNextDecrease;
    }

    public class Combatant
    {
        public string CharacterId { get; private set; }
        public string Name { get; private set; }
        public string BattleAppearanceResource { get { return definition.battleAppearance; } }
        private readonly CharacterDefinition definition;
        private readonly CharacterLevelRules levelRules;
        private readonly CharacterStatGrowth growth;
        private readonly CharacterProgression progression;

        public int Level { get { return progression.Level; } }
        public int Experience { get { return progression.Experience; } }
        public int ExperienceRequiredForNextLevel { get { return progression.ExperienceRequiredForNextLevel; } }
        public bool IsMaxLevel { get { return progression.IsMaxLevel; } }
        public float Health;
        public float MaxHealth;
        public float Mana;
        public float MaxMana;
        public float AttackPower;
        public float MagicPower;
        public float Speed;
        public bool IsPlayer;
        public float NextTurnTime;
        public bool IsDefending;
        private readonly System.Collections.Generic.List<ActiveStatus> activeStatuses =
            new System.Collections.Generic.List<ActiveStatus>();
        public System.Collections.Generic.IReadOnlyList<ActiveStatus> ActiveStatuses
        {
            get { return activeStatuses; }
        }

        public Combatant(string name, float maxHealth,float maxMana, float attackPower, float magicPower, float speed, bool isPlayer, string characterId = null)
            : this(new CharacterDefinition
            {
                id = characterId,
                name = name,
                maxHealth = maxHealth,
                maxMana = maxMana,
                attackPower = attackPower,
                magicPower = magicPower,
                speed = speed
            }, new CharacterLevelRules(), isPlayer)
        {
        }

        public Combatant(CharacterDefinition definition, CharacterLevelRules levelRules,
            bool isPlayer, int? level = null, int experience = 0)
        {
            this.definition = definition ?? throw new System.ArgumentNullException(nameof(definition));
            this.levelRules = levelRules ?? throw new System.ArgumentNullException(nameof(levelRules));
            progression = new CharacterProgression(levelRules, level ?? definition.startingLevel, experience);
            growth = definition.growthPerLevel ?? new CharacterStatGrowth();
            // Reject invalid growth before awarding EXP can change the character's level.
            growth.CalculateStats(definition, levelRules.maxLevel);

            CharacterId = definition.id;
            Name = definition.name;
            ApplyLevelStats();
            Health = MaxHealth;
            Mana = MaxMana;
            IsPlayer = isPlayer;
            NextTurnTime = 0;
            IsDefending = false;
        }

        public int GainExperience(int amount)
        {
            int levelsGained = progression.GainExperience(amount);
            if (levelsGained == 0)
                return 0;

            bool wasAlive = IsAlive();
            ApplyLevelStats();
            Health = System.Math.Min(Health, MaxHealth);
            Mana = System.Math.Min(Mana, MaxMana);
            if (levelRules.refillHealthAndManaOnLevelUp && wasAlive)
            {
                Health = MaxHealth;
                Mana = MaxMana;
            }

            return levelsGained;
        }

        private void ApplyLevelStats()
        {
            CharacterStats stats = growth.CalculateStats(definition, Level);
            MaxHealth = stats.MaxHealth;
            MaxMana = stats.MaxMana;
            AttackPower = stats.AttackPower;
            MagicPower = stats.MagicPower;
            Speed = stats.Speed;
        }
        public bool IsAlive()
        {
            return Health > 0;
        }
        public float TakeDamage(float damage)
        {
            float actualDamage = damage;
            if(IsDefending)
            {
                actualDamage *= 0.5f;
                IsDefending = false; 
            }
            Health -= actualDamage;
            if (Health < 0)
            {
                Health = 0;
            }
            return actualDamage;
        }
        private void TakeStatusDamage(float damage)
        {
            Health -= damage;
            if (Health < 0)
                Health = 0;
        }
        public void Defend()
        {
            IsDefending = true;
        }
        public void ApplyStatus(StatusDefinition definition, bool skipNextDecrease = false)
        {
            ActiveStatus existing = activeStatuses.Find(status =>
                status.Definition.id == definition.id);
            if (existing != null)
            {
                existing.Definition = definition;
                existing.RemainingTurns = definition.durationTurns;
                existing.SkipNextDecrease = skipNextDecrease;
                return;
            }

            activeStatuses.Add(new ActiveStatus
            {
                Definition = definition,
                RemainingTurns = definition.durationTurns,
                SkipNextDecrease = skipNextDecrease
            });
        }
        public bool ResolveTurnStartStatuses()
        {
            bool skipTurn = false;
            System.Collections.Generic.List<ActiveStatus> ordered =
                new System.Collections.Generic.List<ActiveStatus>(activeStatuses);
            ordered.Sort((left, right) =>
                left.Definition.priority.CompareTo(right.Definition.priority));

            foreach (ActiveStatus status in ordered)
            {
                StatusEffectType effect = status.Definition.GetEffectType();
                if (status.Definition.timing == "TurnStart")
                {
                    if (effect == StatusEffectType.Heal)
                        RestoreHealth(status.Definition.value);
                    else if (effect == StatusEffectType.Damage)
                        TakeStatusDamage(status.Definition.value);
                }
            }
            foreach (ActiveStatus status in ordered)
            {
                if (status.Definition.timing == "BeforeAction" &&
                    status.Definition.GetEffectType() == StatusEffectType.SkipTurn)
                    skipTurn = true;
            }
            return skipTurn;
        }
        public float GetEffectiveSpeed()
        {
            float multiplier = 1f;
            foreach (ActiveStatus status in activeStatuses)
            {
                if (status.Definition.timing == "TurnDelay" &&
                    status.Definition.GetEffectType() == StatusEffectType.SpeedMultiplier)
                    multiplier *= status.Definition.value;
            }
            return Speed * multiplier;
        }
        public void FinishStatusTurn()
        {
            foreach (ActiveStatus status in activeStatuses)
            {
                if (status.SkipNextDecrease)
                    status.SkipNextDecrease = false;
                else
                    status.RemainingTurns--;
            }

            activeStatuses.RemoveAll(status => status.RemainingTurns <= 0);
        }
        public int GetStatusTurns(string statusId)
        {
            ActiveStatus status = activeStatuses.Find(entry => entry.Definition.id == statusId);
            return status == null ? 0 : status.RemainingTurns;
        }
        public bool SpendMana(float amount)
        {
            if (amount < 0 || Mana < amount)
                return false;

            Mana -= amount;
            return true;
        }
        public float RestoreHealth(float amount)
        {
            if (!IsAlive() || amount <= 0)
                return 0;

            float restored = System.Math.Min(amount, MaxHealth - Health);
            Health += restored;
            return restored;
        }
        public float Revive(float amount)
        {
            if (IsAlive() || amount <= 0)
                return 0;

            Health = System.Math.Min(amount, MaxHealth);
            return Health;
        }
    }
}
