# 序章素材接入清单

更新日期：2026-09-22。已对照当前脚本、场景与动画资源；这是制作约束，不是新增系统的开发授权。美术风格、最终尺寸、帧数与时长尚未定案，先通过一套样板再批量制作。

## 接入级别

- **直接换图**：已有 Image／SpriteRenderer 的静态显示，保留组件、层级和引用即可换图；仍需检查比例与遮挡。
- **配置接入**：不需要新增 C#，但需在 Unity 导入、制作 Clip／Prefab／Appearance、绑定 Inspector 或填写现有 JSON 字段。不是只把源文件丢进目录就自动生效。
- **程序配合**：现有逻辑没有对应的数据或播放入口，素材可先制作样板，批量制作前需确认行为。

| 素材／用途 | 接入级别 | 当前条件 |
| --- | --- | --- |
| 现有 UI 背板、边框、静态图标 | 直接换图 | 保留原 Button／Image／TMP／布局组件和引用；Sprite 与 Texture 类型按目标组件选择 |
| 战斗角色静态外观 | 配置接入 | BattleAppearance 的 sprite、footPoint、scale；有 Animator 时也需替换 Idle Clip，避免覆盖静态图 |
| 普通攻击、受击、死亡动画 | 配置接入 | 沿用 Idle／Attack／Hurt／Defeat 与事件约定，见下文 |
| 独立施法、防御姿态、投射物、多段命中、冲刺攻击 | 程序配合 | 目前专用 Attack 动画只接普通攻击；不可仅靠添加 Clip 实现新行动逻辑 |
| 每张地图的战斗背景 | 配置接入 | 环境 Prefab 由地图指定；可设置默认回退；不复制战斗控制器或整套 UI |
| 探索地面、建筑、装饰 | 配置接入 | 3D 场景中摆放美术与碰撞，保留玩家、出生点及探索控制器 |
| 探索角色八方向待机／行走、朝向镜头精灵 | 程序配合 | 目前只有方块移动，没有动画方向选择或 billboard 接入 |
| 队伍页立绘 | 静态可换图，动态需程序配合 | PartyMenu 目前只绑定按钮、姓名和数值；换位后立绘不会自动按角色更新 |
| 镜头距离、角度、跟随平滑 | 配置接入 | 修改 ExplorationCameraFollow 参数和 Camera 投影；不是只拖动摄像机位置 |
| 剧情镜头、定点构图、区域镜头边界、墙体遮挡处理 | 程序配合 | 现有镜头只有固定角度跟随，不能自动播放演出或避免穿墙 |
| 新 UI 页面、后备角色替换、对话／任务界面 | 程序配合 | 新图或面板本身不会生成对应操作与数据逻辑 |
| BGM、脚步、菜单／战斗音效 | 程序配合 | 尚无完整音频管理与事件播放入口；先交付声音与触发说明 |

## 战斗角色与动画

**现有入口**：`Assets/Scipts/Animation/BattleAppearance.cs`；参考 `Assets/Resources/Battle/Appearances/GoblinBattleAppearance.asset`、`Assets/Animations/Battle/Battle_Base.controller` 和 Goblin 的 Override Controller。

1. 每个角色先交付透明底待机图和一套普通攻击、受击、死亡样板。注明左右朝向、画布尺寸、PPU、脚底位置、逐帧时长、攻击接触帧；不用统一强制成哥布林的尺寸或帧数。
2. 同一动作各帧使用稳定画布、脚底与 Pivot；尽量让该角色不同动作使用一致基准。当前脚底校正在绑定外观时计算，不会逐帧重新对齐；随意裁切每帧容易产生跳动。动作确需不同画布时，先验证 Pivot 与位移，不依赖自动纠正。
3. 像素素材沿用 Point、透明通道、关闭 Mip Maps 与无损导入的现有做法；不同角色 PPU／scale 应用实际站位比对，不以图片像素尺寸直接判断游戏内大小。
4. Animator 位于带 BattleActorView 的角色根物件；现有 Clip 的 Sprite 曲线目标为直接子物件 `BodySprite`。保留此路径，不把动画挂到不接收事件的子物件上；人物动作不要动画化控制器、站位根节点、GroundTile 或 SelectionMarker。
5. 用共用 Controller 的四个状态制作 Override Controller，配置到 `battleAnimatorController`。Attack 的接触帧加入 `OnBattleAnimationImpact`，Attack／Hurt／Defeat 的结束加入 `OnBattleAnimationComplete`。动画事件只回报时机，不扣血、不发奖励；不要依赖缺事件时的退路来获得正确命中节奏。
6. Attack／Hurt／Defeat 不循环；受击后由程序回 Idle，死亡保留末帧。Idle 若制作循环，单独配置并实测；当前哥布林 Idle 是单张图，不代表所有待机都必须静止。
7. 当前程序关闭 Root Motion；需要角色接近敌人、镜头震动、多段命中或新施法状态时，先确定程序与表现配合，不能只在 Clip 添加多个命中事件就改变伤害次数。
8. JSON 的 `battleAppearance` 填 Resources 相对路径，如 `Battle/Appearances/GoblinBattleAppearance`，不写扩展名。角色显示名可改，现有稳定 ID 不随美术改名。

`.aseprite`／绘图源文件可保留，不要求所有源稿都是 PNG；实际绑定需要 Unity 已导入的 Sprite、AnimationClip 或 Controller。导入器自动生成的 Prefab／动画层级未必符合 `BodySprite` 和事件约定，先核对后再接入。序列 PNG 或切好的 Sprite Sheet 可作为明确帧序的交付副本。

## 地图与战斗背景

- 探索为 XZ 地面的 3D 场景、Y 向上；地图使用 Universal Renderer，战斗继续使用 2D Renderer 与正交镜头。不要为一份新美术替换整个项目的默认 Renderer。
- 探索建筑和地面可替换灰盒外观，但必须保留或重新设置合理的碰撞。不要删除 Player 上的 CharacterController／移动脚本，也不要让装饰模型的尺寸迫使人物整体缩放。
- 遇敌步距当前是 1 世界单位；改地图尺度会影响行走距离与遇敌节奏。先在当前主角尺寸与镜头下做一段可走通的道路，再决定整体尺度。
- 战斗环境单独保存为 Prefab，根节点建议位置／旋转为零、缩放为一，内容放在子物件。实例在 BattleEnvironmentRoot 下以局部空间生成；不要包含摄影机、角色、控制器、UI 或 BattleEnvironmentRoot 自己。
- 背景保持在角色后方，按当前 Sorting Layer／Order 和相机实际测试。不要只凭 Scene 窗口中的前后位置判断遮挡，也不在这里强制指定未经验证的排序数字。
- 探索 Encounter Controller 的 `Battle Environment Prefab` 为地图入口，BattleEnvironmentView 的 `Default Environment Prefab` 为回退。地图优先，Retry 保留，离场释放；无配置允许空背景。美术预览实例不要与自动生成实例同时保留。
- 当前两处背景字段都为空，只有一张探索地图。3D 战斗场景灯光、透视镜头、前景遮挡特效或按剧情阶段动态换背景，需要另行确认，不能假设现有背景入口已包含这些功能。

## 镜头与画面尺寸

| 当前场景值 | 已核对配置 | 制作含义 |
| --- | --- | --- |
| 探索摄影机 | Perspective，FOV 60；offset (0,7,-10)，角度 (35,0,0)，平滑 0.15 | 世界坐标上从负 Z 侧朝正 Z 看；以此视角制作地面、建筑正面和角色样板 |
| 战斗摄影机 | Orthographic，Size 5 | 可见世界高度 10 单位；宽度随画面宽高比改变，不是固定像素画布 |
| UI 参考尺寸 | 探索／战斗均为 2560×1440 | 这是布局参考，不要求每张图都导出全屏尺寸 |
| Canvas | 探索 Screen Space Overlay；战斗 Screen Space Camera | 保留原 Canvas 模式与摄像机绑定，不为换皮新建重复 Canvas |

这些是当前基线，不是最终美术定稿。探索摄像机的 Start／LateUpdate 会写入位置和角度，调整构图应改跟随组件参数；剧情动画直接驱动同一 Camera 会与跟随逻辑争用，需要程序交接控制权。

背景不要只画刚好覆盖当前窗口的边缘；先用 2560×1440、1920×1080 和较窄窗口检查。人物、站位及文字要留空间；目前没有自动适配所有画幅的背景缩放或镜头限位系统。

## UI 换皮边界

- 探索菜单改 `Assets/Prefabs/UI/ExplorationCanvas.prefab`，存档页改 `Assets/Prefabs/UI/SaveDataMenu.prefab`。存档页同时用于标题、探索、战斗，三处都需验收；不 Unpack 或复制成独立版本。
- 每张地图的 Canvas 实例只绑定自己的玩家与场景控制器，不能把这些场景引用应用回共用 Prefab。
- 保留三个队伍槽、姓名与 InformationPanel 文本引用。静态立绘只属于槽位，不等同角色数据；待按 ID 绑定后才可跟随队伍交换。
- 经验和奖励列表运行时生成，修改 `CharacterExpRow.prefab`／`ItemRewardPrefab.prefab`，不要在 Content 手工堆放假数据行。行高由布局管理时调整 LayoutElement，不强拖被驱动的 RectTransform。
- Image 使用 Sprite，RawImage 使用 Texture；可拉伸边框设置切片边界，立绘／图标保留比例，不用整张带文字的底图替代可更新文字。已有引用类型决定交付对象，而非一律要求 PNG。
- 保留正常、选中、按下、禁用及队伍待交换状态的可辨识反馈。游戏只用键盘：方向键、Z、X、Esc；不设计依赖 Hover 或鼠标点击才能发现的入口。
- 游戏文字用简体中文，交付最长名称、较大数值、满级／空槽样例检查空间。需要换字体时连同中文字形与 TMP 回退一起检查，不通过关闭警告隐藏缺字。

## 一套样板的交付内容

先制作一个角色、一段可走的探索地图、一个战斗背景和一套 UI 换皮样板。每份包含：

- 源文件及可导入副本；名称、角色／地图用途、版本与素材授权记录。
- 尺寸、透明留白、Pivot／脚底、PPU／世界尺度；不要在未定样板前批量套用固定规格。
- 动画帧序、时长、循环与命中帧；声音触发时机另列，音乐提供循环段说明。
- 一张目标镜头下的构图参考，标注哪些元素可移动、遮挡或需要程序控制。
- 明确列出超出现有入口的需求，例如八方向切换、动态立绘、对话镜头或独立技能演出。

程序接入前先核对本清单的级别，再讨论缺少的最小功能；素材到位不意味着自动授权新增系统。样板验收和多地图／背景实测清单见 `Tests/SceneIntegration/README.md`，未通过 Play Mode 的项目继续保留在 TODO.md。
