namespace Game.Battle
{
    public static class CharacterExperienceText
    {
        public static string Format(BattleData data, BattlePartyMember player,
            CharacterExperienceReward change, int received)
        {
            CharacterDefinition definition = data.FindCharacter(player.characterId);
            string name = definition == null ? player.characterId : definition.name;
            int beforeLevel = change == null ? player.level : change.beforeLevel;
            int beforeExperience = change == null ? player.experience : change.beforeExperience;
            var progress = new CharacterProgression(data.levelRules, beforeLevel, beforeExperience);
            if (change != null)
                progress.GainExperience(received);
            bool leveledUp = progress.Level > beforeLevel;
            string level = "LV " + progress.Level;
            if (leveledUp)
                level = "LV " + beforeLevel + " -> <color=#FFD700>" + progress.Level + "</color>";
            string experience = progress.Level >= data.levelRules.maxLevel ? "MAX" :
                progress.Experience + "/" + data.levelRules.GetExperienceRequired(progress.Level);
            string before = beforeLevel >= data.levelRules.maxLevel ? "MAX" :
                beforeExperience + "/" + data.levelRules.GetExperienceRequired(beforeLevel);
            return "<noparse>" + name + "</noparse><pos=25%>" + level +
                "<pos=45%>EXP " + before + " -> " + experience +
                (leveledUp ? "  <color=#FFD700>\u5347\u7ea7\uff01</color>" : "");
        }
    }
}
