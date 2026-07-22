# Enemies Features

正式敌人身份、生命、韧性与平衡、感知、状态机、围攻协调、基础移动和动画桥接详情。

主索引见 [FEATURES.md](../FEATURES.md)。

<a id="feature-enemy-foundation"></a>
<a id="feature-enemy-identity-health"></a>

### Feature：敌人身份与生命目标

<details>
<summary>展开详情</summary>

功能说明：
- `EnemyActor` 是正式敌人的根入口组件，只暴露敌人身份和能力组件引用，并要求同物体存在 `CombatTarget`、`EnemyHealth` 和 `EnemyBalance`。
- `EnemyActor.enemyKind` 记录敌人的根类别，当前分为 `APShell`、`APFree`、`DAgent`、`RSUnit`，用于后续创建具体敌人时快速归纳设定来源。
- `EnemyActor.combatRole` 记录敌人的战斗定位，当前分为 `GroundMelee`、`GroundRanged`、`FlyingRanged`，用于区分基础地面近战、地面远程和浮空远程等行为方向。
- `APShell` 表示异常程序显壳态，可以接受运行伤害与结构伤害；`APFree` 表示异常程序游离态，不接受结构伤害。
- `EnemyCombatRole` 只做定位标记和查询，不自动覆盖动作、移动、感知或数值配置；后续如果需要一键套模板，再由独立数据资产承载。
- 敌人类别不表示封装、继承、多态等战斗特性；这些应作为后续独立特性、标签、配置或能力系统处理。
- `EnemyActor` 要求同物体挂载 `CombatTagContainer`，保证正式敌人天然支持战斗标签、持续标签和协议反应。
- `EnemyActor` 持有 `EnemyMotorBase` 和 `EnemyCombatDriver` 引用，状态机通过 Actor 读取敌人能力，而不是直接查找具体实现。
- `EnemyHealth` 负责正式敌人的血量、受击、死亡、死亡事件和白模调试反馈。
- `EnemyHealth` 实现 `IHitReceiver`、`IDamageable` 和 `ICombatTargetLifeState`，不再重复实现目标身份。
- `EnemyHealth` 完成生命重置后会通知状态机同步清理目标、动作、冷却和死亡状态，支持敌人重新启用及后续对象池复用。
- `CombatTarget` 统一提供敌人的根身份、存活/可选状态、锁定点和 Collider 表面距离；敌人死亡后会自动失效。
- `EnemyHealth` 死亡后会立即让目标失效，可选禁用非 Trigger Collider，并在延迟后隐藏敌人根物体，作为当前无死亡动画阶段的最小退场流程。

对应脚本：
- `Assets/_EndLink/Enemies/Base/EnemyActor.cs`
- `Assets/_EndLink/Enemies/Base/EnemyKind.cs`
- `Assets/_EndLink/Enemies/Base/EnemyCombatRole.cs`
- `Assets/_EndLink/Enemies/Base/EnemyHealth.cs`
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
  - `EnemyBalance`
  - `CombatTarget`
  - `CombatTagContainer`
  - Collider
  - Layer 设置为 `Enemy`

关键配置：
- `enemyKind`：敌人根类别；`APShell` / `APFree` 分别代表异常程序显壳态和游离态，`DAgent` 代表受污染智能体，`RSUnit` 代表失控系统单元
- `combatRole`：敌人战斗定位；当前用于标记地面近战、地面远程和浮空远程，不直接改动其它组件配置
- `CombatTarget.lockPoint`：锁定、瞄准和攻击朝向参考点，空则使用敌人根物体
- `motor`：敌人移动能力引用，普通地面敌人拖 `EnemyMotorBase`
- `combatDriver`：敌人战斗执行器引用，需要攻击能力的敌人拖 `EnemyCombatDriver`
- `maxHealth`：敌人最大生命值
- `initialTags`：敌人启用时默认拥有的战斗标签
- `combinationRules`：敌人身上触发协议反应使用的规则
- `disableCollidersOnDeath`：死亡后是否禁用非 Trigger Collider，让死亡敌人不再阻挡角色
- `deactivateOnDeath`：死亡后是否自动隐藏敌人根物体
- `deathDeactivateDelay`：死亡事件触发后等待多久隐藏敌人
- `feedbackRenderer`：受击和死亡变色使用的 Renderer；为空时会自动查找敌人视觉体上的 Renderer，可支持 SkinnedMeshRenderer
- `showHealthInName`：是否在 GameObject 名字上显示血量

</details>

<a id="feature-enemy-balance-stagger"></a>

### Feature：敌人韧性、平衡与失衡
<details>
<summary>展开详情</summary>

功能说明：
- 韧性 `Poise` 是敌人状态机上的隐性阈值，只决定一次有效命中是否触发 `Hit` 受击硬直，不会被消耗。
- 动作的 `HitStrength` 与敌人 `Poise` 比较；达到阈值才进入 `Hit`，伤害高低不再直接决定是否硬直。
- `EnemyBalance` 独立管理可消耗的平衡值。动作的 `BalanceDamage` 会削减平衡，停止受击一段时间后平衡自动恢复。
- 平衡归零后进入独立 `Stagger` 大状态，中断当前动作、停止移动，并在失衡持续时间内开放 `CanBeExecuted`。
- 第一版只提供处决资格和事件，不实现处决输入、处决动画或处决伤害，后续系统无需反向判断状态机即可接入。
- 失衡结束后恢复满平衡；死亡和敌人重置会关闭处决资格，避免对象复用时残留运行状态。

对应脚本：
- `Assets/_EndLink/Enemies/Base/EnemyBalance.cs`
- `Assets/_EndLink/Enemies/Base/EnemyHealth.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyStaggerState.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyStateMachine.cs`
- `Assets/_EndLink/Combat/CombatActionDefinition.cs`

相关物体：
- 正式敌人根物体
  - `EnemyBalance`
  - `EnemyStateMachine`

关键配置：
- `CombatActionDefinition.hitStrength`：动作的单次命中强度，用于和敌人韧性比较
- `CombatActionDefinition.balanceDamage`：动作造成的平衡削减量；设为 0 表示不影响平衡
- `EnemyStateMachine.poise`：敌人的隐性受击抗性阈值
- `EnemyStateMachine.staggerDuration`：失衡持续时间和第一版处决资格窗口
- `EnemyBalance.maxBalance`：最大平衡值
- `EnemyBalance.recoveryDelay`：受击后开始恢复平衡前的等待时间
- `EnemyBalance.recoveryPerSecond`：未失衡时每秒恢复的平衡值

</details>

<a id="feature-enemy-animator"></a>

### Feature：敌人 Animator 桥接
<details>
<summary>展开详情</summary>

功能说明：
- `EnemyAnimatorDriver` 负责把敌人移动速度、移动状态、敌人大状态和战斗动作开始信息同步到 Animator，不决定 AI、状态切换或攻击判定。
- 连续同步 `MoveSpeed`、`IsMoving`、`IsCombatManeuver`、`StateId`、`IsDead`；其中 `IsCombatManeuver` 用于区分追击 Run 和观察/准备阶段的横移 Move。
- `EnemyCombatDriver` 成功开始动作时通过本地事件通知桥接层，写入 `ActionId`、`ActionType` 并触发 `ActionTrigger`，短动作也不会依赖逐帧轮询捕获。
- Animator、移动能力和战斗执行器均支持自动查找；特殊敌人没有移动或攻击能力时，对应引用可以留空。
- Animator Controller 缺少某个协议参数时会跳过写入，可选开启一次性警告排查配置。
- `EnemyAnimatorDriver` 会在 Animator 同物体上自动确保动画事件接收器；Action 设为 `AnimationEventDriven` 后，可由 Clip 事件控制判定窗口、取消通知和动作结束。
- 动画事件接收器通过 `OnAnimatorMove` 读取 Animator 根位移；只有当前 Action 启用 `UseRootMotion` 时，`EnemyMotorBase` 才会用 `CharacterController` 应用水平位移并同步 NavMeshAgent。
- 动作结束、取消、受击打断或死亡后会立即停止接收根位移；Y 轴继续由敌人重力与贴地逻辑管理，动画旋转暂不驱动敌人根物体。

对应脚本：
- `Assets/_EndLink/Enemies/Anime/EnemyAnimatorDriver.cs`
- `Assets/_EndLink/Enemies/Abilities/EnemyMotorBase.cs`
- `Assets/_EndLink/Enemies/Abilities/EnemyCombatDriver.cs`
- `Assets/_EndLink/Control/Animation/CombatAnimationEventReceiver.cs`
- `Assets/_EndLink/Control/Animation/ICombatRootMotionReceiver.cs`
- `Assets/_EndLink/Control/Animation/CombatAnimatorParams.cs`

相关物体：
- 正式敌人根物体
  - `EnemyAnimatorDriver`
- 敌人视觉子物体
  - `Animator`

Animator 参数：
- `MoveSpeed`：Float，当前水平移动速度
- `IsMoving`：Bool，当前是否正在移动
- `IsCombatManeuver`：Bool，当前是否正在执行 `Position` / `Prepare` 阶段的观察或攻击准备机动
- `StateId`：Int，对应 `EnemyStateId`
- `IsDead`：Bool，当前是否处于 Dead
- `ActionId`：Int，当前动作 ActionId 字符串的 Animator Hash
- `ActionType`：Int，对应 `CombatActionType`
- `ActionTrigger`：Trigger，动作成功开始
- `HitTrigger`：Trigger，进入 Hit
- `StaggerTrigger`：Trigger，进入 Stagger；当前 Animator Controller 未配置时会被安全跳过
- `DeadTrigger`：Trigger，进入 Dead

动画事件配置：
- 需要由动画关键帧控制判定的敌人 Action 设为 `AnimationEventDriven`。
- 在攻击 Clip 上依次配置 `OnActionHitboxStart`、`OnActionHitboxEnd`，可选配置 `OnActionCanCancel`，并在收招结束配置 `OnActionEnd`。
- 事件接收组件由 `EnemyAnimatorDriver` 在运行时自动补到 Animator 同物体；动作数据总时长用于漏配结束事件时安全退出。
- 需要实体跟随动画前进的动作额外启用 `UseRootMotion`，再用 `RootMotionScale` 调整移动距离；不需要位移的动作保持关闭。

</details>

<a id="feature-enemy-state-sensor"></a>

### Feature：敌人感知与大状态机

<details>
<summary>展开详情</summary>

功能说明：
- `EnemyStateMachine` 管理 `Idle`、`Alert`、`Combat`、`Hit`、`Stagger`、`Return`、`Dead` 七个敌人大状态。
- `EnemyStateMachine` 集中暴露索敌配置，`EnemyTargetSensor` 只作为执行器读取状态机参数，不在自身 Inspector 中重复配置。
- `EnemyTargetSensor` 负责第一版敌人索敌：玩家进入发现范围后请求进入 `Alert`，持续停留达到警觉时间后请求进入 `Combat`。
- `EnemyTargetSensor` 在 `Idle` / `Alert` 阶段建立目标；进入 `Combat` / `Hit` / `Stagger` 后由状态机持有当前战斗目标，Sensor 仅在该目标失效时重新扫描接管。
- 自动索敌可以在 `EnemyStateMachine` 中关闭，关闭后不会主动触发 `Alert` / `Combat`。
- `Combat` 的最大追击距离以出生区域 `Home` 为圆心计算，不再使用敌人与当前目标的距离；越界后进入 `Return`。
- 战斗目标失效后会等待 `lostTargetDelay`，期间允许 Sensor 重新获取目标；延迟结束仍无目标时进入 `Return`。
- `Return` 会清除战斗目标、取消当前动作并返回 `Home`，抵达出生区域后恢复 `Idle`。
- Return 开始时仍处于警戒范围内的玩家不会立刻重新触发；玩家离开后再次进入范围会转入 `Alert`，警戒失败则继续 Return，警戒完成则重新进入 Combat。
- `Hit` 作为独立大状态处理普通受击打断；`Stagger` 作为更高优先级的平衡归零状态，两者都不放进 Combat 行为树。
- 敌人受到有效伤害时，会优先把当前战斗目标切换为伤害来源；动作 `HitStrength` 达到敌人 `Poise` 才进入 `Hit`，不再按最终伤害值判断。
- `Hit` 状态触发带有短冷却，避免多段 Hitbox 在极短时间内反复刷新受击打断。
- 离开 `Combat`、进入 `Hit` / `Stagger` / `Dead` 或禁用敌人时会取消尚未结束的动作时间线，避免受击、失衡或死亡后继续生成攻击判定。
- 状态机每次启用都会按当前生命状态重新进入初始状态或 `Dead`；生命重置会同步恢复初始状态。

对应脚本：
- `Assets/_EndLink/Enemies/Abilities/EnemyTargetSensor.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyStateMachine.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyStateId.cs`
- `Assets/_EndLink/Enemies/StateMachine/IEnemyState.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyStateBase.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyStateContext.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyIdleState.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyAlertState.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyCombatState.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyHitState.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyStaggerState.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyReturnState.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyDeadState.cs`

相关物体：
- 正式敌人根物体
  - `EnemyStateMachine`
  - `EnemyTargetSensor`

关键配置：
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
- `retargetOnDamage`：受到有效伤害时是否把当前目标切换为伤害来源
- `poise`：敌人的隐性韧性阈值；动作 `HitStrength` 达到该值才触发 `Hit`
- `hitReactCooldown`：两次 `Hit` 触发之间的最短间隔
- `staggerDuration`：平衡归零后保持 `Stagger` 和处决资格的时间
- `homePoint`：敌人的归位参考点；留空时自动记录创建位置
- `maxChaseRadius`：相对 Home 的最大水平追击半径；小于等于 0 表示不限制
- `lostTargetDelay`：战斗目标失效后等待重新获取目标的时间
- `returnStopDistance`：Return 抵达 Home 时允许的水平停止距离

</details>

<a id="feature-enemy-combat-behavior"></a>

### Feature：敌人基础战斗行为
<details>
<summary>展开详情</summary>

功能说明：
- `Combat` 大状态根据 `EnemyActor.combatRole` 选择内部行为：`GroundMelee` 使用 `EnemyCombatBehavior`，`GroundRanged` 和 `FlyingRanged` 使用 `EnemyCombatBehaviorR`；脱战、受击、失衡、死亡和归位仍由外层状态机管理。
- 行为统一使用 `Approach`、`Position`、`Prepare`、`Engage`、`Attack`、`Recover`、`Reposition` 七个内部阶段。
- `Approach` 负责进入战斗位置，`Position` 负责观察和等待许可，`Prepare` 在获得许可后完成攻击预备，`Engage` 修正到动作执行距离，`Attack` 提交动作，`Recover` 等待动作结束，`Reposition` 负责攻击后或站位失效时重新选位。
- 每轮攻击会从 `EnemyCombatDriver` 配置的普攻和技能中选择一次并保持到恢复结束；`combatBasicAttacksBeforeSkill > 0` 时优先按固定普攻次数触发技能，设为 `0` 时改用 `combatSkillChance` 概率规则。
- 固定计数只在动作完整结束后更新，被受击打断不计数；短暂进入 `Hit` 会保留计数，脱战、归位、死亡或完整重置时清零。固定轮到技能但技能仍在冷却时，敌人继续观察等待且不会提前占用攻击许可。
- 定位阶段使用距离滞回：进入距离由本轮动作的 `EffectiveAttackRange - combatAttackInnerOffset` 决定，退出距离由 `EffectiveAttackRange + combatAttackRangeTolerance` 决定，避免敌人在攻击边界反复切换移动和停位。
- 远程行为以当前 Action 的 `EffectiveAttackRange` 作为最远攻击距离，并在状态机配置的最小距离与偏好距离之间维持距离带；目标过近时在 `Engage` 内后撤，不额外增加 `Retreat` 阶段。
- 远程行为攻击前会检查发射点到目标 `LockPoint` 的视线。视线受阻或攻击恢复结束后会向左右选择新的位置，重新获得视线后再申请攻击许可。
- Projectile 在判定实际生成时重新瞄准目标 `LockPoint`，允许朝不同高度发射；敌人根物体仍只做水平转向。
- 没有配置 `EnemyCombatDriver`，或普攻与技能都未配置的敌人，仍保持只追击和面向目标，方便制作不会攻击的测试敌人。
- 行为层当前不包含复杂技能条件或连招，后续可以继续扩展决策规则，或在保持外层状态不变的前提下替换为行为树。

对应脚本：
- `Assets/_EndLink/Enemies/StateMachine/EnemyCombatBehavior.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyCombatBehaviorR.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyCombatState.cs`
- `Assets/_EndLink/Enemies/Abilities/EnemyCombatDriver.cs`
- `Assets/_EndLink/Combat/CombatActionDefinition.cs`

关键配置：
- `combatChaseStopDistance`：Combat 追击时保留的目标表面间隔，实际停止距离会额外加上敌人自身碰撞半径
- `combatAttackRangeTolerance`：Position 退出攻击范围时向外增加的容差，用于距离滞回
- `combatAttackInnerOffset`：Approach 进入 Position 前相对动作极限距离向内靠近的距离
- `combatBasicAttacksBeforeSkill`：大于 0 时，完整执行指定次数普攻后固定释放一次技能；设为 0 时关闭计数
- `combatSkillChance`：未启用固定计数时，每轮普攻和技能都可用时选择技能的概率
- `rangedMinimumDistance`：远程目标进入该表面距离以内时开始后撤
- `rangedPreferredDistance`：远程行为接近或后撤时希望恢复到的目标表面距离
- `rangedRequireLineOfSight` / `rangedObstructionLayers`：是否检查远程攻击视线，以及哪些 Layer 会阻挡射击
- `rangedRepositionDistance`：视线受阻或一次攻击结束后的侧向重新选位距离
- `CombatActionDefinition.EffectiveAttackRange`：当前远程 Action 的最远攻击距离

</details>

<a id="feature-enemy-combat-coordination"></a>

### Feature：敌人围攻协调

<details>
<summary>展开详情</summary>

功能说明：
- `EnemyCombatCoordinator` 是区域级敌人战斗协调器，挂在战斗区域的 `EnemyCoordinator` 物体上，通过半径扫描自动接管范围内敌人。
- 敌人在非战斗状态下可按协调器优先级和距离切换归属；进入 `Combat` / `Hit` / `Stagger` 后会保留当前协调器，避免战斗中围攻规则跳变。
- 同一目标周围的敌人会按距离和等待时间计算攻击评分，竞争有限的攻击许可；同时攻击数量和两次许可授予间隔由区域统一控制。
- 攻击许可包含短时预留。敌人获准后进入 `Engage` 并接近攻击距离，预留到期仍未开始攻击时会自动释放，避免多个敌人在接近途中突破同时攻击上限。
- 未获准攻击的敌人不再全部贴近目标，而是由协调器在目标周围动态分配软站位；软站位不是固定环形槽位。
- 软站位会综合敌人间距和移动成本选择。等待许可的敌人会在软站位距离带内间歇执行缓慢侧移或后撤，不再长期站死；目标明显移动、等待间隔到期、位置拥挤或攻击结束后仍会进入 `Reposition`。
- 敌人获得攻击许可后先进入 `Prepare`：默认观察停顿 `0.5` 秒，再按 `70%` 侧移、`30%` 后撤执行一次短机动，随后进入 `Engage` 接近并攻击。
- `Prepare` 全程占用攻击许可；许可过期或目标失效时会取消当前尝试并重新定位。默认许可预留时间由 `3` 秒提高到 `4` 秒，为准备机动和接近留出余量。
- 攻击恢复结束后，敌人会先释放攻击许可并返回新的软站位，不继续贴在目标旁排队。
- 敌人目标失效、离开 Combat、受击打断、死亡或切换协调器时，会清理攻击许可、候选记录和软站位。
- 没有区域协调器或关闭软站位时，敌人保持原有直接接近并攻击的单敌人行为。
- 运行时 Gizmo 可显示当前目标的等待距离和已分配软站位，便于观察 3-5 个近战敌人的围攻分布。

对应脚本：
- `Assets/_EndLink/Enemies/EnemyCombatCoordinator.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyCombatBehavior.cs`
- `Assets/_EndLink/Enemies/StateMachine/EnemyStateMachine.cs`

相关物体：
- 战斗区域物体 `EnemyCoordinator`
  - `EnemyCombatCoordinator`

关键配置：
- `coordinationRadius`：区域协调器自动接管敌人的半径
- `enemyLayerMask`：区域扫描敌人使用的 LayerMask，通常为 Enemy
- `scanInterval`：自动扫描敌人的间隔，默认 0.5 秒
- `priority`：多个协调器范围重叠时的接管优先级
- `attackCoordinationEnabled`：是否限制同时出手数量
- `maxSimultaneousAttackers`：同一目标最多允许多少个敌人同时进入攻击流程，第一版建议 1-2
- `attackGrantInterval`：同一目标两次授予攻击许可之间的最短间隔
- `attackScoreDistanceWeight` / `attackScoreWaitWeight`：攻击评分中的距离和等待时间权重
- `attackReservationDuration`：许可授予后允许敌人接近并开始攻击的最长时间
- `softPositioningEnabled`：是否启用克制型动态软站位
- `softPositionMinDistance` / `softPositionMaxDistance`：软站位距离目标根节点的水平距离范围
- `softPositionMinSpacing`：等待敌人之间期望保持的最小间距
- `softRepositionInterval`：到位后主动重新定位的随机时间范围
- `softPositionTargetRefreshDistance`：目标移动多远后刷新软站位
- `softPositionArriveDistance`：抵达软站位的允许距离
- `observationPauseInterval`：等待许可时两次观察移动之间的随机停顿范围
- `observationMoveDistance` / `observationMoveSpeedMultiplier`：观察侧移或后撤的距离与速度倍率
- `attackPrepareDelay`：获得许可后开始预备机动前的观察时间，默认 `0.5` 秒
- `attackPrepareMoveDistance` / `attackPrepareMoveDuration` / `attackPrepareSpeedMultiplier`：预备机动的距离、最长时间和速度倍率
- `maneuverRetreatChance`：随机机动选择后撤的概率，默认 `0.3`；其余概率平均分给左右侧移
- `drawSoftPositionGizmos`：是否显示目标等待范围和已分配站位

</details>

<a id="feature-enemy-motor-combat"></a>

### Feature：敌人移动与战斗能力

<details>
<summary>展开详情</summary>

功能说明：
- `EnemyMotorBase` 是第一版地面敌人移动能力组件，基于 `CharacterController` 提供移动、转向、重力和停止能力。
- `EnemyMotorBase` 已预留可选 NavMesh 后端：同物体存在并启用 `NavMeshAgent`、且场景有有效 NavMesh 时，`MoveTo` 会按路径移动；否则保持原有直线 CharacterController 移动。
- `EnemyMotorBase` 支持按“根物体在脚底”的白模约定自动校正 `CharacterController.center.y`，避免第一次移动时因胶囊底部埋入地面而被弹起。
- `EnemyMotorBase` 在水平追击移动后会抑制碰撞带来的异常上抬，重力在 `LateUpdate` 中补充处理。
- `EnemyMotorBase` 在正常移动撞到实现 `IExternalDisplacementReceiver` 的玩家或队友时，会把挡路角色沿敌人移动方向挤开；敌人自身不接收这条外部位移，因此队友和玩家不会反向顶动敌人。
- 敌人受到战斗击退时会停止当前移动和 NavMesh 路径，通过 `CombatKnockbackMotion` 逐帧衰减后退，并持续同步 Agent 位置。
- `EnemyCombatDriver` 是敌人战斗执行器，按 `CombatActionDefinition` 生成 Hitbox、记录冷却并广播动作开始事件。
- `EnemyCombatDriver` 暴露当前动作、执行阶段和取消入口；完整运行时重置会同时清理当前动作与动作冷却。
- 敌人动作支持数据时序或动画事件时序；受击、死亡、脱战和状态退出会中断动作并立即关闭普通驻留 Hitbox，已经发射的 Projectile 不受影响。
- 动画事件动作支持重复配置多组 `HitboxStart / HitboxEnd`，每组生成新的 Hitbox，因此同一 Action 可以完成连续多次命中，同时仍只占用一次攻击许可和一次冷却。
- `EnemyCombatDriver` 提供普通攻击和技能两个动作槽；`EnemyCombatBehavior` 每轮按可用性和技能概率选择动作，Driver 仍只负责动作时序、Hitbox、冷却和事件。

对应脚本：
- `Assets/_EndLink/Enemies/Abilities/EnemyMotorBase.cs`
- `Assets/_EndLink/Enemies/Abilities/EnemyCombatDriver.cs`
- `Assets/_EndLink/Control/IExternalDisplacementReceiver.cs`

相关物体：
- 正式敌人根物体
  - `EnemyMotorBase`
  - 可选 `EnemyCombatDriver`
  - `CharacterController`
  - 可选 `NavMeshAgent`

关键配置：
- `EnemyMotorBase.autoAlignControllerToFeet`：是否自动按脚底根物体约定校正 `CharacterController`
- `EnemyMotorBase.preventPlanarCollisionLift`：是否抑制水平移动碰撞导致的异常上抬
- `EnemyMotorBase.useNavMeshWhenAvailable`：存在有效 `NavMeshAgent` 时是否优先使用 NavMesh 路径移动
- `EnemyMotorBase.navMeshSampleDistance`：敌人当前位置或目标点吸附到最近 NavMesh 的最大搜索距离
- `EnemyMotorBase.pushExternalDisplacementReceivers`：敌人正常移动撞到玩家或队友时，是否把挡路角色挤开
- `EnemyMotorBase.collisionPushMultiplier`：敌人本帧移动量转换为推挤位移的倍率
- `EnemyMotorBase.maxCollisionPushDistance`：单次碰撞最多传递给玩家或队友的位移
- `EnemyMotorBase.combatKnockbackDuration`：战斗击退的衰减持续时间，默认 `0.12` 秒

</details>
