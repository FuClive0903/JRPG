# 美术与剧情阶段交接

更新日期：2026-09-22。当前进入内容设计阶段，不自动开展新系统、重构或资源迁移。

制作前先看 [序章素材接入清单](PROLOGUE_ASSETS.md)：区分直接换图、Unity 配置接入及程序配合，包含战斗动画事件、探索角色／立绘缺口、背景、镜头和共用 UI 约束。

已确定阶段目标：完成一个从头到尾可玩的序章，整合玩法、美术、剧情、音乐与音效。下面的地图战斗背景接入为用户已授权并完成的素材准备工作。

## 基线结论与证据

当前是**已有用户验收基础、近期加固仍待统一实测的开发基线**，不是“全部稳定”或“序章完成”。以下结果截至 2026-09-22；本次交接整理没有重新运行测试，也未创建版本标签、Check in 或可回退的发布包。

| 证据层级 | 最近有记录的结果 | 不能据此推断 |
| --- | --- | --- |
| 用户实际验收 | 探索／战斗／存档一般闭环、统一菜单输入、动态经验列表已有确认；第一项战斗暂停专项用户回复“测试通过” | 没有逐项操作记录，不外推为所有分辨率、异常档案或动画边界都通过 |
| 外部 C# 编译 | 第二项资料安全修改后，Assembly-CSharp.csproj 编译 0 错误、10 个既有警告 | 不是 Unity Play Mode、打包成功或实际游玩通过；警告仍存在 |
| 最近独立回归 | 第三项完成后：27 项战斗／背景、12 项暂停菜单、7 项资料安全通过，共 46 项 | Unity 生命周期、Animator、渲染、真实 UI 焦点与场景切换使用替身或未覆盖 |
| 较早专项回归 | 第二项另有 10 项编队／快照／Retry／文件写入检查通过 | 不与最新 46 项拼成一次执行；完整成长测试仍有 UnityEngine.Random 原生调用限制 |
| 静态引用检查 | 四份场景／Prefab 的 883 个序列化对象本地引用检查通过，菜单绑定及背景根节点检查通过 | 不等于所有外部资源正确、实际实例化成功或画面正常 |
| 文档与素材准备 | 序章素材接入条件已整理；这一轮只更新文档 | 不代表已制作或导入正式素材，也不表示缺失系统已实现 |

资料安全测试的磁盘操作是真实的隔离临时文件，JSON 使用替身；暂停菜单测试的存档服务与场景切换也使用替身。没有操作实际玩家存档，未自动进行 Unity Play Mode 验收。

**统一验收尚未完成**：用户明确第二、三项最后一起测试。具体问题状态见 TODO.md 的“当前问题与验收入口”，操作清单见 Tests/SceneIntegration/README.md。历史“待测”记录不抹除，历史“通过”也不自动套用于后续修改。

## 当前实现

下表描述已存在的代码与配置，不是逐项实际游玩验收表。

| 模块 | 当前状态 |
| --- | --- |
| 场景流程 | Title_scene → Village_scene → Battle_scene → 胜利返回原探索位置；失败 Retry／返回标题 |
| 探索 | 3D 灰盒、方块主角、八方向移动、碰撞、跟随镜头；没有场景可见敌人 |
| 遇敌 | 实际移动距离每 1 单位计一步，前 20 步安全，第 21 步起每步 50%；目前三个哥布林 |
| 战斗 | CTB、攻击／防御／技能／物品、目标选择、状态、动画等待、死亡处理及暂停 |
| 成长与奖励 | JSON 可调成长、连续升级、全体已招募角色含后备获得经验；物品与金币结算 |
| 结算 UI | 完整名单动态经验行、升级提示、经验动画、上下滚动与 Z 切页；用户已确认本轮改动正常 |
| 队伍 | 三个出战槽位、等级姓名及 EXP／HP／MP、左右选择与交换顺序；后备替换页未做 |
| 存档 | 版本 3、一个自动槽＋十个手动槽、覆盖确认、旧档迁移、临时文件写入与上一份 .bak |
| 菜单 | 键盘操作、统一高亮、输入页面隔离；探索 Canvas 已提取为共用 Prefab，待本轮回归 |

资料流：GameRuntimeState 是当前进度来源；BattleSession 保存战前快照并传递战后结果；GameSaveSystem 负责持久化。显示动画与切页不能再次发奖励。探索遇敌不从自动档重读角色。

## 验收与限制

- 用户已确认探索战斗存档闭环、统一菜单输入、经验列表以及第一项暂停专项；第二、三项修改后的统一实测仍未完成。
- 共用 ExplorationCanvas 的静态引用检查通过；尚未完成提取 Prefab 后的专项 Play Mode 回归，未建立第二张地图测试。
- 编译通过、独立测试通过和实际游玩通过是三种不同结论。历史记录及尚未确认的边界保留在 TODO.md，不一并勾选完成。
- 尚无正式对话／剧情事件系统、调查／宝箱、地图出口与事件状态存档、完整音乐音效管理、装备与背包界面。不要按“已有接口”安排内容接入。
- 新游戏覆盖自动档前尚无确认框；保存失败／备份恢复没有专用 UI。探索设置禁用；战斗暂停支持继续、恢复战前资料的重新挑战、共用存档读取及返回标题，设置仍禁用。一般暂停专项有用户验收，后续资料安全修改及异常路径仍需统一回归。
- 中文字体资源已存在，缺字警告消失与实际显示仍需专项确认；当前仍有英文占位名称和文字，正式游戏文案按简体中文统一，显示名称与稳定 ID 分开。

## 美术资源入口

| 用途 | 现有入口与约束 |
| --- | --- |
| 地图 | Assets/Scenes/Village_scene.unity；保留灰盒作为功能基线，不将它误认为正式关卡 |
| 渲染 | Assets/Settings/UniversalRP.asset 保留 2D Renderer 与 ExplorationRenderer；探索使用独立 Universal Renderer，不整体替换项目默认渲染方式 |
| 源文件与精灵 | Assets/Art/_Source、Assets/Art/Sprites；继续沿用现有目录，移动已有资源通过 Unity 保留 .meta／GUID |
| 战斗角色 | Assets/Scipts/Animation/BattleAppearance.cs；配置 sprite、footPoint、scale、battleAnimatorController |
| 战斗动画 | Assets/Animations/Battle/Battle_Base.controller 与 Goblin 示例；Idle／Attack／Hurt／Defeat，保留命中和完成事件契约 |
| 角色外观配置 | Assets/Resources/Battle/Appearances/GoblinBattleAppearance.asset；JSON 的 battleAppearance 使用 Resources 相对路径，不包含扩展名 |
| 探索角色 | 当前只有移动控制，尚未接入八方向人物动画和朝向摄影机的精灵表现；先准备角色样板，再确认实现 |
| 共用探索 UI | Assets/Prefabs/UI/ExplorationCanvas.prefab；Prefab Mode 内编辑共用布局，不在各地图重复改一份 |
| 共用存档 UI | Assets/Prefabs/UI/SaveDataMenu.prefab；同时服务标题、探索与战斗暂停，修改后需检查三处 |
| 经验／物品行 | Assets/Prefabs/Battle/CharacterExpRow.prefab、ItemRewardPrefab.prefab；列表运行时生成，Content 为空不代表缺少数据 |
| 数值与名称 | Assets/Resources/Data/BattleData.json；更改 name 不必更改 player／ally／healer／goblin 等稳定 ID |

角色画布尺寸、脚底基准点、像素密度及动画帧率先用一份样板确认，再批量制作；不同动作保持脚底稳定，不直接照搬哥布林的尺寸作为全部角色规范。不要删除或替换 Player 上的 CharacterController 与移动脚本来换外观。

音乐可以先准备探索、战斗、胜利与失败的情绪参考，记录循环点和使用情境；目前尚未确定正式音频规格，也没有确认音频系统接入。素材来源与授权记录应随资源保存。

## 战斗背景制作与指定

1. 在 Battle_scene 中参考现有镜头和角色站位制作环境，把环境美术集中到一个空根物件下，保存为 Prefab（可放 Assets/Prefabs/Battle）。不包含 Main Camera、BattleEnvironmentRoot 本身、角色、UI 或控制器。
2. 以背景根节点局部位置 (0,0,0)、旋转 (0,0,0)、缩放 (1,1,1) 为制作基准，用子物件安排画面；运行时按 Prefab 的局部变换挂在 BattleEnvironmentRoot 下。当前战斗仍为正交摄影机与 2D Renderer，Sprite 背景的排序需位于角色后面。
3. 在探索地图的 ExplorationController → ExplorationEncounterController → Battle Environment Prefab 指定素材。每张地图可指定不同 Prefab，不用复制整套战斗场景。
4. 需要直接在 Battle_scene 测试时，在 BattleEnvironmentRoot → BattleEnvironmentView → Default Environment Prefab 指定默认素材。地图素材优先；两者都空时仍能战斗。
5. 制作时暂放的预览实例在正式 Play 前移出场景或删除，避免与运行时实例重复；只删除自己的预览环境，不删除背景根节点和其他场景物件。

背景通过内存传递，不写入存档；Retry 重用同一配置，离开后释放引用。读取探索存档再次遇敌时使用该地图当前配置。最近背景选择与回退测试包含在上方 27 项中；目前地图背景与默认背景都未指定，只有一张探索地图，素材渲染、排序与实际跨地图加载仍待 Unity 验收。

## 剧情设计交付建议

先确定一段完整体验，而非立刻铺开全部世界：

1. 主角是谁、当前目的是什么、为什么来到第一张地图。
2. 玩家从哪里开始、经过哪些地标／岔路、在哪里结束。
3. 出场角色、主要对话与事件顺序；哪些行动需要玩家确认。
4. 战斗发生的理由、敌人种类、奖励与探索节奏。
5. 每个关键事件完成后，哪些状态必须在读档后保留。

约 10～15 分钟体验只是此前建议，不是强制时长。故事、美术风格、地图规模和角色名单尚未定案，由用户设计后再确认系统需求。

## 新地图与返回开发

1. 先回归当前共用菜单：Esc 开关与恢复移动、队伍页 X 返回焦点、保存／读取、覆盖取消／确认、返回标题。
2. 新地图放入一个 ExplorationCanvas 实例；在根物件 ExplorationMenuController 绑定自己的 Scene Controller 和 Player Controller。引用不应用回 Prefab。地图保留一个现有键盘配置的 EventSystem。
3. 地图自身还需要玩家、出生点、摄影机和探索控制器。Prefab 只复用菜单，不会自动建立地图流程；新地图加入 Build Settings，出口及场景间跳转另行实现。
4. 有剧情与地图草案后，选一个最小功能（例如对话或地图出口）再开始程序开发；不先造大型事件框架。
5. 每次更换美术，先检查显示比例、碰撞、镜头、UI 文字与动画命中，再扩大资源量。

## 换机与保存

- 当前使用 Unity Version Control／Plastic。本次没有 Check in；切换电脑前同步代码、场景、Prefab、资源及 .meta，以及根目录文档。
- 本机玩家存档位于 Application.persistentDataPath，不在 Assets 内，不当成项目配置提交。自动档 save.json；手动档 save_slot_01.json～save_slot_10.json；另一台机器没有本机进度属正常情况。
- 同步 PROJECT.md（结构）、TODO.md（问题与验收）、本页、PROLOGUE_ASSETS.md（制作约束）及 Tests 下的测试与检查清单；DEVELOPMENT_PROGRESS.md 是旧交接，不据其重复开发。
- 换机后先核对实际文件和 Unity 引用，阅读本页证据表，再做待验收项。当前文档不是已提交版本的唯一标识；如用户之后 Check in，应把实际 changeset 编号补入交接，不能自行编造或代替提交。
- 项目整理阶段仅修改文档；其后经用户授权新增地图战斗背景配置、生成组件及场景根节点。未制作替代美术、改动数值或玩家存档，也未清理未知／未使用资源。
