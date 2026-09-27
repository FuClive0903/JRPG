using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Battle;
using UnityEngine;

internal static class Program
{
    private static void Set(object owner, string field, object value) =>
        owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static void Call(object owner, string method) =>
        owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    // Deliberately hold each stage until the test releases it: completion is not a timer.
    private sealed class GatedView : BattleActorView
    {
        public bool ActionStarted, RecoveryStarted, ReactionStarted, DefeatStarted;
        public BattleNumberStyle LastNumberStyle;
        public bool FinishAction, FinishReaction, FinishDefeat, FinishRecovery = true;
        public override IEnumerator PlayAction(BattleCommandType command, BattleActionDefinition action)
        { ActionStarted = true; while (!FinishAction) yield return null; }
        public override IEnumerator PlayActionRecovery()
        { RecoveryStarted = true; while (!FinishRecovery) yield return null; }
        public override IEnumerator PlayReaction(float delta, BattleNumberStyle numberStyle)
        { ReactionStarted = true; LastNumberStyle = numberStyle; while (!FinishReaction) yield return null; }
        public override IEnumerator PlayDefeat()
        { DefeatStarted = true; while (!FinishDefeat) yield return null; }
    }

    private sealed class Fixture
    {
        public readonly BattleController Controller = new BattleController();
        public readonly Combatant Player = new Combatant("Player", 100, 20, 30, 0, 10, true, "p");
        public readonly Combatant Enemy = new Combatant("Enemy", 20, 10, 1, 0, 5, false, "e");
        public readonly GatedView PlayerView = new GatedView();
        public readonly GatedView EnemyView = new GatedView();
        public readonly BattleFieldView Field = new BattleFieldView();
        public int EndCount;
        public Fixture()
        {
            var players = new List<Combatant> { Player };
            var enemies = new List<Combatant> { Enemy };
            Set(Field, "playerSlots", new[] { new Transform { Children = new Component[] { PlayerView } } });
            Set(Field, "enemySlots", new[] { new Transform { Children = new Component[] { EnemyView } } });
            Field.Setup(players, enemies, Controller.OnActorClicked);
            Set(Controller, "battleFieldView", Field);
            Set(Controller, "playerParty", players);
            Set(Controller, "enemyParty", enemies);
            Set(Controller, "currentCombatant", Player);
            Set(Controller, "battleData", new BattleData
            {
                characters = new[] { new CharacterDefinition { id = "e", experienceReward = 10 } }
            });
            var order = new BattleTurnOrder();
            order.InitializeTurnTimes(new List<Combatant> { Player, Enemy });
            Set(Controller, "turnOrder", order);
            BattleSession.Prepare(players, new[] { "e" }, "Title_scene",
                new[] { new BattleItemAmount { itemId = "potion", count = 2 } });
            BattleSession.BeginBattle();
            Controller.BattleEnded += _ => EndCount++;
            Controller.ChangeState(BattleState.WaitingForCommand);
        }
        public void Attack()
        {
            Controller.OnAttackButtonPressed();
            Controller.OnActorClicked(Enemy);
            Controller.StepFrame();
        }
        public void Frames(int count = 3)
        { for (int i = 0; i < count; i++) Controller.StepFrame(); }
    }

    private static void Main()
    {
        Action[] tests = { MapBackgroundOverridesDefault, BackgroundFallsBackToDefault,
            EmptyBackgroundIsAllowed, RetryAndNextMapInstantiateCorrectBackground,
            TerminalResultCannotBeReentered, EnvironmentSurvivesRetryAndClearsBetweenMaps, WaitsForAllStages, RecoveryOverlapsReaction,
            WaitsForNextTurn, PoisonDeathWaits,
            ItemConsumedOnce, CancelStopsPendingDamage, ReviveAndReuseRestoreColors,
            MissingViewsComplete, AllTargetsFinishBeforeVictory, StunReturnsToCommand,
            AppearanceKeepsFootFixed, SpriteAttackStopsAtImpactAndRecovers,
            SpriteHurtPlaysOnceAndReturnsIdle,
            SpriteDefeatPlaysOnceAndHoldsLastFrame,
            DataAppearanceOverridesSlot, GroundAndMarkerDoNotFade,
            IntroLocksCommandsUntilFadeOut, OutcomeMovesToIndependentDock,
            MenuPausePreservesSelection, MenuPauseStopsActionAndDefeat, MenuPauseStopsIntro };
        foreach (var test in tests) { test(); Console.WriteLine("PASS " + test.Method.Name); }
        PauseMenuChecks.Run();
        Console.WriteLine($"{tests.Length} managed playback tests passed; Unity Play Mode still required.");
    }

    private static void CheckBackground(GameObject fallback, GameObject expected)
    {
        UnityEngine.Object.Instantiations.Clear();
        var view = new BattleEnvironmentView();
        Set(view, "defaultEnvironmentPrefab", fallback);
        Call(view, "Awake");
        var calls = UnityEngine.Object.Instantiations;
        Check(calls.Count == (expected == null ? 0 : 1), "Unexpected background instance count.");
        if (expected != null)
            Check(ReferenceEquals(calls[0].prefab, expected) &&
                ReferenceEquals(calls[0].parent, view.transform) && !calls[0].worldPositionStays,
                "Wrong background, parent or local-space instantiation.");
    }

    private static void PrepareBackground(GameObject background)
    {
        BattleSession.Prepare(new List<Combatant> { new Combatant("Player", 100, 20, 30, 0, 10, true, "p") },
            new[] { "e" }, "Exploration_scene", environmentPrefab: background);
        BattleSession.BeginBattle();
    }

    private static void MapBackgroundOverridesDefault()
    {
        var map = new GameObject();
        PrepareBackground(map);
        CheckBackground(new GameObject(), map);
        CheckBackground(null, map);
    }

    private static void BackgroundFallsBackToDefault()
    {
        var fallback = new GameObject();
        PrepareBackground(null);
        CheckBackground(fallback, fallback);
        BattleSession.LeaveBattle();
        CheckBackground(fallback, fallback);
    }

    private static void EmptyBackgroundIsAllowed()
    {
        PrepareBackground(null);
        CheckBackground(null, null);
        BattleSession.LeaveBattle();
        CheckBackground(null, null);
    }

    private static void RetryAndNextMapInstantiateCorrectBackground()
    {
        var first = new GameObject();
        var second = new GameObject();
        var fallback = new GameObject();
        PrepareBackground(first);
        CheckBackground(fallback, first);
        BattleSession.RecordResult(BattleState.Defeat, new List<Combatant>());
        BattleSession.ClearResult();
        BattleSession.BeginBattle();
        CheckBackground(fallback, first);
        BattleSession.LeaveBattle();
        PrepareBackground(second);
        CheckBackground(fallback, second);
        BattleSession.LeaveBattle();
        PrepareBackground(null);
        CheckBackground(fallback, fallback);
        BattleSession.LeaveBattle();
    }

    private static void TerminalResultCannotBeReentered()
    {
        var f = new Fixture();
        f.Controller.ChangeState(BattleState.Victory);
        var result = BattleSession.Result;
        f.Controller.ChangeState(BattleState.WaitingForCommand);
        f.Controller.ChangeState(BattleState.Victory);
        f.Controller.ChangeState(BattleState.Defeat);
        Check(f.EndCount == 1 && f.Player.Experience == 10 &&
            f.Controller.CurrentState == BattleState.Victory && ReferenceEquals(result, BattleSession.Result),
            "A finished battle was reopened or granted rewards again.");
    }

    private static void EnvironmentSurvivesRetryAndClearsBetweenMaps()
    {
        var players = new List<Combatant> { new Combatant("Player", 100, 20, 30, 0, 10, true, "p") };
        var forest = new GameObject();
        var village = new GameObject();
        BattleSession.Prepare(players, new[] { "e" }, "Forest", environmentPrefab: forest);
        BattleSession.BeginBattle();
        BattleSession.RecordResult(BattleState.Defeat, players);
        BattleSession.ClearResult();
        BattleSession.BeginBattle();
        Check(ReferenceEquals(forest, BattleSession.EnvironmentPrefab), "Retry must keep the map background.");
        BattleSession.LeaveBattle();
        Check(BattleSession.EnvironmentPrefab == null, "Leaving must release the background reference.");
        BattleSession.Prepare(players, new[] { "e" }, "Village", environmentPrefab: village);
        Check(ReferenceEquals(village, BattleSession.EnvironmentPrefab), "A new map must use its own background.");
        BattleSession.Prepare(players, new[] { "e" }, "Empty");
        Check(BattleSession.EnvironmentPrefab == null, "An unconfigured map must not reuse the last background.");
        BattleSession.Prepare(players, new[] { "e" }, "Forest", environmentPrefab: forest);
        typeof(BattleSession).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Check(BattleSession.Entry == null && BattleSession.EnvironmentPrefab == null,
            "Play Mode reset must clear the background and entry.");
    }

    private static void WaitsForAllStages()
    {
        var f = new Fixture();
        f.Attack();
        Check(f.PlayerView.ActionStarted && f.Enemy.Health == 20, "Damage happened before action completed.");
        f.Controller.OnDefendButtonPressed();
        f.Controller.OnActorClicked(f.Enemy);
        f.Frames();
        Check(!f.Player.IsDefending && f.EndCount == 0, "Input was accepted while resolving.");
        f.PlayerView.FinishAction = true;
        f.Frames();
        Check(f.Enemy.Health == 0 && !f.EnemyView.ReactionStarted &&
            f.EnemyView.DefeatStarted && f.EndCount == 0,
            "Fatal damage should skip reaction and start defeat.");
        f.EnemyView.FinishDefeat = true;
        f.Frames();
        Check(f.Controller.CurrentState == BattleState.Victory && f.EndCount == 1, "Victory must happen exactly once.");
        Check(f.Player.Experience == 10, "Experience was duplicated.");
    }

    private static void RecoveryOverlapsReaction()
    {
        var f = new Fixture();
        f.Enemy.Health = 100;
        f.Enemy.NextTurnTime = 15;
        float turnTime = f.Player.NextTurnTime;
        f.PlayerView.FinishRecovery = false;
        f.Attack();
        f.PlayerView.FinishAction = true;
        f.Frames();
        Check(f.PlayerView.RecoveryStarted && f.EnemyView.ReactionStarted,
            "Recovery did not begin together with the target reaction.");

        f.EnemyView.FinishReaction = true;
        f.Frames();
        Check(f.Controller.CurrentState == BattleState.ResolvingAction,
            "Turn advanced before the overlapping recovery finished.");

        f.PlayerView.FinishRecovery = true;
        f.Frames();
        Check(f.Player.NextTurnTime > turnTime,
            "Battle did not continue after the recovery finished.");
    }

    private static void MenuPausePreservesSelection()
    {
        var f = new Fixture();
        f.Controller.OnAttackButtonPressed();
        f.Controller.SetMenuPaused(true);
        f.Controller.OnActorClicked(f.Enemy);
        f.Controller.OnDefendButtonPressed();
        f.Frames(10);
        Check(f.Controller.CurrentState == BattleState.SelectingTarget &&
            f.Controller.SelectedTarget == f.Enemy && !f.PlayerView.ActionStarted,
            "Pause changed selection or accepted input.");
        f.Controller.SetMenuPaused(false);
        f.Controller.OnActorClicked(f.Enemy);
        Check(!f.PlayerView.ActionStarted, "Close-frame confirmation leaked into battle.");
        f.Frames(1);
        f.Controller.OnActorClicked(f.Enemy);
        f.Frames(1);
        Check(f.PlayerView.ActionStarted, "Battle did not accept input after resume.");
    }

    private static void MenuPauseStopsActionAndDefeat()
    {
        var f = new Fixture();
        f.Controller.OnAttackButtonPressed();
        f.Controller.OnActorClicked(f.Enemy);
        f.Controller.SetMenuPaused(true);
        f.Frames(10);
        Check(!f.PlayerView.ActionStarted && f.Enemy.Health == 20,
            "Frame-only action prelude advanced during pause.");
        f.Controller.SetMenuPaused(false);
        f.Frames(1);
        f.Controller.SetMenuPaused(true);
        f.PlayerView.FinishAction = true;
        f.Frames(10);
        Check(f.Enemy.Health == 20, "Nested playback applied damage during pause.");
        f.Controller.SetMenuPaused(false);
        f.Frames(2);
        f.Frames(2);
        Check(f.EnemyView.DefeatStarted, "Fatal damage did not start defeat playback.");
        f.Controller.SetMenuPaused(true);
        f.EnemyView.FinishDefeat = true;
        f.Frames(10);
        Check(f.EndCount == 0 && f.Player.Experience == 0, "Paused defeat granted rewards.");
        f.Controller.SetMenuPaused(false);
        f.Frames(3);
        Check(f.EndCount == 1 && f.Player.Experience == 10, "Resume duplicated or lost rewards.");
    }

    private static void MenuPauseStopsIntro()
    {
        var f = new Fixture();
        var transition = new BattleTransitionView();
        var text = new TMPro.TextMeshProUGUI();
        Set(transition, "messageText", text);
        Set(f.Controller, "transitionView", transition);
        f.Controller.ChangeState(BattleState.Start);
        var intro = (IEnumerator)typeof(BattleController).GetMethod("BeginBattle",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f.Controller, null);
        var playback = (IEnumerator)typeof(BattleController).GetMethod("RunPlayback",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f.Controller, new object[] { intro });
        f.Controller.StartCoroutine(playback);
        float alpha = text.alpha;
        f.Controller.SetMenuPaused(true);
        f.Frames(30);
        Check(text.alpha == alpha && f.Controller.CurrentState == BattleState.Start,
            "Unscaled intro continued during menu pause.");
        f.Controller.SetMenuPaused(false);
        f.Frames(40);
        Check(f.Controller.CurrentState == BattleState.WaitingForCommand,
            "Intro did not resume from its paused position.");
    }

    private static void WaitsForNextTurn()
    {
        var f = new Fixture();
        f.Enemy.Health = 100;
        f.Enemy.NextTurnTime = 15;
        float turnTime = f.Player.NextTurnTime;
        f.Attack();
        f.PlayerView.FinishAction = true;
        f.Frames();
        Check(f.Player.NextTurnTime == turnTime, "Turn advanced before reaction completed.");
        f.EnemyView.FinishReaction = true;
        f.Frames();
        Check(f.Player.NextTurnTime > turnTime && f.EnemyView.ActionStarted, "Next enemy turn did not start.");
        Check(!f.EnemyView.DefeatStarted, "Living target played defeat.");
    }

    private static void PoisonDeathWaits()
    {
        var f = new Fixture();
        f.Player.ApplyStatus(new StatusDefinition
        { id = "poison", effect = "Damage", timing = "TurnStart", value = 100, durationTurns = 2 });
        Call(f.Controller, "BeginNextTurn");
        f.Frames();
        Check(f.Player.Health == 0 && !f.PlayerView.ReactionStarted &&
            f.PlayerView.DefeatStarted && f.EndCount == 0,
            "Fatal poison should skip reaction and start defeat.");
        f.PlayerView.FinishDefeat = true;
        f.Frames();
        Check(f.Controller.CurrentState == BattleState.Defeat && f.EndCount == 1, "Poison defeat failed.");
    }

    private static void ItemConsumedOnce()
    {
        var f = new Fixture();
        var item = new BattleActionDefinition
        { id = "potion", target = "Self", effect = "Heal", power = 10 };
        f.Player.Health = 50;
        f.Controller.OnItemSelected(item);
        f.Controller.OnActorClicked(f.Player);
        f.Controller.StepFrame();
        f.Controller.OnItemSelected(item);
        f.Controller.OnActorClicked(f.Player);
        f.Frames();
        Check(BattleSession.GetItemCount("potion") == 1 && f.Player.Health == 50, "Item was repeated or applied early.");
        f.PlayerView.FinishAction = true;
        f.Frames();
        Check(f.Player.Health == 60 && BattleSession.GetItemCount("potion") == 1, "Item result was not applied once.");
        Check(f.PlayerView.LastNumberStyle == BattleNumberStyle.Healing, "Healing was not green-style.");
    }

    private static void CancelStopsPendingDamage()
    {
        var f = new Fixture();
        f.Attack();
        Call(f.Controller, "OnDisable");
        f.PlayerView.FinishAction = f.EnemyView.FinishReaction = f.EnemyView.FinishDefeat = true;
        f.Frames();
        Check(f.Enemy.Health == 20 && f.EndCount == 0 && f.Controller.CurrentState == BattleState.Paused,
            "Cancelled playback continued changing combat state.");
        BattleSession.BeginBattle();
        Check(BattleSession.GetItemCount("potion") == 2 && BattleSession.Entry.players[0].health == 100,
            "Cancellation damaged the retry snapshot.");
    }

    private static void ReviveAndReuseRestoreColors()
    {
        var f = new Fixture();
        var sprite = new SpriteRenderer { color = new Color(0.7f, 0.8f, 0.9f, 0.8f) };
        var view = new BattleActorView { Children = new Component[] { sprite } };
        view.Setup(f.Player, null);
        f.Player.Health = 0;
        var defeat = new Coroutine(view.PlayDefeat());
        while (defeat.Step()) { }
        Check(sprite.color.a == 0, "Defeated sprite stayed visible.");
        f.Player.Revive(10);
        var revive = new Coroutine(view.PlayReaction(10, BattleNumberStyle.Healing));
        while (revive.Step()) { }
        Check(sprite.color.a == 0.8f && sprite.color.r == 0.7f, "Revive lost the original appearance.");
        f.Player.Health = 0;
        view.ResetPresentation();
        view.Setup(f.Enemy, null);
        Check(sprite.color.a == 0.8f, "Rebinding a defeated view cached transparent colors.");
        var flash = new Coroutine(view.PlayAction(BattleCommandType.Attack, null));
        flash.Step();
        view.ResetPresentation();
        Check(sprite.color.r == 0.7f && sprite.color.g == 0.8f, "Cancel left a temporary tint.");
    }

    private static void MissingViewsComplete()
    {
        var f = new Fixture();
        Set(f.Field, "playerSlots", Array.Empty<Transform>());
        Set(f.Field, "enemySlots", Array.Empty<Transform>());
        f.Field.Setup(new List<Combatant> { f.Player }, new List<Combatant> { f.Enemy }, null);
        f.Attack();
        f.Frames(10);
        Check(f.Controller.CurrentState == BattleState.Victory, "Missing art blocked combat.");
    }

    private static void AllTargetsFinishBeforeVictory()
    {
        var f = new Fixture();
        var second = new Combatant("Enemy2", 20, 10, 1, 0, 5, false, "e");
        var secondView = new GatedView();
        Set(f.Field, "enemySlots", new[] {
            new Transform { Children = new Component[] { f.EnemyView } },
            new Transform { Children = new Component[] { secondView } } });
        var enemies = new List<Combatant> { f.Enemy, second };
        Set(f.Controller, "enemyParty", enemies);
        f.Field.Setup(new List<Combatant> { f.Player }, enemies, f.Controller.OnActorClicked);
        f.Controller.OnSkillSelected(new BattleActionDefinition
        { id = "area", target = "AllEnemies", effect = "Damage", power = 30, manaCost = 5 });
        f.PlayerView.FinishAction = true;
        f.EnemyView.FinishDefeat = true;
        f.Frames();
        Check(!f.EnemyView.ReactionStarted && secondView.DefeatStarted &&
            f.EndCount == 0 && f.Player.Mana == 15,
            "Area action did not skip hurt or wait for every defeat.");
        secondView.FinishDefeat = true;
        f.Frames();
        Check(f.EndCount == 1 && f.Player.Experience == 20, "Area victory rewards were duplicated or incomplete.");
    }

    private static void StunReturnsToCommand()
    {
        var f = new Fixture();
        f.Enemy.NextTurnTime = 100;
        float turnTime = f.Player.NextTurnTime;
        f.Player.ApplyStatus(new StatusDefinition
        { id = "stun", effect = "SkipTurn", timing = "BeforeAction", durationTurns = 1 });
        Call(f.Controller, "BeginNextTurn");
        f.Frames();
        Check(f.PlayerView.ActionStarted && f.Player.NextTurnTime == turnTime, "Stun was not awaited.");
        f.PlayerView.FinishAction = true;
        f.Frames();
        Check(f.Player.GetStatusTurns("stun") == 0 && f.Controller.CurrentState == BattleState.WaitingForCommand,
            "Stun did not expire and return control.");
    }

    private static void AppearanceKeepsFootFixed()
    {
        var body = new SpriteRenderer();
        var view = new BattleActorView();
        Set(view, "bodyRenderer", body);
        view.transform.localPosition = new Vector3(3, -2, 0);
        var profile = new BattleAppearance
        {
            sprite = new Sprite { rect = new Rect { width = 64, height = 64 }, pivot = new Vector2(32, 32) },
            footPoint = new Vector2(0.5f, 0.125f), scale = 2
        };
        Set(view, "appearance", profile);
        view.ApplyAppearance();
        Check(body.transform.localPosition.y == 0.75f, "Foot padding was not corrected.");
        profile.sprite = new Sprite { rect = new Rect { width = 96, height = 96 }, pivot = new Vector2(48, 48) };
        view.ApplyAppearance();
        Check(body.transform.localPosition.y == 1.125f && view.transform.localPosition.x == 3 &&
            view.transform.localPosition.y == -2, "Changing sprite moved the station.");
    }

    private static void DataAppearanceOverridesSlot()
    {
        const string resourcePath = "Battle/Appearances/GoblinBattleAppearance";
        var fallbackSprite = new Sprite();
        var goblinSprite = new Sprite
        {
            rect = new Rect { width = 92, height = 92 },
            pivot = new Vector2(46, 46),
            pixelsPerUnit = 64
        };
        var body = new SpriteRenderer { sprite = fallbackSprite };
        var view = new BattleActorView();
        Set(view, "bodyRenderer", body);
        Set(view, "appearance", new BattleAppearance { sprite = fallbackSprite });
        Resources.Register(resourcePath, new BattleAppearance
        {
            sprite = goblinSprite,
            footPoint = new Vector2(0.5f, 15f / 92f),
            scale = 1f
        });

        var goblin = new Combatant(new CharacterDefinition
        {
            id = "goblin",
            name = "Goblin",
            battleAppearance = resourcePath,
            maxHealth = 70,
            maxMana = 5,
            attackPower = 5,
            magicPower = 10,
            speed = 9
        }, new CharacterLevelRules(), false);
        view.Setup(goblin, null);

        Check(body.sprite == goblinSprite, "Character data did not override the slot appearance.");
        Check(body.transform.localPosition.y > 0f, "Transparent foot padding was not aligned.");
        Resources.Clear();
    }

    private static void SpriteAttackStopsAtImpactAndRecovers()
    {
        var idle = new Sprite();
        var windup = new Sprite();
        var impact = new Sprite();
        var recovery = new Sprite();
        var body = new SpriteRenderer { sprite = idle };
        var view = new BattleActorView();
        Set(view, "bodyRenderer", body);
        Set(view, "appearance", new BattleAppearance
        {
            sprite = idle,
            attackSprites = new[] { windup, impact, recovery },
            attackFrameSeconds = new[] { 0.1f, 0.1f, 0.1f },
            attackImpactFrame = 1
        });
        view.Setup(new Combatant("Goblin", 20, 0, 5, 0, 5, false, "goblin"), null);

        var attack = new Coroutine(view.PlayAction(BattleCommandType.Attack, null));
        while (attack.Step()) { }
        Check(body.sprite == impact, "Sprite attack did not stop on its impact frame.");

        var finish = new Coroutine(view.PlayActionRecovery());
        while (finish.Step()) { }
        Check(body.sprite == idle, "Sprite attack did not recover to the idle sprite.");
    }

    private static void SpriteHurtPlaysOnceAndReturnsIdle()
    {
        var idle = new Sprite();
        var hurtFrames = new[] { new Sprite(), new Sprite(), new Sprite(), new Sprite(), new Sprite() };
        var body = new SpriteRenderer { sprite = idle };
        var view = new BattleActorView();
        Set(view, "bodyRenderer", body);
        Set(view, "appearance", new BattleAppearance
        {
            sprite = idle,
            hurtSprites = hurtFrames,
            hurtFrameSeconds = new[] { 0.08f, 0.05f, 0.06f, 0.12f, 0.08f }
        });
        view.Setup(new Combatant("Goblin", 20, 0, 5, 0, 5, false, "goblin"), null);

        var seen = new List<Sprite>();
        var reaction = new Coroutine(view.PlayReaction(-5f, BattleNumberStyle.Damage));
        Sprite previous = null;
        while (reaction.Step())
        {
            if (body.sprite != previous)
            {
                seen.Add(body.sprite);
                previous = body.sprite;
            }
        }

        Check(seen.Count >= hurtFrames.Length, "Hurt animation skipped frames.");
        for (int i = 0; i < hurtFrames.Length; i++)
            Check(seen[i] == hurtFrames[i], "Hurt frames played out of order.");
        Check(body.sprite == idle, "Hurt animation did not return to idle.");
        Check(!reaction.Step(), "Hurt animation looped after completion.");
    }

    private static void SpriteDefeatPlaysOnceAndHoldsLastFrame()
    {
        var idle = new Sprite();
        var defeatFrames = new[] { new Sprite(), new Sprite(), new Sprite() };
        var body = new SpriteRenderer { sprite = idle };
        var view = new BattleActorView();
        Set(view, "bodyRenderer", body);
        Set(view, "appearance", new BattleAppearance
        {
            sprite = idle,
            defeatSprites = defeatFrames,
            defeatFrameSeconds = new[] { 0.04f, 0.06f, 0.2f }
        });
        view.Setup(new Combatant("Goblin", 20, 0, 5, 0, 5, false, "goblin"), null);

        var seen = new List<Sprite>();
        var defeat = new Coroutine(view.PlayDefeat());
        Sprite previous = null;
        while (defeat.Step())
        {
            if (body.sprite != previous)
            {
                seen.Add(body.sprite);
                previous = body.sprite;
            }
        }

        Check(seen.Count >= defeatFrames.Length, "Defeat animation skipped frames.");
        for (int i = 0; i < defeatFrames.Length; i++)
            Check(seen[i] == defeatFrames[i], "Defeat frames played out of order.");
        Check(body.sprite == defeatFrames[defeatFrames.Length - 1] && body.color.a == 1f,
            "Defeat did not hold the visible final frame.");
        Check(!defeat.Step(), "Defeat animation looped after completion.");
    }

    private static void GroundAndMarkerDoNotFade()
    {
        var f = new Fixture();
        var body = new SpriteRenderer();
        var ground = new SpriteRenderer();
        var marker = new SpriteRenderer();
        var view = new BattleActorView { Children = new Component[] { body, ground, marker } };
        Set(view, "bodyRenderer", body);
        Set(view, "selectionMarker", marker);
        view.Setup(f.Player, null);
        view.SetSelected(true);
        var defeat = new Coroutine(view.PlayDefeat());
        while (defeat.Step()) { }
        Check(body.color.a == 0 && ground.color.a == 1 && marker.color.a == 1 && marker.enabled,
            "Actor defeat changed station decorations.");
        view.SetSelected(false);
        Check(!marker.enabled && ground.enabled, "Deselecting hid the ground.");
    }

    private static void IntroLocksCommandsUntilFadeOut()
    {
        var f = new Fixture();
        var transition = new BattleTransitionView();
        var text = new TMPro.TextMeshProUGUI();
        Set(transition, "messageText", text);
        Set(f.Controller, "transitionView", transition);
        f.Controller.ChangeState(BattleState.Start);
        var intro = (IEnumerator)typeof(BattleController).GetMethod("BeginBattle",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f.Controller, null);
        f.Controller.StartCoroutine(intro);
        f.Controller.StepFrame();
        Check(text.alpha > 0 && text.alpha < 1 && f.Controller.CurrentState == BattleState.Start,
            "Intro did not fade in while commands remained locked.");
        f.Controller.OnDefendButtonPressed();
        Check(!f.Player.IsDefending, "Intro accepted combat input.");
        f.Frames(4);
        Check(text.alpha == 1 && f.Controller.CurrentState == BattleState.Start, "Intro did not hold before fading out.");
        f.Frames(30);
        Check(!text.gameObject.activeSelf && f.Controller.CurrentState == BattleState.WaitingForCommand,
            "Combat did not start after intro completed.");
    }

    private static void OutcomeMovesToIndependentDock()
    {
        var transition = new BattleTransitionView();
        var text = new TMPro.TextMeshProUGUI();
        text.rectTransform.parent = new RectTransform { rect = new Rect { width = 2560, height = 1440 } };
        var panel = new GameObject();
        Set(transition, "messageText", text);
        Set(transition, "selectedCharacterPanel", panel);
        var reveal = new Coroutine(transition.PlayEnd("Victory"));
        reveal.Step();
        Check(text.alpha > 0 && text.alpha < 1, "Outcome did not fade in.");
        while (reveal.Step()) { }
        Check(panel.activeSelf && text.alpha == 1 && text.rectTransform.anchoredPosition.y == 0,
            "Outcome should wait in the center before Next.");
        var move = new Coroutine(transition.MoveToDock());
        move.Step();
        Check(!panel.activeSelf && text.rectTransform.anchoredPosition.y > 0 &&
            text.rectTransform.anchoredPosition.y < 570, "Docking did not hide the panel and move gradually.");
        while (move.Step()) { }
        Check(text.rectTransform.anchorMax.y == 1 && text.rectTransform.anchoredPosition.y == -150,
            "Dock destination did not match the independent top-center offset.");
        transition.Hide();
        Check(!text.gameObject.activeSelf, "Transition cleanup left text visible.");
    }
}
