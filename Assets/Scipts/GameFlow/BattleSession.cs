using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle
{
    [Serializable]
    public class BattlePartyMember
    {
        public string characterId;
        public int level = 1;
        public int experience;
        public float health;
        public float mana;
    }

    public class BattleEntry
    {
        public BattlePartyMember[] players;
        public BattlePartyMember[] reserves;
        public string[] enemies;
        public BattleItemAmount[] items;
        public string returnScene;
        public int gold;

        public List<Combatant> CreatePlayers(BattleData data)
        {
            if (players == null || players.Length == 0)
                throw new InvalidOperationException("Battle entry: player party cannot be empty.");

            List<Combatant> party = new List<Combatant>();
            foreach (BattlePartyMember member in players)
            {
                Combatant character = data.CreateCharacter(member.characterId, true, member.level, member.experience);
                character.Health = Mathf.Clamp(member.health, 0, character.MaxHealth);
                character.Mana = Mathf.Clamp(member.mana, 0, character.MaxMana);
                party.Add(character);
            }
            return party;
        }

        public List<Combatant> CreateRewardParty(BattleData data, List<Combatant> activePlayers)
        {
            var party = new List<Combatant>(activePlayers);
            if (reserves != null && reserves.Length > 0)
                party.AddRange(new BattleEntry { players = reserves }.CreatePlayers(data));
            return party;
        }
    }

    public class BattleOutcome
    {
        public BattleState state;
        public BattlePartyMember[] players;
        public BattleItemAmount[] items;
        public BattleRewardResult rewards;
        public int gold;
    }

    // In-memory handoff only. Entry stays unchanged so Retry restores the same battle.
    public static class BattleSession
    {
        public static BattleEntry Entry { get; private set; }
        public static BattleOutcome Result { get; private set; }
        public static GameObject EnvironmentPrefab { get; private set; }
        private static BattleItemAmount[] currentItems;
        private static bool resultRecorded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Entry = null;
            Result = null;
            EnvironmentPrefab = null;
            currentItems = null;
            resultRecorded = false;
        }

        public static void Prepare(List<Combatant> players, string[] enemies, string returnScene,
            BattleItemAmount[] items = null, int gold = 0, List<Combatant> roster = null,
            GameObject environmentPrefab = null)
        {
            var reservePlayers = roster == null ? new List<Combatant>() :
                roster.FindAll(character => !players.Exists(player => player.CharacterId == character.CharacterId));
            Entry = new BattleEntry
            {
                players = CapturePlayers(players),
                reserves = CapturePlayers(reservePlayers),
                enemies = (string[])enemies.Clone(),
                items = CopyItems(items),
                returnScene = returnScene,
                gold = gold
            };
            EnvironmentPrefab = environmentPrefab;
            Result = null;
            currentItems = null;
            resultRecorded = false;
        }

        public static void BeginBattle()
        {
            currentItems = CopyItems(Entry.items);
            Result = null;
            resultRecorded = false;
        }

        public static int GetItemCount(string itemId)
        {
            if (currentItems == null)
                return 0;
            BattleItemAmount item = Array.Find(currentItems, entry => entry.itemId == itemId);
            return item == null ? 0 : item.count;
        }

        public static bool ConsumeItem(string itemId)
        {
            if (currentItems == null)
                return false;
            BattleItemAmount item = Array.Find(currentItems, entry => entry.itemId == itemId);
            if (item == null || item.count <= 0)
                return false;

            item.count--;
            return true;
        }

        public static void RecordResult(BattleState state, List<Combatant> players,
            BattleRewardResult rewards = null)
        {
            if (resultRecorded)
                return;
            if (Entry == null || currentItems == null ||
                (state != BattleState.Victory && state != BattleState.Defeat))
                throw new InvalidOperationException("A running battle and terminal state are required.");

            BattlePartyMember[] resultPlayers = CapturePlayers(players);
            int gold = Entry.gold;
            if (state == BattleState.Victory && rewards != null)
            {
                gold = checked(gold + rewards.gold);
                AddRewardItems(rewards.items);
            }

            Result = new BattleOutcome
            {
                state = state,
                players = resultPlayers,
                items = CopyItems(currentItems),
                rewards = rewards,
                gold = gold
            };
            resultRecorded = true;
        }

        public static void ClearResult()
        {
            // Clearing presentation data does not allow this battle to grant rewards again.
            Result = null;
        }

        public static void LeaveBattle()
        {
            // Keep Result available for the destination scene to read.
            Entry = null;
            EnvironmentPrefab = null;
            currentItems = null;
        }

        private static BattlePartyMember[] CapturePlayers(List<Combatant> players)
        {
            return players.ConvertAll(character => new BattlePartyMember
            {
                characterId = character.CharacterId,
                level = character.Level,
                experience = character.Experience,
                health = character.Health,
                mana = character.Mana
            }).ToArray();
        }

        private static BattleItemAmount[] CopyItems(BattleItemAmount[] items)
        {
            if (items == null)
                return new BattleItemAmount[0];

            return Array.ConvertAll(items, item => new BattleItemAmount
            {
                itemId = item.itemId,
                count = item.count
            });
        }

        private static void AddRewardItems(BattleItemAmount[] rewards)
        {
            if (rewards == null || rewards.Length == 0)
                return;

            List<BattleItemAmount> items = new List<BattleItemAmount>(CopyItems(currentItems));
            foreach (BattleItemAmount reward in rewards)
            {
                BattleItemAmount existing = items.Find(item => item.itemId == reward.itemId);
                if (existing == null)
                    items.Add(new BattleItemAmount { itemId = reward.itemId, count = reward.count });
                else
                    existing.count = checked(existing.count + reward.count);
            }
            currentItems = items.ToArray();
        }
    }
}
