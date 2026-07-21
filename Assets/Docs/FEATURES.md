# EndLink Features

本文件记录 EndLink 当前已经完成或已经建立骨架的功能。后续开发规划请看 `ROADMAP.md`。

更新原则：更新尽量简洁明了，写清现有内容，不用把演变沿革都写上去。

子文档指引：
- `FEATURES.md` 只维护当前情况概览、Feature 目录和状态索引，不再堆放完整详情。
- 功能详情按模块拆到 `Assets/Docs/Features`，更新时只改对应子文档。
- [PLAYER](Features/PLAYER.md)：玩家、3C、玩家状态机和玩家侧战斗接线。
- [COMBAT](Features/COMBAT.md)：战斗数据、目标、伤害、受击、Hitbox、标签和事件系统。
- [PARTY](Features/PARTY.md)：小队管理、小队战斗上下文和连携窗口。
- [ALLY](Features/ALLY.md)：队友状态机、助战和跟随表现。
- [ENEMIES](Features/ENEMIES.md)：正式敌人身份、生命、感知、状态机和基础移动。
- [WORLD](Features/WORLD.md)：灰盒地图中的门、电梯、机关等世界交互底座。
- [UI](Features/UI.md)：运行时 HUD、动作槽位和通用 UI 组件。
- [TOOLS](Features/TOOLS.md)：Editor 工具、数据创建工具和调试监视窗口。

## 已完成内容

### 当前情况概览

项目使用 Unity 6，当前核心代码集中在 `Assets/_EndLink/Control`、`Assets/_EndLink/Player`、`Assets/_EndLink/Combat`、`Assets/_EndLink/Ally`、`Assets/_EndLink/Party`、`Assets/_EndLink/Enemies`、`Assets/_EndLink/World` 和 `Assets/_EndLink/UI`。控制与玩家状态机代码主要使用命名空间 `EndLink.Core`，战斗相关代码使用 `EndLink.Combat`，队友相关代码使用 `EndLink.Ally`，固定小队管理使用 `EndLink.Party`，敌人相关代码使用 `EndLink.Enemies`，世界交互代码使用 `EndLink.World`，运行时 UI 使用 `EndLink.UI`。目前已经完成了玩家输入读取、CharacterController 移动控制、Cinemachine 第三人称相机控制、玩家有限状态机最小战斗骨架、通用生命值与角色受击接线、统一 Combat Target、统一 Action 执行接口、玩家与敌人 Animator 桥接、动画事件动作时序第一版、基础攻击驱动、基础 Hitbox 配置、通用战斗反馈调度基础、战斗标签系统、战斗事件总栈基础版、事件接线、队友助战基础组件、队友目标选择、小队战斗状态上下文、队友状态机骨架、队友跟随移动与动态站位第一版、固定三人小队管理第一版、正式敌人通用基底、敌人大状态机骨架、世界交互底座和战斗 UI 基础。

项目仍处于白模阶段，角色以胶囊体为主，当前重点是验证控制手感和后续架构边界。

### Feature 目录

#### PLAYER

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [新版 Input System 输入读取](Features/PLAYER.md#feature-input-system) | 已完成硬锁切换输入版 | 负责读取玩家移动、攻击、防御、目标锁定/切换、世界交互和相机等输入，并把输入缓存为控制层可使用的数据。 |
| [玩家 CharacterController 移动](Features/PLAYER.md#feature-player-movement) | 已完成锁定移动版 | 负责玩家平滑移动、基础跳跃、重力贴地、转向、目标相对移动，以及战斗击退的短时衰减后退。 |
| [第三人称视角模式](Features/PLAYER.md#feature-third-person-camera) | 已完成锁定操控版 | 独立保存高速自由与魂类近距两套镜头预设；魂类硬锁会统一驱动镜头、玩家朝向、锁定移动和定向闪避。 |
| [玩家有限状态机](Features/PLAYER.md#feature-player-state-machine) | 已完成动作取消策略版 | 负责八个玩家状态，并承接普攻缓冲、动作锁、取消窗口、普攻/技能派生、闪避/格挡取消和强制打断。 |
| [玩家 ActCombat 基础](Features/PLAYER.md#feature-player-act-combat) | 已完成格挡反馈版 | 提供三段普攻连段、无效下一段超时退出、攻击踏步与软锁追踪，以及带白模反馈的正面格挡和短窗口弹反。 |
| [玩家 Animator 桥接](Features/PLAYER.md#feature-player-animator) | 已完成动画事件接线版 | 同步玩家状态、移动和 Action 参数，并把动画判定、取消窗口与动作结束事件转发给战斗 Driver 和状态机。 |
| [玩家目标选择](Features/PLAYER.md#feature-player-targeting) | 已完成硬锁切换版 | 自动软目标按固定间隔刷新；魂类视角可固定、解除并按左右方向切换硬锁目标，攻击系统统一读取有效目标。 |
| [玩家战斗驱动](Features/PLAYER.md#feature-player-combat-driver) | 已完成多判定窗口版 | 由状态机调用，支持数据或动画事件驱动判定，并允许单个动画动作重复开启独立 Hitbox 窗口。 |
| [当前架构边界](Features/PLAYER.md#feature-architecture-boundary) | 已建立初版约定 | 初步明确输入读取、玩家移动、相机控制、状态机、战斗驱动、命中检测之间的职责边界。 |

#### COMBAT

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [通用生命值与角色受击接线](Features/COMBAT.md#feature-character-health) | 已完成桥接版 | 提供可复用的血量、受击、治疗和死亡；玩家、队友通过薄桥接层接入各自状态机。 |
| [统一 Combat Target](Features/COMBAT.md#feature-combat-target) | 已完成第一版 | 为玩家、队友和敌人统一提供唯一根身份、存活/可选状态、锁定点、Collider 表面点和水平表面距离。 |
| [角色战斗数值基础](Features/COMBAT.md#feature-character-stats) | 已完成第一版 | 提供玩家、队友和敌人共用的攻击力与承受击退倍率，并支持动作按固定伤害与攻击力倍率组合计算伤害。 |
| [战斗动作配置](Features/COMBAT.md#feature-combat-action) | 已完成命中反馈配置版 | 使用 `CombatActionDefinition` 描述伤害、冷却、Hitbox、标签、协同率、动画根位移和命中反馈，并支持数据时间或动画事件驱动动作。 |
| [通用战斗反馈](Features/COMBAT.md#feature-combat-feedback) | 已完成调度基础版 | 通过可复用反馈资产与场景调度器，统一提供 Hitstop、Cinemachine Impulse、手柄震动、音效和 VFX 请求入口。 |
| [统一 Action 执行接口](Features/COMBAT.md#feature-combat-action-executor) | 已完成多判定窗口版 | 统一玩家、队友和敌人的动作检查、执行、冷却和目标传入，并支持动画事件动作在一次执行中开启多个判定窗口。 |
| [伤害结算管线基础](Features/COMBAT.md#feature-damage-pipeline) | 已完成基础版 | 建立 `DamageContext`、`DamageResult` 和 `DamageCalculator`，让 Hitbox、标签反应和直接伤害先进入统一伤害上下文，再交给生命组件扣血。 |
| [受击规则基础](Features/COMBAT.md#feature-hit-response) | 已完成动态拦截版 | Hitbox 造成伤害后统一计算击退，并允许格挡、弹反、临时护盾等规则动态接入生命结算，修改伤害与击退结果。 |
| [战斗标签系统](Features/COMBAT.md#feature-combat-tags) | 已完成基础版 | 提供战斗专用标签定义、目标标签容器、多标签、持续时间、带来源的增删事件、合法检查和协议反应规则。 |
| [战斗事件总栈](Features/COMBAT.md#feature-combat-events-bus) | 已完成基础接线版 | 提供全局战斗事件类型、事件数据、事件广播入口、Console 日志监听器和 Editor 战斗事件监视窗口，当前已接入攻击、命中、受伤、死亡和标签变化。 |
| [基础 Hitbox 配置](Features/COMBAT.md#feature-hitbox) | 已完成动画窗口接线版 | 提供通用与远程 Hitbox；普通判定支持数据有效段或动画事件关闭，弹体始终使用自身寿命规则。 |

#### PARTY

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [连携触发与窗口](Features/PARTY.md#feature-party-link-context) | 已完成奥义充能接线版 | 协议反应触发后为三人小队开启 4 秒共享连携窗口，允许玩家释放一个连携技，并按连携动作配置提升全队协同率。 |
| [小队战斗状态上下文](Features/PARTY.md#feature-party-combat-context) | 已完成基础版 | 监听战斗事件，记录小队是否处于战斗、当前主目标和已知敌人，供队友目标选择、战斗 UI 和后续连携系统读取。 |
| [固定三人小队管理](Features/PARTY.md#feature-party-manager) | 已完成动态槽位版 | 负责保存固定主控和 2 个队友槽位，统一分配跟随目标、动态队形、小队查询和战斗命令，并支持暂时禁用单个队友。 |

#### ALLY

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [队友助战基础组件](Features/ALLY.md#feature-ally-assist) | 已完成持续助战第一版 | 提供队友事件响应大脑和队友战斗执行器，用于主控命中敌人后让队友自动接近目标并持续攻击。 |
| [队友有限状态机](Features/ALLY.md#feature-ally-state-machine) | 已完成动作打断版 | 提供 Idle、Follow、Assist、Action、Hit、LinkDown 外层状态，并在受击、离开助战或链接中断时清理当前动作判定。 |
| [队友跟随移动](Features/ALLY.md#feature-ally-follow-motor) | 已完成 NavMesh 接线版 | 负责队友在 Follow 状态中跟随主控，移动到主控附近的队形偏移范围，并支持 NavMesh 寻路、按路径高度移动、高低差脱离死区、坡道贴地、平滑减速、追赶、远距离归位和简易避让。 |

#### ENEMIES

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [敌人身份与生命目标](Features/ENEMIES.md#feature-enemy-identity-health) | 已完成分类基础版 | 提供正式敌人根身份、根类别、战斗定位、生命受击、视觉受击反馈、死亡事件、目标有效性、死亡退场、战斗标签容器和基础调试显示。 |
| [敌人韧性、平衡与失衡](Features/ENEMIES.md#feature-enemy-balance-stagger) | 已完成第一版 | 用隐性韧性判断单次命中是否触发受击，以可恢复平衡值驱动独立失衡状态，并向后续处决流程暴露资格和事件。 |
| [敌人 Animator 桥接](Features/ENEMIES.md#feature-enemy-animator) | 已完成受控根运动版 | 把敌人移动、大状态和动作参数同步给 Animator，并支持动画关键帧驱动判定、动作结束和按动作应用水平根位移。 |
| [敌人感知与大状态机](Features/ENEMIES.md#feature-enemy-state-sensor) | 已完成失衡接线版 | 提供 Idle、Alert、Combat、Hit、Stagger、Return、Dead 大状态，以及 Combat 内部的接近、观察、攻击准备、攻击、恢复和重新定位流程。 |
| [敌人围攻协调](Features/ENEMIES.md#feature-enemy-combat-coordination) | 已完成观察移动版 | 通过区域协调器统一管理敌人归属、攻击评分、同时攻击数量、许可预留、动态软站位和等待/攻击准备机动。 |
| [敌人移动与战斗能力](Features/ENEMIES.md#feature-enemy-motor-combat) | 已完成多段动作版 | 提供地面移动、NavMesh 追击、转向、重力、碰撞推挤、衰减击退、普攻/技能选择和单 Action 多段判定。 |

#### WORLD

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [世界交互底座](Features/WORLD.md#feature-world-interaction) | 已完成接口一致性版 | 提供统一接口扫描、交互点距离判断、执行前范围复检和玩家输入桥接，用于门、电梯、开关等灰盒机关扩展。 |
| [通用出生点与检查点](Features/WORLD.md#feature-world-spawn-checkpoint) | 已完成第一版 | 提供可复用的世界出生锚点、开场出生、检查点休整和玩家死亡复活流程，并为未来存档与敌人生成保留稳定位置身份。 |
| [两层移动电梯](Features/WORLD.md#feature-elevator-platform) | 已完成第一版 | 提供可交互的上下层往返平台，通过运动学 Rigidbody 驱动物理实体，并为 CharacterController 乘客补偿平台三维位移。 |
| [通用开关门](Features/WORLD.md#feature-world-door) | 已完成第一版 | 提供接入世界交互系统的平移门和旋转门，支持平滑开关、运行中反向、动态提示和开关事件。 |

#### UI

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [战斗 UI 基础](Features/UI.md#feature-combat-ui-foundation) | 已完成队伍血条接线版 | 提供 HUD 总入口、小队动作栏、成员头像连携高亮、终链奥义条、运行时调试日志、动作槽位冷却显示、主角/队友状态血条自绑定、通用血条、敌人头顶血条和 UGUI 生成入口。 |

#### TOOLS

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [战斗数据编辑工具](Features/TOOLS.md#feature-combat-data-tool) | 已完成字段同步版 | 提供 Editor 窗口快捷创建和查看战斗动作、战斗标签、标签组合规则数据资产，并显示伤害、倍率、类型和连携协同率等关键字段摘要。 |
| [战斗 HUD 生成工具](Features/TOOLS.md#feature-combat-hud-generator) | 已完成 Panel 生成版 | 提供 Editor 菜单入口生成基础 UGUI 战斗 HUD Panel Prefab，默认去掉可见英文占位文案，主角/队友状态条使用绿色、奥义条使用黄色，作为后续由 Agent 或人工扩展 HUD 的稳定通道。 |
| [EndLink Combat Lab](Features/TOOLS.md#feature-combat-lab) | 已完成新模型实验版 | 提供浏览器端战斗实验工具，用于快速验证标签定义、层数、持续时间、反应效果、基础伤害和连携窗口。 |
| [队友调试监视窗口](Features/TOOLS.md#feature-ally-monitor) | 已完成第一版 | 提供 Editor 窗口集中查看队友状态快照和队友行为日志，辅助排查助战、冷却、距离和目标问题。 |
