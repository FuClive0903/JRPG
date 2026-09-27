using System;

namespace Game.Battle
{
    [Serializable]
    public class BattleRewardResult
    {
        public int experiencePerCharacter;
        public int gold;
        public CharacterExperienceReward[] characters = new CharacterExperienceReward[0];
        public BattleItemAmount[] items = new BattleItemAmount[0];
    }

    [Serializable]
    public class CharacterExperienceReward
    {
        public string characterId;
        public int beforeLevel;
        public int beforeExperience;
        public int afterLevel;
        public int afterExperience;
    }
}
