# EndLink Features

本文件记录 EndLink 当前已经完成或已经建立骨架的功能。后续开发规划请看 `ROADMAP.md`。

更新原则：更新尽量简洁明了，写清现有内容，不用把演变沿革都写上去。

子文档指引：
- `FEATURES.md` 只维护当前情况概览、Feature 目录和状态索引，不再堆放完整详情。
- 功能详情按模块拆到 `Assets/Docs/Features`，更新时只改对应子文档。
- [PLAYER](Features/PLAYER.md)：玩家、3C、玩家状态机和玩家侧战斗接线。
- [COMBAT](Features/COMBAT.md)：战斗数据、目标、伤害、受击、Hitbox、标签和事件系统。
- [ALLY](Features/ALLY.md)：队友状态机、助战、跟随表现、小队管理、战斗上下文和连携窗口。
- [ENEMIES](Features/ENEMIES.md)：正式敌人身份、生命、感知、状态机和基础移动。
- [WORLD](Features/WORLD.md)：灰盒地图中的武器物体交互、门、电梯和检查点。
- [UI](Features/UI.md)：运行时 HUD、动作槽位和通用 UI 组件。
- [TOOLS](Features/TOOLS.md)：Editor 工具、数据创建工具和调试监视窗口。

## 已完成内容

### 当前情况概览

项目使用 Unity 6，当前核心代码集中在 `Assets/_EndLink/Control`、`Assets/_EndLink/Player`、`Assets/_EndLink/Combat`、`Assets/_EndLink/Ally`、`Assets/_EndLink/Enemies`、`Assets/_EndLink/World` 和 `Assets/_EndLink/UI`。控制与玩家状态机代码主要使用命名空间 `EndLink.Core`，战斗相关代码使用 `EndLink.Combat`，队友相关代码使用 `EndLink.Ally`，队友目录下的小队管理代码使用 `EndLink.Party`，敌人相关代码使用 `EndLink.Enemies`，世界交互代码使用 `EndLink.World`，运行时 UI 使用 `EndLink.UI`。目前已经完成了玩家输入读取、CharacterController 移动控制、Cinemachine 第三人称相机控制、玩家有限状态机最小战斗骨架、通用生命值与角色受击接线、统一 Combat Target、统一 Action 执行接口、玩家与敌人 Animator 桥接、动画事件动作时序第一版、基础攻击驱动、基础 Hitbox 配置、通用战斗反馈调度基础、战斗标签系统、战斗事件总栈基础版、事件接线、队友助战基础组件、队友目标选择、小队战斗状态上下文、队友状态机骨架、队友跟随移动与动态站位第一版、固定三人小队管理第一版、正式敌人通用基底、敌人大状态机骨架、武器物体交互和战斗 UI 基础。

项目仍处于白模阶段，角色以胶囊体为主，当前重点是验证控制手感和后续架构边界。

### Feature 目录

#### PLAYER

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [新版 Input System 输入读取](Features/PLAYER.md#feature-input-system) | 已完成三形态输入版 | 负责读取玩家移动、攻击、防御、瞄准、目标锁定、武器形态切换和 Look 等输入；手柄 `LT` 可快捷切入 B 形态瞄准，攻击/格挡为 `RB / LB`。 |
| [玩家 CharacterController 移动](Features/PLAYER.md#feature-player-movement) | 已完成锁定移动版 | 负责玩家平滑移动、基础跳跃、重力贴地、转向、目标相对移动，以及战斗击退的短时衰减后退。 |
| [第三人称视角模式](Features/PLAYER.md#feature-third-person-camera) | 已完成锁定操控版 | 独立保存高速自由与魂类近距两套镜头预设；魂类硬锁会统一驱动镜头、玩家朝向、锁定移动和定向闪避。 |
| [玩家有限状态机](Features/PLAYER.md#feature-player-state-machine) | 已完成动作取消策略版 | 负责八个玩家状态，并承接普攻缓冲、动作锁、取消窗口、普攻/技能派生、闪避/格挡取消和强制打断。 |
| [玩家 ActCombat 基础](Features/PLAYER.md#feature-player-act-combat) | 已完成格挡反馈版 | 提供三段普攻连段、无效下一段超时退出、攻击踏步与软锁追踪，以及带白模反馈的正面格挡和短窗口弹反。 |
| [玩家 Animator 桥接](Features/PLAYER.md#feature-player-animator) | 已完成动画器骨架版 | 同步玩家状态、移动和 Action 参数，提供 `Reaction > Action > Locomotion` 统一动画器结构，并转发动画判定、取消窗口与动作结束事件。 |
| [玩家目标选择](Features/PLAYER.md#feature-player-targeting) | 已完成方向切换版 | 自动软目标按固定间隔刷新；魂类视角可固定、解除，并通过鼠标横向滑动或手柄右摇杆左右推动切换硬锁目标。 |
| [玩家战斗驱动](Features/PLAYER.md#feature-player-combat-driver) | 已完成多判定窗口版 | 由状态机调用，支持数据或动画事件驱动判定，并允许单个动画动作重复开启独立 Hitbox 窗口。 |
| [当前架构边界](Features/PLAYER.md#feature-architecture-boundary) | 已建立初版约定 | 初步明确输入读取、玩家移动、相机控制、状态机、战斗驱动、命中检测之间的职责边界。 |

#### COMBAT

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [通用生命值与角色受击接线](Features/COMBAT.md#feature-character-health) | 已完成桥接版 | 提供可复用的血量、受击、治疗和死亡；玩家、队友通过薄桥接层接入各自状态机。 |
| [统一 Combat Target](Features/COMBAT.md#feature-combat-target) | 已完成第一版 | 为玩家、队友和敌人统一提供唯一根身份、存活/可选状态、锁定点、Collider 表面点和水平表面距离。 |
| [角色战斗数值基础](Features/COMBAT.md#feature-character-stats) | 已完成第一版 | 提供玩家、队友和敌人共用的攻击力与承受击退倍率，并支持动作按固定伤害与攻击力倍率组合计算伤害。 |
| [战斗动作配置](Features/COMBAT.md#feature-combat-action) | 已完成命中反馈配置版 | 使用 `CombatActionDefinition` 描述伤害、冷却、Hitbox、标签、协同率、动画根位移和命中反馈，并支持数据时间或动画事件驱动动作。 |
| [三种武器形态基础](Features/COMBAT.md#feature-player-weapon-forms) | 已完成解锁基础版 | 固定提供 A、B、C 三种形态；A 始终可用，B/C 支持初始锁定与运行时解锁，切换时自动跳过未解锁形态，并保留射击瞄准流程。 |
| [通用战斗反馈](Features/COMBAT.md#feature-combat-feedback) | 已完成调度基础版 | 通过可复用反馈资产与场景调度器，统一提供 Hitstop、Cinemachine Impulse、手柄震动、音效和 VFX 请求入口。 |
| [统一 Action 执行接口](Features/COMBAT.md#feature-combat-action-executor) | 已完成多判定窗口版 | 统一玩家、队友和敌人的动作检查、执行、冷却和目标传入，并支持动画事件动作在一次执行中开启多个判定窗口。 |
| [伤害结算管线基础](Features/COMBAT.md#feature-damage-pipeline) | 已完成基础版 | 建立 `DamageContext`、`DamageResult` 和 `DamageCalculator`，让 Hitbox、标签反应和直接伤害先进入统一伤害上下文，再交给生命组件扣血。 |
| [受击规则基础](Features/COMBAT.md#feature-hit-response) | 已完成命中结算版 | Hitbox 先取得受击方的 `HitResolution`，统一区分正常命中、格挡、弹反、闪避、免疫和拒绝，再决定事件、反馈与标签。 |
| [战斗标签系统](Features/COMBAT.md#feature-combat-tags) | 已完成基础版 | 提供战斗专用标签定义、目标标签容器、多标签、持续时间、带来源的增删事件、合法检查和协议反应规则。 |
| [战斗事件总栈](Features/COMBAT.md#feature-combat-events-bus) | 已完成命中结果版 | 提供全局事件广播、日志与 Editor 监视；当前会广播完整 `HitResolved`，并保留成立命中的 `HitLanded` 供战斗参与逻辑使用。 |
| [基础 Hitbox 配置](Features/COMBAT.md#feature-hitbox) | 已完成结算接线版 | 提供通用与远程 Hitbox；支持数据/动画判定窗口，并依据受击结果控制成立命中事件、普通反馈和标签附加。 |

#### ALLY

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [队友助战基础组件](Features/ALLY.md#feature-ally-assist) | 已完成持续助战第一版 | 提供队友事件响应大脑和队友战斗执行器，用于主控命中敌人后让队友自动接近目标并持续攻击。 |
| [队友有限状态机](Features/ALLY.md#feature-ally-state-machine) | 已完成动作打断版 | 提供 Idle、Follow、Assist、Action、Hit、LinkDown 外层状态；当前停用队友主动技能，Action 仅保留为连携等通用单次动作承载层。 |
| [队友跟随移动](Features/ALLY.md#feature-ally-follow-motor) | 已完成 NavMesh 接线版 | 负责队友在 Follow 状态中跟随主控，移动到主控附近的队形偏移范围，并支持 NavMesh 寻路、按路径高度移动、高低差脱离死区、坡道贴地、平滑减速、追赶、远距离归位和简易避让。 |
| [连携触发与窗口](Features/ALLY.md#feature-party-link-context) | 基础保留，运行入口停用 | 保留协议反应窗口与协同率基础代码；角色连携动作槽和全部连携输入绑定已移除，当前流程无法主动释放连携技。 |
| [小队战斗状态上下文](Features/ALLY.md#feature-party-combat-context) | 已完成基础版 | 监听战斗事件，记录小队是否处于战斗、当前主目标和已知敌人，供队友目标选择、战斗 UI 和后续连携系统读取。 |
| [固定三人小队管理](Features/ALLY.md#feature-party-manager) | 已完成动态槽位版 | 负责保存固定主控和 2 个队友槽位，统一分配跟随目标、动态队形、小队查询和战斗命令，并支持暂时禁用单个队友。 |

#### ENEMIES

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [敌人身份与生命目标](Features/ENEMIES.md#feature-enemy-identity-health) | 已完成分类基础版 | 提供正式敌人根身份、根类别、战斗定位、生命受击、视觉受击反馈、死亡事件、目标有效性、死亡退场、战斗标签容器和基础调试显示。 |
| [敌人韧性、平衡与失衡](Features/ENEMIES.md#feature-enemy-balance-stagger) | 已完成第一版 | 用隐性韧性判断单次命中是否触发受击，以可恢复平衡值驱动独立失衡状态，并向后续处决流程暴露资格和事件。 |
| [敌人 Animator 桥接](Features/ENEMIES.md#feature-enemy-animator) | 已完成受控根运动版 | 把敌人移动、大状态和动作参数同步给 Animator，并支持动画关键帧驱动判定、动作结束和按动作应用水平根位移。 |
| [敌人感知与大状态机](Features/ENEMIES.md#feature-enemy-state-sensor) | 已完成失衡接线版 | 提供 Idle、Alert、Combat、Hit、Stagger、Return、Dead 外层状态，以及自动感知、受击接战、脱战归位和目标所有权管理。 |
| [敌人基础战斗行为](Features/ENEMIES.md#feature-enemy-combat-behavior) | 已完成近战/远程第一版 | 根据敌人战斗定位选用近战或远程七阶段行为；远程单位会维持距离带、检查攻击视线、过近后撤并在受阻或攻击后侧向重新选位。 |
| [敌人围攻协调](Features/ENEMIES.md#feature-enemy-combat-coordination) | 已完成观察移动版 | 通过区域协调器统一管理敌人归属、攻击评分、同时攻击数量、许可预留、动态软站位和等待/攻击准备机动。 |
| [敌人移动与战斗能力](Features/ENEMIES.md#feature-enemy-motor-combat) | 已完成多段动作版 | 提供地面移动、NavMesh 追击、转向、重力、碰撞推挤、衰减击退、普攻/技能选择和单 Action 多段判定。 |

#### WORLD

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [白盒关卡模块库](Features/WORLD.md#feature-blockout-module-library) | 已完成通行与环境模块扩展版 | 提供统一使用 `BOX_` 前缀、带封闭实体网格的石块、树木、大型塔体占位、数据体块、窄桥及常用建筑结构 Prefab。 |
| [武器物体交互](Features/WORLD.md#feature-weapon-object-interaction) | 已完成场景接线版 | 通过通用 `ObjInteractable` 接收 A/B/C 武器命中，统一驱动 `IObjFunction`，并在功能生效时提供闪白反馈。 |
| [通用出生点与检查点](Features/WORLD.md#feature-world-spawn-checkpoint) | 已完成坠落死亡版 | 提供世界出生锚点、开场出生、检查点休整、玩家死亡复活和默认 `Y=-50` 坠落出界判定，并为未来存档与敌人生成保留稳定位置身份。 |
| [两层移动电梯](Features/WORLD.md#feature-elevator-platform) | 已完成武器交互版 | 提供由交互子物体触发的上下层往返平台，通过运动学 Rigidbody 驱动物理实体，并为 CharacterController 乘客补偿平台三维位移。 |
| [通用开关门](Features/WORLD.md#feature-world-door) | 已完成武器交互版 | 提供由交互子物体触发的平移门和旋转门，支持平滑开关、运行中反向和开关事件。 |

#### UI

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [战斗 UI 基础](Features/UI.md#feature-combat-ui-foundation) | 已完成武器形态显示第一版 | 提供生命与平衡值显示、敌人头顶血条、瞄准准星、A/B/C 武器形态显示、运行时调试日志和 UGUI 生成入口。 |

#### TOOLS

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [战斗数据编辑工具](Features/TOOLS.md#feature-combat-data-tool) | 已完成校验摘要版 | 提供 Editor 窗口快捷创建和查看战斗动作、战斗标签、标签组合规则，并同步显示命中、平衡、动作时序、反馈及基础合法性状态。 |
| [战斗 HUD 生成工具](Features/TOOLS.md#feature-combat-hud-generator) | 已收束当前组件版 | 生成运行时调试面板、敌人头顶血条、居中瞄准准星和 A/B/C 武器形态显示；旧版小队面板不再生成。 |
| [EndLink Combat Lab](Features/TOOLS.md#feature-combat-lab) | 已完成新模型实验版 | 提供浏览器端战斗实验工具，用于快速验证标签定义、层数、持续时间、反应效果、基础伤害和连携窗口。 |
| [美术资源校验工具](Features/TOOLS.md#feature-art-asset-validator) | 已完成自动校验第一版 | 自动检查 `_Incoming` 中的 Shader、材质、Prefab、FBX、贴图和临时命名问题，并提供完整结果窗口与资源定位。 |
| [场景配置体检](Features/TOOLS.md#feature-scene-doctor) | 已完成白盒规则版 | 扫描当前活动场景中的缺失引用、关键接线、NavMesh、BOX_ 子节点 Override、网格碰撞错位、场景归类和 Layer/Tag，支持筛选、定位与复制报告。 |
| [ProBuilder 网格修补](Features/TOOLS.md#feature-probuilder-mesh-repair) | 已完成安全修补第一版 | 扫描选中 ProBuilder 网格的开放边界并在 Scene View 预览，支持手动选择近平面封口、等顶点数双环桥接、Undo 和 MeshCollider 刷新。 |
| [输入绑定工具](Features/TOOLS.md#feature-input-binding-tool) | 已完成双页面版 | 使用接近游戏键位设置的键鼠/手柄双页面展示默认绑定，支持点击槽位监听修改、清除、冲突检查、Undo 和手动应用。 |
