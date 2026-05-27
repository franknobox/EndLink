# EndLink 项目开发文档

EndLink 是一个早期 3D 连携战斗 demo，目标参考类似《异度之刃》和《明日方舟：终末地》的小队协同战斗体验。当前阶段仍处在白模验证期，优先建立单人控制、镜头、状态机和战斗基础能力，再逐步扩展到连携与小队表现。

文档更新原则：不变动结构，实际开发与计划有冲突则调整文档表述。
开发规划：有新增就新增，做完了就去掉，表述简洁。
已做内容：更新尽量简洁明了，写清现有的内容，不用把演变沿革都写上去。

## 后续开发规划

### 1. 单人行动基底
内容：基本完成

### 2. 连携战斗基底
内容：基础建设完成，具体规则有待设计。

### 3. 固定主控与助战队友表现

内容：
- 队伍管理：维护固定主控、助战队友列表、存活状态、入队/离队和队伍槽位，小队表现调度。
- 基础队友战斗 AI：队友能选择目标、调整站位、释放简单技能，但不承担完整玩家操作能力。

### 4. 敌人基础制作

计划内容：
- 敌人通用基底：已建立正式敌人脚本结构和大状态机骨架，后续接入移动、战斗执行和行为树。
- 敌人移动与转向/敌人攻击能力/敌人目标选择/死亡与目标失效，保留必要死亡表现。

阶段完成标准：
- 至少一个正式敌人可以主动接近并攻击主控。
- 主控和两个队友可以围绕该敌人触发持续助战。
- 敌人死亡后能从目标系统和助战流程中稳定移除。

### 5. 连携触发与四类 Action Base 版

目标是在敌人基底可用于稳定测试后，做出第一版可验证的连携规则闭环：主控、队友、敌人、标签、事件和 Action 数据都能参与一次完整的“触发连携 -> 执行反应 -> 观察结果”流程。

计划内容：
- 连携触发规则初版：基于 `CombatEventsBus`、`CombatTagContainer` 和 `CombatTagDefinition`，定义最小可用的触发条件，例如指定标签命中、标签组合、目标处于可连携窗口等。
- 连携反应规则初版：触发后能明确由谁响应、响应哪个目标、执行哪个 Action，并广播可观察事件，方便调试。
- 四类 Action Base 版：分别做出`Skill`、`LinkAttack`、`Ultimate` 的基础数据资产和最小执行路径，键位。

阶段完成标准：
- 主控攻击敌人后，队友或主控能根据配置的规则执行一次 `LinkAttack`。
- 四类 `CombatActionType` 都有可创建、可配置、可在调试链路中识别的 base 数据。
- `Combat Monitor` 能看清 ActionStarted、HitLanded、Damaged、TagAdded、TagTransformed 等关键事件顺序。

## 当前已完成内容

### 当前情况概览

项目使用 Unity 6，当前核心代码集中在 `Assets/_EndLink/Control`、`Assets/_EndLink/StateMachine`、`Assets/_EndLink/Combat`、`Assets/_EndLink/Ally`、`Assets/_EndLink/Party` 和 `Assets/_EndLink/Enemies`。控制与状态机代码主要使用命名空间 `EndLink.Core`，战斗相关代码使用 `EndLink.Combat`，队友相关代码使用 `EndLink.Ally`，固定小队管理使用 `EndLink.Party`，敌人相关代码使用 `EndLink.Enemies`。目前已经完成了玩家输入读取、CharacterController 移动控制、Cinemachine 第三人称相机控制、玩家有限状态机最小战斗骨架、玩家生命值与受击接线、玩家 Animator 桥接、基础攻击驱动、基础 Hitbox 配置、战斗标签系统、战斗事件总栈基础版、事件接线、队友助战基础组件、队友状态机骨架、队友跟随移动第一版、固定三人小队管理第一版、正式敌人通用基底、敌人大状态机骨架和木桩敌人的第一版基础设施。

项目仍处于白模阶段，角色以胶囊体为主，当前重点是验证控制手感和后续架构边界。

### Feature 目录

#### 3C

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [新版 Input System 输入读取](#feature-input-system) | 已完成第一版 | 负责读取玩家移动、相机旋转和鼠标滚轮缩放输入，并把输入缓存为控制层可使用的数据。 |
| [玩家 CharacterController 移动](#feature-player-movement) | 已完成第一版 | 负责玩家在 XZ 平面的平滑移动、加减速、重力贴地和面向移动方向的平滑转向。 |
| [第三人称自由相机](#feature-third-person-camera) | 已完成第一版 | 负责越肩第三人称视角、自由旋转、上下角度限制、滚轮缩放和较开阔的战斗观察距离。 |
| [玩家有限状态机](#feature-player-state-machine) | 已完成最小战斗骨架 | 负责 Idle、Move、Attack、Skill、Hit、Dead 的状态切换，由状态机决定什么时候允许移动、攻击、释放技能、受击和死亡。 |
| [玩家 Animator 桥接](#feature-player-animator) | 已完成第一版 | 负责把玩家状态、移动速度和状态进入触发器同步到 Animator 参数，不参与状态决策。 |
| [当前架构边界](#feature-architecture-boundary) | 已建立初版约定 | 初步明确输入读取、玩家移动、相机控制、状态机、战斗驱动、命中检测之间的职责边界。 |

#### 战斗

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [玩家生命值与受击接线](#feature-player-health) | 已完成第一版 | 负责玩家扣血、治疗、死亡事件，并把有效受伤和死亡转发到玩家状态机。 |
| [玩家目标选择](#feature-player-targeting) | 已完成基础版 | 负责在 Enemy Layer 中按范围、角度和距离选择当前战斗目标，不控制镜头或 UI。 |
| [战斗动作配置](#feature-combat-action) | 已完成第一版 | 使用 `CombatActionDefinition` 数据资产描述普通攻击、技能、连携攻击和大招的伤害、冷却、时序、Hitbox 和命中标签。 |
| [战斗标签系统](#feature-combat-tags) | 已完成基础版 | 提供战斗专用标签定义、目标标签容器、多标签、持续时间、带来源的增删事件、合法检查和标签组合转化规则。 |
| [战斗数据编辑工具](#feature-combat-data-tool) | 已完成第一版 | 提供 Editor 窗口快捷创建和查看战斗动作、战斗标签、标签组合规则数据资产。 |
| [战斗 UI 槽位组件](#feature-combat-action-slot-ui) | 已完成最小版 | 提供小队命令槽位 UI，用于按主控/队友槽位读取当前动作、键位和冷却染色。 |
| [战斗事件总栈](#feature-combat-events-bus) | 已完成基础接线版 | 提供全局战斗事件类型、事件数据、事件广播入口、Console 日志监听器和 Editor 战斗事件监视窗口，当前已接入攻击、命中、受伤、死亡和标签变化。 |
| [玩家战斗驱动](#feature-player-combat-driver) | 已完成第一版 | 由状态机调用，负责执行攻击表现和判定，在角色前方生成 Hitbox 并管理攻击冷却。 |
| [敌人通用基底](#feature-enemy-foundation) | 已完成第一版 | 提供正式敌人身份入口、生命受击、死亡目标失效、目标有效性接口和大状态机骨架。 |
| [基础 Hitbox 配置](#feature-hitbox) | 已完成第一版 | 提供通用 Hitbox 基类和远程直线 Hitbox，用于配置近战判定、远程飞行判定、目标过滤、生命周期、伤害、击退和标签。 |
| [木桩敌人](#feature-enemy-dummy) | 已完成第一版 | 用于验证 Hitbox 命中、扣血、死亡、受击/死亡事件和基础调试显示。 |

#### 队伍

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| [队友助战基础组件](#feature-ally-assist) | 已完成持续助战第一版 | 提供队友事件响应大脑和队友战斗执行器，用于主控命中敌人后让队友自动接近目标并持续攻击。 |
| [队友调试监视窗口](#feature-ally-monitor) | 已完成第一版 | 提供 Editor 窗口集中查看队友状态快照和队友行为日志，辅助排查助战、冷却、距离和目标问题。 |
| [队友有限状态机](#feature-ally-state-machine) | 已完成通用动作状态版 | 提供 Idle、Follow、Assist、Action、Hit、Dead 外层状态，Assist 处理自动助战，Action 承载主动技能等指令动作。 |
| [队友跟随移动](#feature-ally-follow-motor) | 已完成手感增强版 | 负责队友在 Follow 状态中跟随主控，移动到主控附近的队形偏移范围，并支持平滑减速、追赶、远距离归位和简易避让。 |
| [固定三人小队管理](#feature-party-manager) | 已完成第一版 | 负责保存固定主控和 2 个队友槽位，统一分配队友跟随目标和队形偏移，并提供第一版小队战斗命令路由。 |

<a id="feature-input-system"></a>

### Feature：新版 Input System 输入读取

<details>
<summary>展开详情</summary>
功能说明：
- 使用已生成的 `InputSystem_Actions` C# 包装类。
- 玩家移动输入和相机输入分开读取，避免输入读取器承担移动或相机逻辑。
- 移动输入读取 `Player/Move`。
- 攻击输入读取 `Player/Attack`，由状态机消费后决定是否进入攻击状态。
- 主控主动技能读取 `Player/PlayerSkill`，默认键位 Q。
- 队友主动技能读取 `Player/AllySlotASkill` 和 `Player/AllySlotBSkill`，默认键位 E / F。
- 主控和队友连携请求读取 `Player/PlayerLinkAttack`、`Player/AllySlotALinkAttack`、`Player/AllySlotBLinkAttack`，默认键位 1 / 2 / 3；这些输入不会绕过连携机制直接释放动作。
- 全队极限技读取 `Player/PartyUltimate`，默认键位 V。
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
- 支持移动方向参考，拖入 `Main Camera` 后可实现相机相对移动。
- 支持按住 `Left Shift` 冲刺；当前冲刺作为移动速度修饰，不单独进入状态机大状态。
- 移动调用由 `PlayerStateMachine` 驱动，`PlayerController` 通过 `TickMovement` 执行实际位移。

对应脚本：
- `Assets/_EndLink/Control/PlayerController.cs`
- `Assets/_EndLink/Control/PlayerInputReader.cs`

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
- 当前包含 `Idle`、`Move`、`Attack`、`Skill`、`Hit`、`Dead` 六个状态。
- `Idle` 和 `Move` 会消费攻击输入，检查攻击冷却后切换到 `Attack`。
- `Skill` 是通用技能状态，当前通过 `PlayerStateMachine.RequestSkill()` 预留入口，具体键位待后续确定后接入新版 Input System。
- `Attack` 状态进入时调用 `PlayerCombatDriver.ExecuteAttack()`，攻击持续时间结束后根据移动输入回到 `Move` 或 `Idle`。
- 攻击期间移动输入会乘以 `attackMoveInputScale`，当前默认可以做站桩攻击。
- `Hit` 可以被外部通过 `RequestHit()` 触发，用于短暂受击硬直，结束后根据移动输入回到 `Move` 或 `Idle`。
- `Dead` 可以被外部通过 `RequestDead()` 触发，是当前最高优先级终止状态。

对应脚本：
- `Assets/_EndLink/StateMachine/IPlayerState.cs`
- `Assets/_EndLink/StateMachine/PlayerStateId.cs`
- `Assets/_EndLink/StateMachine/PlayerStateContext.cs`
- `Assets/_EndLink/StateMachine/PlayerStateBase.cs`
- `Assets/_EndLink/StateMachine/PlayerIdleState.cs`
- `Assets/_EndLink/StateMachine/PlayerMoveState.cs`
- `Assets/_EndLink/StateMachine/PlayerAttackState.cs`
- `Assets/_EndLink/StateMachine/PlayerSkillState.cs`
- `Assets/_EndLink/StateMachine/PlayerHitState.cs`
- `Assets/_EndLink/StateMachine/PlayerDeadState.cs`
- `Assets/_EndLink/StateMachine/PlayerStateMachine.cs`

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
- `hitDuration`：受击硬直持续时间
- `hitMoveInputScale`：受击期间移动输入倍率

</details>

<a id="feature-player-health"></a>

### Feature：玩家生命值与受击接线

<details>
<summary>展开详情</summary>


功能说明：
- `PlayerHealth` 负责玩家生命值、受伤、治疗和死亡接线。
- 同时实现 `IHitReceiver` 和 `IDamageable`，方便后续敌人 Hitbox、环境伤害或调试工具统一调用。
- `ReceiveHit(HitboxHitInfo hitInfo)` 会转发到 `TakeDamage(int damage, CombatTagDefinition tag)`。
- 受到有效伤害且未死亡时，会扣除生命值并请求 `PlayerStateMachine.RequestHit()`。
- 生命值首次降到 0 时，会请求 `PlayerStateMachine.RequestDead()`。
- 受到伤害和死亡时会通过 `CombatEventsBus` 广播 `Damaged` / `Dead`。
- 支持 `OnHealthChanged`、`OnDamaged`、`OnHealed` 和 `OnDead` 事件。
- 当前不把 Debuff / Buff 逻辑直接放进 `PlayerHealth`，后续应由独立状态效果系统处理，再通过事件或接口影响生命值与状态机。

对应脚本：
- `Assets/_EndLink/Combat/PlayerHealth.cs`
- `Assets/_EndLink/Combat/IHitReceiver.cs`
- `Assets/_EndLink/Combat/IDamageable.cs`
- `Assets/_EndLink/StateMachine/PlayerStateMachine.cs`

相关物体：
- 玩家根物体
  - `PlayerHealth`
  - `PlayerStateMachine`

关键配置：
- `maxHealth`：玩家最大生命值
- `requestHitStateOnDamage`：受伤时是否请求进入 Hit 状态
- `requestDeadStateOnDeath`：死亡时是否请求进入 Dead 状态
- `logHealthChanges`：是否打印血量变化调试信息
- `showHealthInName`：是否在 GameObject 名字上显示血量

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
- `Assets/_EndLink/StateMachine/PlayerStateMachine.cs`

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

### Feature：玩家目标选择

<details>
<summary>展开详情</summary>


功能说明：
- `PlayerTargeting` 是基础目标选择组件，放在战斗层。
- 只负责搜索并保存当前目标，不控制相机、不绘制 UI、不决定攻击逻辑。
- 默认搜索 `Enemy` Layer。
- 使用 `Physics.OverlapSphereNonAlloc` 搜索范围内目标，减少运行时 GC。
- 目标评分同时考虑视角/朝向夹角和距离，默认更偏向玩家前方或画面中心附近的敌人。
- 当前目标离开搜索范围、Layer 不匹配或被销毁时，会自动清除。
- 对外提供 `TryAcquireTarget()`、`SetCurrentTarget(Transform target)` 和 `ClearTarget()`。

对应脚本：
- `Assets/_EndLink/Combat/PlayerTargeting.cs`

相关物体：
- 玩家根物体
  - `PlayerTargeting`

关键配置：
- `searchRadius`：搜索半径
- `maxTargetAngle`：最大可锁定角度
- `targetLayerMask`：目标 LayerMask，默认 Enemy
- `searchOrigin`：搜索原点，空则使用玩家 Transform
- `viewReference`：视角参考，空则使用玩家朝向，通常可拖 Main Camera 或 CameraTarget
- `angleScoreWeight`：角度评分权重
- `distanceScoreWeight`：距离评分权重
- `logTargetChanges`：是否打印目标变化日志

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
- 动作配置包含伤害、击退、`CombatTagDefinition` 命中标签、标签持续时间、冷却、前摇、有效时间、后摇、Hitbox prefab、Hitbox 生成位置、生命周期和 AI 有效攻击距离。

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

<a id="feature-combat-tags"></a>

### Feature：战斗标签系统

<details>
<summary>展开详情</summary>


功能说明：
- 战斗标签系统只服务 Combat，不做全项目泛用 GameplayTag。
- `CombatTagDefinition` 是标签定义资产，包含 `tagId`、显示名和说明。
- 标签合法检查当前要求标签资产非空且 `tagId` 非空。
- `CombatTagContainer` 挂在目标身上，负责保存多标签、持续时间、添加、移除、过期和清空。
- `CombatTagContainer` 支持永久标签和限时标签，限时标签会在 `Update` 中自动倒计时并过期移除。
- `CombatTagCombinationRule` 描述 A + B => C 的组合转化规则，可选择转化后移除源标签，并可配置结果标签持续时间。
- 容器提供 `OnTagAdded`、`OnTagRemoved`、`OnTagExpired`、`OnTagRefreshed` 和 `OnTagTransformed` 事件。
- 标签添加、移除、过期和组合转化时会同步通过 `CombatEventsBus` 广播事件。
- 标签添加和移除接口支持传入 `source`，事件总线可以表达“谁给谁挂载或移除了某个标签”。
- 对外提供 `ICombatTagReadable` 和 `ICombatTagReceiver`，后续连携规则、AI、UI 和状态效果系统应优先依赖接口。
- `CombatActionDefinition`、`HitboxBase` 和 `HitboxHitInfo` 使用 `CombatTagDefinition` 作为标签数据。
- 命中时如果目标实现 `ICombatTagReceiver`，`HitboxBase` 会把 `CombatTagDefinition` 添加到目标标签容器，并把 Hitbox owner 作为标签来源。

对应脚本：
- `Assets/_EndLink/Combat/Tags/CombatTagDefinition.cs`
- `Assets/_EndLink/Combat/Tags/CombatTagCombinationRule.cs`
- `Assets/_EndLink/Combat/Tags/CombatTagContainer.cs`
- `Assets/_EndLink/Combat/Tags/CombatTagInterfaces.cs`

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

<a id="feature-combat-action-slot-ui"></a>

### Feature：战斗 UI 槽位组件

<details>
<summary>展开详情</summary>
功能说明：
- `CombatActionSlotUI` 是小队战斗命令槽位的最小 UI 组件。
- 槽位直接绑定 UI 键位槽，例如 `PlayerSkill`、`AllySlotASkill`、`AllySlotBSkill`。
- 组件通过 `PartyManager` 解析当前角色，通过角色 CombatDriver 读取当前槽位动作和该动作自己的冷却。
- 组件通过 `PartyManager.CombatRouter` 读取该槽位当前键位显示文本。
- 组件不读取输入、不释放动作、不判断战斗规则。
- 默认每帧自动刷新当前槽位动作的冷却染色，也支持外部通过 `SetCooldown(normalized)` 手动刷新。
- `normalized > 0` 表示图标显示冷却染色，`normalized = 0` 表示恢复图标原色。
- 冷却染色颜色可在 Inspector 中配置，默认半透明灰色，不做过渡插值。

对应脚本：
- `Assets/_EndLink/UI/CombatActionSlotUI.cs`

相关物体：
- 战斗 UI Canvas 下的技能、连携、大招等圆形动作槽位
  - `CombatActionSlotUI`
  - `Image` 图标

关键配置：
- `partyManager`：小队管理器，空时自动查找
- `slot`：该 UI 对应的键位槽，例如 PlayerSkill、AllySlotASkill、AllySlotBSkill
- `iconImage`：技能图标 Image，可拖子物体上的 Icon
- `cooldownTintColor`：冷却染色颜色，默认半透明灰色
- `autoRefreshCooldown`：是否每帧从对应角色槽位读取冷却

</details>

<a id="feature-combat-events-bus"></a>

### Feature：战斗事件总栈

<details>
<summary>展开详情</summary>

功能说明：
- `CombatEventsBus` 是全局战斗事件广播入口。
- `CombatEvent` 是一条战斗事件的数据结构，包含事件类型、来源、目标、动作配置、战斗标签、伤害、命中信息和时间戳。
- `CombatEventType` 目前包含 `ActionStarted`、`HitLanded`、`Damaged`、`Dead`、`TagAdded`、`TagRemoved`、`TagExpired`、`TagTransformed`。
- `CombatEventLog` 是白模阶段用的 Console 日志监听器，默认不打印，必要时手动开启。
- `CombatMonitorWindow` 是 Editor 战斗事件监视窗口，通过 `EndLink > Debug > Combat Monitor` 打开，订阅事件后以表格查看最近的战斗事件。
- 事件总栈只广播事实，不保存状态，不决定连携规则，不直接驱动队友 AI。
- 接入范围包括 `PlayerCombatDriver` 的动作开始、`HitboxBase` 的命中、`PlayerHealth` / `EnemyDummy` 的受伤与死亡，以及 `CombatTagContainer` 的标签添加、移除、过期和组合转化。

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
- `CombatEventsBus.RaiseTagTransformed(...)`

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
- `AllyTargetingUtility` 用目标 Collider 表面计算助战接近和攻击距离，避免大型敌人按中心点判断导致队友贴边却无法攻击。
- 当前版本用于验证木桩队友参与连携的最短链路。

对应脚本：
- `Assets/_EndLink/Ally/AllyBrain.cs`
- `Assets/_EndLink/Ally/AllyCombatDriver.cs`
- `Assets/_EndLink/Ally/AllyStateMachine.cs`
- `Assets/_EndLink/Ally/AllyTargetingUtility.cs`
- `Assets/_EndLink/Combat/CombatActionDefinition.cs`
- `Assets/_EndLink/Combat/Events/CombatEventsBus.cs`

相关物体：
- 队友根物体
  - `AllyBrain`
  - `AllyStateMachine`
  - `AllyCombatDriver`

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
- `AllyFollowMotor` 会常驻绘制跟随死区 Gizmo，运行时以该队友当前死区中心为圆心，非运行时以队友自身为圆心；选中队友时 Gizmo 会更明显。
- 支持第一版简易避让：离主控太近时会被推开，配置 `avoidanceLayerMask` 后也能对其他队友做局部排斥。
- `formationOffset` 由 `PartyManager` 的队友槽位统一配置，并写入 `AllyFollowMotor`。
- 当前只处理平面 XZ 跟随和局部避让，后续如果需要复杂地形、障碍绕路，再接 NavMesh 或更完整的队伍槽位调度。

对应脚本：
- `Assets/_EndLink/Ally/AllyFollowMotor.cs`
- `Assets/_EndLink/Ally/AllyFollowState.cs`
- `Assets/_EndLink/Ally/AllyStateMachine.cs`
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
- `PartyManager` 持有 `PartyCombatRouter` 引用，供战斗 UI 和后续小队系统读取当前键位路由。
- `PartyCombatRouter` 负责把 `PlayerInputReader` 中的战斗输入翻译成主控、队友 A、队友 B 或全队的命令请求。
- `PartyCombatRouter` 不直接生成 Hitbox，不处理伤害、标签或状态机切换。
- 当前第一版中，主控 `Skill` 命令会由 `PartyCombatRouter` 立即转发给 `PlayerCombatDriver` 执行 `SkillAction`。
- 队友 `Skill` 命令会由 `PartyCombatRouter` 转发给对应 `AllyStateMachine.RequestAction(...)`，进入 `Action` 状态后再由 `AllyCombatDriver` 执行 `SkillAction`。
- `PartyCombatRouter` Inspector 中可以覆盖 Q/E/F 和 1/2/3 对应的技能与连携请求键位，V 键全队极限技暂时固定。
- `LinkAttack` 命令只表示玩家请求使用连携槽位，不能被普通动作执行层直接当成可释放技能处理。
- 后续队友 AI、连携规则或调试工具需要知道“谁是主控，谁是队友”时，可以从 `PartyManager` 查询。

对应脚本：
- `Assets/_EndLink/Party/PartyManager.cs`
- `Assets/_EndLink/Party/PartyFormationSlot.cs`
- `Assets/_EndLink/Party/PartyFollowSettings.cs`
- `Assets/_EndLink/Party/PartyCombatRouter.cs`
- `Assets/_EndLink/Ally/AllyFollowMotor.cs`
- `Assets/_EndLink/Ally/AllyStateMachine.cs`

相关物体：
- 场景管理物体
  - `PartyManager`
  - `PartyCombatRouter`
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
- `EnemyActor` 是正式敌人的根入口组件，只暴露敌人身份、目标点和生命组件引用。
- `EnemyActor` 要求同物体挂载 `CombatTagContainer`，保证正式敌人天然支持战斗标签、持续标签和组合转化。
- `EnemyHealth` 负责正式敌人的血量、受击、死亡、死亡事件和白模调试反馈。
- `EnemyHealth` 同时实现 `IHitReceiver`、`IDamageable` 和 `ICombatTarget`。
- `ICombatTarget` 是战斗目标有效性接口，当前用于判断目标死亡后是否还能被锁定、搜索或命中。
- 敌人死亡后默认 `IsTargetable = false`，后续 `PlayerTargeting`、`AllyBrain` 和 `HitboxBase` 会跳过不可目标对象。
- `EnemyStateMachine` 管理 `Idle`、`Alert`、`Combat`、`Hit`、`Dead` 五个敌人大状态。
- `Combat` 当前保持空转，后续作为行为树的外层挂载点，内部再承载追击、站位、攻击、技能等细节行为。
- `Hit` 作为独立大状态处理受击打断，不放进 Combat 行为树，方便后续加入硬直、霸体、击倒等规则。
- `EnemyDummy` 是轻量命中测试对象，用于快速验证 Hitbox、扣血、死亡和调试显示。

对应脚本：
- `Assets/_EndLink/Enemies/EnemyActor.cs`
- `Assets/_EndLink/Enemies/EnemyHealth.cs`
- `Assets/_EndLink/Enemies/EnemyStateMachine.cs`
- `Assets/_EndLink/Enemies/EnemyStateId.cs`
- `Assets/_EndLink/Enemies/IEnemyState.cs`
- `Assets/_EndLink/Enemies/EnemyStateBase.cs`
- `Assets/_EndLink/Enemies/EnemyStateContext.cs`
- `Assets/_EndLink/Enemies/EnemyIdleState.cs`
- `Assets/_EndLink/Enemies/EnemyAlertState.cs`
- `Assets/_EndLink/Enemies/EnemyCombatState.cs`
- `Assets/_EndLink/Enemies/EnemyHitState.cs`
- `Assets/_EndLink/Enemies/EnemyDeadState.cs`
- `Assets/_EndLink/Combat/Tags/CombatTagContainer.cs`
- `Assets/_EndLink/Combat/ICombatTarget.cs`
- `Assets/_EndLink/Combat/IHitReceiver.cs`
- `Assets/_EndLink/Combat/IDamageable.cs`

相关物体：
- 正式敌人根物体
  - `EnemyActor`
  - `EnemyHealth`
  - `CombatTagContainer`
  - Collider
  - Layer 设置为 `Enemy`

关键配置：
- `targetTransform`：锁定、寻路和计算距离使用的目标点
- `maxHealth`：敌人最大生命值
- `initialState`：敌人启用后的初始大状态，通常为 `Idle`
- `alertDuration`：`Alert` 状态停留时间
- `hitDuration`：`Hit` 受击硬直时间
- `initialTags`：敌人启用时默认拥有的战斗标签
- `combinationRules`：敌人身上标签组合转化使用的规则
- `untargetableOnDeath`：死亡后是否不再作为有效战斗目标
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
- 当前执行内容是在角色正前方生成指定 Hitbox prefab。
- 支持动作冷却，防止动作过快触发。
- 暴露只读动作冷却剩余时间、归一化冷却值，以及指定动作的冷却查询，供战斗 UI 区分普攻、技能和连携槽。
- 支持通过动作资产中的 `Hitbox Spawn Distance` 和 `Hitbox Spawn Height` 调整 Hitbox 生成位置。
- 生成 Hitbox 后会调用 `HitboxBase.Initialize(gameObject)` 传入攻击者。
- Hitbox 会在指定生命周期后自动销毁。
- 成功执行攻击后会通过 `CombatEventsBus` 广播 `ActionStarted`。
对应脚本：
- `Assets/_EndLink/Combat/PlayerCombatDriver.cs`
- `Assets/_EndLink/Combat/CombatActionDefinition.cs`
- `Assets/_EndLink/Combat/HitboxBase.cs`
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
- 使用 `HashSet<Collider>` 记录已经命中过的 Collider，避免同一个 Hitbox 重复命中同一目标。
- 如果目标实现 `ICombatTarget` 且 `IsTargetable == false`，Hitbox 会跳过该目标。
- 命中后查找目标父级上的 `IHitReceiver`。
- 命中信息通过 `HitboxHitInfo` 传递，包含伤害、击退、`CombatTagDefinition` 标签、标签持续时间、命中点、命中方向、Owner、Hitbox 和命中的 Collider。
- 命中时如果目标实现 `ICombatTagReceiver`，会把 `CombatTagDefinition` 添加到目标标签容器，并把 Hitbox owner 传入标签事件来源。
- 命中后触发 `UnityEvent<Collider>`，方便后续挂音效、特效或调试组件。
- 命中后会通过 `CombatEventsBus` 广播 `HitLanded`。

对应脚本：
- `Assets/_EndLink/Combat/HitboxBase.cs`
- `Assets/_EndLink/Combat/HitboxProjectile.cs`
- `Assets/_EndLink/Combat/HitboxHitInfo.cs`
- `Assets/_EndLink/Combat/IHitReceiver.cs`
- `Assets/_EndLink/Combat/ICombatTarget.cs`

相关资产：
- `Assets/_EndLink/Combat/Hitbox_Base.prefab`
- `Assets/_EndLink/Combat/Hitbox_MeleeWave.prefab`
- 远程 Hitbox prefab 可手动创建，并挂载 `HitboxProjectile`。
- `Assets/_EndLink/Combat/Mat_Wave.mat`

关键配置：
- `targetLayerMask`：允许命中的目标 Layer，默认 Enemy
- `damageAmount`：伤害值
- `knockbackForce`：击退力
- `combatTagToApply`：命中战斗标签资产
- `combatTagDuration`：命中战斗标签持续时间，小于等于 0 表示永久标签
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

<a id="feature-enemy-dummy"></a>

### Feature：木桩敌人

<details>
<summary>展开详情</summary>


功能说明：
- `EnemyDummy` 用于验证 Hitbox 命中链路。
- 同时实现 `IHitReceiver` 和 `IDamageable`。
- `ReceiveHit(HitboxHitInfo hitInfo)` 会转发到 `TakeDamage(int damage, CombatTagDefinition tag)`。
- 支持 `maxHealth` / `CurrentHealth` / `IsDead`，受到伤害后会扣血，生命值降到 0 时进入死亡状态。
- 受击后使用 URP 友好的 `MaterialPropertyBlock` 改写 `_BaseColor`，瞬间变为浅红不透明色，`0.1` 秒后恢复原色。
- 死亡后切换为灰色，方便白模阶段观察木桩状态。
- 支持 `OnDamaged` 和 `OnDead` 事件，方便以后挂音效、特效或调试 UI。
- 受到伤害和死亡时会通过 `CombatEventsBus` 广播 `Damaged` / `Dead`。
- 支持 `logHits` 打印伤害、标签和当前血量。
- 支持 `showHealthInName` 把当前血量显示到 GameObject 名字上。

对应脚本：
- `Assets/_EndLink/Combat/EnemyDummy.cs`
- `Assets/_EndLink/Combat/IDamageable.cs`
- `Assets/_EndLink/Combat/IHitReceiver.cs`

相关物体：
- 木桩敌人
  - `EnemyDummy`
  - `MeshRenderer`
  - Collider
  - Layer 设置为 `Enemy`

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
- `PlayerTargeting` 只负责当前战斗目标选择，不控制相机锁定、UI 或攻击执行。
- `PlayerCombatDriver` 不读取输入，只执行攻击表现和判定。
- `AllyBrain` 负责监听战斗事件并判断队友是否响应，不直接生成 Hitbox。
- `AllyStateMachine` 负责队友状态切换，不监听全局事件、不生成 Hitbox。
- `AllyCombatDriver` 负责执行队友助战动作，不订阅事件、不判断触发条件。
- `CombatEventsBus` 只广播战斗事实，不保存状态、不决定连携规则、不直接驱动表现。
- `HitboxBase` 负责命中检测和命中信息派发，不负责敌人如何扣血或表现。
- `ThirdPersonCameraController` 负责相机目标旋转、缩放和 Cinemachine 参数，不负责玩家移动。
- `EnemyDummy` 是临时验证对象，后续正式敌人应复用 `IHitReceiver` / `IDamageable` 接口。

后续需要调整：
- 当前攻击仍是固定时间驱动，后续接动画后应改为动画事件或攻击窗口驱动。
- 战斗事件当前只携带基础来源、目标和单个标签，后续如果连携规则需要更强表达，可扩展事件上下文或增加规则层数据结构。
- 当前 Hitbox 使用即时 Instantiate/Destroy，后续攻击频繁后建议切换对象池。

</details>
