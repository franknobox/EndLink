# Enemies Features

正式敌人身份、生命、感知、状态机和基础移动详情。

主索引见 [FEATURES.md](../FEATURES.md)。

<a id="feature-enemy-foundation"></a>
<a id="feature-enemy-identity-health"></a>

### Feature：敌人身份与生命目标

<details>
<summary>展开详情</summary>

功能说明：
- `EnemyActor` 是正式敌人的根入口组件，只暴露敌人身份和能力组件引用，并要求同物体存在 `CombatTarget`。
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
- `EnemyDummy` 保留为早期轻量命中测试对象，用于快速验证 Hitbox、扣血和死亡显示；正式敌人能力以本节敌人基底为准。

对应脚本：
- `Assets/_EndLink/Enemies/EnemyActor.cs`
- `Assets/_EndLink/Enemies/EnemyKind.cs`
- `Assets/_EndLink/Enemies/EnemyCombatRole.cs`
- `Assets/_EndLink/Enemies/EnemyHealth.cs`
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
- `feedbackRenderer`：受击和死亡变色使用的 MeshRenderer
- `showHealthInName`：是否在 GameObject 名字上显示血量

</details>

<a id="feature-enemy-state-sensor"></a>

### Feature：敌人感知与大状态机

<details>
<summary>展开详情</summary>

功能说明：
- `EnemyStateMachine` 管理 `Idle`、`Alert`、`Combat`、`Hit`、`Return`、`Dead` 六个敌人大状态。
- `EnemyStateMachine` 集中暴露索敌配置，`EnemyTargetSensor` 只作为执行器读取状态机参数，不在自身 Inspector 中重复配置。
- `EnemyTargetSensor` 负责第一版敌人索敌：玩家进入发现范围后请求进入 `Alert`，持续停留达到警觉时间后请求进入 `Combat`。
- `EnemyTargetSensor` 在 `Idle` / `Alert` 阶段建立目标；进入 `Combat` / `Hit` 后由状态机持有当前战斗目标，Sensor 仅在该目标失效时重新扫描接管。
- 自动索敌可以在 `EnemyStateMachine` 中关闭，关闭后不会主动触发 `Alert` / `Combat`。
- `Combat` 当前负责基础追击、攻击距离停位、面向目标和普通攻击循环；追击位置取自目标 Collider 最近表面点，攻击距离来自敌人 Basic Attack 动作的 `EffectiveAttackRange`。
- `Combat` 的最大追击距离以出生区域 `Home` 为圆心计算，不再使用敌人与当前目标的距离；越界后进入 `Return`。
- 战斗目标失效后会等待 `lostTargetDelay`，期间允许 Sensor 重新获取目标；延迟结束仍无目标时进入 `Return`。
- `Return` 会清除战斗目标、取消当前动作并返回 `Home`，抵达出生区域后恢复 `Idle`。
- Return 开始时仍处于警戒范围内的玩家不会立刻重新触发；玩家离开后再次进入范围会转入 `Alert`，警戒失败则继续 Return，警戒完成则重新进入 Combat。
- 没有配置 `EnemyCombatDriver` 或 Basic Attack 动作的敌人仍保持只追击和面向目标，方便制作不会攻击的测试敌人。
- `Combat` 后续作为行为树的外层挂载点，内部再承载站位、技能、撤退和复杂攻击选择等细节行为。
- `Hit` 作为独立大状态处理受击打断，不放进 Combat 行为树，方便后续加入硬直、霸体、击倒等规则。
- 敌人受到有效伤害时，会优先把当前战斗目标切换为伤害来源；轻击只让敌人接战，重击才进入 `Hit` 状态并短暂停止移动。
- `Hit` 状态触发带有短冷却，避免多段 Hitbox 在极短时间内反复刷新受击打断。
- 离开 `Combat`、进入 `Hit` / `Dead` 或禁用敌人时会取消尚未结束的动作时间线，避免受击或死亡后继续生成攻击判定。
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
- `heavyHitDamageThreshold`：实际伤害达到多少才触发 `Hit` 状态；小于等于 0 表示所有有效伤害都会触发
- `hitReactCooldown`：两次 `Hit` 触发之间的最短间隔
- `combatChaseStopDistance`：Combat 追击时保留的目标表面间隔，实际停止距离会额外加上敌人自身碰撞半径
- `combatAttackRangeTolerance`：Combat 判断普通攻击可进入攻击距离时的容差
- `combatAttackInnerOffset`：Combat 接近攻击目标时相对动作极限距离向内靠近的距离
- `homePoint`：敌人的归位参考点；留空时自动记录创建位置
- `maxChaseRadius`：相对 Home 的最大水平追击半径；小于等于 0 表示不限制
- `lostTargetDelay`：战斗目标失效后等待重新获取目标的时间
- `returnStopDistance`：Return 抵达 Home 时允许的水平停止距离

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
- `EnemyCombatDriver` 是敌人战斗执行器，按 `CombatActionDefinition` 生成 Hitbox、记录冷却并广播动作开始事件。
- `EnemyCombatDriver` 暴露当前动作、执行阶段和取消入口；完整运行时重置会同时清理当前动作与动作冷却。
- `EnemyCombatDriver` 当前由 `EnemyCombatState` 在攻击距离内调用 Basic Attack；后续行为树接入后，出手时机和动作选择会转交给行为层。

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

</details>
