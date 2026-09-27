using System;
using UnityEngine;

namespace Game.Battle
{
    public static class GameRuntimeState
    {
        private static GameSaveData current;
        public static bool IsInitialized => current != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => current = null;

        public static void Initialize(GameSaveData save)
        {
            current = Copy(save);
        }

        public static GameSaveData Snapshot()
        {
            if (current == null)
                throw new InvalidOperationException("Game runtime state is not initialized.");
            return Copy(current);
        }

        public static void SetPosition(string sceneName, string spawnId, Vector3 position)
        {
            if (current == null)
                throw new InvalidOperationException("Game runtime state is not initialized.");
            current.exploration = new ExplorationProgress
            {
                sceneName = sceneName, spawnId = spawnId,
                hasSavedPosition = true, position = position
            };
        }

        public static void ApplyVictory(BattleOutcome outcome)
        {
            if (outcome == null || outcome.state != BattleState.Victory)
                throw new InvalidOperationException("Only victory can update runtime progress.");
            GameSaveData next = Snapshot();
            foreach (BattlePartyMember member in outcome.players)
            {
                int index = Array.FindIndex(next.players, player => player.characterId == member.characterId);
                if (index < 0)
                    throw new InvalidOperationException("Battle result contains an unknown roster character.");
                next.players[index] = member;
            }
            next.items = outcome.items;
            next.gold = outcome.gold;
            Initialize(next);
        }

        public static void SetActiveParty(string[] characterIds)
        {
            GameSaveData next = Snapshot();
            next.activePartyIds = characterIds == null ? null : (string[])characterIds.Clone();
            next.ValidateParty();
            current = next;
        }

        private static GameSaveData Copy(GameSaveData save)
        {
            if (save == null || save.players == null || save.exploration == null)
                throw new ArgumentException("Incomplete runtime data.", nameof(save));
            return new GameSaveData
            {
                version = save.version,
                savedAtUtc = string.IsNullOrWhiteSpace(save.savedAtUtc)
                    ? DateTime.UtcNow.ToString("O") : save.savedAtUtc,
                gold = save.gold,
                players = GameSaveSystem.CopyPlayers(save.players),
                activePartyIds = save.activePartyIds == null ? null : (string[])save.activePartyIds.Clone(),
                items = GameSaveSystem.CopyItems(save.items),
                exploration = GameSaveSystem.CopyExploration(save.exploration)
            };
        }
    }
}
