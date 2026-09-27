# JRPG Combat & Exploration Prototype

Unity / C# 单人 JRPG 系统原型，作为开发过程与工程实践的作品展示。当前暂停完整序章制作，保留战斗、成长、存档和探索系统供查看与继续开发；不是完整商业游戏。

## 展示重点

- CTB 回合排序：攻击、防御、技能、物品与合法目标选择；行动、命中、受击和死亡表现分阶段等待。
- 战斗会话：战前独立快照、Retry 恢复、跨场景结果传递，以及终局与奖励去重。
- 成长和奖励：JSON 配置、连续升级、全体已招募角色经验、物品与金币结算。
- 存档：自动槽与十个手动槽、版本迁移、运行时资料与磁盘分离、临时文件替换及备份。
- 共用 UI：键盘菜单、页面输入隔离、队伍页、存读档和暂停菜单。
- 独立验证：Unity API 替身测试、真实临时文件测试及场景引用静态检查，明确与实际游玩验收的区别。

## 运行

1. 安装 Unity **6000.6.0f1**，用 Unity Hub 添加仓库根目录。
2. 等待 Unity 按 `Packages/manifest.json` 与锁文件还原包、导入资源。
3. 打开 `Assets/Scenes/Title_scene.unity` 并进入 Play Mode。
4. 选择“开始游戏”，原有过场结束或按 Z 跳过后进入 `Battle_scene`。

仅键盘操作：方向键选择、Z 确认、X 返回、Esc 打开／关闭菜单。探索角色使用项目 Input Actions 的移动绑定。

**注意：“开始游戏”会沿用现有逻辑初始化并覆盖本机自动存档。** 手动槽不因此被覆盖。存档位于 Unity `Application.persistentDataPath`，不包含在本仓库。

`Village_scene` 可直接打开用于探索演示，或由读取相应存档进入。当前为未完成的 2D 村庄样板：有玩家与白叶树，正式道路／建筑与地图边界尚未完成。随机遇敌已开启，前 20 步安全，第 21 步起每步 50%；默认敌队为三只马来貘。标题直接开始的战斗胜利后 Continue 返回标题；地图遇敌则按会话返回来源地图。

## 代码导航

| 入口 | 关注内容 |
| --- | --- |
| [BattleController](Assets/Scipts/Battle/BattleController.cs) | 状态、行动执行与终局 |
| [BattleSession](Assets/Scipts/GameFlow/BattleSession.cs) | 战前快照、道具与结果 |
| [GameRuntimeState](Assets/Scipts/GameFlow/GameRuntimeState.cs) / [GameSaveSystem](Assets/Scipts/GameFlow/GameSaveSystem.cs) | 当前进度、存读档及校验 |
| [Progression](Assets/Scipts/Progression) | 等级与属性成长 |
| [UI](Assets/Scipts/UI) / [共用 Prefab](Assets/Prefabs/UI) | 菜单、输入、结算和存档页 |
| [BattleData.json](Assets/Resources/Data/BattleData.json) | 角色、行动、成长与遇敌配置 |

`Scipts` 是原有目录名称，保留以避免无关迁移。更多架构见 [PROJECT.md](PROJECT.md)，问题与阶段记录见 [TODO.md](TODO.md)。这些文档包含历史设计，部分旧段落不代表当前场景已完成。

## 验证与限制

普通 .NET 测试可从根目录运行（需要 .NET 9 SDK 或支持该目标框架的 SDK）：

```powershell
dotnet run --project Tests/BattlePlayback/BattlePlayback.Tests.proj
dotnet run --project Tests/SaveSafety/SaveSafety.Tests.proj
./Tests/SceneIntegration/Check-References.ps1
```

`Tests/CharacterProgression` 的部分测试使用本机 Unity 托管程序集，需要相符的 Editor 路径；完整运行存在原生 API 调用限制及敌人预设变更后的旧断言，不能把历史通过数量当成当前全套通过。

编译、独立测试、静态检查、Unity Play Mode 和打包是不同结论。独立测试不验证真实 Animator、渲染、所有 UI 焦点或跨场景时序。近期标题直达战斗、村庄遇敌开关尚待实际游玩确认；当前没有可宣称完成验收的发布版本。已知缺口还包括正式剧情／对话、地图出口、完整音频、动态队伍立绘及部分错误提示 UI。

## 开发与素材说明

这是 **AI 辅助的单人开发项目**：作者进行玩法与界面设计、需求取舍、素材组织及交互验收，使用 Codex 协助编程、排错、测试和文档整理。仓库不声称所有代码均由作者独立手写。自定义美术由作者确认为 AI 生成；Unity／TextMesh Pro 与字体等第三方内容保留各自声明，详见 [素材说明](THIRD_PARTY_NOTICES.md)。

本仓库未额外授予整套项目的开源许可证，不应将公开可见理解为所有素材均可任意再分发。没有包含 Unity 缓存、Plastic 工作区元数据、本机玩家存档或未使用的 ArtDrafts 草稿目录。
