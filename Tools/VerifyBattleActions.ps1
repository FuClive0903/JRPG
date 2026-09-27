$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
# Run the actual controller with a small, manually stepped coroutine boundary.
$boundary = @'
namespace UnityEngine {
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : System.Attribute {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) {}
    }
    public static class Mathf {
        public static float Clamp(float value, float min, float max) { return System.Math.Max(min, System.Math.Min(max, value)); }
    }
    public static class Time { public static int frameCount; }
    public static class Random {
        private static readonly System.Random generator = new System.Random(0);
        public static int Range(int min, int max) { return generator.Next(min, max); }
    }
    public class SerializeField : System.Attribute {}
    public class MonoBehaviour {
        public bool enabled;
        public System.Collections.Generic.List<System.Collections.IEnumerator> Pending = new System.Collections.Generic.List<System.Collections.IEnumerator>();
        public void StartCoroutine(System.Collections.IEnumerator routine) { if (routine.MoveNext()) Pending.Add(routine); }
        public void Step() {
            var frame = Pending.ToArray();
            foreach (var routine in frame) if (!routine.MoveNext()) Pending.Remove(routine);
        }
    }
    public static class Debug {
        public static void Log(object message) {}
        public static void LogError(object message, object context) {}
    }
    public class TextAsset { public string text; }
    public static class Resources { public static T Load<T>(string path) where T : class { return null; } }
    public static class JsonUtility { public static T FromJson<T>(string text) { return default(T); } }
}
namespace UnityEngine.InputSystem {
    public class Key { public bool wasPressedThisFrame; }
    public class Mouse { public static Mouse current; public Key rightButton; }
    public class Keyboard {
        public static Keyboard current;
        public Key leftArrowKey, rightArrowKey, zKey, xKey;
    }
}
namespace Game.Battle {
    public class BattleFieldView {
        public void Setup(List<Combatant> players, List<Combatant> enemies, Action<Combatant> onActorClicked) {}
    }
    public static class ActionChecks {
        static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Set(BattleController c, string field, object value) {
            typeof(BattleController).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(c, value);
        }
        static void Call(BattleController c, string method) {
            typeof(BattleController).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(c, null);
        }
        static BattleController Create(out Combatant player, out Combatant enemy) {
            player = new Combatant("Player", 100, 20, 30, 10, 10, true);
            enemy = new Combatant("Enemy", 100, 20, 20, 10, 10, false);
            player.NextTurnTime = 0; enemy.NextTurnTime = 1000;
            var c = new BattleController();
            Set(c, "playerParty", new List<Combatant> { player });
            Set(c, "enemyParty", new List<Combatant> { enemy });
            Set(c, "currentCombatant", player);
            c.ChangeState(BattleState.WaitingForCommand);
            return c;
        }
        public static string Run() {
            Combatant p, e;
            var c = Create(out p, out e);
            p.Health = 42; p.Mana = 7;
            var enemyIds = new [] { "enemy" };
            p = new Combatant("Player", 100, 20, 30, 10, 10, true, "player");
            p.Health = 42; p.Mana = 7;
            BattleSession.Prepare(new List<Combatant> { p }, enemyIds, "Title_scene");
            var data = new BattleData { characters = new [] {
                new CharacterDefinition { id = "player", name = "Player", maxHealth = 100, maxMana = 20, speed = 10 },
                new CharacterDefinition { id = "enemy", name = "Enemy", maxHealth = 50, speed = 10 }
            }};
            p.Health = 1; enemyIds[0] = "changed";
            var party = BattleSession.Entry.CreatePlayers(data);
            Assert(party[0].Health == 42 && party[0].Mana == 7 && BattleSession.Entry.enemies[0] == "enemy", "Entry shares caller state");
            party[0].Health = 0;
            BattleSession.RecordResult(BattleState.Defeat, party);
            var retryParty = BattleSession.Entry.CreatePlayers(data);
            Assert(retryParty[0].Health == 42 && retryParty[0].Mana == 7 && BattleSession.Result.players[0].health == 0, "Retry lost entry state");
            party[0].Health = 99;
            Assert(BattleSession.Result.players[0].health == 0, "Result shares battle state");
            BattleSession.LeaveBattle();
            Assert(BattleSession.Entry == null && BattleSession.Result.state == BattleState.Defeat, "Result lost when leaving");
            c = Create(out p, out e);
            c.OnAttackButtonPressed();
            c.OnActorClicked(p);
            Assert(c.CurrentState == BattleState.SelectingTarget, "Player click accepted as attack target");
            c.OnActorClicked(e);
            Assert(c.CurrentState == BattleState.ResolvingAction, "Enemy click did not confirm target");
            c = Create(out p, out e);
            c.OnAttackButtonPressed();
            UnityEngine.InputSystem.Mouse.current = new UnityEngine.InputSystem.Mouse {
                rightButton = new UnityEngine.InputSystem.Key { wasPressedThisFrame = true }
            };
            Call(c, "Update");
            Assert(c.CurrentState == BattleState.WaitingForCommand, "Right click did not cancel without keyboard");
            UnityEngine.InputSystem.Mouse.current = null;
            c.OnAttackButtonPressed();
            UnityEngine.InputSystem.Keyboard.current = new UnityEngine.InputSystem.Keyboard {
                leftArrowKey = new UnityEngine.InputSystem.Key(), rightArrowKey = new UnityEngine.InputSystem.Key(),
                zKey = new UnityEngine.InputSystem.Key { wasPressedThisFrame = true }, xKey = new UnityEngine.InputSystem.Key()
            };
            UnityEngine.Time.frameCount = 0;
            Call(c, "Update");
            Assert(c.CurrentState == BattleState.SelectingTarget, "Z confirmed target in command frame");
            UnityEngine.Time.frameCount = 1;
            Call(c, "Update");
            Assert(c.CurrentState == BattleState.ResolvingAction, "Z did not confirm target on next frame");
            UnityEngine.InputSystem.Keyboard.current = null;
            c = Create(out p, out e);
            c.OnAttackButtonPressed(); Call(c, "CancelTargetSelection");
            Assert(c.CurrentState == BattleState.WaitingForCommand && e.Health == 100 && p.NextTurnTime == 0 && c.Pending.Count == 0, "Cancel consumed an action");
            c.OnAttackButtonPressed(); c.OnActorClicked(new Combatant("Other", 1, 0, 0, 0, 0, false));
            Assert(c.CurrentState == BattleState.SelectingTarget && c.Pending.Count == 0, "Invalid target accepted");
            c.OnActorClicked(e);
            c.OnActorClicked(e); c.OnDefendButtonPressed(); c.OnAttackButtonPressed(); Call(c, "CancelTargetSelection");
            Assert(c.CurrentState == BattleState.ResolvingAction && c.Pending.Count == 1 && e.Health == 100, "Input lock or duplicate confirmation failed");
            Assert(c.SelectedTarget == null && c.SelectedTargetIndex == -1, "Selection not cleared");
            c.Step();
            Assert(e.Health == 70 && p.NextTurnTime == 0 && c.CurrentState == BattleState.ResolvingAction, "Effect ordering failed");
            c.Step(); c.Step();
            Assert(p.NextTurnTime == 10 && e.Health == 70 && c.CurrentState == BattleState.WaitingForCommand, "Turn advanced more than once");
            c.OnDefendButtonPressed(); c.Step(); c.Step();
            Assert(p.IsDefending && p.NextTurnTime == 20, "Defend did not use action flow");
            Set(c, "currentCombatant", e); c.ChangeState(BattleState.EnemyTurn); Call(c, "HandleEnemyTurn");
            Assert(c.CurrentState == BattleState.ResolvingAction && p.Health == 100, "Enemy bypassed action flow");
            c.Step(); c.Step();
            Assert(p.Health == 90 && !p.IsDefending && e.NextTurnTime == 1010, "Enemy damage, defense or CTB failed");
            var fire = new BattleActionDefinition { id = "fire", name = "Fire", effect = "Damage", target = "EnemyOne", power = 20, manaCost = 5 };
            c = Create(out p, out e);
            c.OnSkillSelected(fire); Call(c, "CancelTargetSelection");
            Assert(p.Mana == 20, "Canceled skill spent MP");
            c.OnSkillSelected(fire); c.OnActorClicked(e);
            Assert(p.Mana == 20 && e.Health == 100, "Skill paid before resolving");
            c.Step();
            Assert(p.Mana == 15 && e.Health == 70, "Damage skill failed");

            var heal = new BattleActionDefinition { id = "heal", name = "Heal", effect = "Heal", target = "AllyOne", power = 20, manaCost = 4 };
            c = Create(out p, out e); p.Health = 40;
            c.OnSkillSelected(heal); c.OnActorClicked(e);
            Assert(c.CurrentState == BattleState.SelectingTarget, "Enemy accepted as healing target");
            c.OnActorClicked(p); c.Step();
            Assert(p.Health == 70 && p.Mana == 16, "Healing skill failed");

            var potion = new BattleActionDefinition { id = "potion", name = "Potion", effect = "Heal", target = "AllyOne", power = 30 };
            c = Create(out p, out e); p.Health = 40;
            BattleSession.Prepare(new List<Combatant> { p }, new [] { "enemy" }, "Title_scene",
                new [] { new BattleItemAmount { itemId = "potion", count = 3 } });
            BattleSession.BeginBattle();
            c.OnItemSelected(potion); Call(c, "CancelTargetSelection");
            Assert(BattleSession.GetItemCount("potion") == 3, "Canceled item was consumed");
            c.OnItemSelected(potion); c.OnActorClicked(p); c.Step();
            Assert(p.Health == 70 && BattleSession.GetItemCount("potion") == 2, "Potion failed");

            var selfHeal = new BattleActionDefinition { id = "self", name = "Self", effect = "Heal", target = "Self", power = 10 };
            c = Create(out p, out e); p.Health = 40;
            c.OnSkillSelected(selfHeal); c.OnActorClicked(p); c.Step();
            Assert(p.Health == 60, "Self target failed");

            var revive = new BattleActionDefinition { id = "revive", name = "Revive", effect = "Revive", target = "DeadAlly", power = 15 };
            c = Create(out p, out e);
            var fallen = new Combatant("Fallen", 50, 10, 5, 5, 5, true); fallen.Health = 0;
            Set(c, "playerParty", new List<Combatant> { p, fallen });
            c.OnSkillSelected(revive);
            Assert(c.SelectedTarget == fallen, "Living ally selected for revive");
            c.OnActorClicked(fallen); c.Step();
            Assert(fallen.Health == 25, "Dead ally target failed");

            var allDamage = new BattleActionDefinition { id = "all", name = "All", effect = "Damage", target = "AllEnemies", power = 5 };
            c = Create(out p, out e);
            var e2 = new Combatant("Enemy2", 100, 0, 1, 1, 1, false);
            Set(c, "enemyParty", new List<Combatant> { e, e2 });
            c.OnSkillSelected(allDamage); c.Step();
            Assert(e.Health == 85 && e2.Health == 85, "All-enemy target failed");

            var allHeal = new BattleActionDefinition { id = "allheal", name = "All Heal", effect = "Heal", target = "AllAllies", power = 5 };
            c = Create(out p, out e); p.Health = 50;
            var ally = new Combatant("Ally", 100, 0, 1, 0, 1, true); ally.Health = 50;
            Set(c, "playerParty", new List<Combatant> { p, ally });
            c.OnSkillSelected(allHeal); c.Step();
            Assert(p.Health == 65 && ally.Health == 65, "All-ally target failed");

            var hasteStatus = new StatusDefinition { id = "haste", effect = "SpeedMultiplier", timing = "TurnDelay", value = 1.5f, durationTurns = 3, priority = 10 };
            var poisonStatus = new StatusDefinition { id = "poison", effect = "Damage", timing = "TurnStart", value = 5, durationTurns = 3, priority = 20 };
            var statusData = new BattleData { statuses = new [] { hasteStatus, poisonStatus } };
            var hasteAction = new BattleActionDefinition { id = "haste", name = "Haste", effect = "ApplyStatus", target = "Self", manaCost = 4, statusId = "haste" };
            c = Create(out p, out e); Set(c, "battleData", statusData);
            c.OnSkillSelected(hasteAction);
            Assert(c.CurrentState == BattleState.SelectingTarget && c.SelectedTarget == p && c.SelectableTargetCount == 1 && p.Mana == 20, "Self target was not selected");
            c.OnActorClicked(p);
            Assert(c.CurrentState == BattleState.ResolvingAction && p.Mana == 20, "Haste paid before resolving");
            c.Step();
            Assert(p.Mana == 16 && p.GetStatusTurns("haste") == 3 && p.GetEffectiveSpeed() == 15, "Haste application failed");
            c.Step();
            Assert(System.Math.Abs(p.NextTurnTime - (100f / 15f)) < 0.001f && p.GetStatusTurns("haste") == 3, "Haste CTB failed");

            var poisonAction = new BattleActionDefinition { id = "poison", name = "Poison", effect = "ApplyStatus", target = "EnemyOne", manaCost = 4, statusId = "poison" };
            c = Create(out p, out e); Set(c, "battleData", statusData);
            c.OnSkillSelected(poisonAction); Call(c, "CancelTargetSelection");
            Assert(p.Mana == 20 && e.GetStatusTurns("poison") == 0, "Canceled poison had an effect");
            c.OnSkillSelected(poisonAction); c.OnActorClicked(e); c.Step();
            Assert(p.Mana == 16 && e.GetStatusTurns("poison") == 3, "Poison application failed");
            e.Defend();
            for (int turn = 0; turn < 3; turn++) {
                e.ResolveTurnStartStatuses();
                e.FinishStatusTurn();
            }
            Assert(e.Health == 85 && e.IsDefending && e.GetStatusTurns("poison") == 0, "Poison ticks or duration failed");
            e.ApplyStatus(poisonStatus); e.FinishStatusTurn(); e.ApplyStatus(poisonStatus);
            Assert(e.GetStatusTurns("poison") == 3, "Status refresh failed");

            var regenStatus = new StatusDefinition { id = "regen", effect = "Heal", timing = "TurnStart", value = 5, durationTurns = 1, priority = 10 };
            var orderedTarget = new Combatant("Ordered", 100, 0, 0, 0, 1, true); orderedTarget.Health = 1;
            orderedTarget.ApplyStatus(poisonStatus); orderedTarget.ApplyStatus(regenStatus);
            orderedTarget.ResolveTurnStartStatuses();
            Assert(orderedTarget.Health == 1, "Status priority order failed");

            var stunStatus = new StatusDefinition { id = "stun", effect = "SkipTurn", timing = "BeforeAction", durationTurns = 1, priority = 30 };
            orderedTarget.ApplyStatus(stunStatus);
            Assert(orderedTarget.ResolveTurnStartStatuses(), "Stun did not skip the turn");
            foreach (bool victory in new [] { true, false }) {
                c = Create(out p, out e);
                int results = 0; c.BattleEnded += state => results++;
                if (victory) { e.Health = 1; c.OnAttackButtonPressed(); c.OnActorClicked(e); }
                else { p.Health = 1; Set(c, "currentCombatant", e); c.ChangeState(BattleState.EnemyTurn); Call(c, "HandleEnemyTurn"); }
                c.Step(); c.Step(); c.Step();
                c.OnAttackButtonPressed(); c.OnDefendButtonPressed();
                Assert(c.CurrentState == (victory ? BattleState.Victory : BattleState.Defeat) && results == 1 && c.Pending.Count == 0, "Battle did not end exactly once");
                Assert(p.NextTurnTime == 0 && e.NextTurnTime == 1000, "Terminal action advanced CTB");
            }
            return "PASS: actions, costs, targets, Haste CTB, Poison ticks, status order/refresh, Stun, retry and battle end.";
        }
    }
}
'@
$source = "#pragma warning disable 0649`nusing System;`nusing System.Collections;`nusing System.Collections.Generic;`nusing UnityEngine;`nusing UnityEngine.InputSystem;`n"
foreach ($file in @('BattleController.cs', 'BattleSession.cs', 'BattleData.cs', 'Combatant.cs', 'BattleTurnOrder.cs', 'BattleState.cs', 'BattleCommandType.cs')) {
    $source += (Get-Content "Assets/Scipts/$file" -Raw) -replace '(?m)^using .*;\r?\n', ''
}
Add-Type -TypeDefinition ($source + $boundary)
[Game.Battle.ActionChecks]::Run()
