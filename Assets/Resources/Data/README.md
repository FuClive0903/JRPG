# 战斗角色资料

编辑 `BattleData.json` 即可调整基础数值，无须修改战斗场景或程式。

| 字段 | 用途 |
| --- | --- |
| id | 唯一角色 ID，队伍以此引用；改名时必须一起更新队伍清单 |
| name | 介面显示名称 |
| maxHealth / maxMana | 等级 1 的最大 HP / MP |
| attackPower / magicPower | 攻击力 / 魔法攻击力 |
| speed | 等级 1 的速度，必须大于 0；现有行动间隔为 100 / 有效速度 |
| startingLevel | 新建角色的初始等级，默认测试数据为 1；战前记录中的等级优先 |
| growthPerLevel | 每提升一级增加的 maxHealth、maxMana、attackPower、magicPower、speed；省略或未填写的增长量为 0 |
| defaultPlayerParty | 预设我方角色 ID，顺序对应我方站位 |
| defaultEnemyParty | 预设敌方角色 ID，顺序对应敌方站位 |

现有场景每方有三个站位，暂时请不要配置超过三名成员。

基础属性为等级 1 的数值；任意等级的属性 = 基础属性 + 每级增长量 * (等级 - 1)。增长量必须非负，基础值和计算结果必须是有限数字。当前速度增长量设为 0，避免升级自动改变 CTB 行动频率；之后可自行调整。

## 等级与经验规则

`levelRules` 是共用的升级规则，以下都是可修改的测试数值：

| 字段 | 当前值 | 用途 |
| --- | --- | --- |
| maxLevel | 99 | 等级上限，至少为 1 |
| firstLevelExperience | 100 | 等级 1 升到 2 所需经验，必须大于 0 |
| experienceIncreasePerLevel | 50 | 每一级增加的经验需求，必须非负 |
| refillHealthAndManaOnLevelUp | false | true 时，存活角色升级会补满 HP / MP；倒下角色不会复活或补满 |

等级 L 升到下一级需要：`firstLevelExperience + (L - 1) * experienceIncreasePerLevel`。

当前规则下，1 -> 2 需要 100 EXP，2 -> 3 需要 150 EXP，3 -> 4 需要 200 EXP。一次获得 300 EXP 会从等级 1 升到 3，剩余 50 EXP。

`Experience` 是当前等级累积的经验，不是终生经验；升级时扣除门槛，剩余经验带入下一级。达到上限后不再累积经验，`Experience` 和 `ExperienceRequiredForNextLevel` 都为 0。负数经验会被拒绝。

默认升级保留当前 HP / MP 数字，只提升上限；不会消除状态效果、防御状态或重排行动时间。修改补满规则也不会使倒下的角色复活。

## 程式接口

```csharp
BattleData data = BattleData.Load();
Combatant character = data.CreateCharacter("player", true);
int levelsGained = character.GainExperience(300);
// Level == 3, Experience == 50, ExperienceRequiredForNextLevel == 200.
// MaxHealth == 120. With refill disabled, Health stays at 100.

Combatant restored = data.CreateCharacter("player", true, level: 3, experience: 50);
```

新建角色按对应等级计算属性，并以满 HP / MP 开始。创建战斗队伍时，`BattleEntry` 会再恢复记录中的当前 HP / MP。相同角色 ID 的多个实例各自保有等级与经验，不会修改 JSON 模板。

`BattleSession.Prepare` 与 `RecordResult` 会记录每位角色的等级、经验、HP / MP。战前记录不会随战中角色变化，Retry 会恢复原本等级和经验；战后结果供后续场景读取。

## 游戏存档

标题画面的「开始游戏」会使用 `BattleData.json` 的预设队伍与物品建立并覆盖存档；「读取游戏」只在存档存在时可以点击，并恢复角色等级、经验、HP / MP 与物品数量。

胜利进入结算时会写入战斗后的角色和物品资料。失败不会覆盖存档，Retry 仍恢复该场战斗开始前的资料。存档位于 `Application.persistentDataPath/save.json`，目前只有一个存档栏位。

本阶段没有自动战斗经验奖励、金币奖励、战利品发放或技能解锁；结算页面也还不会自动调用升级。之后加入奖励时，可先更新战斗结果，再沿用现有胜利存档流程。

修改 JSON 后重新开始 Play 以载入新规则。可临时调整 `startingLevel` 检查高等级属性；这只影响新建角色，不覆盖已存在的战前记录。

独立逻辑验证：`dotnet run --project Tests/CharacterProgression/CharacterProgression.Tests.proj`。测试直接使用生产程式，并引用本机 Unity 的托管组件，不会启动 Unity 或改变场景。
