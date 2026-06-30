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

项目使用 Unity 6，当前核心代码集中在 `Assets/_EndLink/Control`、`Assets/_EndLink/Player`、`Assets/_EndLink/Combat`、`Assets/_EndLink/Ally`、`Assets/_EndLink/Party`、`Assets/_EndLink/Enemies`、`Assets/_EndLink/World` 和 `Assets/_EndLink/UI`。控制与玩家状态机代码主要使用命名空间 `EndLink.Core`，战斗相关代码使用 `EndLink.Combat`，队友相关代码使用 `EndLink.Ally`，固定小队管理使用 `EndLink.Party`，敌人相关代码使用 `EndLink.Enemies`，世界交互代码使用 `EndLink.World`，运行时 UI 使用 `EndLink.UI`。目前已经完成了玩家输入读取、CharacterController 移动控制、Cinemachine 第三人称相机控制、玩家有限状态机最小战斗骨架、通用生命值与角色受击接线、统一 Combat Target、统一 Action 执行接口、玩家 Animator 桥接、动画协议接口骨架、基础攻击驱动、基础 Hitbox 配置、战斗标签系统、战斗事件总栈基础版、事件接线、队友助战基础组件、队友目标选择、小队战斗状态上下文、队友状态机骨架、队友跟随移动与动态站位第一版、固定三人小队管理第一版、正式敌人通用基底、敌人大状态机骨架、世界交互底座和战斗 UI 基础。

项目仍处于白模阶段，角色以胶囊体为主，当前重点是验证控制手感和后续架构边界。

### Feature 目录

#### PLAYER

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [新版 Input System 输入读取](Features/PLAYER.md#feature-input-system) | 已完成第一版 | 负责读取玩家移动、战斗、世界交互和相机等输入，并把输入缓存为控制层可使用的数据。 |
| [玩家 CharacterController 移动](Features/PLAYER.md#feature-player-movement) | 已完成受击后退版 | 负责玩家平滑移动、基础跳跃、重力贴地、转向，以及战斗击退的短时衰减后退。 |
| [第三人称自由相机](Features/PLAYER.md#feature-third-person-camera) | 已完成自动景别版 | 负责越肩第三人称视角、自由旋转和上下角度限制，并在脱战待机时缓慢拉近、移动或战斗时较快拉远。 |
| [玩家有限状态机](Features/PLAYER.md#feature-player-state-machine) | 已完成攻击缓冲版 | 负责 Idle、Move、Attack、Skill、Dodge、Hit、Dead 的状态切换，并提供短时普攻输入缓冲与攻击期间软锁跟随转向。 |
| [玩家 Animator 桥接](Features/PLAYER.md#feature-player-animator) | 已完成协议骨架版 | 负责把玩家状态、移动速度和状态进入触发器同步到 Animator 参数，并预留通用 Animator 参数协议、动画事件、Root Motion 和动作锁定/退出接口。 |
| [玩家自动软锁定](Features/PLAYER.md#feature-player-targeting) | 已完成基础版 | 负责在 Enemy Layer 中按固定间隔自动选择当前战斗目标，默认优先最近敌人，并显示轻量目标点。 |
| [玩家战斗驱动](Features/PLAYER.md#feature-player-combat-driver) | 已完成软锁朝向接线版 | 由状态机调用，负责执行攻击表现和判定，按角色实时正前方生成 Hitbox 并管理独立动作冷却。 |
| [当前架构边界](Features/PLAYER.md#feature-architecture-boundary) | 已建立初版约定 | 初步明确输入读取、玩家移动、相机控制、状态机、战斗驱动、命中检测之间的职责边界。 |

#### COMBAT

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [通用生命值与角色受击接线](Features/COMBAT.md#feature-character-health) | 已完成桥接版 | 提供可复用的血量、受击、治疗和死亡；玩家、队友通过薄桥接层接入各自状态机。 |
| [统一 Combat Target](Features/COMBAT.md#feature-combat-target) | 已完成第一版 | 为玩家、队友和敌人统一提供唯一根身份、存活/可选状态、锁定点、Collider 表面点和水平表面距离。 |
| [角色战斗数值基础](Features/COMBAT.md#feature-character-stats) | 已完成第一版 | 提供玩家、队友和敌人共用的攻击力与承受击退倍率，并支持动作按固定伤害与攻击力倍率组合计算伤害。 |
| [战斗动作配置](Features/COMBAT.md#feature-combat-action) | 已完成动画时序预留版 | 使用 `CombatActionDefinition` 数据资产描述普通攻击、技能、连携攻击和终链奥义的伤害、冷却、时序来源、Hitbox、命中标签和连携协同率收益，并已接入第一版 startup / active / recovery 执行时序。 |
| [统一 Action 执行接口](Features/COMBAT.md#feature-combat-action-executor) | 已完成时序接线第一版 | 统一玩家、队友和敌人的动作可执行检查、执行请求、目标传入和冷却查询，并让 Driver 按动作前摇后再真正生成 Hitbox。 |
| [伤害结算管线基础](Features/COMBAT.md#feature-damage-pipeline) | 已完成基础版 | 建立 `DamageContext`、`DamageResult` 和 `DamageCalculator`，让 Hitbox、标签反应和直接伤害先进入统一伤害上下文，再交给生命组件扣血。 |
| [受击规则基础](Features/COMBAT.md#feature-hit-response) | 已完成衰减击退版 | Hitbox 造成伤害后统一计算总击退距离；主角和基础地面敌人会在短时间内逐帧衰减后退。 |
| [战斗标签系统](Features/COMBAT.md#feature-combat-tags) | 已完成基础版 | 提供战斗专用标签定义、目标标签容器、多标签、持续时间、带来源的增删事件、合法检查和协议反应规则。 |
| [战斗事件总栈](Features/COMBAT.md#feature-combat-events-bus) | 已完成基础接线版 | 提供全局战斗事件类型、事件数据、事件广播入口、Console 日志监听器和 Editor 战斗事件监视窗口，当前已接入攻击、命中、受伤、死亡和标签变化。 |
| [基础 Hitbox 配置](Features/COMBAT.md#feature-hitbox) | 已完成时序接线第一版 | 提供通用 Hitbox 基类和远程直线 Hitbox，用于配置近战判定、远程飞行判定、目标过滤、生命周期、伤害、击退和标签；标准判定支持动作 `active` 覆盖寿命，弹体仍使用自身寿命规则。 |

#### PARTY

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [连携触发与窗口](Features/PARTY.md#feature-party-link-context) | 已完成奥义充能接线版 | 协议反应触发后为三人小队开启 4 秒共享连携窗口，允许玩家释放一个连携技，并按连携动作配置提升全队协同率。 |
| [小队战斗状态上下文](Features/PARTY.md#feature-party-combat-context) | 已完成基础版 | 监听战斗事件，记录小队是否处于战斗、当前主目标和已知敌人，供队友目标选择、战斗 UI 和后续连携系统读取。 |
| [固定三人小队管理](Features/PARTY.md#feature-party-manager) | 已完成动态槽位版 | 负责保存固定主控和 2 个队友槽位，统一分配队友跟随目标、动态队形槽位、小队状态查询和第一版小队战斗命令路由。 |

#### ALLY

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [队友助战基础组件](Features/ALLY.md#feature-ally-assist) | 已完成持续助战第一版 | 提供队友事件响应大脑和队友战斗执行器，用于主控命中敌人后让队友自动接近目标并持续攻击。 |
| [队友有限状态机](Features/ALLY.md#feature-ally-state-machine) | 已完成 Hit 恢复版 | 提供 Idle、Follow、Assist、Action、Hit、LinkDown 外层状态，Assist 处理自动助战，Action 承载主动技能等指令动作，Hit 结束后按上下文恢复行为。 |
| [队友跟随移动](Features/ALLY.md#feature-ally-follow-motor) | 已完成 NavMesh 接线版 | 负责队友在 Follow 状态中跟随主控，移动到主控附近的队形偏移范围，并支持 NavMesh 寻路、按路径高度移动、高低差脱离死区、坡道贴地、平滑减速、追赶、远距离归位和简易避让。 |

#### ENEMIES

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [敌人身份与生命目标](Features/ENEMIES.md#feature-enemy-identity-health) | 已完成分类基础版 | 提供正式敌人根身份、根类别、战斗定位、生命受击、死亡事件、目标有效性、死亡退场、战斗标签容器和基础调试显示。 |
| [敌人感知与大状态机](Features/ENEMIES.md#feature-enemy-state-sensor) | 已完成基础战斗循环版 | 提供 Idle、Alert、Combat、Hit、Return、Dead 大状态，以及 Combat 内部的接近、定位、攻击、恢复和重新定位流程。 |
| [敌人围攻协调](Features/ENEMIES.md#feature-enemy-combat-coordination) | 已完成软站位第一版 | 通过区域协调器统一管理敌人归属、攻击评分、同时攻击数量、许可预留和克制型动态软站位。 |
| [敌人移动与战斗能力](Features/ENEMIES.md#feature-enemy-motor-combat) | 已完成受击后退版 | 提供地面移动、NavMesh 追击、转向、重力、碰撞推挤、衰减击退和敌人战斗执行器基底。 |

#### WORLD

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [世界交互底座](Features/WORLD.md#feature-world-interaction) | 已完成第一版 | 提供可复用的可交互对象接口、基类、范围扫描器和玩家输入桥接，用于后续门、电梯、开关等灰盒机关扩展。 |
| [两层移动电梯](Features/WORLD.md#feature-elevator-platform) | 已完成第一版 | 提供可交互的上下层往返平台，通过运动学 Rigidbody 驱动物理实体，并为 CharacterController 乘客补偿平台三维位移。 |

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
