using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Game.Battle;
using UnityEngine;

namespace UnityEngine
{
    // The production persistentDataPath is never used by this test executable.
    public static class Application { public static string persistentDataPath; }
}

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        { IncludeFields = true, IgnoreReadOnlyProperties = true, WriteIndented = true };
    private static BattleData data;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception e) when (e is InvalidOperationException || e is IOException || e is JsonException) { return; }
        throw new Exception("Expected a rejected operation.");
    }
    private static string Encode(object value) => JsonSerializer.Serialize(value, Json);
    private static GameSaveData NewSave() => new GameSaveData
    {
        players = new[] { new BattlePartyMember { characterId = "player", health = 80, mana = 10 } },
        activePartyIds = new[] { "player" },
        items = new[] { new BattleItemAmount { itemId = "potion", count = 3 } }, gold = 50,
        exploration = new ExplorationProgress { sceneName = "Exploration_scene", spawnId = "PlayerSpawn" },
        savedAtUtc = "2026-09-21T00:00:00.0000000Z"
    };
    private static void Main()
    {
        data = JsonSerializer.Deserialize<BattleData>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "BattleData.json")), Json);
        JsonUtility.Deserialize = (json, type) => JsonSerializer.Deserialize(json, type, Json);
        JsonUtility.Serialize = Encode;
        Action[] tests = { LegacyPreviewIsReadOnly, RenamedVillageInCurrentSaveIsReadOnly,
            InvalidLoadPreservesProgress,
            ManualActivationFailurePreservesProgress, ManualActivationKeepsSource,
            DuplicateResultDoesNotAddItems, BattleInventoryIsClearedOnExit, InvalidVitalsAreRejected };
        int failed = 0;
        foreach (var test in tests)
        {
            string root = Path.Combine(Path.GetTempPath(), "JRPG-SaveSafety-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Application.persistentDataPath = root;
            GameRuntimeState.Clear();
            BattleSession.LeaveBattle();
            BattleSession.ClearResult();
            try { test(); Console.WriteLine("PASS " + test.Method.Name); }
            catch (Exception e) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + e.Message); }
            finally { Directory.Delete(root, true); }
        }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} save safety checks passed. JSON/Unity APIs use substitutes; file IO is real and isolated.");
        if (failed > 0) Environment.ExitCode = 1;
    }
    private static void LegacyPreviewIsReadOnly()
    {
        var legacy = NewSave(); legacy.version = 2; legacy.activePartyIds = null;
        string path = GameSaveSystem.GetManualSavePath(1);
        string json = Encode(legacy);
        File.WriteAllText(path, json);
        File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1));
        DateTime timestamp = File.GetLastWriteTimeUtc(path);
        GameRuntimeState.Initialize(NewSave());
        string before = Encode(GameRuntimeState.Snapshot());
        var loaded = GameSaveSystem.LoadManualSave(data, 1);
        Check(loaded.version == 3 && loaded.activePartyIds[0] == "player", "Migration must work in memory.");
        Check(loaded.exploration.sceneName == GameSaveSystem.DefaultExplorationScene,
            "Old village scene name must resolve to the renamed scene in memory.");
        Check(File.ReadAllText(path) == json && File.GetLastWriteTimeUtc(path) == timestamp && !File.Exists(path + ".bak"),
            "Preview must not rewrite old saves or rotate backups.");
        Check(before == Encode(GameRuntimeState.Snapshot()), "Preview changed runtime data.");
    }
    private static void RenamedVillageInCurrentSaveIsReadOnly()
    {
        var save = NewSave();
        save.version = 3;
        save.exploration.hasSavedPosition = true;
        save.exploration.position = new Vector3(4, 2, 8);
        string original = Encode(save);
        File.WriteAllText(GameSaveSystem.AutoSavePath, original);

        var loaded = GameSaveSystem.LoadAutoSave(data);
        Check(loaded.exploration.sceneName == GameSaveSystem.DefaultExplorationScene &&
            loaded.exploration.position.x == 4 && loaded.exploration.hasSavedPosition,
            "Renamed village must retain the saved position in memory.");
        Check(File.ReadAllText(GameSaveSystem.AutoSavePath) == original,
            "Reading a current save must not rewrite the original scene name on disk.");
    }
    private static void InvalidLoadPreservesProgress()
    {
        GameRuntimeState.Initialize(NewSave());
        string before = Encode(GameRuntimeState.Snapshot());
        File.WriteAllText(GameSaveSystem.AutoSavePath, "{ broken json");
        Reject(() => GameSaveSystem.LoadAutoSave(data));
        Check(before == Encode(GameRuntimeState.Snapshot()), "Failed load changed runtime.");
        Check(File.ReadAllText(GameSaveSystem.AutoSavePath) == "{ broken json", "Failed read rewrote save.");
    }
    private static void ManualActivationFailurePreservesProgress()
    {
        GameRuntimeState.Initialize(NewSave());
        string before = Encode(GameRuntimeState.Snapshot());
        var manual = NewSave(); manual.gold = 999;
        string path = GameSaveSystem.GetManualSavePath(1);
        File.WriteAllText(path, Encode(manual));
        File.WriteAllText(GameSaveSystem.AutoSavePath, before);
        Directory.CreateDirectory(GameSaveSystem.AutoSavePath + ".tmp");
        bool rejected = false;
        try { GameSaveSystem.ActivateManualSave(data, 1); }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { rejected = true; }
        Check(rejected && before == Encode(GameRuntimeState.Snapshot()) && File.ReadAllText(GameSaveSystem.AutoSavePath) == before,
            "Failed activation must preserve runtime and auto save.");
        Check(File.ReadAllText(path) == Encode(manual), "Failed activation changed source slot.");
    }
    private static void ManualActivationKeepsSource()
    {
        var manual = NewSave(); manual.version = 2; manual.activePartyIds = null;
        string path = GameSaveSystem.GetManualSavePath(1);
        string original = Encode(manual);
        File.WriteAllText(path, original);
        GameSaveSystem.ActivateManualSave(data, 1);
        Check(GameRuntimeState.Snapshot().version == 3 && GameSaveSystem.LoadAutoSave(data).version == 3,
            "Activated progress must be current version.");
        Check(File.ReadAllText(path) == original, "Activation must not overwrite the manual source slot.");
    }
    private static void DuplicateResultDoesNotAddItems()
    {
        var save = NewSave();
        var players = save.CreatePlayers(data);
        BattleSession.Prepare(players, data.defaultEnemyParty, "Exploration_scene", save.items, save.gold);
        BattleSession.BeginBattle();
        var reward = new BattleRewardResult { gold = 10, items = new[] { new BattleItemAmount { itemId = "potion", count = 2 } } };
        BattleSession.RecordResult(BattleState.Victory, players, reward);
        string first = Encode(BattleSession.Result);
        BattleSession.RecordResult(BattleState.Victory, players, reward);
        Check(Encode(BattleSession.Result) == first && BattleSession.GetItemCount("potion") == 5, "Duplicate result granted items twice.");
        BattleSession.ClearResult();
        BattleSession.RecordResult(BattleState.Victory, players, reward);
        Check(BattleSession.GetItemCount("potion") == 5, "Clearing UI result must not unlock rewards.");
        BattleSession.BeginBattle();
        BattleSession.RecordResult(BattleState.Victory, players, reward);
        Check(BattleSession.GetItemCount("potion") == 5, "A retry must start with the entry inventory.");
    }
    private static void BattleInventoryIsClearedOnExit()
    {
        var save = NewSave();
        BattleSession.Prepare(save.CreatePlayers(data), data.defaultEnemyParty, "Exploration_scene", save.items);
        BattleSession.BeginBattle();
        BattleSession.LeaveBattle();
        Check(BattleSession.GetItemCount("potion") == 0 && !BattleSession.ConsumeItem("potion"), "Old inventory survived leaving battle.");
    }
    private static void InvalidVitalsAreRejected()
    {
        var validate = typeof(GameSaveSystem).GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
        foreach (bool health in new[] { true, false })
        {
            var save = NewSave();
            if (health) save.players[0].health = value; else save.players[0].mana = value;
            bool rejected = false;
            try { validate.Invoke(null, new object[] { save, data }); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
            Check(rejected, "Invalid HP/MP was accepted: " + value);
        }
    }
}
