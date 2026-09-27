# 项目说明

更新日期：2026-09-22。根据当前代码与已确认需求整理。

## 项目定位

Unity 单机 JRPG 原型，采用 CTB 回合排序。已建立标题 → 探索 → 随机战斗 → 结算 → 返回探索的基础闭环，以及成长、编队与本机存档。2026-09-21 起用户转向美术与剧情设计；系统暂不扩张，后续按实际关卡需求逐项接入，不代表所有边界与视觉测试已完成。

## 文档分工

- AGENTS.md：长期开发与协作规则。
- PROJECT.md：当前项目结构、资料与功能流程。
- TODO.md：开发进度、待验收与暂缓项目。
- HANDOFF.md：美术／剧情阶段的当前快照、资源入口与恢复开发顺序；具体实现以代码和 PROJECT.md 为准。
- PROLOGUE_ASSETS.md：序章素材制作与接入约束，区分直接替换、配置接入和需要程序配合的内容。

## 环境与目录

- Unity：6000.6.0f1；C#；UGUI、TextMeshPro、Input System。
- 版本管理：Unity Version Control / Plastic。根目录文档需加入版本控制并 Check in 才能同步。
- Assets/Scipts：游戏脚本，目录名目前确实是 Scipts，不要顺手更名。内部按下表分类，移动脚本时必须连同 `.meta` 一起移动。

| 子目录 | 内容 |
| --- | --- |
| Animation | Battle Animator 接入、角色显示、战场显示、浮动数字与过渡动画 |
| Battle | 战斗控制、角色运行状态、回合排序、指令、奖励及战斗枚举 |
| UI | 战斗 HUD、暂停、结算、状态图标与中文字体回退 |
| Settings | JSON 战斗资料结构、角色与技能等设定 |
| Progression | 角色属性成长与升级计算 |
| GameFlow | 标题流程、战斗会话与存档 |
| Exploration | 探索移动、摄影机、地图菜单、计步和随机遇敌切换 |
- Assets/Resources/Data/BattleData.json：角色、成长、技能、状态、物品、掉落及默认队伍资料。
- Assets/Scenes/Title_scene.unity：标题场景。
- Assets/Scenes/Battle_scene.unity：可复用战斗场景。
- Assets/Scenes/Village_scene.unity：靠山村 2D 探索场景；正交固定视角、XY 平面移动与 2D 碰撞，保留标题进入、位置恢复、战后返回和村内禁遇敌。旧道路生成器与 Scene 绘制工具已移除，当前尚无正式地面美术与道路边界碰撞；待用户完成 PixelLab 地图后接入。VillageLandmarkBlockout 只生成白叶树和树干碰撞，临时树影、住宅、村长宅邸、矿区与南门占位已移除。Player 上冲突的旧 CharacterController、MeshRenderer 和 MeshFilter 已移除；旧 3D 地面仍停用。正式建筑美术与出口尚未接入。

靠山村地图规划：当前先修饰 Village_scene 的村庄户外部分。村长宅邸室内、村庄通往矿洞途中的一小段野外、矿洞内部之后各做独立地图；村庄场景内暂只表现宅邸及矿区方向的外观或入口。场景出口与切换尚未接入；野外的遇敌设定留待制作该地图时确认，村庄继续保持不可随机遇敌。
- Assets/Prefabs/Battle/StatusIcon.prefab：状态图标模板。
- Assets/Prefabs/Battle/ItemRewardPrefab.prefab：结算物品行模板。
- Assets/Prefabs/Battle/CharacterExpRow.prefab：完整角色名单的结算经验行模板。
- Assets/Prefabs/UI/ExplorationCanvas.prefab：各探索地图共用的菜单 Canvas，包含根菜单、队伍页、返回标题／覆盖存档确认框；内部嵌套原 SaveDataMenu.prefab。
- Assets/Resources/Fonts：简体中文字体。ChineseFontSetup 在编辑器脚本刷新后自动创建 NotoSansSC-Dynamic.asset（含材质和动态图集），将其登记到 TMP Settings 全局回退，编辑模式和运行时共用；ChineseFontFallback 运行时加载该资源。可手动执行 Tools > UI > Configure Chinese Font 修复配置。
- Tests/CharacterProgression：独立成长测试。
- Tests/BattlePlayback：使用 Unity 替身运行实际战斗脚本的独立播放流程测试，不代表 Unity Play Mode 验证。
- Tests/SaveSafety：实际存档／会话代码的独立安全测试；磁盘操作使用隔离临时目录，Unity 与 JSON API 使用替身，不访问玩家存档。
- Tests/SceneIntegration：共用 UI 与现有场景引用的只读检查脚本及最终 Unity 验收清单；背景选择调用测试沿用 Tests/BattlePlayback，不代表多地图实机渲染已验证。
- DEVELOPMENT_PROGRESS.md：2026-09-07 的历史交接，部分“未完成”项目现已完成，不能作为当前进度依据。

## 资料安全约定

- 每次 BeginBattle 后只接受一次胜败结果；清除结算显示不会解除去重。BattleController 的胜败状态不可重新进入战斗，避免重复经验、奖励及结束事件；Retry 重新从 Entry 开始。
- 离开战斗清除临时道具和战前引用，Result 按原流程留给目的场景读取。
- 旧档迁移和缺失保存时间补全只在内存执行；浏览或读取不会重写原槽位。明确保存时才写入当前格式，读取手动档仍以副本更新自动槽，源手动档不变。
- 读档拒绝负数、NaN 或无穷大的 HP／MP；有效但超过当前配置上限的数值仍沿用原有上限裁剪规则。

## 脚本职责

| 脚本 | 职责 |
| --- | --- |
| TitleController | 标题菜单键盘焦点、退出确认、新游戏、读档、播放与 Z 跳过开场、进入存档指定的探索场景及退出游戏 |
| BattleData | 读取 JSON、查找定义、创建角色与默认队伍 |
| Combatant | 单个角色的运行时属性、HP/MP、成长和多个状态 |
| CharacterProgression / CharacterGrowth | 经验与连续升级规则、按等级计算属性 |
| BattleTurnOrder | CTB 排序和行动顺序预览，间隔为 100 / 有效速度 |
| BattleController | 战斗状态、指令、合法目标、行动执行、敌人决策和胜败判定 |
| BattleHud | 指令与目标 UI、焦点、角色资讯、状态图标 |
| BattlePauseMenu | 战斗暂停菜单、战前快照重开、共用存档读取页、恢复游戏时间与原 UI 焦点 |
| BattleFieldView / BattleActorView | 站位与角色绑定，等待行动、受击和退场播放完成，以及中止后的显示清理 |
| BattleAppearance | 可共用的外观配置：待机 Sprite、脚底基准点、显示比例、Battle Animator Controller，以及尚未迁移角色使用的旧逐帧配置 |
| BattleEnvironmentView | 挂在 BattleEnvironmentRoot，生成来源地图指定的背景 Prefab；未指定时使用默认背景，均为空时保持空背景 |
| BattleTransitionView | 开场文字淡入淡出、胜败提示淡入，以及向独立设置的位置平移 |
| BattleFloatingNumberView | 将角色世界坐标换算到 BattleCanvas，显示上浮淡出的伤害与治疗数字 |
| StatusIconView | 状态图标、分类背景和剩余回合显示 |
| BattleSession | 内存中的战前快照、当前物品和战后结果，支持跨场景与 Retry |
| BattleRewardCalculator / BattleRewardResult | 计算奖励、发放经验、记录每个角色升级前后的等级与经验 |
| BattleResultView | 经验动画、Level Up 提示、物品与金币结算、结果页及跳转 |
| CharacterExperienceText | 计算经验动画的显示文字，不修改真实角色或奖励资料 |
| GameSaveSystem | 一个自动存档槽与十个手动存档槽的读写、验证、版本迁移与探索位置保存 |
| GameRuntimeState | 跨场景保存当前进度，提供独立快照、位置更新及胜利结果合并；进入 Play Mode 时清空 |
| SaveSlotMenu | 标题与探索共用的存档 Scroll View；依读取／保存模式控制槽位可选状态、标题、确认与返回 |
| ExplorationPlayerController | 读取 Player/Move，以 Rigidbody2D 在 XY 平面移动，保持斜向等速，并按 Y 更新 Sprite 排序 |
| ExplorationStepCounter | 根据玩家实际 2D 位移累计探索步数，并提供重置与完成一步事件 |
| ExplorationEncounterController | 读取资料化遇敌规则，安全步数后逐步判定；成功时保存位置、从运行时快照准备 BattleSession 并切换战斗场景 |
| ExplorationCameraFollow | 在 LateUpdate 中平滑跟随玩家；当前村庄使用正交镜头与固定 Z 偏移 |
| VillageLandmarkBlockout | 只生成中央白叶树 Sprite 和横向 CapsuleCollider2D；树高、碰撞大小／偏移及 Y 排序偏移可在 Inspector 调整，编辑模式修改后重建；阴影待建筑完成后统一处理 |
| ExplorationSceneController | 从运行时状态恢复位置，否则使用 PlayerSpawn；直接运行场景时初始化运行时状态，提供保存接口 |
| ExplorationMenuController | 探索 Esc 菜单、时间与移动锁定、焦点、读档、手动保存／覆盖确认及返回标题流程 |
| PartyMenu | 共用探索菜单样式，显示三个出战角色等级姓名及 InformationPanel 的 EXP、下一级、HP、MP，左右选择、Z 确认交换顺序、X 取消或返回 |
| MenuInput / MenuInputState | 当前菜单页面的输入归属、键盘读取、切页当帧隔离及统一按钮焦点设置；进入 Play Mode 重置 |
| KeyboardMenuInputModule | 继承现有 Input System UI 模块，保留 Action 引用及导航；切页帧和 X／Esc 帧不发送 UI 导航或提交 |

## 当前游戏流程

2026-09-27 展示入口调整：标题“开始游戏”仍初始化新游戏与播放原过场，但目的地改为 Battle_scene；清理旧战斗会话，由战斗场景创建默认阵容，战后 Continue 沿直接运行战斗的既有规则返回 Title_scene。读取存档仍进入存档记录的探索场景；新游戏自动档的探索位置仍为默认村庄，不改变存档格式。下文标题进入探索的描述为此前闭环，当前新游戏入口以此条为准。

### 探索菜单复用

- ExplorationMenuController 位于共用 ExplorationCanvas 根物件（保持启用），不再挂在地图 ExplorationController 上；菜单内部按钮与面板引用保存于 Prefab。页面仍由原有 Awake 关闭，Esc 开启，不采用跨场景常驻 UI。
- 新地图放入一个 ExplorationCanvas Prefab，在根物件的 ExplorationMenuController 中绑定该地图的 Scene Controller 与 Player Controller。两个引用只保存在场景实例中，不应用回 Prefab；现有 Village_scene 已绑定。
- 每张地图保留一个使用现有 KeyboardMenuInputModule 和键盘 Actions 的 EventSystem；共用 Canvas 不包含 EventSystem，也不包含地图玩家、摄影机或遇敌控制器。
- 修改共用布局时打开 Prefab Mode 编辑；存档页继续复用 SaveDataMenu，标题与探索不各自复制一份。不要通过 Unpack 断开共用关系，也不要同时保留旧菜单与新实例。

### 场景与输入

- 探索地图的 ExplorationEncounterController 可在 Inspector 指定 Battle Environment Prefab，遇敌时通过 BattleSession.EnvironmentPrefab 临时传递。背景不进入存档或奖励数据；Retry 保留引用，离开战斗、准备下一场和 Play Mode 重置会清理或替换。Battle_scene 的 BattleEnvironmentRoot 在 Awake 生成背景，Default Environment Prefab 用于未指定地图背景或直接运行战斗场景时的回退。当前两处均未指定素材，不影响战斗。
- 探索地图通过 ExplorationEncounterController 的 Allow Random Encounters 区分可遇敌与安全场景。2026-09-27 按用户要求开启 Village_scene 随机遇敌，沿用 JSON 的步距、安全步数、概率及默认敌队；本条取代之前村内禁遇敌的规划与历史描述。开关保存在场景 Inspector 中。
- 背景 Prefab 根节点以 BattleEnvironmentRoot 为局部坐标原点，仅包含环境美术，不包含摄影机、角色、战斗控制器或 UI。当前仍沿用 2D Renderer 与正交战斗摄影机；3D 灯光和镜头改造不在本次范围。

- 标题、探索根菜单／确认框、存档与队伍页通过 MenuInput 激活当前接收页；切页／返回当帧屏蔽输入，返回时由原流程恢复入口按钮或存档槽。Party 的 X 优先取消待交换角色，再返回；标题退出取消沿用回到新游戏。探索 Esc 关闭整个菜单，战斗 Esc 沿用暂停限制。UI 输入模块优先屏蔽 X／Esc 帧提交，避免 Update 顺序导致 Z 穿透；战斗 HUD、暂停与结算也共用帧保护。普通按钮继续使用原 Input Actions 导航／Z 提交，不另建一套按钮执行系统。
1. 标题菜单进入场景后默认选择“新游戏”，方向键切换、Z 确认。根页面按 X 或确认“退出游戏”会打开“是否退出游戏？”提示，默认选择“否”；X 或“否”返回根页面并重新选择“新游戏”，“是”退出游戏。
2. 新游戏使用默认队伍和物品建立版本 3 自动存档，并将探索目的地设为 Village_scene／PlayerSpawn；标题 Load Game 打开纵向存档列表，可读取自动槽或已有手动槽，版本 1／2 存档会自动迁移。旧档中的 Exploration_scene 名称只在读取内存时映射为 Village_scene，不因读取而改写源档。
3. 标题播放临时开场影片。影片结束、播放报错、未指定影片或播放期间按 Z 时，进入存档指定的探索场景；启动游戏当帧的确认不会跳过影片。
4. ExplorationSceneController 在场景初始化时恢复同一场景内已保存的玩家位置；没有位置记录时使用 PlayerSpawn。旧村庄 XZ 存档位置在内存中转为 XY，不读取改写源档；摄影机随后以玩家位置开始跟随。
5. Village_scene 的 Esc 菜单启动时关闭；Esc 开关菜单并暂停时间、锁定移动，上下选择、Z 确认、X 返回。继续、队伍、读取、手动保存／覆盖及返回标题已接入；设置仍禁用，返回标题前更新自动槽。探索步数按实际水平移动距离累计；村内关闭随机遇敌，未来可遇敌地图按资料概率判定。触发遇敌时保存当前位置，从 GameRuntimeState 快照建立玩家、物品与金币，以资料中的敌方队伍准备 BattleSession，再进入 Battle_scene。直接运行 Battle_scene 且没有战前资料时，仍使用 JSON 的默认队伍作测试。
6. 胜利时计算奖励、给角色经验，再将物品与金币合入战后结果；进入结算时写入存档，并保留原探索资料。
7. 战斗结束先在中央淡入胜败文字；按“下一个”后隐藏 SelectedCharacterPanel，文字平移至该信息区原位置。胜利随后依次显示经验页、物品页、结果页；失败直接显示重试与返回菜单。胜利先将最终结果合入运行时状态，再写自动档；Continue 使用战前传入的 returnScene 返回探索场景，由运行时状态恢复位置与战后资料。
8. 失败不发奖励、不覆盖存档；Retry 使用战前快照恢复角色与物品。

## 当前战斗与数据

- 暂停菜单通过 Inspector 引用 BattleField，打开时保存并关闭子级 Renderer 的绘制，关闭或停用菜单时恢复原值。只隐藏画面，不停用角色对象或打断播放协程。

- 输入约定：游戏不再使用鼠标操作。菜单与战斗使用方向键、Z 确认、X 返回及原有 Esc；开场影片用 Z 跳过。三个场景的 UI 输入模块已清空指针、点击、滚轮和追踪设备引用；共用 Input Actions 的鼠标／指针绑定路径已清空，保留 Action ID 与键盘绑定。角色 OnMouseDown 与战斗右键取消入口已移除，后续页面沿用此规则。
- 战斗开场、指令选择与行动播放期间，Esc 打开 BattleCanvas/BattlePauseRoot；胜败结算及初始化失败时不打开。保留经典蓝底银框与原五项布局，上下/左右移动游标，Z 确认。继续游戏、读取存档、重新挑战、返回标题有效；设置仍灰色保留。X 在根页面返回战斗，Esc 在根页面及存档页直接关闭整个暂停菜单。
- 重新挑战直接重载当前战斗场景，保留 BattleSession.Entry 与背景引用；由初始化恢复战前 HP／MP、等级／经验、编队、物品与金币，重新建立敌人与回合，不读取自动档、不覆盖存档、不保留本场已消耗资源。
- 读取存档使用 BattleCanvas 下的共用 SaveDataMenu Prefab 实例（默认隐藏）：读档时保持暂停，隐藏暂停选项但保留原背景，启用 UI 模块处理存档页 Z 提交；X 返回原“读取存档”选项，Esc 返回战斗。浏览或取消不修改当前进度。确认后先读取并验证存档及目标场景，沿用自动／手动档加载规则，清理战斗会话并进入存档的探索场景；读取、验证或手动档激活失败时保留存档页和暂停状态，输出错误。切场景前恢复时间与输入模块。
- 菜单打开时保存 Time.timeScale 后设为 0，同时通过 IsMenuPaused 阻止战斗协程（包括嵌套的开场动画及逐帧等待）继续推进，不停用 BattleController、不更改当前战斗状态。浮动数字改用游戏时间并在零时间倍率时停止。恢复时保留指令子菜单、目标、播放进度与之前的时间倍率；菜单关闭当帧隔离 Z/X，下一帧恢复原 EventSystem 输入模块及焦点。停用菜单组件或离开场景会清理暂停状态。
- 战斗暂停根菜单由独立键盘逻辑控制，根页关闭原 UI 输入模块，存档子页临时启用；切页当帧仍由 MenuInput 隔离。游戏不支持鼠标操作。选项和标题使用比例锚点与文字自动缩放，沿用 Canvas 的 2560x1440 参考尺寸。菜单 Inspector 可调整常态、选中与停用文字颜色。
- 初始化站位后保持 Start 状态，等待“战斗开始”淡入、停留、淡出才启动第一回合。BattleCanvas 上的 BattleTransitionView 可调整 Fade Seconds（默认 0.5）、Hold Seconds（0.6）、Move Seconds（0.8）；这些 UI 动画使用不受 Time.timeScale 影响的时间。
- 开场动画期间角色信息栏预览行动顺序中的第一位角色，指令仍保持锁定；正式进入回合后改为显示 CurrentCombatant。
- 角色信息栏的 HP 与 MP 使用横向资源条显示。HP 为红色填充、MP 为蓝色填充，未填充区域为黑色；`当前值 / 最大值` 白色文字居中覆盖在槽体上层，资源比例限制在 0%–100%。
- 过渡复用原结算标题文字与 Next 按钮，胜败提示淡入和平移期间禁止切页。Dock Anchor 为 (0.5,1)、Dock Position 为 (0,-150)，对应当前角色信息区中央；位置独立保存，不读取或跟随 SelectedCharacterPanel 的位置，该引用只用于隐藏信息区。奖励与保存仍在胜利判定时处理，点击和动画不会重复发放。
- 已有攻击、防御、技能、物品、合法目标筛选与 ResolvingAction 流程。取消目标不提前消耗 MP 或物品。
- 行动确认后锁定操作，依次等待行动表现、应用一次实际效果、等待目标反应及死亡退场，再判定胜败或推进 CTB。多人目标目前逐个播放反应和退场，全部完成后才结算。
- HP 变化会在角色上方显示数字并上浮淡出：一般伤害红色、治疗与复活绿色并带 `+`、Poison 回合伤害紫色；未另外分类的伤害沿用红色。颜色、字号、位置、距离和时间可在 BattleCanvas 的 BattleFloatingNumberView 调整。
- 回合开始的 HP 变化与中毒死亡也等待反应和退场；跳过回合使用短暂提示，连续跳过不递归调用回合流程。
- BattleActorView 优先播放 BattleAppearance 指定的 Battle Animator；尚未配置 Animator 的角色继续使用旧逐帧配置或变色、淡出表现。Reaction Seconds、Defeat Seconds 默认分别为 0.2、0.35 秒。复活恢复原色及透明度，缺少角色显示时跳过对应表现，流程仍可继续。
- 战斗角色共用 Assets/Animations/Battle/Battle_Base.controller 的 Idle、Attack、Hurt、Defeat 状态结构，每个角色通过 Animator Override Controller 替换自己的动画片段。哥布林目前使用 Goblin/Goblin_Battle.overrideController；大世界动画未来使用独立的 World 基础 Controller，不与战斗状态共用。
- 哥布林普通攻击的 Attack Clip 使用 17 张 128x128 朝左逐帧图片，Point 过滤、PPU 64、无压缩。砸地帧上的 Animation Event 通知战斗结算命中，动画不暂停并继续播放收招；完成事件后恢复 Idle。扬尘已合入攻击帧，Animator 使用游戏时间，暂停菜单会冻结动画。
- 哥布林受击的 Hurt Clip 使用 5 张 128x128 独立图片，依次播放 0.08、0.05、0.06、0.12、0.08 秒且不循环，完成事件后恢复 Idle。原素材第 2–5 张白底已转为透明，五张统一为 Sprite、Point、PPU 64、无 Mip Maps、无压缩。物理伤害播放 Hurt；Poison 与治疗沿用原显示效果。
- 哥布林死亡的 Defeat Clip 使用 17 张 96x96 透明独立图片，按 1.435 秒节奏播放一次。目标由存活变为 HP 0 时时跳过 Hurt 并直接进入 Defeat；完成后保持最后一帧，不恢复 Idle 也不淡出。没有配置 Animator 或旧死亡逐帧动画的角色继续使用淡出退场。
- 马来貘使用同一 Battle_Base 状态结构，通过 Tapir_Battle.overrideController 播放左向待机、攻击、受击及死亡逐帧动画。BattleData.json 新增 malayan_tapir，直接运行战斗与探索遇敌的默认敌队改为三只马来貘；哥布林资料保留。马来貘暂沿用哥布林的战斗数值与经验，但不掉落哥布林物品；实际画面与站位仍待 Unity Play Mode 验收。
- 行动、反应与退场继续通过 BattleActorView 的 PlayAction、PlayActionRecovery、PlayReaction、PlayDefeat 协程串联；Animator Clip 用命中及完成事件回报播放节点。显示代码不扣血、不发奖励；中止时 ResetPresentation 回到 Idle、原色和正确透明度。
- 六个站位保留原坐标。每个 ActorCard 下分别是 BodySprite、GroundTile、SelectionMarker；地块目前为未指定 Sprite 的占位物件。选取提示及左右目标切换按钮统一使用 BattlePauseCursor 白色手指，左侧按钮保留水平翻转。手指同时供菜单 RawImage 与场景 SpriteRenderer 使用，PPU 按旧箭头宽度换算为 3086.7692，避免高分辨率素材放大世界提示。只有 BodySprite 参与人物变色与淡出，选取提示根据当前目标显示。
- CharacterDefinition 的 battleAppearance 可指定 Resources 下的 BattleAppearance 路径；有指定时角色资料会覆盖站位预设外观，未指定的角色继续使用场景原图。哥布林目前指定 Battle/Appearances/GoblinBattleAppearance，使用 PixelLab 92x92 朝左素材、PPU 64、Scale 1.5，并按图片底部 15 像素透明留白校正脚底；待机、攻击、受击与死亡共用此比例，其余三个方向保留在 Assets/Art/Sprites/Enemies/Goblin/PixelLab。
- 在 Project 的 Create > Battle > Appearance 创建共用外观，将素材中的 Sprite 子资源拖入 Sprite，设定 Foot Point（图片矩形内的归一化脚底位置，左下为 0,0，默认 0.5,0）、Scale 与 Battle Animator Controller，再指定给 ActorCard 的 Appearance。调整后用组件菜单 Apply Appearance 应用，进入战斗时也会应用。没有配置时沿用 BodySprite 的图片与比例，按图片底部中央对齐。BodySprite 须保持为 ActorCard 的无旋转直接子物件；动画各帧需统一脚底基准，当前不会逐帧自动重新对齐。
- 静态检查未找到部分旧 BodySprite 引用对应的 .meta，但用户确认 Unity 中角色显示正常；不能据此断言引用失效，也无需仅因此重新指定素材。
- 停用 BattleController 会停止其全部播放协程、清除选择并恢复角色显示状态；未结束战斗进入 Paused。这是中止处理，不是可恢复的暂停功能，重新勾选组件不会自动续播；重试应重新加载战斗，使用原有战前快照。行动确认后的资源消耗不在中止时回滚，战前快照不受影响。
- 敌人随机选择存活的我方角色攻击，尚无复杂技能决策。
- 状态可共存并按优先级结算：Regen、Poison、Stun、Haste。背景为蓝色 Buff、红色 Debuff、灰色 Special。
- 默认我方：player、ally、healer。技能包括 Fire、Heal、Haste、Poison；Haste 仍需进入自身目标确认。
- 敌人只有 goblin（哥布林）一种，默认一场三个。当前每只提供 30 EXP。
- 每只敌人金币：等级 × 100 + 随机整数 [-100, 100]。
- 当前每只哥布林固定掉落一个 goblin_dirty_pants（哥布林的脏裤子），类别 Material，不可装备、不可在战斗使用。
- Potion 是战斗可用消耗品，默认数量 3。正式掉落概率与数量平衡暂缓。
- 全部已招募角色获得全额 EXP，包含后备及死亡角色。BattleSession 保存出战与后备的独立战前快照；只有出战角色参与战斗，胜利时合并名单发经验，失败不合并，Retry 恢复战前资料。
- players 保存完整角色名单；activePartyIds 保存 1～3 个出战角色 ID 与站位顺序。GameRuntimeState.SetActiveParty 校验空队、超员、重复及未招募 ID，成功后一次替换编队。遇敌按此顺序建立战斗角色，空玩家站位隐藏。
- 探索菜单的队伍调整已开放，设置仍停用。现有 PartyPanel 默认关闭，三个槽位显示 LV 与姓名；左右选择、Z 标记角色，再选位置按 Z 交换，X 优先取消未完成交换，否则返回并恢复队伍按钮焦点，Esc 关闭整个菜单。选中浅蓝、待交换金色。交换立即更新运行时编队，沿用既有保存时机。三个 InformationPanel/ExpText 已绑定 Details Text，显示当前等级 EXP／升级门槛（Next LV 下一级）、当前／最大 HP、MP，使用 pos=90 对齐；满级显示 MAX（已满级），空位清空文字。打开页面及交换时从运行时资料刷新；立绘仍为用户占位，后备区待后续设计。

## 成长与结算

- 等级上限、升级门槛和每级属性成长来自 JSON，支持连续升级与剩余经验结转。
- 当前升级默认不补满 HP/MP，具体开关在 levelRules。
- 已记录并显示升级前后等级、经验；尚未记录并显示 HP 上限、MP 上限、攻击、魔攻、速度的前后变化。
- 经验数字动画默认约 2 秒，可在 BattleResultView 的 Experience Animation Seconds 调整；按 Next 先完成动画，再按进入物品页。
- 达到升级门槛后，升级后的等级和“升级！”显示金色，保留到切页。
- 动画只用于显示，真实奖励已先应用和保存，不能在动画或切页时再次发放。
- 经验页使用 CharacterExpRow Prefab，按战后完整角色名单动态生成单行 TMP（姓名、等级、经验），包含后备角色，不再限制三行。上下键滚动，Z 先完成经验动画、再次按下进入物品页；CharacterExperienceText 只计算显示，不修改真实角色资料。物品页使用纵向 Scroll View 动态生成物品行，只显示本次获得物品及金币。

## 存档与同步

- GameRuntimeState 是当前进度来源。新游戏和明确读档初始化内存状态；浏览存档摘要不改变它。遇敌使用内存快照，自动／手动保存将当前快照写入磁盘，存档格式为版本 3，新增 activePartyIds。
- 快照与输入存档均深复制，战斗通过 BattleSession 保留独立战前状态；失败不合并，胜利用最终值替换而非重复累加。未来物品／队伍操作应通过运行时状态更新接口接入。

- 自动存档路径：Application.persistentDataPath/save.json，沿用旧档路径以保持兼容。
- 所有存档写入先序列化，再写同目录的 .tmp 文件并刷新到磁盘；已有正式文件使用 File.Replace 替换，同时将上一份保留为 .bak，首次保存用 File.Move。替换失败直接向调用方报告错误，不先删除原档；正常结束清理临时文件，下次保存可覆盖中断遗留的 .tmp。备份只保留上一份，尚未接入自动恢复或恢复 UI。
- 手动存档共十槽，路径为 Application.persistentDataPath/save_slot_01.json 至 save_slot_10.json；不存在对应文件代表空槽，不建立无效的空 JSON 文件。
- 内容：完整角色名单及各自等级、经验、HP/MP，出战角色 ID 与顺序，物品 ID 与数量、金币、探索场景、出生点 ID、探索位置、保存时间及版本号。
- 版本 1／2 存档读取时自动迁移为版本 3，按原 players 顺序初始化出战名单；版本 2 保留探索位置，版本 1 使用 Village_scene／PlayerSpawn。旧存档中的 Exploration_scene 名称在内存中映射到 Village_scene。没有保存时间的旧档采用原文件修改时间，没有 gold 时按 0 处理。更改稳定 ID 前需考虑存档兼容，显示名称可另行调整。
- 新游戏、探索中退出应用与战斗胜利会覆盖自动槽；战斗失败仍不写入，避免保存团灭状态。手动槽只在明确调用保存时建立或覆盖。
- 从手动槽继续时，先验证存档与目标场景，再复制到自动槽作为当前进度；之后的自动保存不会修改原手动槽。
- SaveDataMenu 已做成共用 Prefab，并由 SaveSlotMenu 切换读取／保存模式。读取模式允许选择自动槽与有效手动槽；保存模式将自动槽设为只读，并允许选择十个手动槽。标题随模式显示“读取存档”或“保存游戏”。
- 标题读取页固定显示自动槽和十个手动槽；每槽显示保存时间、主角等级与场景，空槽及损坏槽不可读取。上下键选择并自动滚动，Z 确认，X 返回标题，不接受鼠标操作。
- 探索菜单已共用 SaveDataMenu：读取模式可读取自动槽或已有手动槽；保存模式中自动槽只读，空手动槽直接保存，已有手动槽先确认覆盖。保存成功后刷新槽位摘要并保留当前选择。
- 本机存档不随 Unity Version Control 同步；另一台电脑没有存档是正常情况。
- 新游戏覆盖旧存档前目前没有确认窗口。

## 验证边界

当前证据汇总以 HANDOFF.md“基线结论与证据”为准，当前待办以 TODO.md“当前问题与验收入口”为准。代码实现、静态引用、外部编译、独立测试和用户实际验收分别记录。第二、三项仍待用户统一实测，当前不称为全部验收通过或已完成序章；下方历史数量不累计为一次最新测试结果。

- 战斗暂停菜单专项（2026-09-21）：延迟恢复输入只在当前没有新焦点且战斗仍可操作时恢复旧按钮，避免抢走结算／新页面焦点。切场景恢复全局时间与 UI 模块，但旧 BattleController 保持 IsMenuPaused，直到场景卸载，防止旧攻击／奖励流程继续推进；正常“继续游戏”仍解除暂停。
- 独立播放测试现在直接编译 BattlePauseMenu、MenuInput 与 MenuInputState，并增加 12 项菜单状态检查。存档页、文件服务和场景切换使用替身，不代表真实读写或 Unity EventSystem／SceneManager 验收。

当前优先级：用户已确认探索战斗存档闭环、统一菜单输入和本轮动态经验列表验收通过；最新共用探索菜单 Prefab 仅完成静态检查与外部编译，仍待 Play Mode 回归。以下带历史测试数量的说明保留为阶段记录，不代表本轮重新执行；未明确确认的专项仍见 TODO.md。

哥布林素材接入：四方向 PNG 已导入并使用独立 GUID，JSON 与 Resources 外观引用静态检查通过；Unity C# 编译通过，17 项独立播放测试通过，包含资料外观覆盖站位预设图及脚底留白校正。实际尺寸、清晰度与三个敌方站位仍待 Play Mode 视觉验收。

哥布林 Battle Animator：Battle_Base.controller 提供共用 Idle、Attack、Hurt、Defeat 状态，Goblin_Battle.overrideController 将四个基础占位 Clip 替换为哥布林 Clip 并绑定 GoblinBattleAppearance；状态、Override 对应关系、Sprite 曲线、命中与完成事件及资源 GUID 静态检查通过。Unity C# 编译 0 警告、0 错误，21 项独立播放测试通过；独立测试覆盖旧逐帧兼容流程，不替代 Animator 在 Unity 中的真实播放验收。

哥布林攻击动画：17 张最终 PNG、Point/PPU 64/无压缩设置及 Attack Clip 的 Sprite 曲线检查通过；砸地命中事件后不暂停，收招与目标反应并行。实际节奏、扬尘、脚底稳定与三只哥布林播放仍待 Play Mode 视觉验收。

哥布林受击动画：5 张受击 PNG 的透明度、尺寸与导入设置检查通过，Hurt Clip 的顺序与完成事件检查通过；实际节奏与位置仍待 Play Mode 验收。

哥布林死亡动画：17 张 96x96 PNG 已接入 GoblinBattleAppearance，致死伤害跳过 Hurt，播放完成后保持最后一帧；Unity C# 编译通过，21 项独立播放测试通过。实际节奏、站位、暂停恢复与结算画面仍待 Play Mode 验收。

战斗暂停菜单：C# 编译通过（生成工程原有框架引用警告仍在）；16 项独立播放测试通过，包括选择保留、关闭当帧输入隔离、行动开始前的逐帧等待、嵌套开场及行动/退场暂停，恢复后奖励只发一次。场景新增对象与引用经过静态检查；实际 Unity 键盘焦点、时间倍率恢复、菜单画面与分辨率适配仍待 Play Mode 验收。

浮动数字：Unity C# 编译与既有独立流程测试通过，并检查攻击、治疗、中毒传入正确显示分类；实际位置、字号与动画观感待 Play Mode 验收。

战斗过渡：C# 编译通过，独立测试共 13 项通过，新增开场锁定与淡入淡出、结束提示独立位置平移检查。场景引用与文字父级检查通过；真实 UI 焦点、切页、动画视觉仍需 Unity Play Mode 验收。

站位修改：C# 编译通过，沿用独立测试并新增脚底校正、地块与提示隔离两项，共 11 项通过；检查六个站位引用与场景对象 ID，原有站位坐标保持不变。尚未执行 Unity Play Mode 验收。

最近修改通过 Unity C# 编译；升级前后快照与连续升级的新增独立测试通过。完整独立测试中的掉落计算使用 UnityEngine.Random，无法直接在普通 .NET 进程执行，需 Unity 环境验证。最新动画、Level Up 排版与过场跳过仍需 Play Mode 实测确认，具体清单见 TODO.md。

2026-09-09：播放流程修改通过 Unity 生成的 Assembly-CSharp.csproj 的 MSBuild 编译；现有生成工程存在 .NET Framework 4.7.1/4.7.2 引用警告。9 个独立播放测试通过，覆盖等待阶段、锁定输入、一次消耗、下一回合、多人退场、中毒死亡、中止、复活与缺少美术资源。测试替代了 Unity 协程调度、渲染及随机接口，不能代替真实生命周期、画面和掉落随机验证；尚未执行 Unity Play Mode 实测。
