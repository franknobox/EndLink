# EndLink 项目文档

EndLink 是一个早期 3D 连携战斗 demo，目标参考类似《异度之刃》和《明日方舟：终末地》的小队协同战斗体验。当前阶段仍处在白模验证期，优先建立单人控制、镜头、状态机和战斗基础能力，再逐步扩展到连携与小队表现。

文档更新原则：不变动结构，实际开发与计划有冲突则调整文档表述。
开发规划：有新增就新增，做完了就去掉，表述简洁。
已做内容：更新尽量简洁明了，

## 后续开发规划

### 1. 单人行动基底

内容：基本完成，技能后面做，键位后面再配置完整


### 2. 连携战斗基底

目标是建立“角色之间如何协同”的底层规则，而不是先追求完整表现。

计划内容：

- 事件总栈接线：已完成全局战斗事件总栈基础版，并已把攻击开始、命中、受伤、死亡和标签变化接入事件流。
- 连携触发规则：定义哪些事件、标签和条件可以触发队友协同。
- 助战队友木桩验证：已完成队友大脑和队友战斗执行器基础脚本，后续在场景中用固定队友占位验证连携触发和响应链路。
- 战斗日志/调试面板：显示事件流和标签判断结果，便于调试复杂连携逻辑。

阶段完成标准：

- 玩家技能可以触发某类连携条件。
- 固定助战队友木桩能根据事件做出一次协同响应。
- 标签、事件、连携规则之间的职责边界清楚。

### 3. 固定主控与助战队友表现

目标是从“系统能跑”推进到“固定主控 + 2 到 3 名助战队友在场景中有可信表现”。主控角色保持唯一且长期稳定，队友只负责跟随、站位、响应事件和释放助战行为。

计划内容：

- 队伍管理：维护固定主控、助战队友列表、存活状态、入队/离队和队伍槽位。
- 主控绑定：相机、输入、玩家状态机始终绑定同一个主控角色，暂不实现主控切换。
- 队友专用状态机：为助战队友建立 `AllyStateMachine`，复用通用移动、受击、死亡和战斗判定能力，但由 AI 和战斗事件驱动，而不是玩家输入驱动。
- 跟随 AI：助战队友跟随主控移动，保持合适距离、队形和避让。
- 助战响应：队友根据主控攻击、命中标签、连携事件或冷却窗口释放简单助战行为。
- 基础队友战斗 AI：队友能选择目标、调整站位、释放简单技能，但不承担完整玩家操作能力。
- 通用角色能力沉淀：在玩家和队友都出现重复需求后，再抽出 `CharacterMotor`、`CharacterHealth`、通用 Hit/Dead 规则等共享层，避免过早抽象。
- 小队表现调度：避免多个队友同时挤占同一空间或同时触发过多表现。

阶段完成标准：

- 同屏至少 2 到 3 名角色能稳定行动。
- 主控角色始终固定，镜头和输入不会被队友逻辑打断。
- 助战队友能跟随主控，并根据战斗事件参与简单战斗。
- 队友状态机和玩家状态机职责分离，队友不依赖玩家输入系统也能独立行动。

### 4. 待定方向

后续根据前三阶段验证结果再定，可能包括：

- 锁定目标系统与战斗镜头。
- 更完整的技能编辑方式。
- 连携 UI 和时机反馈。
- 敌人 AI 与 Boss 机制。
- 关卡小场景和战斗节奏验证。

## 当前已完成内容

### 当前情况概览

项目使用 Unity 6，当前核心代码集中在 `Assets/_EndLink/Control`、`Assets/_EndLink/StateMachine` 和 `Assets/_EndLink/Combat`。控制与状态机代码主要使用命名空间 `EndLink.Core`，战斗相关代码使用命名空间 `EndLink.Combat`。目前已经完成了玩家输入读取、CharacterController 移动控制、Cinemachine 第三人称相机控制、玩家有限状态机最小战斗骨架、玩家生命值与受击接线、玩家 Animator 桥接、基础攻击驱动、通用 Hitbox、战斗标签系统、战斗事件总栈基础版、事件接线、队友助战基础组件和木桩敌人的第一版基础设施。

项目仍处于白模阶段，角色以胶囊体为主，当前重点是验证控制手感和后续架构边界。

### Feature 总览

| 功能名 | 当前状态 | 内容说明 |
| --- | --- | --- |
| 新版 Input System 输入读取 | 已完成第一版 | 负责读取玩家移动、相机旋转和鼠标滚轮缩放输入，并把输入缓存为控制层可使用的数据。 |
| 玩家 CharacterController 移动 | 已完成第一版 | 负责玩家在 XZ 平面的平滑移动、加减速、重力贴地和面向移动方向的平滑转向。 |
| 第三人称自由相机 | 已完成第一版 | 负责越肩第三人称视角、自由旋转、上下角度限制、滚轮缩放和较开阔的战斗观察距离。 |
| 玩家有限状态机 | 已完成最小战斗骨架 | 负责 Idle、Move、Attack、Skill、Hit、Dead 的状态切换，由状态机决定什么时候允许移动、攻击、释放技能、受击和死亡。 |
| 玩家生命值与受击接线 | 已完成第一版 | 负责玩家扣血、治疗、死亡事件，并把有效受伤和死亡转发到玩家状态机。 |
| 玩家 Animator 桥接 | 已完成第一版 | 负责把玩家状态、移动速度和状态进入触发器同步到 Animator 参数，不参与状态决策。 |
| 玩家目标选择 | 已完成基础版 | 负责在 Enemy Layer 中按范围、角度和距离选择当前战斗目标，不控制镜头或 UI。 |
| 战斗动作配置 | 已完成第一版 | 使用 `CombatActionDefinition` 数据资产描述普通攻击、技能、连携攻击和大招的伤害、冷却、时序、Hitbox 和命中标签。 |
| 战斗标签系统 | 已完成基础版 | 提供战斗专用标签定义、目标标签容器、多标签、持续时间、增删事件、合法检查和标签组合转化规则。 |
| 战斗数据编辑工具 | 已完成第一版 | 提供 Editor 窗口快捷创建和查看战斗动作、战斗标签、标签组合规则数据资产。 |
| 战斗事件总栈 | 已完成基础接线版 | 提供全局战斗事件类型、事件数据、事件广播入口和调试日志监听器，当前已接入攻击、命中、受伤、死亡和标签变化。 |
| 玩家战斗驱动 | 已完成第一版 | 由状态机调用，负责执行攻击表现和判定，在角色前方生成 Hitbox 并管理攻击冷却。 |
| 队友助战基础组件 | 已完成脚本第一版 | 提供队友事件响应大脑和队友战斗执行器，用于验证队友根据战斗事件释放一次助战行为。 |
| 通用 Hitbox 基类 | 已完成第一版 | 负责 Trigger 命中检测、Enemy Layer 过滤、重复命中去重，并向目标传递伤害、击退和标签。 |
| 木桩敌人 | 已完成第一版 | 用于验证 Hitbox 命中、扣血、死亡、受击/死亡事件和基础调试显示。 |
| 当前架构边界 | 已建立初版约定 | 初步明确输入读取、玩家移动、相机控制、状态机、战斗驱动、命中检测之间的职责边界。 |

### Feature：新版 Input System 输入读取

功能说明：

- 使用已生成的 `InputSystem_Actions` C# 包装类。
- 玩家移动输入和相机输入分开读取，避免输入读取器承担移动或相机逻辑。
- 移动输入读取 `Player/Move`。
- 攻击输入读取 `Player/Attack`，由状态机消费后决定是否进入攻击状态。
- 相机旋转读取 `Player/Look`。
- 鼠标滚轮缩放暂时通过 `Mouse.current.scroll` 读取，后续可迁移到独立 `Zoom` action。

对应脚本：

- `Assets/_EndLink/Control/InputSystem_Actions.cs`
- `Assets/_EndLink/Control/PlayerInputReader.cs`
- `Assets/_EndLink/Control/PlayerCameraInputReader.cs`

相关资产：

- `Assets/_EndLink/Control/InputSystem_Actions.inputactions`

相关物体：

- 玩家物体：挂载 `PlayerInputReader`
- 第三人称相机控制物体：挂载 `PlayerCameraInputReader`

### Feature：玩家 CharacterController 移动

功能说明：

- 基于 `CharacterController` 移动。
- 使用 `Mathf.SmoothDamp` 做平滑加速和减速。
- 支持手动重力和贴地速度。
- 移动时角色本地 Z 轴正方向会平滑转向移动方向。
- 支持移动方向参考，拖入 `Main Camera` 后可实现相机相对移动。
- 当前移动调用权已经交给 `PlayerStateMachine`，`PlayerController` 暴露 `TickMovement`，不再自行在 `Update` 中读取输入移动。

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
- `accelerationSmoothTime`：加速阻尼
- `decelerationSmoothTime`：减速阻尼
- `rotationSharpness`：转向响应
- `movementReference`：移动方向参考，通常拖 `Main Camera`

### Feature：第三人称自由相机

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


### Feature：玩家有限状态机

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

### Feature：玩家生命值与受击接线

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

### Feature：玩家 Animator 桥接

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

### Feature：玩家目标选择

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

### Feature：战斗动作配置

功能说明：

- `CombatActionDefinition` 是战斗动作数据资产，用于描述一次普通攻击、技能、连携攻击或大招。
- `CombatActionType` 描述动作性质，不描述释放者来源。
- 当前动作类型包括 `BasicAttack`、`Skill`、`LinkAttack`、`Ultimate`。
- 主控、队友和敌人后续可以共用同一套动作类型，释放者来源应由后续战斗事件数据携带。
- 动作配置包含伤害、击退、`CombatTagDefinition` 命中标签、标签持续时间、冷却、前摇、有效时间、后摇、Hitbox prefab、Hitbox 生成位置和生命周期。

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

### Feature：战斗标签系统

功能说明：

- 战斗标签系统只服务 Combat，不做全项目泛用 GameplayTag。
- `CombatTagDefinition` 是标签定义资产，包含 `tagId`、显示名和说明。
- 标签合法检查当前要求标签资产非空且 `tagId` 非空。
- `CombatTagContainer` 挂在目标身上，负责保存多标签、持续时间、添加、移除、过期和清空。
- `CombatTagContainer` 支持永久标签和限时标签，限时标签会在 `Update` 中自动倒计时并过期移除。
- `CombatTagCombinationRule` 描述 A + B => C 的组合转化规则，可选择转化后移除源标签，并可配置结果标签持续时间。
- 容器提供 `OnTagAdded`、`OnTagRemoved`、`OnTagExpired`、`OnTagRefreshed` 和 `OnTagTransformed` 事件。
- 标签添加、移除、过期和组合转化时会同步通过 `CombatEventsBus` 广播事件。
- 对外提供 `ICombatTagReadable` 和 `ICombatTagReceiver`，后续连携规则、AI、UI 和状态效果系统应优先依赖接口。
- `CombatActionDefinition`、`HitboxBase` 和 `HitboxHitInfo` 已支持 `CombatTagDefinition` 标签通道。
- 旧的 string tag 通道已移除，伤害链路统一使用 `CombatTagDefinition`。
- 命中时如果目标实现 `ICombatTagReceiver`，`HitboxBase` 会把 `CombatTagDefinition` 添加到目标标签容器。

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

### Feature：战斗数据编辑工具

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

### Feature：战斗事件总栈

功能说明：

- `CombatEventsBus` 是全局战斗事件广播入口。
- `CombatEvent` 是一条战斗事件的数据结构，包含事件类型、来源、目标、动作配置、战斗标签、伤害、命中信息和时间戳。
- `CombatEventType` 目前包含 `ActionStarted`、`HitLanded`、`Damaged`、`Dead`、`TagAdded`、`TagRemoved`、`TagExpired`、`TagTransformed`。
- `CombatEventLog` 是白模阶段用的日志监听器，订阅事件后把事件流打印到 Console。
- 事件总栈只广播事实，不保存状态，不决定连携规则，不直接驱动队友 AI。
- 当前已接入 `PlayerCombatDriver` 的动作开始、`HitboxBase` 的命中、`PlayerHealth` / `EnemyDummy` 的受伤与死亡，以及 `CombatTagContainer` 的标签添加、移除、过期和组合转化。

对应脚本：

- `Assets/_EndLink/Combat/Events/CombatEventType.cs`
- `Assets/_EndLink/Combat/Events/CombatEvent.cs`
- `Assets/_EndLink/Combat/Events/CombatEventsBus.cs`
- `Assets/_EndLink/Combat/Events/CombatEventLog.cs`

相关物体：

- 任意调试物体
  - `CombatEventLog`

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

### Feature：队友助战基础组件

功能说明：

- `AllyBrain` 是队友大脑，负责监听 `CombatEventsBus` 并判断是否响应。
- `AllyBrain` 默认响应 `HitLanded` 事件，忽略自己发出的事件，可选只响应指定来源，例如主控玩家。
- `AllyBrain` 不直接生成 Hitbox，不写伤害数据，只把事件目标交给 `AllyCombatDriver`。
- `AllyCombatDriver` 是队友战斗执行器，职责类似 `PlayerCombatDriver`，但不读取输入，也不决定什么时候出手。
- `AllyCombatDriver` 根据 `CombatActionDefinition` 生成 Hitbox，并写入伤害、击退、战斗标签和标签持续时间。
- `AllyCombatDriver` 执行助战时会朝目标方向生成判定，并广播 `ActionStarted` 事件。
- 当前版本用于验证木桩队友参与连携的最短链路，后续可接入队友状态机、跟随 AI、站位和更完整的连携规则。

对应脚本：

- `Assets/_EndLink/Ally/AllyBrain.cs`
- `Assets/_EndLink/Ally/AllyCombatDriver.cs`
- `Assets/_EndLink/Combat/CombatActionDefinition.cs`
- `Assets/_EndLink/Combat/Events/CombatEventsBus.cs`

相关物体：

- 队友根物体
  - `AllyBrain`
  - `AllyCombatDriver`

关键配置：

- `assistAction`：队友助战动作配置资产
- `faceTargetBeforeAttack`：助战前是否转向目标
- `respondToHitLanded`：是否响应命中事件
- `requiredSource`：可选事件来源过滤，通常可拖主角
- `ignoreSelfEvents`：是否忽略自己发出的事件
- `requireTarget`：是否要求事件必须带目标
- `logDecisions`：是否打印队友响应决策日志

### Feature：玩家战斗驱动

功能说明：

- `PlayerCombatDriver` 不读取输入，不决定是否能进入攻击状态。
- 状态机决定能否攻击，`PlayerCombatDriver` 只负责执行攻击表现和判定。
- 支持通过 `CombatActionDefinition` 配置普攻的伤害、击退、`CombatTagDefinition` 标签、标签持续时间、冷却、Hitbox 和生成参数。
- 未配置 `CombatActionDefinition` 时，仍会回退使用组件上的兼容默认字段。
- 当前执行内容是在角色正前方生成指定 Hitbox prefab。
- 支持攻击冷却，防止攻击过快触发。
- 支持 `spawnDistance` 和 `spawnHeight` 调整 Hitbox 生成位置。
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

关键配置：

- `basicAttackDefinition`：玩家普攻配置资产，配置后优先使用资产参数
- `hitboxPrefab`：攻击生成的 Hitbox prefab
- `spawnDistance`：生成在角色前方的距离
- `spawnHeight`：生成高度偏移
- `hitboxLifetime`：Hitbox 自动销毁时间
- `attackCooldown`：攻击冷却时间

### Feature：通用 Hitbox 基类

功能说明：

- `HitboxBase` 是大多数攻击判定的基础组件。
- 使用 `OnTriggerEnter` 检测命中。
- 只对 Layer 为 `Enemy` 的目标生效。
- 使用 `HashSet<Collider>` 记录已经命中过的 Collider，避免同一个 Hitbox 重复命中同一目标。
- 命中后查找目标父级上的 `IHitReceiver`。
- 命中信息通过 `HitboxHitInfo` 传递，包含伤害、击退、`CombatTagDefinition` 标签、标签持续时间、命中点、命中方向、Owner、Hitbox 和命中的 Collider。
- 命中时如果目标实现 `ICombatTagReceiver`，会把 `CombatTagDefinition` 添加到目标标签容器。
- 命中后触发 `UnityEvent<Collider>`，方便后续挂音效、特效或调试组件。
- 命中后会通过 `CombatEventsBus` 广播 `HitLanded`。

对应脚本：

- `Assets/_EndLink/Combat/HitboxBase.cs`
- `Assets/_EndLink/Combat/HitboxHitInfo.cs`
- `Assets/_EndLink/Combat/IHitReceiver.cs`

相关资产：

- `Assets/_EndLink/Combat/Hitbox_Base.prefab`
- `Assets/_EndLink/Combat/Hitbox_MeleeWave.prefab`
- `Assets/_EndLink/Combat/Mat_Wave.mat`

关键配置：

- `damageAmount`：伤害值
- `knockbackForce`：击退力
- `combatTagToApply`：命中战斗标签资产
- `combatTagDuration`：命中战斗标签持续时间，小于等于 0 表示永久标签
- `onHit`：命中事件

配置注意：

- Hitbox 的 Collider 必须勾选 `Is Trigger`。
- 为保证 `OnTriggerEnter` 稳定触发，Hitbox prefab 建议带 `Rigidbody`，设置 `Is Kinematic = true`、`Use Gravity = false`。
- 敌人 Collider 所在物体必须设置为 `Enemy` Layer。
- `ProjectSettings/TagManager.asset` 中已经添加 `Enemy` Layer。

### Feature：木桩敌人

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

### Feature：当前架构边界

当前约定：

- 输入读取器只读输入，不做业务逻辑。
- `PlayerStateMachine` 决定当前状态，负责 Idle、Move、Attack 等流程切换。
- `PlayerController` 负责移动能力和朝向，不负责读取输入或判断是否允许移动。
- `PlayerAnimatorDriver` 只把状态机和移动速度同步到 Animator 参数，不反向控制状态机。
- `PlayerTargeting` 只负责当前战斗目标选择，不控制相机锁定、UI 或攻击执行。
- `PlayerCombatDriver` 不读取输入，只执行攻击表现和判定。
- `AllyBrain` 负责监听战斗事件并判断队友是否响应，不直接生成 Hitbox。
- `AllyCombatDriver` 负责执行队友助战动作，不订阅事件、不判断触发条件。
- `CombatEventsBus` 只广播战斗事实，不保存状态、不决定连携规则、不直接驱动表现。
- `HitboxBase` 负责命中检测和命中信息派发，不负责敌人如何扣血或表现。
- `ThirdPersonCameraController` 负责相机目标旋转、缩放和 Cinemachine 参数，不负责玩家移动。
- `EnemyDummy` 是临时验证对象，后续正式敌人应复用 `IHitReceiver` / `IDamageable` 接口。

后续需要调整：

- 当前攻击仍是固定时间驱动，后续接动画后应改为动画事件或攻击窗口驱动。
- 战斗事件当前只携带基础来源、目标和单个标签，后续如果连携规则需要更强表达，可扩展事件上下文或增加规则层数据结构。
- 当前 Hitbox 使用即时 Instantiate/Destroy，后续攻击频繁后建议切换对象池。
