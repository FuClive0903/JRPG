# 开发进度交接

> 历史记录（2026-09-07），下文按当时状态保留，不作为当前待办。最新状态请阅读 HANDOFF.md、PROJECT.md 与 TODO.md；例如当前已是多槽存档、探索战斗闭环与动态经验列表。

更新日期：2026-09-07

## 今天完成

### 角色等级与成长

- 新增角色等级、当前等级经验、升级门槛与等级上限。
- 支持一次取得大量经验并连续升级，剩余经验会带入下一级。
- 新增每级 HP、MP、攻击、魔攻、速度成长规则。
- 成长数值与共同等级规则均由 `Assets/Resources/Data/BattleData.json` 配置。
- `Combatant` 会依等级重算属性，并提供 `GainExperience` 接口。
- `BattleSession` 的战前资料和战后结果会保留等级、经验、HP 与 MP。
- Retry 会恢复该场战斗开始前的角色与物品状态。

### 单一游戏存档

- 新增 `Assets/Scipts/GameSaveSystem.cs`。
- 存档记录我方角色 ID、等级、经验、HP、MP，以及物品 ID 与数量。
- 标题画面的「开始游戏」会使用预设资料建立并覆盖存档。
- `LoadGameButton` 已连接 `TitleController.LoadGame`；没有存档时按钮不可点击。
- 胜利进入 SettlementPanel 时更新存档。
- 失败不会覆盖存档，Retry 也不会修改原存档。
- 存档位置为 `Application.persistentDataPath/save.json`，目前只有一个存档栏位。
- `save.json` 位于每台电脑的本机 AppData，不会随 Unity Cloud / Plastic 同步。另一台电脑第一次运行时没有存档是正常的。

## 当前尚未完成

- 战斗不会自动发放经验值。
- 尚未有金币资料。
- 尚未有战利品掉落、武器或装备背包资料。
- SettlementPanel 只有流程外壳，还不会显示或应用奖励。
- 「开始游戏」覆盖已有存档前尚无确认视窗。
- 新增存档流程已通过 C# 编译，但尚未完成 Unity Play Mode 点击测试。

## 下一步

制作战斗奖励与 SettlementPanel，建议依序进行：

1. 在敌人或战斗定义中加入 EXP、金币与掉落物资料。
2. 新增独立的战斗奖励结果资料，不让奖励计算进入 `BattleController`。
3. 仅在胜利时计算并发放奖励；失败不发放。
4. 把 EXP 发给角色，记录升级前后等级、经验及属性变化。
5. 把金币与取得的道具、武器加入玩家存档。
6. SettlementPanel 只显示本次取得的 EXP、金币、道具与武器，不显示战斗中消耗的物品。
7. 奖励应用完成后再写入存档。现有的 `GameSaveSystem.SaveVictory` 呼叫点在 `BattleResultView.ShowSettlement`，加入奖励时需要确保它发生在奖励应用之后。

## 验证

- Unity C# 专案编译：通过，0 个错误。
- 现有等级与成长独立测试：17 项通过。
- 编译时仍有 7 个既有 NUnit / Unity Framework 版本警告，与本次功能无关。
