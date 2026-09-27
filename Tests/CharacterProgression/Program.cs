using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Game.Battle;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        { IncludeFields = true, IgnoreReadOnlyProperties = true };

    private static void Main(string[] args)
    {
        (string Name, Action Test)[] tests =
        {
            ("Level-one stats and CTB ordering remain unchanged", LevelOneBaseline),
            ("EXP accumulates and crosses an exact threshold", ExactThreshold),
            ("One award can grant multiple levels and keep remaining EXP", MultipleLevels),
            ("Zero and negative awards do not change progress", InvalidAwards),
            ("Max level discards excess EXP and rejects further growth", LevelCap),
            ("A level-one cap requires no experience", LevelOneCap),
            ("Large EXP awards do not overflow", LargeAward),
            ("Edited growth, starting level and EXP rules are applied", EditableRules),
            ("Invalid growth and EXP configuration are rejected", InvalidConfiguration),
            ("Default level-up preserves current HP, MP, statuses and turn time", PreserveBattleState),
            ("Optional refill restores a living character only", OptionalRefill),
            ("Default level-up never revives a defeated character", DeadCharacter),
            ("Characters using the same definition progress independently", IndependentCharacters),
            ("Session results capture growth while Retry restores entry", SessionRoundTrip),
            ("Mid-battle restart restores entry without changing runtime progress", MidBattleRestart),
            ("Runtime snapshots isolate input and output", RuntimeIsolation),
            ("Runtime victory preserves position and is idempotent", RuntimeVictory),
            ("Party selection validates order and isolates snapshots", PartySelection),
            ("Party migration preserves old progress", PartyMigration),
            ("Party reserves gain EXP and Retry restores everyone", PartyReserves),
            ("Save file creation and backup rotation preserve each slot", SaveFileRotation),
            ("Save file replace failure preserves original and backup", SaveFileReplaceFailure),
            ("Save file staging failure preserves original", SaveFileStagingFailure),
            ("Menu input belongs only to the active page", MenuInputOwnership),
            ("Menu input blocks open and return frames", MenuInputTransitions),
            ("Menu input ignores cleanup from an inactive page", MenuInputCleanup),
            ("Experience row animates levels without changing rewards", ExperienceRowAnimation),
            ("Experience row handles max level and missing reward", ExperienceRowBoundaries),
            ("Victory EXP is granted in full to every player", FullPartyBattleReward),
            ("Reward snapshots preserve before and after multiple level-ups", RewardSnapshots),
            ("Restored levels rebuild stats without granting EXP again", RestoreProgress),
            ("Invalid stored levels and EXP are rejected", InvalidStoredProgress),
            ("Legacy constructor remains usable", LegacyConstructor)
        };

        if (args.Length > 0)
            tests = tests.Where(test => test.Name.Contains(args[0], StringComparison.OrdinalIgnoreCase)).ToArray();

        foreach (var test in tests)
        {
            test.Test();
            Console.WriteLine("PASS: " + test.Name);
        }

        Console.WriteLine("Passed " + tests.Length + " progression checks. Unity Play Mode was not run.");
    }

    private static BattleData Data()
    {
        return JsonSerializer.Deserialize<BattleData>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "BattleData.json")), JsonOptions);
    }

    private static void LevelOneBaseline()
    {
        BattleData data = Data();
        var players = data.CreateParty(data.defaultPlayerParty, true);
        var enemies = data.CreateParty(data.defaultEnemyParty, false);
        Equal(1, players[0].Level);
        Equal(100f, players[0].Health);
        Equal(20f, players[0].Mana);
        Equal(30f, players[0].AttackPower);
        Equal(20f, players[0].MagicPower);
        Equal(10f, players[0].Speed);
        Equal(70f, enemies[0].Health);
        players.AddRange(enemies);
        var order = new BattleTurnOrder();
        order.InitializeTurnTimes(players);
        Equal("ally,player,goblin,goblin,goblin", string.Join(",", order.GetUpcomingCombatants(players, 5).Select(character => character.CharacterId)));
    }

    private static void ExactThreshold()
    {
        Combatant character = Data().CreateCharacter("player", true);
        Equal(0, character.GainExperience(99));
        Equal(1, character.Level);
        Equal(99, character.Experience);
        Equal(1, character.GainExperience(1));
        Equal(2, character.Level);
        Equal(0, character.Experience);
        Equal(150, character.ExperienceRequiredForNextLevel);
        Equal(110f, character.MaxHealth);
        Equal(22f, character.MaxMana);
        Equal(32f, character.AttackPower);
        Equal(22f, character.MagicPower);
    }

    private static void MultipleLevels()
    {
        Combatant character = Data().CreateCharacter("player", true);
        Equal(2, character.GainExperience(300));
        Equal(3, character.Level);
        Equal(50, character.Experience);
        Equal(200, character.ExperienceRequiredForNextLevel);
        Equal(120f, character.MaxHealth);
        Equal(24f, character.MaxMana);
        Equal(34f, character.AttackPower);
        Equal(24f, character.MagicPower);
        Equal(10f, character.Speed);
    }

    private static void InvalidAwards()
    {
        var progress = new CharacterProgression(new CharacterLevelRules());
        Equal(0, progress.GainExperience(0));
        Throws<ArgumentOutOfRangeException>(() => progress.GainExperience(-1));
        Equal(1, progress.Level);
        Equal(0, progress.Experience);
    }

    private static void LevelCap()
    {
        var progress = new CharacterProgression(new CharacterLevelRules { maxLevel = 3 });
        Equal(2, progress.GainExperience(int.MaxValue));
        Equal(true, progress.IsMaxLevel);
        Equal(0, progress.Experience);
        Equal(0, progress.ExperienceRequiredForNextLevel);
        Equal(0, progress.GainExperience(500));
    }

    private static void LevelOneCap()
    {
        var progress = new CharacterProgression(new CharacterLevelRules { maxLevel = 1 });
        Equal(true, progress.IsMaxLevel);
        Equal(0, progress.GainExperience(100));
        Equal(0, progress.ExperienceRequiredForNextLevel);
    }

    private static void LargeAward()
    {
        var rules = new CharacterLevelRules { maxLevel = 3, firstLevelExperience = int.MaxValue, experienceIncreasePerLevel = 0 };
        var progress = new CharacterProgression(rules, experience: int.MaxValue - 1);
        Equal(1, progress.GainExperience(int.MaxValue));
        Equal(2, progress.Level);
        Equal(int.MaxValue - 1, progress.Experience);
    }

    private static void EditableRules()
    {
        BattleData data = Data();
        data.levelRules.firstLevelExperience = 10;
        data.levelRules.experienceIncreasePerLevel = 5;
        CharacterDefinition definition = data.FindCharacter("player");
        definition.startingLevel = 4;
        definition.growthPerLevel.maxHealth = 7;
        definition.growthPerLevel.speed = 0.5f;
        Combatant character = data.CreateCharacter("player", true);
        Equal(4, character.Level);
        Equal(121f, character.MaxHealth);
        Equal(11.5f, character.Speed);
        Equal(25, character.ExperienceRequiredForNextLevel);
        Equal(1, character.GainExperience(25));
        Equal(128f, character.MaxHealth);
        Equal(12f, character.Speed);
        Equal(100f, definition.maxHealth);
    }

    private static void InvalidConfiguration()
    {
        Throws<InvalidOperationException>(() => new CharacterProgression(new CharacterLevelRules { firstLevelExperience = 0 }));
        Throws<InvalidOperationException>(() => new CharacterProgression(new CharacterLevelRules { maxLevel = 0 }));
        Throws<InvalidOperationException>(() => new CharacterProgression(new CharacterLevelRules { experienceIncreasePerLevel = -1 }));
        Throws<InvalidOperationException>(() => new CharacterProgression(new CharacterLevelRules { experienceIncreasePerLevel = int.MaxValue }));
        BattleData data = Data();
        data.FindCharacter("player").growthPerLevel.maxHealth = -1;
        Throws<InvalidOperationException>(() => data.CreateCharacter("player", true));
        data.FindCharacter("player").growthPerLevel.maxHealth = float.MaxValue;
        Throws<InvalidOperationException>(() => data.CreateCharacter("player", true));
        data.FindCharacter("player").growthPerLevel.maxHealth = 10;
        data.FindCharacter("player").speed = float.NaN;
        Throws<InvalidOperationException>(() => data.CreateCharacter("player", true));
    }

    private static void PreserveBattleState()
    {
        BattleData data = Data();
        Combatant character = data.CreateCharacter("player", true);
        character.TakeDamage(60);
        character.SpendMana(15);
        character.Defend();
        character.NextTurnTime = 12;
        character.ApplyStatus(data.FindStatus("haste"));
        character.GainExperience(300);
        Equal(40f, character.Health);
        Equal(5f, character.Mana);
        Equal(true, character.IsDefending);
        Equal(12f, character.NextTurnTime);
        Equal(3, character.GetStatusTurns("haste"));
        Equal(15f, character.GetEffectiveSpeed());
    }

    private static void OptionalRefill()
    {
        BattleData data = Data();
        data.levelRules.refillHealthAndManaOnLevelUp = true;
        Combatant character = data.CreateCharacter("player", true);
        character.TakeDamage(50);
        character.SpendMana(15);
        character.GainExperience(99);
        Equal(50f, character.Health);
        Equal(5f, character.Mana);
        character.GainExperience(1);
        Equal(110f, character.Health);
        Equal(22f, character.Mana);
        character.TakeDamage(1000);
        character.SpendMana(20);
        character.GainExperience(150);
        Equal(0f, character.Health);
        Equal(2f, character.Mana);
    }

    private static void DeadCharacter()
    {
        Combatant character = Data().CreateCharacter("player", true);
        character.TakeDamage(1000);
        character.GainExperience(300);
        Equal(3, character.Level);
        Equal(0f, character.Health);
        Equal(false, character.IsAlive());
    }

    private static void IndependentCharacters()
    {
        BattleData data = Data();
        var party = data.CreateParty(new[] { "player", "player" }, true);
        party[0].GainExperience(300);
        Equal(3, party[0].Level);
        Equal(1, party[1].Level);
        Equal(0, party[1].Experience);
        Equal(100f, party[1].MaxHealth);
        Equal(100f, data.FindCharacter("player").maxHealth);
        Equal(1, data.FindCharacter("player").startingLevel);
    }

    private static void SessionRoundTrip()
    {
        BattleData data = Data();
        var players = data.CreateParty(data.defaultPlayerParty, true);
        players[0].GainExperience(300);
        players[0].Health = 40;
        players[0].Mana = 5;
        BattleSession.Prepare(players, data.defaultEnemyParty, "Title_scene", data.defaultInventory);
        BattleSession.BeginBattle();
        var battlePlayers = BattleSession.Entry.CreatePlayers(data);
        battlePlayers[0].GainExperience(150);
        battlePlayers[0].Health = 0;
        BattleSession.ConsumeItem("potion");
        BattleSession.RecordResult(BattleState.Defeat, battlePlayers);
        Equal(4, BattleSession.Result.players[0].level);
        Equal(0, BattleSession.Result.players[0].experience);
        Equal(0f, BattleSession.Result.players[0].health);

        var retryPlayers = BattleSession.Entry.CreatePlayers(data);
        BattleSession.BeginBattle();
        Equal(3, retryPlayers[0].Level);
        Equal(50, retryPlayers[0].Experience);
        Equal(120f, retryPlayers[0].MaxHealth);
        Equal(40f, retryPlayers[0].Health);
        Equal(5f, retryPlayers[0].Mana);
        Equal(3, BattleSession.GetItemCount("potion"));

        BattleSession.RecordResult(BattleState.Victory, retryPlayers);
        BattlePartyMember saved = JsonSerializer.Deserialize<BattlePartyMember>(JsonSerializer.Serialize(BattleSession.Result.players[0], JsonOptions), JsonOptions);
        Combatant restored = data.CreateCharacter(saved.characterId, true, saved.level, saved.experience);
        Equal(3, restored.Level);
        Equal(50, restored.Experience);
        Equal(120f, restored.MaxHealth);
        BattleSession.LeaveBattle();
        Equal(true, BattleSession.Entry == null);
        Equal(3, BattleSession.Result.players[0].level);
    }

    private static void MidBattleRestart()
    {
        BattleData data = Data();
        GameRuntimeState.Initialize(RuntimeFixture());
        GameSaveData before = GameRuntimeState.Snapshot();
        BattleSession.Prepare(before.CreateActiveParty(data), data.defaultEnemyParty,
            before.exploration.sceneName, before.items, before.gold, before.CreatePlayers(data));
        BattleSession.BeginBattle();
        var party = BattleSession.Entry.CreatePlayers(data);
        party[0].Health = 1;
        party[0].Mana = 0;
        party[0].GainExperience(300);
        Equal(true, BattleSession.ConsumeItem("potion"));

        // Reload initialization reuses Entry even when no victory/defeat result exists.
        var restarted = BattleSession.Entry.CreatePlayers(data);
        BattleSession.BeginBattle();
        BattleSession.ClearResult();
        Equal(before.players[0].health, restarted[0].Health);
        Equal(before.players[0].mana, restarted[0].Mana);
        Equal(before.players[0].level, restarted[0].Level);
        Equal(before.players[0].experience, restarted[0].Experience);
        Equal(before.items[0].count, BattleSession.GetItemCount(before.items[0].itemId));
        Equal(before.gold, BattleSession.Entry.gold);
        Equal(JsonSerializer.Serialize(before, JsonOptions),
            JsonSerializer.Serialize(GameRuntimeState.Snapshot(), JsonOptions));
        Equal(true, BattleSession.Result == null);
        BattleSession.LeaveBattle();
    }

    private static void ExperienceRowAnimation()
    {
        var data = Data();
        var player = new BattlePartyMember { characterId = "player", level = 3, experience = 50 };
        var change = new CharacterExperienceReward
        {
            characterId = "player", beforeLevel = 1, beforeExperience = 0,
            afterLevel = 3, afterExperience = 50
        };
        string start = CharacterExperienceText.Format(data, player, change, 0);
        Equal(true, start.Contains("EXP 0/100 -> 0/100"));
        Equal(false, start.Contains("#FFD700"));
        string exact = CharacterExperienceText.Format(data, player, change, 100);
        Equal(true, exact.Contains("<color=#FFD700>2</color>"));
        Equal(true, exact.Contains("EXP 0/100 -> 0/150"));
        string finished = CharacterExperienceText.Format(data, player, change, 300);
        Equal(true, finished.Contains("<color=#FFD700>3</color>"));
        Equal(true, finished.Contains("EXP 0/100 -> 50/200"));
        Equal(finished, CharacterExperienceText.Format(data, player, change, 300));
        Equal(3, player.level);
        Equal(50, player.experience);
        Equal(0, change.beforeExperience);
        Equal(50, change.afterExperience);
    }

    private static void ExperienceRowBoundaries()
    {
        var data = Data();
        var player = new BattlePartyMember { characterId = "player", level = data.levelRules.maxLevel };
        var change = new CharacterExperienceReward
        {
            characterId = "player", beforeLevel = player.level, afterLevel = player.level
        };
        Equal(true, CharacterExperienceText.Format(data, player, change, 9999).Contains("EXP MAX -> MAX"));
        player.level = 2;
        player.experience = 10;
        string fallback = CharacterExperienceText.Format(data, player, null, 300);
        Equal(true, fallback.Contains("EXP 10/150 -> 10/150"));
        Equal(false, fallback.Contains("#FFD700"));
    }

    private static void MenuInputOwnership()
    {
        var input = new MenuInputState();
        object root = new object(), child = new object();
        input.Activate(root, 10);
        Equal(true, input.CanRead(root, 11));
        Equal(false, input.CanRead(child, 11));
        input.Activate(child, 11);
        Equal(false, input.CanRead(root, 12));
        Equal(true, input.CanRead(child, 12));
        input.Block(12);
        Equal(false, input.CanRead(child, 12));
        Equal(true, input.CanRead(child, 13));
    }

    private static void MenuInputTransitions()
    {
        var input = new MenuInputState();
        object root = new object(), child = new object();
        input.Activate(root, 0);
        Equal(false, input.CanRead(root, 0));
        input.Activate(child, 1);
        Equal(false, input.CanRead(child, 1));
        input.Release(child, 2);
        input.Activate(root, 2);
        Equal(false, input.CanRead(root, 2));
        Equal(true, input.IsBlocked(2));
        Equal(true, input.CanRead(root, 3));
        input.Release(root, 3);
        Equal(true, input.IsBlocked(3));
        Equal(false, input.IsBlocked(4));
    }

    private static void MenuInputCleanup()
    {
        var input = new MenuInputState();
        object root = new object(), child = new object();
        input.Activate(child, 5);
        input.Release(root, 6);
        Equal(true, input.CanRead(child, 6));
        input.Release(child, 6);
        Equal(true, input.Owner == null);
        input = new MenuInputState();
        Equal(false, input.IsBlocked(0));
        Equal(false, input.CanRead(child, 0));
    }

    private static void WithSaveDirectory(Action<string> test)
    {
        string directory = Path.Combine(Path.GetTempPath(), "PartyGameSaveTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { test(directory); }
        finally
        {
            foreach (string file in Directory.GetFiles(directory))
                File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static void SaveFileRotation()
    {
        WithSaveDirectory(directory =>
        {
            foreach (string name in new[] { "save.json", "save_slot_01.json", "save_slot_10.json" })
            {
                string path = Path.Combine(directory, name);
                string first = "{\"gold\":1,\"name\":\"\u89d2\u8272\"}";
                GameSaveSystem.WriteSaveFile(path, first);
                Equal(first, File.ReadAllText(path));
                Equal(false, File.Exists(path + ".bak"));
                File.WriteAllText(path + ".tmp", "interrupted earlier write");
                GameSaveSystem.WriteSaveFile(path, "{\"gold\":2}");
                Equal(first, File.ReadAllText(path + ".bak"));
                GameSaveSystem.WriteSaveFile(path, "{\"gold\":3}");
                Equal("{\"gold\":2}", File.ReadAllText(path + ".bak"));
                Equal("{\"gold\":3}", File.ReadAllText(path));
                Equal(false, File.Exists(path + ".tmp"));
            }
        });
    }

    private static void SaveFileReplaceFailure()
    {
        WithSaveDirectory(directory =>
        {
            string path = Path.Combine(directory, "save.json");
            GameSaveSystem.WriteSaveFile(path, "{\"gold\":1}");
            GameSaveSystem.WriteSaveFile(path, "{\"gold\":2}");
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                Throws<IOException>(() => GameSaveSystem.WriteSaveFile(path, "{\"gold\":3}"));
            Equal("{\"gold\":2}", File.ReadAllText(path));
            Equal("{\"gold\":1}", File.ReadAllText(path + ".bak"));
            Equal(false, File.Exists(path + ".tmp"));
            GameSaveSystem.WriteSaveFile(path, "{\"gold\":3}");
            Equal("{\"gold\":3}", File.ReadAllText(path));
        });
    }

    private static void SaveFileStagingFailure()
    {
        WithSaveDirectory(directory =>
        {
            string path = Path.Combine(directory, "save.json");
            GameSaveSystem.WriteSaveFile(path, "{\"gold\":1}");
            using (var locked = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                Throws<IOException>(() => GameSaveSystem.WriteSaveFile(path, "{\"gold\":2}"));
            Equal("{\"gold\":1}", File.ReadAllText(path));
            Equal(false, File.Exists(path + ".bak"));
            GameSaveSystem.WriteSaveFile(path, "{\"gold\":2}");
            Equal("{\"gold\":1}", File.ReadAllText(path + ".bak"));
        });
    }

    private static GameSaveData RuntimeFixture()
    {
        return new GameSaveData
        {
            players = new[] { new BattlePartyMember { characterId = "player", health = 80, mana = 10 } },
            activePartyIds = new[] { "player" },
            items = new[] { new BattleItemAmount { itemId = "potion", count = 3 } },
            gold = 50,
            exploration = new ExplorationProgress { sceneName = "Exploration_scene", spawnId = "PlayerSpawn" }
        };
    }

    private static void RuntimeIsolation()
    {
        GameRuntimeState.Clear();
        Throws<InvalidOperationException>(() => GameRuntimeState.Snapshot());
        var source = RuntimeFixture();
        GameRuntimeState.Initialize(source);
        source.players[0].health = 0;
        source.items[0].count = 0;
        source.exploration.sceneName = "changed";
        var copy = GameRuntimeState.Snapshot();
        Equal(80f, copy.players[0].health);
        Equal(3, copy.items[0].count);
        Equal("Village_scene", copy.exploration.sceneName);
        copy.players[0].health = 1;
        copy.items[0].count = 1;
        copy.gold = 0;
        Equal(80f, GameRuntimeState.Snapshot().players[0].health);
        Equal(3, GameRuntimeState.Snapshot().items[0].count);
        Equal(50, GameRuntimeState.Snapshot().gold);
        GameRuntimeState.Initialize(RuntimeFixture());
        Equal(80f, GameRuntimeState.Snapshot().players[0].health);
    }

    private static void RuntimeVictory()
    {
        GameRuntimeState.Initialize(RuntimeFixture());
        GameRuntimeState.SetPosition("Exploration_scene", "PlayerSpawn", new UnityEngine.Vector3(4, 2, 8));
        var result = new BattleOutcome
        {
            state = BattleState.Victory,
            players = new[] { new BattlePartyMember { characterId = "player", level = 2, health = 35, mana = 4 } },
            items = new[] { new BattleItemAmount { itemId = "potion", count = 2 } }, gold = 250
        };
        GameRuntimeState.ApplyVictory(result);
        GameRuntimeState.ApplyVictory(result);
        result.players[0].health = 0;
        var current = GameRuntimeState.Snapshot();
        Equal(35f, current.players[0].health);
        Equal(2, current.players[0].level);
        Equal(2, current.items[0].count);
        Equal(250, current.gold);
        Equal(4f, current.exploration.position.x);
        Equal(true, current.exploration.hasSavedPosition);
        result.state = BattleState.Defeat;
        Throws<InvalidOperationException>(() => GameRuntimeState.ApplyVictory(result));
        Equal(35f, GameRuntimeState.Snapshot().players[0].health);
        GameRuntimeState.Clear();
    }

    private static GameSaveData PartyFixture()
    {
        var save = RuntimeFixture();
        save.players = Data().CreateParty(Data().defaultPlayerParty, true).Select(character =>
            new BattlePartyMember { characterId = character.CharacterId, health = 25, mana = 4 }).ToArray();
        return save;
    }

    private static void PartySelection()
    {
        GameRuntimeState.Initialize(PartyFixture());
        var ids = new[] { "healer", "player" };
        GameRuntimeState.SetActiveParty(ids);
        ids[0] = "ally";
        var snapshot = GameRuntimeState.Snapshot();
        Equal("healer,player", string.Join(",", snapshot.CreateActiveParty(Data()).Select(p => p.CharacterId)));
        Equal(3, snapshot.CreatePlayers(Data()).Count);
        snapshot.activePartyIds[0] = "ally";
        foreach (var invalid in new[] { Array.Empty<string>(), new[] { "player", "player" },
            new[] { "goblin" }, new[] { "player", "ally", "healer", "player" } })
            Throws<InvalidOperationException>(() => GameRuntimeState.SetActiveParty(invalid));
        Throws<InvalidOperationException>(() => GameRuntimeState.SetActiveParty(null));
        Equal("healer,player", string.Join(",", GameRuntimeState.Snapshot().activePartyIds));
        GameRuntimeState.SetActiveParty(new[] { "ally" });
        Equal(1, GameRuntimeState.Snapshot().CreateActiveParty(Data()).Count);
        var roundTrip = JsonSerializer.Deserialize<GameSaveData>(
            JsonSerializer.Serialize(GameRuntimeState.Snapshot(), JsonOptions), JsonOptions);
        Equal("ally", roundTrip.activePartyIds[0]);
        Equal(3, roundTrip.players.Length);
    }

    private static void PartyMigration()
    {
        var migrate = typeof(GameSaveSystem).GetMethod("Migrate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        foreach (int version in new[] { 1, 2 })
        {
            var save = PartyFixture();
            save.version = version;
            save.activePartyIds = null;
            save.exploration.position = new UnityEngine.Vector3(7, 0, 9);
            Equal(true, (bool)migrate.Invoke(null, new object[] { save }));
            save.ValidateParty();
            Equal(3, save.version);
            Equal("player,ally,healer", string.Join(",", save.activePartyIds));
            Equal(25f, save.players[0].health);
            Equal(version == 2 ? 7f : 0f, save.exploration.position.x);
            Equal(false, (bool)migrate.Invoke(null, new object[] { save }));
        }
    }

    private static void PartyReserves()
    {
        var data = Data();
        GameRuntimeState.Initialize(PartyFixture());
        GameRuntimeState.SetActiveParty(new[] { "healer", "player" });
        var save = GameRuntimeState.Snapshot();
        BattleSession.Prepare(save.CreateActiveParty(data), data.defaultEnemyParty,
            "Exploration_scene", save.items, save.gold, save.CreatePlayers(data));
        BattleSession.BeginBattle();
        var active = BattleSession.Entry.CreatePlayers(data);
        active[0].Health = 3;
        var everyone = BattleSession.Entry.CreateRewardParty(data, active);
        Equal("healer,player,ally", string.Join(",", everyone.Select(p => p.CharacterId)));
        var reward = new BattleRewardResult { experiencePerCharacter = 300 };
        BattleRewardCalculator.GrantExperience(reward, everyone);
        BattleSession.RecordResult(BattleState.Victory, everyone, reward);
        GameRuntimeState.ApplyVictory(BattleSession.Result);
        GameRuntimeState.ApplyVictory(BattleSession.Result);
        var result = GameRuntimeState.Snapshot();
        Equal("healer,player", string.Join(",", result.activePartyIds));
        Equal("player,ally,healer", string.Join(",", result.players.Select(p => p.characterId)));
        foreach (var member in result.players) { Equal(3, member.level); Equal(50, member.experience); }
        Equal(25f, result.players[1].health);
        Equal(3f, result.players[2].health);
        var retry = BattleSession.Entry.CreateRewardParty(data, BattleSession.Entry.CreatePlayers(data));
        foreach (var member in retry) { Equal(1, member.Level); Equal(0, member.Experience); Equal(25f, member.Health); }
        BattleSession.BeginBattle();
        BattleSession.RecordResult(BattleState.Defeat, retry);
        Throws<InvalidOperationException>(() => GameRuntimeState.ApplyVictory(BattleSession.Result));
        Equal(3, GameRuntimeState.Snapshot().players[1].level);
        BattleSession.LeaveBattle();
        GameRuntimeState.Clear();
    }

    private static void FullPartyBattleReward()
    {
        BattleData data = Data();
        var players = data.CreateParty(data.defaultPlayerParty, true);
        BattleRewardResult reward = BattleRewardCalculator.Calculate(data,
            data.CreateParty(data.defaultEnemyParty, false));

        Equal(90, reward.experiencePerCharacter);
        Equal("goblin_dirty_pants", reward.items[0].itemId);
        Equal(3, reward.items[0].count);
        BattleRewardCalculator.GrantExperience(reward, players);

        foreach (Combatant player in players)
            Equal(90, player.Experience);
    }

    private static void RewardSnapshots()
    {
        BattleData data = Data();
        var player = data.CreateCharacter("player", true, experience: 90);
        var reward = new BattleRewardResult { experiencePerCharacter = 300 };
        BattleRewardCalculator.GrantExperience(reward, new System.Collections.Generic.List<Combatant> { player });
        var change = reward.characters[0];
        Equal("player", change.characterId);
        Equal(1, change.beforeLevel);
        Equal(90, change.beforeExperience);
        Equal(3, change.afterLevel);
        Equal(140, change.afterExperience);

        var display = new CharacterProgression(data.levelRules, change.beforeLevel, change.beforeExperience);
        display.GainExperience(10);
        Equal(2, display.Level);
        Equal(0, display.Experience);
        display.GainExperience(290);
        Equal(change.afterLevel, display.Level);
        Equal(change.afterExperience, display.Experience);
        player.GainExperience(1000);
        Equal(3, change.afterLevel);
        Equal(90, change.beforeExperience);
    }

    private static void RestoreProgress()
    {
        Combatant character = Data().CreateCharacter("healer", true, level: 3, experience: 50);
        Equal(3, character.Level);
        Equal(50, character.Experience);
        Equal(72f, character.MaxHealth);
        Equal(16f, character.MaxMana);
        Equal(32f, character.AttackPower);
        Equal(38f, character.MagicPower);
        Equal(72f, character.Health);
        Equal(16f, character.Mana);
    }

    private static void InvalidStoredProgress()
    {
        BattleData data = Data();
        Throws<ArgumentOutOfRangeException>(() => data.CreateCharacter("player", true, level: 0));
        Throws<ArgumentOutOfRangeException>(() => data.CreateCharacter("player", true, level: 100));
        Throws<ArgumentOutOfRangeException>(() => data.CreateCharacter("player", true, experience: -1));
        Throws<ArgumentOutOfRangeException>(() => data.CreateCharacter("player", true, experience: 100));
        Throws<ArgumentOutOfRangeException>(() => data.CreateCharacter("player", true, level: 99, experience: 1));
    }

    private static void LegacyConstructor()
    {
        Combatant character = new Combatant("Test", 10, 2, 3, 4, 5, true);
        Equal(1, character.Level);
        Equal(10f, character.Health);
        Equal(1, character.GainExperience(100));
        Equal(10f, character.MaxHealth);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual))
            throw new Exception("Expected " + expected + ", got " + actual);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
}
