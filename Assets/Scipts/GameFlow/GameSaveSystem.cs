using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Battle
{
    [Serializable]
    public class ExplorationProgress
    {
        public string sceneName;
        public string spawnId;
        public bool hasSavedPosition;
        public Vector3 position;
    }

    [Serializable]
    public class GameSaveData
    {
        public int version = 3;
        public BattlePartyMember[] players;
        public string[] activePartyIds;
        public BattleItemAmount[] items;
        public int gold;
        public ExplorationProgress exploration;
        public string savedAtUtc;

        public List<Combatant> CreatePlayers(BattleData data)
        {
            return new BattleEntry { players = players }.CreatePlayers(data);
        }

        public List<Combatant> CreateActiveParty(BattleData data)
        {
            ValidateParty();
            return new BattleEntry
            {
                players = Array.ConvertAll(activePartyIds,
                    id => Array.Find(players, player => player.characterId == id))
            }.CreatePlayers(data);
        }

        public void ValidateParty()
        {
            if (players == null || players.Length == 0)
                throw new InvalidOperationException("The character roster is empty.");
            var roster = new HashSet<string>();
            foreach (var player in players)
                if (player == null || string.IsNullOrWhiteSpace(player.characterId) ||
                    !roster.Add(player.characterId))
                    throw new InvalidOperationException("Invalid or duplicate roster character.");
            if (activePartyIds == null || activePartyIds.Length < 1 || activePartyIds.Length > 3)
                throw new InvalidOperationException("The active party must contain 1 to 3 characters.");
            var selected = new HashSet<string>();
            foreach (string id in activePartyIds)
                if (id == null || !roster.Contains(id) || !selected.Add(id))
                    throw new InvalidOperationException("Invalid or duplicate active party character.");
        }
    }

    public static class GameSaveSystem
    {
        public const int AutoSaveSlotNumber = 0;
        public const int ManualSaveSlotCount = 10;
        public const string DefaultExplorationScene = "Village_scene";
        private const string LegacyVillageScene = "Exploration_scene";
        public const string DefaultSpawnId = "PlayerSpawn";
        private const int LegacyVersion = 1;
        private const int CurrentVersion = 3;
        private const string AutoSaveFileName = "save.json";
        private const string ManualSaveFileNameFormat = "save_slot_{0:00}.json";

        public static bool HasAutoSave { get { return File.Exists(AutoSavePath); } }
        public static bool HasAnySave
        {
            get
            {
                if (HasAutoSave)
                    return true;

                for (int slotNumber = 1; slotNumber <= ManualSaveSlotCount; slotNumber++)
                {
                    if (HasManualSave(slotNumber))
                        return true;
                }

                return false;
            }
        }

        public static string AutoSavePath
        {
            get { return Path.Combine(Application.persistentDataPath, AutoSaveFileName); }
        }

        public static bool HasManualSave(int slotNumber)
        {
            return File.Exists(GetManualSavePath(slotNumber));
        }

        public static string GetManualSavePath(int slotNumber)
        {
            ValidateManualSlotNumber(slotNumber);
            return Path.Combine(Application.persistentDataPath,
                string.Format(ManualSaveFileNameFormat, slotNumber));
        }

        public static void SaveNewGame(List<Combatant> players, BattleItemAmount[] items)
        {
            GameSaveData save = new GameSaveData
            {
                version = CurrentVersion,
                players = CapturePlayers(players),
                activePartyIds = players.ConvertAll(player => player.CharacterId).ToArray(),
                items = CopyItems(items),
                gold = 0,
                exploration = CreateDefaultExploration()
            };
            save.ValidateParty();
            Save(save, AutoSavePath);
            GameRuntimeState.Initialize(save);
        }

        public static void SaveVictory(BattleOutcome outcome)
        {
            if (outcome == null || outcome.state != BattleState.Victory)
                throw new InvalidOperationException("Only a victory result can update the game save.");

            if (!GameRuntimeState.IsInitialized)
            {
                GameRuntimeState.Initialize(new GameSaveData
                {
                    version = CurrentVersion,
                    players = CopyPlayers(outcome.players),
                    activePartyIds = BattleSession.Entry == null
                        ? Array.ConvertAll(outcome.players, player => player.characterId)
                        : Array.ConvertAll(BattleSession.Entry.players, player => player.characterId),
                    items = CopyItems(outcome.items),
                    gold = outcome.gold,
                    exploration = LoadExplorationForVictory()
                });
            }
            GameRuntimeState.ApplyVictory(outcome);
            Save(GameRuntimeState.Snapshot(), AutoSavePath);
        }

        public static void SaveAutoExplorationPosition(BattleData data, string sceneName,
            string spawnId, Vector3 position)
        {
            GameRuntimeState.SetPosition(sceneName, spawnId, position);
            GameSaveData save = GameRuntimeState.Snapshot();
            Validate(save, data);
            Save(save, AutoSavePath);
        }

        public static void SaveManualSlot(BattleData data, int slotNumber, string sceneName,
            string spawnId, Vector3 position)
        {
            string path = GetManualSavePath(slotNumber);
            GameRuntimeState.SetPosition(sceneName, spawnId, position);
            GameSaveData save = GameRuntimeState.Snapshot();
            Validate(save, data);
            Save(save, path);
        }

        public static GameSaveData LoadAutoSave(BattleData data)
        {
            return Load(data, AutoSavePath, "No auto save exists.");
        }

        public static GameSaveData LoadManualSave(BattleData data, int slotNumber)
        {
            string path = GetManualSavePath(slotNumber);
            return Load(data, path, "Manual save slot " + slotNumber + " is empty.");
        }

        public static GameSaveData ActivateManualSave(BattleData data, int slotNumber)
        {
            GameSaveData save = LoadManualSave(data, slotNumber);
            Save(save, AutoSavePath);
            GameRuntimeState.Initialize(save);
            return save;
        }

        public static DateTime GetSavedAtLocal(GameSaveData save)
        {
            if (save == null || !DateTime.TryParse(save.savedAtUtc,
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime savedAt))
                throw new InvalidOperationException("The game save contains an invalid save time.");

            return savedAt.ToLocalTime();
        }

        private static GameSaveData Load(BattleData data, string path, string missingMessage)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(missingMessage, path);

            GameSaveData save = Read(path);
            // Reading a slot must not rewrite its source, including legacy saves.
            Migrate(save);
            if (string.IsNullOrWhiteSpace(save.savedAtUtc))
            {
                save.savedAtUtc = File.GetLastWriteTimeUtc(path)
                    .ToString("O", CultureInfo.InvariantCulture);
            }
            Validate(save, data);
            save.exploration = CopyExploration(save.exploration);
            return save;
        }

        private static GameSaveData Read(string path)
        {
            return JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
        }

        private static void Save(GameSaveData save, string path, bool updateSaveTime = true)
        {
            if (updateSaveTime)
                save.savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            WriteSaveFile(path, JsonUtility.ToJson(save, true));
        }

        internal static void WriteSaveFile(string path, string json)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = path + ".tmp";
            bool ownsTemporaryFile = false;
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.Create,
                    FileAccess.Write, FileShare.None))
                {
                    ownsTemporaryFile = true;
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
                        writer.Write(json);
                    stream.Flush(true);
                }

                // Keep staging on the same volume and never delete the destination first.
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, path + ".bak");
                else
                    File.Move(temporaryPath, path);
            }
            finally
            {
                if (ownsTemporaryFile)
                {
                    // Cleanup must not hide the original write/replace failure.
                    try { File.Delete(temporaryPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        private static void Validate(GameSaveData save, BattleData data)
        {
            if (save == null || save.version != CurrentVersion)
                throw new InvalidOperationException("The game save version is not supported.");
            if (save.players == null || save.players.Length == 0)
                throw new InvalidOperationException("The game save has no player party.");
            if (save.gold < 0)
                throw new InvalidOperationException("The game save contains invalid gold data.");
            if (!IsValidExploration(save.exploration))
                throw new InvalidOperationException("The game save contains invalid exploration data.");
            if (!DateTime.TryParse(save.savedAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out _))
                throw new InvalidOperationException("The game save contains an invalid save time.");

            save.ValidateParty();
            foreach (BattlePartyMember player in save.players)
            {
                if (float.IsNaN(player.health) || float.IsInfinity(player.health) || player.health < 0 ||
                    float.IsNaN(player.mana) || float.IsInfinity(player.mana) || player.mana < 0)
                    throw new InvalidOperationException("The game save contains invalid HP/MP data.");
            }
            save.CreatePlayers(data);
            if (save.items == null)
                save.items = new BattleItemAmount[0];

            HashSet<string> itemIds = new HashSet<string>();
            foreach (BattleItemAmount item in save.items)
            {
                if (item == null || item.count < 0 || data.FindItem(item.itemId) == null ||
                    !itemIds.Add(item.itemId))
                    throw new InvalidOperationException("The game save contains invalid item data.");
            }
        }

        private static bool Migrate(GameSaveData save)
        {
            if (save == null)
                throw new InvalidOperationException("The game save is empty.");
            if (save.version == CurrentVersion)
                return false;
            if (save.version != LegacyVersion && save.version != 2)
                throw new InvalidOperationException("The game save version is not supported.");

            if (save.version == LegacyVersion)
                save.exploration = CreateDefaultExploration();
            if (save.players == null || save.players.Length == 0)
                throw new InvalidOperationException("The game save has no player party.");
            save.activePartyIds = Array.ConvertAll(save.players, player => player.characterId);
            save.version = CurrentVersion;
            return true;
        }

        private static ExplorationProgress LoadExplorationForVictory()
        {
            if (!HasAutoSave)
                return CreateDefaultExploration();

            try
            {
                GameSaveData save = Read(AutoSavePath);
                Migrate(save);
                if (save != null && save.version == CurrentVersion &&
                    IsValidExploration(save.exploration))
                    return CopyExploration(save.exploration);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Cannot preserve exploration progress: " + exception.Message);
            }

            return CreateDefaultExploration();
        }

        private static void ValidateManualSlotNumber(int slotNumber)
        {
            if (slotNumber < 1 || slotNumber > ManualSaveSlotCount)
                throw new ArgumentOutOfRangeException(nameof(slotNumber), slotNumber,
                    "Manual save slot must be between 1 and " + ManualSaveSlotCount + ".");
        }

        private static ExplorationProgress CreateDefaultExploration()
        {
            return new ExplorationProgress
            {
                sceneName = DefaultExplorationScene,
                spawnId = DefaultSpawnId,
                hasSavedPosition = false,
                position = Vector3.zero
            };
        }

        internal static ExplorationProgress CopyExploration(ExplorationProgress exploration)
        {
            if (exploration == null)
                return CreateDefaultExploration();

            return new ExplorationProgress
            {
                sceneName = exploration.sceneName == LegacyVillageScene
                    ? DefaultExplorationScene : exploration.sceneName,
                spawnId = exploration.spawnId,
                hasSavedPosition = exploration.hasSavedPosition,
                position = exploration.position
            };
        }

        private static bool IsValidExploration(ExplorationProgress exploration)
        {
            return exploration != null &&
                !string.IsNullOrWhiteSpace(exploration.sceneName) &&
                !string.IsNullOrWhiteSpace(exploration.spawnId) &&
                IsFinite(exploration.position);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static BattlePartyMember[] CapturePlayers(List<Combatant> players)
        {
            if (players == null || players.Count == 0)
                throw new InvalidOperationException("Cannot save an empty player party.");

            return players.ConvertAll(character => new BattlePartyMember
            {
                characterId = character.CharacterId,
                level = character.Level,
                experience = character.Experience,
                health = character.Health,
                mana = character.Mana
            }).ToArray();
        }

        internal static BattlePartyMember[] CopyPlayers(BattlePartyMember[] players)
        {
            if (players == null || players.Length == 0)
                throw new InvalidOperationException("Cannot save an empty player party.");

            return Array.ConvertAll(players, player => new BattlePartyMember
            {
                characterId = player.characterId,
                level = player.level,
                experience = player.experience,
                health = player.health,
                mana = player.mana
            });
        }

        internal static BattleItemAmount[] CopyItems(BattleItemAmount[] items)
        {
            if (items == null)
                return new BattleItemAmount[0];

            return Array.ConvertAll(items, item => new BattleItemAmount
            {
                itemId = item.itemId,
                count = item.count
            });
        }
    }
}
