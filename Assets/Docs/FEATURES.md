# EndLink Features

本文件记录 EndLink 当前已经完成或已经建立骨架的功能。后续开发规划请看 `ROADMAP.md`。

更新原则：更新尽量简洁明了，写清现有内容，不用把演变沿革都写上去。

## 已完成内容

### 当前情况概览

项目使用 Unity 6，当前核心代码集中在 `Assets/_EndLink/Control`、`Assets/_EndLink/Player`、`Assets/_EndLink/Combat`、`Assets/_EndLink/Ally`、`Assets/_EndLink/Party`、`Assets/_EndLink/Enemies` 和 `Assets/_EndLink/UI`。控制与玩家状态机代码主要使用命名空间 `EndLink.Core`，战斗相关代码使用 `EndLink.Combat`，队友相关代码使用 `EndLink.Ally`，固定小队管理使用 `EndLink.Party`，敌人相关代码使用 `EndLink.Enemies`，运行时 UI 使用 `EndLink.UI`。目前已经完成了玩家输入读取、CharacterController 移动控制、Cinemachine 第三人称相机控制、玩家有限状态机最小战斗骨架、通用生命值与角色受击接线、统一 Combat Target、统一 Action 执行接口、玩家 Animator 桥接、基础攻击驱动、基础 Hitbox 配置、战斗标签系统、战斗事件总栈基础版、事件接线、队友助战基础组件、队友目标选择、小队战斗状态上下文、队友状态机骨架、队友跟随移动与动态站位第一版、固定三人小队管理第一版、正式敌人通用基底、敌人大状态机骨架和战斗 UI 基础。

项目仍处于白模阶段，角色以胶囊体为主，当前重点是验证控制手感和后续架构边界。

### Feature 目录

#### 3C

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [新版 Input System 输入读取](#feature-input-system) | 已完成第一版 | 负责读取玩家移动、相机旋转和鼠标滚轮缩放输入，并把输入缓存为控制层可使用的数据。 |
| [玩家 CharacterController 移动](#feature-player-movement) | 已完成第一版 | 负责玩家在 XZ 平面的平滑移动、加减速、重力贴地和面向移动方向的平滑转向。 |
| [第三人称自由相机](#feature-third-person-camera) | 已完成第一版 | 负责越肩第三人称视角、自由旋转、上下角度限制、滚轮缩放和较开阔的战斗观察距离。 |
| [玩家有限状态机](#feature-player-state-machine) | 已完成最小战斗骨架 | 负责 Idle、Move、Attack、Skill、Dodge、Hit、Dead 的状态切换，由状态机决定什么时候允许移动、攻击、释放技能、闪避、受击和死亡。 |
| [玩家 Animator 桥接](#feature-player-animator) | 已完成第一版 | 负责把玩家状态、移动速度和状态进入触发器同步到 Animator 参数，不参与状态决策。 |
| [当前架构边界](#feature-architecture-boundary) | 已建立初版约定 | 初步明确输入读取、玩家移动、相机控制、状态机、战斗驱动、命中检测之间的职责边界。 |

#### 战斗

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [通用生命值与角色受击接线](#feature-character-health) | 已完成桥接版 | 提供可复用的血量、受击、治疗和死亡；玩家、队友通过薄桥接层接入各自状态机。 |
| [统一 Combat Target](#feature-combat-target) | 已完成第一版 | 为玩家、队友和敌人统一提供唯一根身份、存活/可选状态、锁定点、Collider 表面点和水平表面距离。 |
| [角色战斗数值基础](#feature-character-stats) | 已完成第一版 | 提供玩家、队友和敌人共用的攻击力与承受击退倍率，并支持动作按固定伤害与攻击力倍率组合计算伤害。 |
| [玩家自动软锁定](#feature-player-targeting) | 已完成基础版 | 负责在 Enemy Layer 中按固定间隔自动选择当前战斗目标，默认优先最近敌人，并显示轻量目标点。 |
| [战斗动作配置](#feature-combat-action) | 已完成第一版 | 使用 `CombatActionDefinition` 数据资产描述普通攻击、技能、连携攻击和大招的伤害、冷却、时序、Hitbox 和命中标签。 |
| [统一 Action 执行接口](#feature-combat-action-executor) | 已完成第一版 | 统一玩家、队友和敌人的动作可执行检查、执行请求、目标传入和冷却查询，保留各 Driver 的具体表现实现。 |
| [伤害结算管线基础](#feature-damage-pipeline) | 已完成基础版 | 建立 `DamageContext`、`DamageResult` 和 `DamageCalculator`，让 Hitbox、标签反应和直接伤害先进入统一伤害上下文，再交给生命组件扣血。 |
| [受击规则基础](#feature-hit-response) | 已完成瞬时击退第一版 | Hitbox 实际造成伤害后，按动作基础击退距离与受击者倍率对玩家、队友和敌人施加水平瞬时击退。 |
| [战斗标签系统](#feature-combat-tags) | 已完成基础版 | 提供战斗专用标签定义、目标标签容器、多标签、持续时间、带来源的增删事件、合法检查和协议反应规则。 |
| [战斗数据编辑工具](#feature-combat-data-tool) | 已完成第一版 | 提供 Editor 窗口快捷创建和查看战斗动作、战斗标签、标签组合规则数据资产。 |
| [战斗 UI 基础](#feature-combat-ui-foundation) | 已完成基础版 | 提供 HUD 总入口、小队动作栏、动作槽位冷却显示和通用血条组件。 |
| [战斗事件总栈](#feature-combat-events-bus) | 已完成基础接线版 | 提供全局战斗事件类型、事件数据、事件广播入口、Console 日志监听器和 Editor 战斗事件监视窗口，当前已接入攻击、命中、受伤、死亡和标签变化。 |
| [玩家战斗驱动](#feature-player-combat-driver) | 已完成第一版 | 由状态机调用，负责执行攻击表现和判定，在角色前方生成 Hitbox 并管理攻击冷却。 |
| [敌人通用基底](#feature-enemy-foundation) | 已完成追击基底版 | 提供正式敌人身份入口、生命受击、大状态机骨架、基础玩家感知和按目标表面距离工作的地面追击移动。 |
| [基础 Hitbox 配置](#feature-hitbox) | 已完成第一版 | 提供通用 Hitbox 基类和远程直线 Hitbox，用于配置近战判定、远程飞行判定、目标过滤、生命周期、伤害、击退和标签。 |

#### 队伍

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [连携触发与窗口](#feature-party-link-context) | 已完成第一版 | 协议反应触发后为三人小队开启 4 秒共享连携窗口，记录反应目标并允许玩家从三个连携技中选择一个释放。 |
| [队友助战基础组件](#feature-ally-assist) | 已完成持续助战第一版 | 提供队友事件响应大脑和队友战斗执行器，用于主控命中敌人后让队友自动接近目标并持续攻击。 |
| [队友调试监视窗口](#feature-ally-monitor) | 已完成第一版 | 提供 Editor 窗口集中查看队友状态快照和队友行为日志，辅助排查助战、冷却、距离和目标问题。 |
| [队友有限状态机](#feature-ally-state-machine) | 已完成通用动作状态版 | 提供 Idle、Follow、Assist、Action、Hit、Dead 外层状态，Assist 处理自动助战，Action 承载主动技能等指令动作。 |
| [小队战斗状态上下文](#feature-party-combat-context) | 已完成基础版 | 监听战斗事件，记录小队是否处于战斗、当前主目标和已知敌人，供队友目标选择、战斗 UI 和后续连携系统读取。 |
| [队友跟随移动](#feature-ally-follow-motor) | 已完成动态站位配套版 | 负责队友在 Follow 状态中跟随主控，移动到主控附近的队形偏移范围，并支持死区、平滑减速、追赶、远距离归位和简易避让。 |
| [固定三人小队管理](#feature-party-manager) | 已完成动态槽位版 | 负责保存固定主控和 2 个队友槽位，统一分配队友跟随目标、动态队形槽位、小队状态查询和第一版小队战斗命令路由。 |

<a id="feature-input-system"></a>

### Feature：新版 Input System 输入读取

<details>
<summary>展开详情</summary>

功能说明：
- 使用已生成的 `InputSystem_Actions` C# 包装类。
- 输入动作以 `InputSystem_Actions.inputactions` 为唯一源头；修改动作或绑定后，需要在 Unity 中重新 Generate C# Class 生成 `InputSystem_Actions.cs`。
- 玩家移动输入和相机输入分开读取，避免输入读取器承担移动或相机逻辑。
- 移动输入读取 `Player/Move`。
- 攻击输入读取 `Player/Attack`，由状态机消费后决定是否进入攻击状态。
- 闪避输入读取 `Player/Dodge`，默认键位为键盘 `Left Ctrl`、手柄 `buttonEast`。
- 主控主动技能读取 `Player/PlayerSkill`，默认键位 Q。
- 队友主动技能读取 `Player/AllySlotASkill` 和 `Player/AllySlotBSkill`，默认键位 E / F。
- 主控和队友连携请求读取 `Player/PlayerLinkAttack`、`Player/AllySlotALinkAttack`、`Player/AllySlotBLinkAttack`，默认键位 1 / 2 / 3；这些输入不会绕过连携机制直接释放动作。
- 全队极限技读取 `Player/PartyUltimate`，默认键位 V。
- 手柄第一版临时绑定：主控技能 `rightShoulder`，队友 A 技能 `leftShoulder`，队友 B 技能 `rightTrigger`，主控/队友连携为 D-Pad 上/左/右，全队极限技为 D-Pad 下；后续可根据实际手柄手感统一调整。
- 相机旋转读取 `Player/Look`。
- 鼠标滚轮缩放通过 `Mouse.current.scroll` 读取。

对应脚本：
- `Assets/_EndLink/Control/InputSystem_Actions.cs`
- `Assets/_EndLink/Control/PlayerInputReader.cs`
- `Assets/_EndLink/Control/PlayerCameraInputReader.cs`

相关资产：
- `Assets/_EndLink/Control/InputSystem_Actions.inputactions`

相关物体：
- 玩家物体：挂载 `PlayerInputReader`
- 第三人称相机控制物体：挂载 `PlayerCameraInputReader`

</details>

<a id="feature-player-movement"></a>

### Feature：玩家 CharacterController 移动

<details>
<summary>展开详情</summary>

功能说明：
- 基于 `CharacterController` 移动。
- 使用 `Mathf.SmoothDamp` 做平滑加速和减速。
- 支持手动重力和贴地速度。
- 移动时角色本地 Z 轴正方向会平滑转向移动方向。
- 暴露面向指定世界方向的接口，供攻击和自动软锁目标在出手前让角色正面与动作方向一致。
- 支持移动方向参考，拖入 `Main Camera` 后可实现相机相对移动。
- 支持按住 `Left Shift` 冲刺；当前冲刺作为移动速度修饰，不单独进入状态机大状态。
- 支持状态机驱动的闪避位移，闪避期间由 `PlayerDodgeState` 决定方向、速度和持续时间。
- 支持接收敌人移动碰撞带来的外部位移，玩家可以被敌人正常前进时挤开，但不会通过该通道反向推动敌人。
- 移动调用由 `PlayerStateMachine` 驱动，`PlayerController` 通过 `TickMovement` 执行实际位移。

对应脚本：
- `Assets/_EndLink/Control/PlayerController.cs`
- `Assets/_EndLink/Control/PlayerInputReader.cs`
- `Assets/_EndLink/Control/IExternalDisplacementReceiver.cs`

相关物体：
- 玩家根物体
  - `CharacterController`
- `PlayerInputReader`
- `PlayerController`
- `PlayerStateMachine`

关键配置：
- `moveSpeed`：移动速度
- `sprintSpeed`：按住冲刺键时的移动速度
- `accelerationSmoothTime`：加速阻尼
- `decelerationSmoothTime`：减速阻尼
- `rotationSharpness`：转向响应
- `movementReference`：移动方向参考，通常拖 `Main Camera`

</details>

<a id="feature-third-person-camera"></a>

### Feature：第三人称自由相机

<details>
<summary>展开详情</summary>

功能说明：
- 基于 Cinemachine 3.1.6。
- 使用 `CinemachineCamera` 和 `CinemachineThirdPersonFollow`。
- 支持越肩、较高、较开阔的第三人称视角。
- 支持自由旋转。
- 支持 pitch 限制，避免镜头过低或过度俯视。
- 支持滚轮缩放，并限制最近和最远距离。
- 支持鼠标锁定，方便第三人称自由视角操作。

对应脚本：
- `Assets/_EndLink/Control/ThirdPersonCameraController.cs`
- `Assets/_EndLink/Control/PlayerCameraInputReader.cs`

相关包：
- `com.unity.cinemachine`：当前版本 `3.1.6`

相关物体：
- `Main Camera`
  - `Camera`
  - `CinemachineBrain`
- 玩家子物体 `CameraTarget`
  - 建议位置在胸口到头部之间，例如本地高度约 `1.65`
- `EndLink ThirdPerson Camera`
  - `CinemachineCamera`
  - `CinemachineThirdPersonFollow`
  - `PlayerCameraInputReader`
  - `ThirdPersonCameraController`

关键配置：
- `defaultDistance`：默认相机距离
- `minDistance` / `maxDistance`：缩放范围
- `zoomSpeed` / `zoomSmoothTime`：缩放速度和平滑
- `minPitch` / `maxPitch`：上下视角限制
- `mouseYawSensitivity` / `mousePitchSensitivity`：鼠标灵敏度
- `gamepadYawSpeed` / `gamepadPitchSpeed`：手柄视角速度
- `targetWorldOffset`：相机目标点高度
- `shoulderOffset`：越肩偏移
- `verticalArmLength`：镜头高度和开阔感
- `cameraSide`：左肩、右肩或居中
- `fieldOfView`：视场角，影响画面开阔程度


</details>

<a id="feature-player-state-machine"></a>

### Feature：玩家有限状态机

<details>
<summary>展开详情</summary>

功能说明：
- 使用代码状态机，不依赖 Animator StateMachine。
- 当前包含 `Idle`、`Move`、`Attack`、`Skill`、`Dodge`、`Hit`、`Dead` 七个状态。
- `Idle` 和 `Move` 会优先消费闪避输入，检查闪避冷却后切换到 `Dodge`。
- `Idle` 和 `Move` 会消费攻击输入，检查攻击冷却后切换到 `Attack`。
- `Skill` 是通用技能状态，当前由 `PartyCombatRouter` 发起请求，状态机决定是否进入，进入状态后再调用 `PlayerCombatDriver` 执行技能表现和判定。
- `Attack` 状态进入时调用 `PlayerCombatDriver.ExecuteAttack()`，攻击持续时间结束后根据移动输入回到 `Move` 或 `Idle`。
- 攻击期间移动输入会乘以 `attackMoveInputScale`，当前默认可以做站桩攻击。
- `Dodge` 状态负责主控闪避：有移动输入时按输入方向闪避，没有移动输入时默认向角色正后方后撤。
- 闪避期间普通移动、攻击和技能不会响应；闪避结束后根据移动输入回到 `Move` 或 `Idle`。
- 闪避开始时会刷新冷却，并给 `CharacterHealth` 设置短暂临时免伤窗口。
- `Hit` 可以被外部通过 `RequestHit()` 触发，用于短暂受击硬直，结束后根据移动输入回到 `Move` 或 `Idle`。
- `Dead` 可以被外部通过 `RequestDead()` 触发，是当前最高优先级终止状态。

对应脚本：
- `Assets/_EndLink/Player/StateMachine/IPlayerState.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerStateId.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerStateContext.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerStateBase.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerIdleState.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerMoveState.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerAttackState.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerSkillState.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerDodgeState.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerHitState.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerDeadState.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerStateMachine.cs`

相关物体：
- 玩家根物体
  - `PlayerStateMachine`
  - `PlayerInputReader`
  - `PlayerController`
  - `PlayerCombatDriver`

关键配置：
- `initialState`：初始状态
- `attackDuration`：攻击状态持续时间
- `attackMoveInputScale`：攻击期间移动输入倍率
- `skillDuration`：通用技能状态持续时间
- `skillMoveInputScale`：技能期间移动输入倍率
- `dodgeDuration`：闪避状态持续时间
- `dodgeDistance`：一次闪避期望移动距离
- `dodgeCooldown`：闪避冷却时间
- `dodgeInvincibleDuration`：闪避开始后的临时免伤窗口
- `hitDuration`：受击硬直持续时间
- `hitMoveInputScale`：受击期间移动输入倍率

</details>

<a id="feature-character-health"></a>

### Feature：通用生命值与角色受击接线

<details>
<summary>展开详情</summary>

功能说明：
- `CharacterHealth` 是玩家、队友和后续更多角色可以复用的通用生命组件。
- 负责最大生命值、当前生命值、治疗、受击、死亡和基础受击闪色反馈。
- 实现 `IHitReceiver`、`IDamageable` 和 `ICombatTargetLifeState`；目标身份、锁定点和表面距离由独立 `CombatTarget` 负责。
- 接收 `HitboxHitInfo` 后会扣血、触发受击事件，并通过 `CombatEventsBus` 广播 `Damaged`。
- 生命值首次降到 0 时会进入死亡状态，并通过 `CombatEventsBus` 广播 `Dead`。
- 死亡状态会被同物体的 `CombatTarget` 读取，使目标自动失效；生命组件仍可配置死亡后是否禁用非 Trigger Collider。
- 支持临时免伤窗口，可用于主控闪避、出生保护或后续特殊状态；免伤期间不会扣血或触发受击事件。
- 支持 `HealthChanged`、`Damaged`、`Healed`、`Died` 代码事件，以及对应 UnityEvent，方便 UI、状态机桥接和表现层接入。
- 支持使用 `MaterialPropertyBlock` 做受击和死亡颜色反馈，适合 URP 白模调试。
- 不直接切换玩家、队友或敌人的状态机；具体角色通过桥接脚本订阅事件。
- `PlayerHealth` 是玩家生命桥接层，订阅 `CharacterHealth` 后把受伤和死亡转发给 `PlayerStateMachine.RequestHit()` / `RequestDead()`。
- `PlayerHealth` 保留玩家侧 `OnHealthChanged`、`OnDamaged`、`OnHealed` 和 `OnDead` 事件，方便玩家 UI 或调试工具监听。
- `AllyHealth` 是队友生命桥接层，订阅 `CharacterHealth` 后把受伤和死亡转发给 `AllyStateMachine.RequestHit()` / `RequestDead()`。
- 当前不把 Debuff / Buff 逻辑直接放进生命桥接层，后续应由独立状态效果系统处理，再通过事件或接口影响生命值与状态机。

对应脚本：
- `Assets/_EndLink/Combat/CharacterHealth.cs`
- `Assets/_EndLink/Combat/Hitbox/IHitReceiver.cs`
- `Assets/_EndLink/Combat/Damage/IDamageable.cs`
- `Assets/_EndLink/Combat/Target/ICombatTargetLifeState.cs`
- `Assets/_EndLink/Player/PlayerHealth.cs`
- `Assets/_EndLink/Ally/AllyHealth.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerStateMachine.cs`
- `Assets/_EndLink/Ally/AllyStateMachine.cs`

相关物体：
- 玩家根物体
  - `CharacterHealth`
  - `PlayerHealth`
  - `PlayerStateMachine`
- 队友根物体
  - `CharacterHealth`
  - `AllyHealth`
  - `AllyStateMachine`

关键配置：
- `maxHealth`：最大生命值
- `resetHealthOnEnable`：启用时是否恢复满血
- `disableCollidersOnDeath`：死亡后是否禁用非 Trigger Collider
- `feedbackRenderer`：受击和死亡变色使用的 MeshRenderer
- `hitColor` / `deadColor`：受击和死亡颜色
- `showHealthInName`：是否在 GameObject 名字上显示血量
- `PlayerHealth.requestHitStateOnDamage`：玩家受伤时是否请求进入 Hit 状态
- `PlayerHealth.requestDeadStateOnDeath`：玩家死亡时是否请求进入 Dead 状态
- `AllyHealth.requestHitStateOnDamage`：队友受伤时是否请求进入 Hit 状态
- `AllyHealth.requestDeadStateOnDeath`：队友死亡时是否请求进入 Dead 状态

</details>

<a id="feature-combat-target"></a>

### Feature：统一 Combat Target

<details>
<summary>展开详情</summary>

功能说明：
- `CombatTarget` 是玩家、队友和敌人的唯一战斗目标身份，统一挂在角色根物体。
- `RootTransform` 固定返回组件所在根物体，用于事件目标、目标缓存、死亡移除和多 Collider 去重。
- `IsAlive` 从同物体实现 `ICombatTargetLifeState` 的生命组件读取；没有生命来源时按存活处理。
- `IsTargetable` 同时考虑手动可选开关、存活状态、组件启用状态和物体激活状态。
- `LockPoint` 用于攻击朝向、瞄准和锁定参考；留空时回退到根物体。
- `GetClosestPoint(from)` 和 `GetSurfaceDistance(from)` 统一扫描有效的非 Trigger 身体 Collider，供软锁、队友接敌、敌人感知、追击和 Hitbox 共用。
- `CombatTargetUtility` 负责把子节点、Collider 和事件对象归一到同一个 `RootTransform`。
- 玩家软锁和 AI 主动选敌要求目标具有 `CombatTarget`；没有该组件但实现 `IHitReceiver` 的可破坏物仍可被 Hitbox 命中。

对应脚本：
- `Assets/_EndLink/Combat/Target/ICombatTarget.cs`
- `Assets/_EndLink/Combat/Target/ICombatTargetLifeState.cs`
- `Assets/_EndLink/Combat/Target/CombatTarget.cs`
- `Assets/_EndLink/Combat/Target/CombatTargetUtility.cs`

相关物体：
- 玩家根物体：`CombatTarget`
- 队友根物体：`CombatTarget`
- 正式敌人根物体：`CombatTarget`
- 需要参与目标选择的特殊战斗对象：`CombatTarget`

关键配置：
- `lockPoint`：锁定、瞄准和攻击朝向参考点，空则使用根物体
- `targetable`：是否允许被主动锁定和选敌；死亡时会自动失效

</details>

<a id="feature-player-animator"></a>

### Feature：玩家 Animator 桥接

<details>
<summary>展开详情</summary>

功能说明：
- `PlayerAnimatorDriver` 是状态机到 Animator 的轻薄桥接层。
- 不读取输入，不决定状态切换，不直接控制移动或战斗。
- 每帧读取 `PlayerStateMachine.CurrentStateId` 和 `CharacterController.velocity`。
- 同步 `MoveSpeed`、`IsMoving`、`StateId`、`IsDead` 等 Animator 参数。
- 进入 `Attack`、`Skill`、`Hit`、`Dead` 状态时，可分别触发对应 Trigger 参数。
- 参数名都可以在 Inspector 修改；Animator Controller 不存在对应参数时会安全跳过。

对应脚本：
- `Assets/_EndLink/Control/PlayerAnimatorDriver.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerStateMachine.cs`

相关物体：
- 玩家根物体
  - `PlayerAnimatorDriver`
  - `PlayerStateMachine`
  - `CharacterController`
- 玩家模型或子物体
  - `Animator`

关键配置：
- `animator`：目标 Animator，可为空自动查找子物体
- `moveSpeedParameter`：水平移动速度参数名
- `isMovingParameter`：是否移动参数名
- `stateIdParameter`：当前状态 ID 参数名
- `isDeadParameter`：是否死亡参数名
- `attackTriggerParameter` / `skillTriggerParameter` / `hitTriggerParameter` / `deadTriggerParameter`：状态进入 Trigger 参数名
- `warnMissingParameters`：缺少 Animator 参数时是否打印警告

</details>

<a id="feature-player-targeting"></a>

### Feature：玩家自动软锁定

<details>
<summary>展开详情</summary>

功能说明：
- `PlayerTargeting` 是玩家自动软锁定组件，放在战斗层。
- 负责按固定刷新间隔搜索并保存当前软锁目标。
- 默认选择范围内距离玩家最近的敌人，也保留 `CameraForward` 模式用于后续偏动作游戏的视角优先设置。
- 最近目标按 `CombatTarget` 的 Collider 水平表面距离计算，不再按敌人根节点或模型中心距离计算。
- 有软锁目标时会在目标朝向玩家一侧的身体表面显示一个最小白点；未配置 prefab 时自动生成简单小球标识。
- 不控制相机、不生成 Hitbox、不决定攻击是否可以释放。
- 默认搜索 `Enemy` Layer。
- 使用 `Physics.OverlapSphereNonAlloc` 搜索范围内目标，减少运行时 GC。
- `Nearest` 模式按距离最近自动刷新目标，刷新间隔默认 0.2 秒。
- `CameraForward` 模式会同时考虑视角/朝向夹角和距离。
- 当前目标离开搜索范围、Layer 不匹配、死亡、被设为不可选或被销毁时，会自动清除。
- 对外提供 `TryAcquireTarget()`、`SetCurrentTarget(Transform target)` 和 `ClearTarget()`。
- `PlayerCombatDriver` 会读取当前软锁目标；有目标时先让玩家正面瞬间转向目标，再沿玩家正面生成 Hitbox / 远程技能；没有目标时继续按玩家自身前方生成。

对应脚本：
- `Assets/_EndLink/Player/PlayerTargeting.cs`
- `Assets/_EndLink/Combat/Target/CombatTarget.cs`

相关物体：
- 玩家根物体
  - `PlayerTargeting`
  - `CombatTarget`

关键配置：
- `autoTargetingEnabled`：是否启用自动软锁定
- `targetRefreshInterval`：自动刷新目标间隔
- `selectionMode`：目标选择模式，默认 `Nearest`
- `searchRadius`：搜索半径
- `maxTargetAngle`：`CameraForward` 模式下的最大可选角度
- `targetLayerMask`：目标 LayerMask，默认 Enemy
- `searchOrigin`：搜索原点，空则使用玩家 Transform
- `viewReference`：`CameraForward` 模式下的视角参考，空则使用玩家朝向
- `angleScoreWeight`：`CameraForward` 模式下的角度评分权重
- `distanceScoreWeight`：`CameraForward` 模式下的距离评分权重
- `showTargetIndicator`：是否显示软锁目标点
- `targetIndicatorPrefab`：自定义目标点 prefab，空则自动生成简单小白点
- `targetIndicatorOffset`：目标点相对计算位置的世界偏移
- `targetIndicatorSurfaceOffset`：目标点从目标身体表面向外推出的距离
- `targetIndicatorAlwaysOnTop`：自动生成目标点是否尽量优先于目标身体显示
- `targetIndicatorScale`：目标点缩放
- `targetIndicatorColor`：自动生成目标点的颜色
- `logTargetChanges`：是否打印目标变化日志

</details>

<a id="feature-character-stats"></a>

### Feature：角色战斗数值基础

<details>
<summary>展开详情</summary>

功能说明：
- `CharacterStats` 是玩家、队友和敌人共用的战斗数值入口，第一版提供攻击力和承受击退倍率。
- `CharacterStats` 可选择 `Auto`、`Player`、`Ally`、`Enemy` 类型；自动模式通过玩家状态机、队友状态机或 `EnemyActor` 识别角色身份，也可手动覆盖。
- 自定义 Inspector 当前只显示通用战斗属性；尚未制造空的玩家、队友或敌人专属字段，后续有真实差异时再按当前类型展示。
- `BaseAttackPower` 表示角色未经临时修正的基础攻击力；`AttackPower` 表示参与伤害计算的最终攻击力，第一版两者相同。
- `KnockbackTakenMultiplier` 表示角色承受攻击击退时的倍率：`0` 为免疫击退，`1` 为标准击退，大于 `1` 表示更容易被击退。
- 承受击退倍率只影响 Hitbox 命中的战斗击退，不影响敌人正常移动碰撞造成的角色推挤。
- 角色生命上限和当前生命仍由生命组件负责，不放入 `CharacterStats`。
- 需要使用攻击力倍率的角色，应在角色根物体挂载 `CharacterStats`；没有该组件时，动作仍会造成固定伤害，但攻击力倍率部分按 0 计算。

对应脚本：
- `Assets/_EndLink/Combat/Stats/CharacterStats.cs`
- `Assets/_EndLink/Combat/Stats/ICharacterStatsTypeProvider.cs`
- `Assets/_EndLink/Editor/CharacterStatsEditor.cs`

</details>

<a id="feature-combat-action"></a>

### Feature：战斗动作配置

<details>
<summary>展开详情</summary>

功能说明：
- `CombatActionDefinition` 是战斗动作数据资产，用于描述一次普通攻击、技能、连携攻击或大招。
- `CombatActionType` 描述动作性质，不描述释放者来源。
- 当前动作类型包括 `BasicAttack`、`Skill`、`LinkAttack`、`Ultimate`。
- 主控、队友和敌人后续可以共用同一套动作类型，释放者来源应由后续战斗事件数据携带。
- 动作配置包含固定伤害 `FlatDamage`、攻击力倍率 `AtkPowerMultiplier`、伤害类型、击退、`CombatTagDefinition` 命中标签、标签持续时间、标签层数、冷却、前摇、有效时间、后摇、Hitbox prefab、Hitbox 生成位置和 AI 有效攻击距离。
- 动作伤害基础公式为 `FlatDamage + AttackPower × AtkPowerMultiplier`，因此可配置纯固定伤害、纯倍率伤害或两者混合。

对应脚本：
- `Assets/_EndLink/Combat/CombatActionDefinition.cs`

相关资产：
- `CombatActionDefinition` 数据资产：可通过 `Create > EndLink > Combat > Combat Action Definition` 创建
- 推荐存放路径：`Assets/_EndLink/Data/CombatData/Actions`

关键类型：
- `BasicAttack`：普通攻击
- `Skill`：普通技能
- `LinkAttack`：连携攻击
- `Ultimate`：大招

</details>

<a id="feature-combat-action-executor"></a>

### Feature：统一 Action 执行接口

<details>
<summary>展开详情</summary>
功能说明：
- `ICombatActionExecutor` 统一提供 `CanExecute`、`TryExecute`、`GetCooldownRemaining` 和 `GetCooldownNormalized`。
- `PlayerCombatDriver`、`AllyCombatDriver`、`EnemyCombatDriver` 均实现该接口，具体 Hitbox 生成、朝向、日志和事件播报仍由各自 Driver 负责。
- 状态机负责角色当前是否允许进入动作状态；执行接口只检查动作配置、Hitbox 资源和动作自身冷却。
- 玩家、队友和敌人都按 `CombatActionDefinition` 独立记录冷却，普攻、技能和连携技不会互相覆盖冷却。
- 玩家和队友状态机在接受动作请求前先检查 `CanExecute`，避免进入状态后才发现动作仍在冷却或缺少资源。
- 战斗 UI 通过统一接口查询对应动作槽位的冷却，不再分别调用不同 Driver 的冷却方法。

对应脚本：
- `Assets/_EndLink/Combat/ICombatActionExecutor.cs`
- `Assets/_EndLink/Player/PlayerCombatDriver.cs`
- `Assets/_EndLink/Ally/AllyCombatDriver.cs`
- `Assets/_EndLink/Enemies/Abilities/EnemyCombatDriver.cs`
- `Assets/_EndLink/UI/UICombatActionSlot.cs`

</details>

<a id="feature-damage-pipeline"></a>

### Feature：伤害结算管线基础

<details>
<summary>展开详情</summary>

功能说明：
- 新增 `Assets/_EndLink/Combat/Damage` 目录，集中放置伤害管线基础类型。
- `CombatDamageType` 现在定义在 `DamageContext.cs` 中，当前包含 `StructuralDamage` 和 `RuntimeDamage`。
- `DamageContext` 是伤害计算输入上下文，包含来源、目标、动作配置、Hitbox 命中信息、基础伤害、伤害类型、战斗标签、命中点和命中方向。
- `DamageResult` 是伤害计算输出结果，包含最终伤害、伤害类型、战斗标签、来源、目标、命中点、命中方向，以及暴击、格挡、闪避等预留结果字段。
- `DamageCalculator` 是统一伤害计算入口。当前会读取来源角色的 `CharacterStats.AttackPower`，按 `FlatDamage + AttackPower × AtkPowerMultiplier` 计算动作伤害；后续会继续接入受击者防御、暴击、抗性、易伤和标签修正。
- 没有 `CombatActionDefinition` 的标签反应、环境伤害和直接伤害不会读取攻击力倍率，只使用伤害上下文中的固定伤害。
- `IDamageModifier` 是预留扩展接口，后续 Buff、Debuff、装备、被动和场地效果可以实现它参与伤害修正。
- `HitboxHitInfo` 现在携带 `CombatActionDefinition`，用于让伤害上下文知道命中来自哪个动作。
- `CharacterHealth`、正式敌人生命组件和木桩受击都通过 `DamageContext -> DamageCalculator -> DamageResult` 后再扣血。
- 现有 `ApplyDamage(int, CombatDamageType, CombatTagDefinition, GameObject)` 兼容入口仍保留，内部会转成 `DamageContext`。

对应脚本：
- `Assets/_EndLink/Combat/Damage/DamageContext.cs`
- `Assets/_EndLink/Combat/Damage/DamageResult.cs`
- `Assets/_EndLink/Combat/Damage/DamageCalculator.cs`
- `Assets/_EndLink/Combat/Damage/IDamageModifier.cs`
- `Assets/_EndLink/Combat/Stats/CharacterStats.cs`
- `Assets/_EndLink/Combat/Hitbox/HitboxHitInfo.cs`
- `Assets/_EndLink/Combat/CharacterHealth.cs`

</details>

<a id="feature-hit-response"></a>

### Feature：受击规则基础

<details>
<summary>展开详情</summary>
功能说明：
- Hitbox 实际造成伤害后，通过 `CombatKnockback` 统一计算并分发瞬时击退；免伤、无伤害和击退距离为 `0` 时不会产生位移。
- 第一版最终击退距离为 `基础击退距离 × CharacterStats.KnockbackTakenMultiplier`，未挂载 `CharacterStats` 的目标默认按 `1` 倍处理。
- `ICombatKnockbackReceiver` 只负责攻击命中的战斗击退，与敌人移动碰撞使用的外部推挤接口保持分离。
- `PlayerController`、`AllyFollowMotor` 和 `EnemyMotorBase` 已接入统一击退协议，第一版只产生 XZ 平面的瞬时位移，不处理击飞和持续受力。
- 当前不新增硬直等级、可打断规则或额外受击组件；现有 Hit 状态行为保持不变。
- 死亡后的 Collider 开关逻辑保持现状，本次没有修改。

对应脚本：
- `Assets/_EndLink/Combat/Hitbox/CombatKnockback.cs`
- `Assets/_EndLink/Combat/Stats/CharacterStats.cs`
- `Assets/_EndLink/Combat/CharacterHealth.cs`
- `Assets/_EndLink/Enemies/EnemyHealth.cs`
- `Assets/_EndLink/Control/PlayerController.cs`
- `Assets/_EndLink/Ally/AllyFollowMotor.cs`
- `Assets/_EndLink/Enemies/Abilities/EnemyMotorBase.cs`

</details>

<a id="feature-combat-tags"></a>

### Feature：战斗标签系统

<details>
<summary>展开详情</summary>

功能说明：
- 战斗标签系统只服务 Combat，不做全项目泛用 GameplayTag。
- `CombatTagDefinition` 是标签定义资产，包含 `tagId`、显示名、说明、标签等级、默认持续时间和最大层数。
- 标签合法检查当前要求标签资产非空且 `tagId` 非空。
- `CombatTagContainer` 挂在目标身上，负责保存多标签、层数、持续时间、添加、移除、过期和清空。
- `CombatTagContainer` 支持永久标签和限时标签；添加标签时如果没有显式传入持续时间，会使用标签定义里的默认持续时间，默认持续时间小于等于 0 时才视为永久标签。
- `CombatTagCombinationRule` 描述 A + B 触发反应效果的规则，可配置源标签所需层数，以及一组 `CombatTagReactionEffect`。
- `CombatTagReactionEffect` 支持 `ApplyTag`、`RemoveTag`、`DealDamage`，并预留 `SpreadTag`、`ApplyControl`、`InterruptAction`、`ModifyResource` 和 `CustomEvent`。
- 消耗源标签也通过 `RemoveTag` 反应效果配置，不再保留旧的单独输出标签或自动移除源标签字段。
- 同一个标签重复添加时会刷新持续时间并增加层数，最终层数会被 `CombatTagDefinition.MaxStackCount` 钳制。
- 当组合规则的两个输入标签相同时，可以表达“同一标签达到指定层数后转化为另一个标签”，例如后续的 3 层火标签转化为燃烧。
- 容器提供 `OnTagAdded`、`OnTagRemoved`、`OnTagExpired`、`OnTagRefreshed` 和 `OnReactionTriggered` 事件。
- 标签添加、移除、过期和协议反应触发时会同步通过 `CombatEventsBus` 广播事件。
- 标签添加和移除接口支持传入 `source`，事件总线可以表达“谁给谁挂载或移除了某个标签”。
- 对外提供 `ICombatTagReadable` 和 `ICombatTagReceiver`，后续连携规则、AI、UI 和状态效果系统应优先依赖接口。
- `CombatActionDefinition`、`HitboxBase` 和 `HitboxHitInfo` 使用 `CombatDamageType` 区分 `RuntimeDamage` 和 `StructuralDamage`，并使用 `CombatTagDefinition` 作为标签数据。
- 命中信息和受击事件会携带伤害类型；当前只记录类型，不做差异化公式结算。
- `CombatActionDefinition`、`HitboxBase` 和 `HitboxHitInfo` 携带命中时要添加的标签持续时间和层数；标签持续时间小于等于 0 时使用标签定义的默认持续时间。
- 命中时如果目标实现 `ICombatTagReceiver`，`HitboxBase` 会把 `CombatTagDefinition` 添加到目标标签容器，并把 Hitbox owner 作为标签来源。

对应脚本：
- `Assets/_EndLink/Combat/Tags/CombatTagDefinition.cs`
- `Assets/_EndLink/Combat/Tags/CombatTagCombinationRule.cs`
- `Assets/_EndLink/Combat/Tags/CombatTagReactionEffect.cs`
- `Assets/_EndLink/Combat/Tags/CombatTagContainer.cs`
- `Assets/_EndLink/Combat/Tags/CombatTagInterfaces.cs`
- `Assets/_EndLink/Combat/Hitbox/HitboxHitInfo.cs`

相关资产：
- `CombatTagDefinition` 数据资产：可通过 `Create > EndLink > Combat > Combat Tag Definition` 创建
- `CombatTagCombinationRule` 数据资产：可通过 `Create > EndLink > Combat > Combat Tag Combination Rule` 创建
- 推荐存放路径：
  - `Assets/_EndLink/Data/CombatData/Tags/Definitions`
  - `Assets/_EndLink/Data/CombatData/Tags/CombinationRules`

相关物体：
- 需要被连携规则查询的目标
  - `CombatTagContainer`

</details>

<a id="feature-combat-data-tool"></a>

### Feature：战斗数据编辑工具

<details>
<summary>展开详情</summary>

功能说明：
- 通过菜单 `EndLink > Combat Data Tool` 打开。
- 提供 `Actions`、`Tag Definitions`、`Combination Rules` 和 `Asset List` 四个标签页。
- 前三个标签页分别用于快捷创建 `CombatActionDefinition`、`CombatTagDefinition` 和 `CombatTagCombinationRule`。
- `Asset List` 标签页会列出三个数据目录下已有的数据资产。
- 创建资产后会自动选中并 Ping 到 Project 窗口，具体字段继续在 Inspector 中编辑。
- 工具会确保目标目录存在，当前固定使用项目约定的数据路径。

对应脚本：
- `Assets/_EndLink/Editor/CombatDataToolWindow.cs`

相关数据目录：
- `Assets/_EndLink/Data/CombatData/Actions`
- `Assets/_EndLink/Data/CombatData/Tags/Definitions`
- `Assets/_EndLink/Data/CombatData/Tags/CombinationRules`

</details>

<a id="feature-combat-ui-foundation"></a>

### Feature：战斗 UI 基础

<details>
<summary>展开详情</summary>

功能说明：
- `HUDCombatController` 是战斗 HUD 总入口，负责绑定 `PartyManager`、控制 HUD 显隐，并驱动下属 UI 模块刷新。
- `UIPartyCombatAction` 是小队动作栏管理器，负责绑定主控、队友 A、队友 B 的主动技能槽、连携请求槽和全队极限技槽。
- `UICombatActionSlot` 是单个动作槽位组件，槽位绑定的是小队命令槽，例如 `PlayerSkill`、`AllySlotASkill`、`AllySlotBSkill`，而不是固定动作资产。
- `UICombatActionSlot` 通过 `PartyManager` 解析当前角色，通过角色 CombatDriver 读取当前槽位动作和该动作自己的冷却。
- `UICombatActionSlot` 通过 `PartyManager.CombatRouter` 读取该槽位当前键位显示文本。
- `UICombatActionSlot` 不读取输入、不释放动作、不判断战斗规则。
- 冷却中直接把图标染成配置颜色，冷却结束后恢复图标原色。
- `UIHealthBar` 是通用血条组件，只依赖 `CharacterHealth`，可用于主角、队友和敌人头顶血条。
- `UIHealthBar` 支持 `Image.fillAmount`、可选血量文本、满血隐藏、死亡隐藏、无生命来源隐藏和运行时绑定生命来源。

对应脚本：
- `Assets/_EndLink/UI/HUDCombatController.cs`
- `Assets/_EndLink/UI/UIPartyCombatAction.cs`
- `Assets/_EndLink/UI/UICombatActionSlot.cs`
- `Assets/_EndLink/UI/UIHealthBar.cs`

相关物体：
- 战斗 UI Canvas / HUD 根物体
  - `HUDCombatController`
- 技能栏或动作栏父物体
  - `UIPartyCombatAction`
- 技能、连携、大招等圆形动作槽位
  - `UICombatActionSlot`
  - `Image` 图标
  - 可选 `TextMeshProUGUI` 键位文本
- 血条物体
  - `UIHealthBar`
  - `Image` 填充图
  - 可选 `TextMeshProUGUI` 血量文本

关键配置：
- `HUDCombatController.partyManager`：小队管理器，空时自动查找
- `HUDCombatController.partyCombatAction`：小队动作栏 UI 管理器
- `UIPartyCombatAction.autoCollectChildSlots`：是否自动从子物体收集动作槽
- `UIPartyCombatAction.driveChildSlotsManually`：是否由动作栏统一驱动子槽刷新
- `UICombatActionSlot.slot`：该 UI 对应的键位槽，例如 PlayerSkill、AllySlotASkill、AllySlotBSkill
- `UICombatActionSlot.iconImage`：技能图标 Image，可拖子物体上的 Icon
- `UICombatActionSlot.keyLabelText`：键位显示文本
- `UICombatActionSlot.cooldownTintColor`：冷却染色颜色，默认半透明灰色
- `UIHealthBar.health`：要显示的 `CharacterHealth`
- `UIHealthBar.fillImage`：血条填充 Image，建议 Image Type 使用 Filled
- `UIHealthBar.valueText`：可选血量文本
- `UIHealthBar.hideWhenFull` / `hideWhenDead`：满血和死亡时是否隐藏

</details>

<a id="feature-combat-events-bus"></a>

### Feature：战斗事件总栈

<details>
<summary>展开详情</summary>

功能说明：
- `CombatEventsBus` 是全局战斗事件广播入口。
- `CombatEvent` 是一条战斗事件的数据结构，包含事件类型、来源、目标、动作配置、战斗标签、伤害、命中信息和时间戳。
- `CombatEventType` 目前包含 `ActionStarted`、`HitLanded`、`Damaged`、`Dead`、`TagAdded`、`TagRemoved`、`TagExpired`、`ReactionTriggered`。
- `CombatEventLog` 是白模阶段用的 Console 日志监听器，默认不打印，必要时手动开启。
- `CombatMonitorWindow` 是 Editor 战斗事件监视窗口，通过 `EndLink > Debug > Combat Monitor` 打开，订阅事件后以表格查看最近的战斗事件。
- 事件总栈只广播事实，不保存状态，不决定连携规则，不直接驱动队友 AI。
- 接入范围包括 `PlayerCombatDriver` / `AllyCombatDriver` / `EnemyCombatDriver` 的动作开始、`HitboxBase` 的命中、`CharacterHealth` / `EnemyHealth` / `EnemyDummy` 的受伤与死亡，以及 `CombatTagContainer` 的标签添加、移除、过期和协议反应。

对应脚本：
- `Assets/_EndLink/Combat/Events/CombatEventType.cs`
- `Assets/_EndLink/Combat/Events/CombatEvent.cs`
- `Assets/_EndLink/Combat/Events/CombatEventsBus.cs`
- `Assets/_EndLink/Combat/Events/CombatEventLog.cs`
- `Assets/_EndLink/Editor/CombatMonitorWindow.cs`

相关物体：
- 任意调试物体
  - `CombatEventLog`

相关 Editor 工具：
- `EndLink > Debug > Combat Monitor`

关键接口：
- `CombatEventsBus.Raised`：全局事件订阅入口
- `CombatEventsBus.Raise(...)`：广播通用事件
- `CombatEventsBus.RaiseActionStarted(...)`
- `CombatEventsBus.RaiseHitLanded(...)`
- `CombatEventsBus.RaiseDamaged(...)`
- `CombatEventsBus.RaiseDead(...)`
- `CombatEventsBus.RaiseTagAdded(...)`
- `CombatEventsBus.RaiseTagRemoved(...)`
- `CombatEventsBus.RaiseTagExpired(...)`
- `CombatEventsBus.RaiseReactionTriggered(...)`

</details>

<a id="feature-ally-assist"></a>

### Feature：队友助战基础组件

<details>
<summary>展开详情</summary>

功能说明：
- `AllyBrain` 是队友大脑，负责监听 `CombatEventsBus` 并判断是否响应。
- `AllyBrain` 默认响应 `HitLanded` 事件，忽略自己发出的事件，可选只响应指定来源，例如主控玩家。
- 主控命中敌人后，`AllyBrain` 会优先锁定该事件目标并请求队友进入助战流程。
- 事件目标为空时，`AllyBrain` 可按 `enemyLayerMask` 和 `targetSearchRadius` 搜索主控附近最近敌人。
- 目标死亡时，`AllyBrain` 会监听 `Dead` 事件并请求状态机取消当前助战。
- `AllyBrain` 不直接生成 Hitbox，不写伤害数据，只把事件目标交给 `AllyStateMachine.RequestAssist(...)`。
- `AllyCombatDriver` 是队友战斗执行器，职责类似 `PlayerCombatDriver`，但不读取输入，也不决定什么时候出手。
- `AllyCombatDriver` 保存队友的自动助战、主动技能、连携技动作槽位，并根据 `CombatActionDefinition` 生成 Hitbox、写入伤害、击退、战斗标签和标签持续时间。
- 队友进入助战流程只要求配置了 `Assist Action`；动作冷却只影响实际出手时间，冷却未结束时会在 Assist 内等待，而不是放弃助战。
- `AllyCombatDriver` 按 `CombatActionDefinition` 分别记录冷却，自动助战动作不会占用主动技能冷却，主动技能也不会重置助战动作冷却。
- `AllyCombatDriver` 暴露只读动作冷却剩余时间、归一化冷却值，以及指定动作的冷却查询，供战斗 UI 或调试窗口读取。
- `CombatActionDefinition.Effective Attack Range` 决定队友距离目标 Collider 表面多远开始攻击。
- `AllyCombatDriver` 执行助战时会朝目标方向生成判定，并广播 `ActionStarted` 事件。
- `AllyTargetSelector` 负责队友目标选择，优先读取 `PartyCombatContext` 的当前主目标和已知敌人列表，当前目标死亡或失效后可以继续切换到下一个可攻击目标。
- `AllyTargetSelector` 通过统一 `CombatTarget` 查询目标有效性和 Collider 表面距离，避免大型敌人按中心点判断导致队友贴边却无法攻击。

对应脚本：
- `Assets/_EndLink/Ally/AllyBrain.cs`
- `Assets/_EndLink/Ally/AllyCombatDriver.cs`
- `Assets/_EndLink/Ally/AllyStateMachine.cs`
- `Assets/_EndLink/Ally/AllyTargetSelector.cs`
- `Assets/_EndLink/Party/PartyCombatContext.cs`
- `Assets/_EndLink/Combat/CombatActionDefinition.cs`
- `Assets/_EndLink/Combat/Target/CombatTargetUtility.cs`
- `Assets/_EndLink/Combat/Events/CombatEventsBus.cs`

相关物体：
- 队友根物体
  - `AllyBrain`
  - `AllyStateMachine`
  - `AllyCombatDriver`
  - `AllyTargetSelector`
- 小队管理物体
  - `PartyCombatContext`

关键配置：
- `assistAction`：队友助战动作配置资产
- `faceTargetBeforeAttack`：助战前是否转向目标
- `respondToHitLanded`：是否响应命中事件
- `requiredSource`：可选事件来源过滤，通常可拖主角
- `ignoreSelfEvents`：是否忽略自己发出的事件
- `searchNearestEnemyWhenNoEventTarget`：事件目标为空时是否搜索最近敌人
- `targetSearchRadius`：最近敌人搜索半径
- `enemyLayerMask`：敌人搜索 LayerMask，通常勾选 Enemy
- `logDecisions`：是否打印队友响应决策日志

</details>

<a id="feature-ally-monitor"></a>

### Feature：队友调试监视窗口

<details>
<summary>展开详情</summary>

功能说明：
- `AllyDebugLog` 是队友专用调试事件流，运行时代码只负责上报状态切换、事件响应、助战阶段、冷却等待和攻击执行等关键行为。
- `AllyMonitorWindow` 是 Editor 队友监视窗口，通过 `EndLink > Debug > Ally Monitor` 打开。
- 窗口上半部分显示当前场景所有 `AllyStateMachine` 的状态快照，包括状态、跟随目标、助战目标、到目标 Collider 表面的距离、攻击距离、重接近距离、冷却和当前 Action。
- 窗口下半部分显示队友行为日志，可以按队友对象和 `State / Brain / Assist / Combat / Follow` 分类过滤。
- `Capture` 控制是否采集队友调试事件，`Console` 控制是否同时镜像到 Unity Console，默认建议只看窗口避免刷屏。

对应脚本：
- `Assets/_EndLink/Ally/AllyDebugLog.cs`
- `Assets/_EndLink/Editor/AllyMonitorWindow.cs`
- `Assets/_EndLink/Ally/AllyStateMachine.cs`
- `Assets/_EndLink/Ally/AllyBrain.cs`
- `Assets/_EndLink/Ally/AllyAssistState.cs`
- `Assets/_EndLink/Ally/AllyCombatDriver.cs`

相关 Editor 工具：
- `EndLink > Debug > Ally Monitor`

</details>

<a id="feature-ally-state-machine"></a>

### Feature：队友有限状态机

<details>
<summary>展开详情</summary>

功能说明：
- `AllyStateMachine` 是队友专用有限状态机，不依赖玩家输入系统。
- 当前包含 `Idle`、`Follow`、`Assist`、`Action`、`Hit`、`Dead` 六个外层状态。
- `Idle` 表示没有跟随目标的待机状态。
- `Follow` 在有跟随目标时每帧调用 `AllyFollowMotor.TickFollow(deltaTime)`，实际移动由跟随移动组件负责。
- `Assist` 是队友助战大状态，内部先接近目标，进入攻击距离后持续攻击；目标拉开距离后在 Assist 内部回到接近阶段。
- `Assist` 当前内部使用轻量 `Approach / Attack` 阶段，后续可以替换为行为树。
- `Action` 是队友通用动作状态，当前用于 E/F 主动技能；进入时执行一次 `CombatActionDefinition`，动作窗口结束后回到 Assist 或 Follow / Idle。
- 目标死亡、目标丢失或主控距离过远时，助战流程会取消并回到 Follow / Idle。
- `Hit` 表示队友受击硬直状态，可打断 Follow、Assist 和 Action。
- `Dead` 是终止状态，不再响应跟随、助战和受击请求。
- `AllyBrain` 判断事件是否值得响应，`AllyStateMachine` 判断当前能否进入 Assist，`AllyCombatDriver` 只执行动作和 Hitbox。
对应脚本：
- `Assets/_EndLink/Ally/AllyStateMachine.cs`
- `Assets/_EndLink/Ally/AllyStateId.cs`
- `Assets/_EndLink/Ally/IAllyState.cs`
- `Assets/_EndLink/Ally/AllyStateBase.cs`
- `Assets/_EndLink/Ally/AllyStateContext.cs`
- `Assets/_EndLink/Ally/AllyIdleState.cs`
- `Assets/_EndLink/Ally/AllyFollowState.cs`
- `Assets/_EndLink/Ally/AllyFollowMotor.cs`
- `Assets/_EndLink/Ally/AllyAssistState.cs`
- `Assets/_EndLink/Ally/AllyActionState.cs`
- `Assets/_EndLink/Ally/AllyHitState.cs`
- `Assets/_EndLink/Ally/AllyDeadState.cs`
相关物体：
- 队友根物体
  - `AllyStateMachine`
  - `AllyCombatDriver`

关键配置：
- `initialState`：初始状态
- `followTarget`：跟随目标，通常后续会绑定主控角色
- `assistAttackRangeTolerance`：助战进入攻击阶段的距离容差，实际进入距离为 `Effective Attack Range + Tolerance`
- `assistApproachInnerOffset`：助战接近目标时的内缩距离，让队友尝试站得比动作极限距离更近
- `assistReengageRange`：持续助战时，目标离队友超过该距离会重新接近
- `assistBreakOffDistance`：队友距离主控过远时取消助战，设置为 0 可关闭
- `assistDuration`：助战状态最短持续时间
- `hitDuration`：受击状态持续时间

</details>

<a id="feature-party-link-context"></a>

### Feature：连携触发与窗口

<details>
<summary>展开详情</summary>

功能说明：
- 标签组合规则成功执行后会广播 `ReactionTriggered`，事件携带触发来源、反应目标、主要结果标签和对应的 `CombatTagCombinationRule`。
- `PartyLinkContext` 监听协议反应事件，并为主控和两个队友同时开启一个全队共享的 4 秒连携窗口。
- 窗口期内再次触发协议反应会把剩余时间刷新为完整 4 秒，并继续记录新的反应目标。
- 玩家可以按 `1`、`2`、`3` 从主控、队友 A、队友 B 的连携技中选择一个释放；任意一个请求成功后会消费整个窗口，另外两个槽位同时锁定。
- 只有一个有效反应目标时默认攻击该目标；记录了多个反应目标时优先攻击主控当前软锁目标。
- 反应目标死亡或失效不会关闭窗口；没有有效反应目标时会回退到当前软锁目标，没有任何有效目标时保留窗口但拒绝本次释放。
- 主控连携技通过玩家通用技能状态执行；队友连携技通过 `AllyActionState` 执行，不绕过角色状态机。
- `PartyLinkContext` 暴露窗口是否开启、剩余时间、归一化剩余时间和目标解析接口，供后续连携 UI 使用。

对应脚本：
- `Assets/_EndLink/Party/PartyLinkContext.cs`
- `Assets/_EndLink/Party/PartyCombatRouter.cs`
- `Assets/_EndLink/Combat/Events/CombatEventType.cs`
- `Assets/_EndLink/Combat/Events/CombatEventsBus.cs`
- `Assets/_EndLink/Combat/Tags/CombatTagContainer.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerStateMachine.cs`
- `Assets/_EndLink/Ally/AllyStateMachine.cs`

相关物体：
- 小队管理物体
  - `PartyManager`
  - `PartyCombatRouter`
  - `PartyLinkContext`

关键配置：
- `PartyLinkContext.linkWindowDuration`：协议反应触发后的共享连携窗口，默认 4 秒
- `PlayerCombatDriver.LinkAction`：主控连携技动作
- `AllyCombatDriver.LinkAction`：对应队友连携技动作
- `PartyCombatRouter` 的 `1` / `2` / `3` 键位：分别选择主控、队友 A、队友 B 的连携技

</details>

<a id="feature-party-combat-context"></a>

### Feature：小队战斗状态上下文

<details>
<summary>展开详情</summary>

功能说明：
- `PartyCombatContext` 是小队战斗状态的轻量上下文，通常挂在小队管理物体上。
- 它监听 `CombatEventsBus`，记录最近的小队战斗事件、当前主目标、已知敌人列表和小队是否处于战斗状态。
- 所有事件目标会先通过 `CombatTarget` 归一为唯一 `RootTransform`，避免同一敌人的不同 Collider 或组件被记录成多个目标。
- 它不读取输入、不执行攻击、不切换状态，只提供上下文查询。
- 队友目标选择器 `AllyTargetSelector` 会从这里读取当前主目标和已知敌人，用于持续助战和目标失效后的目标切换。
- 后续战斗 UI、连携触发规则和队友 AI 都可以优先从这里读取“当前小队正在和谁战斗”。

对应脚本：
- `Assets/_EndLink/Party/PartyCombatContext.cs`
- `Assets/_EndLink/Ally/AllyTargetSelector.cs`
- `Assets/_EndLink/Combat/Events/CombatEventsBus.cs`

相关物体：
- 小队管理物体
  - `PartyCombatContext`
- 队友根物体
  - `AllyTargetSelector`

关键配置：
- 当前主目标和已知敌人由战斗事件自动维护。
- 目标死亡或不再有效时，后续查询会跳过不可作为战斗目标的对象。

</details>

<a id="feature-ally-follow-motor"></a>

### Feature：队友跟随移动

<details>
<summary>展开详情</summary>

功能说明：
- `AllyFollowMotor` 是队友跟随移动执行组件，不依赖 NavMesh。
- 支持使用 `CharacterController.Move` 移动；如果队友没有 `CharacterController`，则直接修改 `Transform.position`。
- `AllyStateMachine` 负责保存跟随目标并同步给 `AllyFollowMotor`。
- `PartyManager` 通过 `PartyFollowSettings` 统一配置两个队友的跟随参数，并在初始化时写入各自的 `AllyFollowMotor`。
- `AllyFollowState` 每帧调用 `TickFollow(deltaTime)`，因此 Assist、Hit、Dead 状态不会继续抢跟随移动。
- 助战接近状态会调用 `TickMoveToPosition(position, arriveDistance, deltaTime)`，让队友临时移动到敌人附近而不修改主控跟随目标。
- 队友会移动到主控的本地队形偏移范围，移动时面向移动方向，停下后的朝向由 `idleFacingMode` 决定。
- 支持 `arrivalSmoothTime` 平滑加减速，降低接近队形点时的机械感。
- 支持主控冲刺同步：主控在 Move 状态按住冲刺时，队友 Follow 状态下的跟随速度会乘以 `sprintSyncSpeedMultiplier`。
- 支持 `catchUpDistance` 和 `catchUpSpeedMultiplier`，队友落后较远时会加速追上。
- 支持 `teleportDistance`，队友极端远离队形点时会直接归位，避免长距离丢失。
- 支持 `followSlotSoftness`，队友进入队形点周围软半径后就算到位，不强制踩死精确坐标。
- 支持 `followDeadZoneRadius`，每个队友站定后会以自己的站位作为死区中心；主控仍在该半径内移动时不会触发该队友重新跟随，也不会跟随主控转向；走出半径后才更新队形点和朝向。
- 归位过程中会同步更新死区圆心，避免动态槽位或重新归位后残留旧死区中心。
- `AllyFollowMotor` 会常驻绘制跟随死区 Gizmo，运行时以该队友当前死区中心为圆心，非运行时以队友自身为圆心；当前使用深蓝色常态显示，不再依赖选中状态。
- 支持第一版简易避让：离主控太近时会被推开，配置 `avoidanceLayerMask` 后也能对其他队友做局部排斥。
- 支持接收敌人移动碰撞带来的外部位移：队友可以被敌人正常前进时挤开，但不会反向顶动敌人。
- `formationOffset` 由 `PartyManager` 的队友槽位统一配置，并写入 `AllyFollowMotor`。
- `SetFormationOffset` 在偏移未变化时不会重复触发重新归位，降低动态槽位评估带来的抖动。
- 当前只处理平面 XZ 跟随和局部避让，后续如果需要复杂地形、障碍绕路，再接 NavMesh 或更完整的队伍槽位调度。

对应脚本：
- `Assets/_EndLink/Ally/AllyFollowMotor.cs`
- `Assets/_EndLink/Ally/AllyFollowState.cs`
- `Assets/_EndLink/Ally/AllyStateMachine.cs`
- `Assets/_EndLink/Control/IExternalDisplacementReceiver.cs`
- `Assets/_EndLink/Party/PartyFollowSettings.cs`
- `Assets/_EndLink/Party/PartyManager.cs`

相关物体：
- 队友根物体
  - `AllyStateMachine`
  - `AllyFollowMotor`
  - 可选 `CharacterController`
- 小队管理物体
  - `PartyManager`

关键配置：
- `PartyManager.followSettings.stopDistance`：距离队形点小于该值时停止移动
- `PartyManager.followSettings.followSlotSoftness`：队形点软半径，范围内算到位
- `PartyManager.followSettings.followDeadZoneRadius`：跟随死区半径，默认 5，主控在该队友站位附近移动时队友保持原地和原朝向
- `PartyManager.followSettings.moveSpeed`：队友跟随移动速度
- `PartyManager.followSettings.sprintSyncSpeedMultiplier`：主控冲刺时队友 Follow 移动速度倍率
- `PartyManager.followSettings.arrivalSmoothTime`：接近队形点时的速度阻尼时间
- `PartyManager.followSettings.catchUpDistance` / `catchUpSpeedMultiplier`：追赶距离和追赶速度倍率
- `PartyManager.followSettings.teleportDistance`：极端远离时的归位距离，设置为 0 可关闭
- `PartyManager.followSettings.rotationSpeed`：队友转向速度
- `PartyManager.followSettings.idleFacingMode`：停下后的朝向模式
- `formationOffset`：由 `PartyFormationSlot` 写入 `AllyFollowMotor`
- `PartyManager.followSettings.avoidanceEnabled`：是否启用简易避让
- `PartyManager.followSettings.followTargetAvoidRadius`：离主控小于该半径时推离主控
- `PartyManager.followSettings.allyAvoidRadius`：离其他队友小于该半径时推开
- `PartyManager.followSettings.avoidanceStrength`：避让方向混合强度
- `PartyManager.followSettings.avoidanceLayerMask`：参与队友间避让检测的 Layer，建议给队友角色单独设置 Layer 后在这里勾选

</details>

<a id="feature-party-manager"></a>

### Feature：固定三人小队管理

<details>
<summary>展开详情</summary>

功能说明：
- `PartyManager` 是固定三人小队的场景级管理入口。
- 第一版只支持固定主控 + 2 个固定队友，不做主控切换、入队离队或复杂编队。
- `PartyFormationSlot` 保存单个队友槽位，包含槽位名、队友状态机和队形偏移。
- 初始化时，`PartyManager` 会把 `mainCharacter` 设置为两个队友的跟随目标。
- 初始化时，`PartyManager` 会把两个槽位的 `formationOffset` 写入各自队友的 `AllyFollowMotor`。
- 初始化时，`PartyManager` 会把统一的 `PartyFollowSettings` 写入两个队友的 `AllyFollowMotor`。
- 支持两个队友的动态站位槽位交换：运行中会比较当前分配和交换后的移动代价，在收益足够且主控不处于队友死区内时交换左右后方槽位。
- `PartyManager` 提供轻量队伍查询：主控引用、已配置队友数量、存活/有效队友数量、存活队友列表和队友是否存活。
- `PartyManager` 提供最小小队表现调度入口：`NotifyCombatStarted`、`NotifyCombatEnded`、`NotifyMemberDead`，供后续 UI、镜头、语音和站位表现监听。
- `PartyManager` 持有 `PartyCombatRouter` 引用，供战斗 UI 和后续小队系统读取当前键位路由。
- `PartyCombatContext` 作为小队战斗状态上下文，供队友目标选择、战斗 UI 和后续连携系统读取当前主目标、已知敌人和战斗状态。
- `PartyCombatRouter` 负责把 `PlayerInputReader` 中的战斗输入翻译成主控、队友 A、队友 B 或全队的命令请求。
- `PartyCombatRouter` 不直接生成 Hitbox，不处理伤害或标签；它只校验连携窗口并把技能/连携请求转发给对应角色状态机。
- 当前第一版中，主控 `Skill` 命令会由 `PartyCombatRouter` 转发给 `PlayerStateMachine.RequestSkill()`，由玩家状态机决定能否进入 `Skill` 状态并执行动作。
- 队友 `Skill` 命令会由 `PartyCombatRouter` 转发给对应 `AllyStateMachine.RequestAction(...)`，进入 `Action` 状态后再由 `AllyCombatDriver` 执行 `SkillAction`。
- `PartyCombatRouter` Inspector 中可以覆盖 Q/E/F 和 1/2/3 对应的技能与连携请求键位，V 键全队极限技暂时固定。
- `LinkAttack` 命令只有在 `PartyLinkContext` 窗口开启时才会被接受；请求成功后由对应角色状态机执行 `LinkAction` 并消费共享窗口。
- 后续队友 AI、连携规则或调试工具需要知道“谁是主控，谁是队友”时，可以从 `PartyManager` 查询。

对应脚本：
- `Assets/_EndLink/Party/PartyManager.cs`
- `Assets/_EndLink/Party/PartyFormationSlot.cs`
- `Assets/_EndLink/Party/PartyFollowSettings.cs`
- `Assets/_EndLink/Party/PartyCombatRouter.cs`
- `Assets/_EndLink/Party/PartyCombatContext.cs`
- `Assets/_EndLink/Party/PartyLinkContext.cs`
- `Assets/_EndLink/Ally/AllyFollowMotor.cs`
- `Assets/_EndLink/Ally/AllyStateMachine.cs`

相关物体：
- 场景管理物体
  - `PartyManager`
  - `PartyCombatRouter`
  - `PartyLinkContext`
- 主控角色根物体
  - 拖入 `mainCharacter`
- 两个队友根物体
  - 分别拖入 `allySlotA` / `allySlotB`

关键配置：
- `mainCharacter`：固定主控角色
- `allySlotA`：第一个队友槽位
- `allySlotB`：第二个队友槽位
- `followSettings`：两个队友共用的跟随移动参数
- `formationOffset`：每个队友相对主控的本地队形偏移，例如左后 `(-2, 0, -2.5)`、右后 `(2, 0, -2.5)`
- `useDynamicFormationSlots`：是否启用两个队友的动态站位槽位交换
- `formationEvaluateInterval`：动态站位重新评估间隔
- `formationSwitchMinImprovement`：交换后至少减少多少移动代价才允许换位
- `formationSwitchCooldown`：站位交换冷却，避免频繁来回抢位
- `playerSkillKey` / `allySlotASkillKey` / `allySlotBSkillKey`：主控和两个队友主动技能键位，默认 Q / E / F
- `playerLinkAttackKey` / `allySlotALinkAttackKey` / `allySlotBLinkAttackKey`：主控和两个队友连携请求键位，默认 1 / 2 / 3
- `logInitialization`：是否打印小队初始化日志
- `logCommands`：是否打印小队战斗命令路由日志

</details>

<a id="feature-enemy-foundation"></a>

### Feature：敌人通用基底

<details>
<summary>展开详情</summary>

功能说明：
- `EnemyActor` 是正式敌人的根入口组件，只暴露敌人身份和能力组件引用，并要求同物体存在 `CombatTarget`。
- `EnemyActor` 要求同物体挂载 `CombatTagContainer`，保证正式敌人天然支持战斗标签、持续标签和协议反应。
- `EnemyHealth` 负责正式敌人的血量、受击、死亡、死亡事件和白模调试反馈。
- `EnemyHealth` 实现 `IHitReceiver`、`IDamageable` 和 `ICombatTargetLifeState`，不再重复实现目标身份。
- `CombatTarget` 统一提供敌人的根身份、存活/可选状态、锁定点和 Collider 表面距离；敌人死亡后会自动失效。
- `EnemyStateMachine` 管理 `Idle`、`Alert`、`Combat`、`Hit`、`Dead` 五个敌人大状态。
- `EnemyStateMachine` 集中暴露索敌配置，`EnemyTargetSensor` 只作为执行器读取状态机参数，不在自身 Inspector 中重复配置。
- `EnemyTargetSensor` 负责第一版敌人索敌：玩家进入发现范围后请求进入 `Alert`，持续停留达到警觉时间后请求进入 `Combat`。
- 自动索敌可以在 `EnemyStateMachine` 中关闭，关闭后不会主动触发 `Alert` / `Combat`。
- `EnemyMotorBase` 是第一版地面敌人移动能力组件，基于 `CharacterController` 提供移动、转向、重力和停止能力。
- `EnemyMotorBase` 支持按“根物体在脚底”的白模约定自动校正 `CharacterController.center.y`，避免第一次移动时因胶囊底部埋入地面而被弹起。
- `EnemyMotorBase` 在水平追击移动后会抑制碰撞带来的异常上抬，重力在 `LateUpdate` 中补充处理。
- `EnemyMotorBase` 在正常移动撞到实现 `IExternalDisplacementReceiver` 的玩家或队友时，会把挡路角色沿敌人移动方向挤开；敌人自身不接收这条外部位移，因此队友和玩家不会反向顶动敌人。
- `EnemyActor` 持有 `EnemyMotorBase` 和 `EnemyCombatDriver` 引用，状态机通过 Actor 读取敌人能力，而不是直接查找具体实现。
- `EnemyCombatDriver` 是敌人战斗执行器，按 `CombatActionDefinition` 生成 Hitbox、记录冷却并广播动作开始事件；当前先作为攻击能力基底，具体何时出手后续交给 Combat 状态内部逻辑或行为树。
- `Combat` 当前只做基础追击和面向目标；追击位置取自目标 Collider 最近表面点，停止距离只保留自身半径和配置间隔，避免持续挤入目标中心。
- `Combat` 后续作为行为树的外层挂载点，内部再承载站位、攻击、技能等细节行为。
- `Hit` 作为独立大状态处理受击打断，不放进 Combat 行为树，方便后续加入硬直、霸体、击倒等规则。
- `EnemyDummy` 保留为早期轻量命中测试对象，用于快速验证 Hitbox、扣血和死亡显示；正式敌人能力以本节敌人基底为准。

对应脚本：
- `Assets/_EndLink/Enemies/EnemyActor.cs`
- `Assets/_EndLink/Enemies/EnemyHealth.cs`
- `Assets/_EndLink/Enemies/EnemyTargetSensor.cs`
- `Assets/_EndLink/Enemies/Abilities/EnemyMotorBase.cs`
- `Assets/_EndLink/Enemies/Abilities/EnemyCombatDriver.cs`
- `Assets/_EndLink/Control/IExternalDisplacementReceiver.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyStateMachine.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyStateId.cs`
- `Assets/_EndLink/Enemies/StateMachine/IEnemyState.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyStateBase.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyStateContext.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyIdleState.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyAlertState.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyCombatState.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyHitState.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyDeadState.cs`
- `Assets/_EndLink/Combat/Tags/CombatTagContainer.cs`
- `Assets/_EndLink/Combat/Target/ICombatTarget.cs`
- `Assets/_EndLink/Combat/Target/CombatTarget.cs`
- `Assets/_EndLink/Combat/Target/CombatTargetUtility.cs`
- `Assets/_EndLink/Combat/Hitbox/IHitReceiver.cs`
- `Assets/_EndLink/Combat/Damage/IDamageable.cs`

相关物体：
- 正式敌人根物体
  - `EnemyActor`
  - `EnemyHealth`
  - `CombatTarget`
  - `EnemyTargetSensor`
  - `EnemyStateMachine`
  - `EnemyMotorBase`
  - 可选 `EnemyCombatDriver`
  - `CombatTagContainer`
  - `CharacterController`
  - Collider
  - Layer 设置为 `Enemy`

关键配置：
- `CombatTarget.lockPoint`：锁定、瞄准和攻击朝向参考点，空则使用敌人根物体
- `motor`：敌人移动能力引用，普通地面敌人拖 `EnemyMotorBase`
- `combatDriver`：敌人战斗执行器引用，需要攻击能力的敌人拖 `EnemyCombatDriver`
- `maxHealth`：敌人最大生命值
- `initialState`：敌人启用后的初始大状态，通常为 `Idle`
- `alertDuration`：`Alert` 状态停留时间
- `EnemyStateMachine.detectionEnabled`：是否启用自动发现玩家
- `EnemyStateMachine.explicitDetectionTarget`：指定玩家目标，配置后优先检测该目标
- `EnemyStateMachine.targetLayerMask`：未指定目标时用于搜索玩家的 LayerMask
- `EnemyStateMachine.detectionOrigin`：索敌检测原点，留空时使用敌人根物体
- `EnemyStateMachine.detectionRadius`：发现目标半径
- `EnemyStateMachine.requiredAlertTime`：目标持续停留多久后进入 `Combat`，当前默认可设置为 3 秒
- `EnemyStateMachine.logSensorChanges`：是否打印索敌发现、丢失和进入 Combat 的日志
- `EnemyStateMachine.drawDetectionGizmo`：是否绘制索敌范围 Gizmo
- `hitDuration`：`Hit` 受击硬直时间
- `combatChaseStopDistance`：Combat 追击时保留的目标表面间隔，实际停止距离会额外加上敌人自身碰撞半径
- `EnemyMotorBase.autoAlignControllerToFeet`：是否自动按脚底根物体约定校正 `CharacterController`
- `EnemyMotorBase.preventPlanarCollisionLift`：是否抑制水平移动碰撞导致的异常上抬
- `EnemyMotorBase.pushExternalDisplacementReceivers`：敌人正常移动撞到玩家或队友时，是否把挡路角色挤开
- `EnemyMotorBase.collisionPushMultiplier`：敌人本帧移动量转换为推挤位移的倍率
- `EnemyMotorBase.maxCollisionPushDistance`：单次碰撞最多传递给玩家或队友的位移
- `initialTags`：敌人启用时默认拥有的战斗标签
- `combinationRules`：敌人身上触发协议反应使用的规则
- `disableCollidersOnDeath`：死亡后是否禁用非 Trigger Collider
- `feedbackRenderer`：受击和死亡变色使用的 MeshRenderer
- `showHealthInName`：是否在 GameObject 名字上显示血量

</details>

<a id="feature-player-combat-driver"></a>

### Feature：玩家战斗驱动

<details>
<summary>展开详情</summary>

功能说明：
- `PlayerCombatDriver` 不读取输入，不决定是否能进入攻击状态。
- 状态机决定能否攻击，`PlayerCombatDriver` 只负责执行攻击表现和判定。
- 支持通过 `CombatActionDefinition` 配置普攻、主动技能、连携技的伤害、击退、`CombatTagDefinition` 标签、标签持续时间、冷却、Hitbox 和生成参数。
- `PlayerCombatDriver` 执行的动作必须来自 `CombatActionDefinition`。
- 当前执行内容是生成指定 Hitbox prefab；有自动软锁目标时先让玩家正面瞬间转向目标，再按玩家正前方生成，没有目标时按角色当前正前方生成。
- 普攻、主动技能和连携技按各自 `CombatActionDefinition` 独立记录冷却。
- 暴露只读动作冷却剩余时间、归一化冷却值，以及指定动作的冷却查询，供战斗 UI 区分普攻、技能和连携槽。
- 实现 `ICombatActionExecutor`，状态机通过统一 `CanExecute` / `TryExecute` 入口检查和执行动作。
- 支持通过动作资产中的 `Hitbox Spawn Distance` 和 `Hitbox Spawn Height` 调整 Hitbox 生成位置。
- 生成 Hitbox 后会调用 `HitboxBase.Initialize(gameObject)` 传入攻击者。
- Hitbox 会在指定生命周期后自动销毁。
- 成功执行攻击后会通过 `CombatEventsBus` 广播 `ActionStarted`。
对应脚本：
- `Assets/_EndLink/Player/PlayerCombatDriver.cs`
- `Assets/_EndLink/Combat/ICombatActionExecutor.cs`
- `Assets/_EndLink/Combat/CombatActionDefinition.cs`
- `Assets/_EndLink/Combat/Hitbox/HitboxBase.cs`
相关物体/资产：
- 玩家根物体：挂载 `PlayerCombatDriver`
- `CombatActionDefinition` 数据资产：可通过 `Create > EndLink > Combat > Combat Action Definition` 创建
- `Assets/_EndLink/Combat/Hitbox_Base.prefab`
- `Assets/_EndLink/Combat/Hitbox_MeleeWave.prefab`
- 远程 Hitbox prefab 和远程技能动作资产由项目配置手动创建。
关键配置：
- `Basic Attack Action`：玩家普攻动作资产，鼠标左键触发的 `Attack` 状态会执行它
- `Skill Action`：玩家主动技能动作资产，后续由 `PartyCombatRouter` 的主角技能命令触发
- `Link Action`：玩家连携技动作资产，后续只能由连携机制确认合法窗口后触发，不能作为普通输入动作直接释放
- Hitbox prefab、生成距离、高度和冷却从对应的 `CombatActionDefinition` 读取；Hitbox 生命周期由 Hitbox prefab 自己配置。
</details>

<a id="feature-hitbox"></a>

### Feature：基础 Hitbox 配置

<details>
<summary>展开详情</summary>

功能说明：
- `HitboxBase` 是大多数攻击判定的基础组件。
- 使用 `OnTriggerEnter` 检测命中。
- 通过 `targetLayerMask` 过滤可命中的目标 Layer，默认回填 `Enemy` Layer。
- 子类可以覆盖目标 Layer 判断，用于后续阵营、友伤或特殊目标规则。
- `HitboxProjectile` 继承 `HitboxBase`，用于沿自身 Z 轴正方向飞行的远程判定。
- `HitboxProjectile` 支持最大飞行距离和命中后销毁，时间生命周期使用 `HitboxBase.lifetime`。
- `destroyOnHit` 关闭后可以临时作为穿透型远程 Hitbox 使用。
- 使用 `HashSet<Collider>` 记录已命中的 Collider，并使用 `CombatTarget.RootTransform` 对多 Collider 角色做根目标去重。
- 角色目标存在 `CombatTarget` 时会检查 `IsTargetable`，死亡或不可选目标会被跳过；没有 `CombatTarget` 的 `IHitReceiver` 可破坏物仍可受击。
- 命中后查找目标父级上的 `IHitReceiver`。
- 命中信息通过 `HitboxHitInfo` 传递，包含伤害、击退、`CombatTagDefinition` 标签、标签持续时间、标签层数、命中点、命中方向、Owner、Hitbox 和命中的 Collider。
- 命中时如果目标实现 `ICombatTagReceiver`，会把 `CombatTagDefinition` 添加到目标标签容器，并把 Hitbox owner 传入标签事件来源。
- 命中后触发 `UnityEvent<Collider>`，方便后续挂音效、特效或调试组件。
- 命中后会通过 `CombatEventsBus` 广播 `HitLanded`。

对应脚本：
- `Assets/_EndLink/Combat/Hitbox/HitboxBase.cs`
- `Assets/_EndLink/Combat/Hitbox/HitboxProjectile.cs`
- `Assets/_EndLink/Combat/Hitbox/HitboxHitInfo.cs`
- `Assets/_EndLink/Combat/Hitbox/IHitReceiver.cs`
- `Assets/_EndLink/Combat/Target/ICombatTarget.cs`
- `Assets/_EndLink/Combat/Target/CombatTargetUtility.cs`

相关资产：
- `Assets/_EndLink/Combat/Hitbox_Base.prefab`
- `Assets/_EndLink/Combat/Hitbox_MeleeWave.prefab`
- 远程 Hitbox prefab 可手动创建，并挂载 `HitboxProjectile`。
- `Assets/_EndLink/Combat/Mat_Wave.mat`

关键配置：
- `targetLayerMask`：允许命中的目标 Layer，默认 Enemy
- `damageAmount`：Hitbox 携带的固定伤害部分；完整动作伤害由伤害结算管线计算
- `knockbackForce`：基础瞬时击退距离，最终位移会乘以受击者 `CharacterStats.KnockbackTakenMultiplier`
- `combatTagToApply`：命中战斗标签资产
- `combatTagDuration`：命中战斗标签持续时间，小于等于 0 表示永久标签
- `combatTagStackCount`：命中时添加的战斗标签层数
- `lifetime`：Hitbox 自动销毁时间，小于等于 0 表示不按时间销毁
- `onHit`：命中事件
- `HitboxProjectile.speed`：远程 Hitbox 飞行速度，单位米/秒
- `HitboxProjectile.maxDistance`：远程 Hitbox 最大飞行距离，小于等于 0 表示不按距离销毁
- `HitboxProjectile.destroyOnHit`：远程 Hitbox 命中后是否立即销毁

配置注意：
- Hitbox 的 Collider 必须勾选 `Is Trigger`。
- 为保证 `OnTriggerEnter` 稳定触发，Hitbox prefab 建议带 `Rigidbody`，设置 `Is Kinematic = true`、`Use Gravity = false`。
- 当前玩家和队友攻击用的 Hitbox 默认要求敌人 Collider 所在物体设置为 `Enemy` Layer；其他攻击类型可通过 `targetLayerMask` 改为 Player、Ally 或自定义 Layer。
- `ProjectSettings/TagManager.asset` 包含 `Enemy` Layer。
</details>

<a id="feature-architecture-boundary"></a>

### Feature：当前架构边界

<details>
<summary>展开详情</summary>

当前约定：
- 输入读取器只读输入，不做业务逻辑。
- `PlayerStateMachine` 决定当前状态，负责 Idle、Move、Attack 等流程切换。
- `PlayerController` 负责移动能力和朝向，不负责读取输入或判断是否允许移动。
- `PlayerAnimatorDriver` 只把状态机和移动速度同步到 Animator 参数，不反向控制状态机。
- `PlayerTargeting` 负责玩家当前自动软锁目标选择，不控制相机、UI 或攻击执行。
- `PlayerCombatDriver` 不读取输入，只执行攻击表现和判定。
- `CharacterHealth` 负责通用生命值、受击和死亡，不直接切换任何角色状态机。
- `CombatTarget` 负责统一目标身份、存活/可选状态、锁定点和 Collider 表面距离，不负责扣血或状态切换。
- `PlayerHealth` 和 `AllyHealth` 是生命到状态机的桥接层，只把受击和死亡结果转发给各自状态机。
- `AllyBrain` 负责监听战斗事件并判断队友是否响应，不直接生成 Hitbox。
- `AllyStateMachine` 负责队友状态切换，不监听全局事件、不生成 Hitbox。
- `AllyCombatDriver` 负责执行队友助战动作，不订阅事件、不判断触发条件。
- `CombatEventsBus` 只广播战斗事实，不保存状态、不决定连携规则、不直接驱动表现。
- `HitboxBase` 负责命中检测和命中信息派发，不负责敌人如何扣血或表现。
- `HUDCombatController` 和 `UIPartyCombatAction` 只刷新显示和绑定 UI 槽位，不执行技能、不判断连携规则。
- `UICombatActionSlot` 只显示对应小队命令槽的键位和冷却，不拥有具体动作释放逻辑。
- `UIHealthBar` 只读取 `CharacterHealth` 并显示血量，不参与生命结算。
- `ThirdPersonCameraController` 负责相机目标旋转、缩放和 Cinemachine 参数，不负责玩家移动。
- `EnemyDummy` 只是早期命中验证对象；正式敌人能力以 `EnemyActor`、`EnemyHealth`、`EnemyStateMachine` 和敌人能力组件为主。

后续需要调整：
- 当前攻击仍是固定时间驱动，后续接动画后应改为动画事件或攻击窗口驱动。
- 战斗事件当前携带基础来源、目标、动作、单个标签和标签层数；后续如果连携规则需要更强表达，可扩展事件上下文或增加规则层数据结构。
- 当前 Hitbox 使用即时 Instantiate/Destroy，后续攻击频繁后建议切换对象池。

</details>
