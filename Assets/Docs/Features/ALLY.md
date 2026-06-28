# Ally Features

队友状态机、助战和跟随表现详情。

主索引见 [FEATURES.md](../FEATURES.md)。

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

<a id="feature-ally-state-machine"></a>

### Feature：队友有限状态机

<details>
<summary>展开详情</summary>

功能说明：
- `AllyStateMachine` 是队友专用有限状态机，不依赖玩家输入系统。
- 当前包含 `Idle`、`Follow`、`Assist`、`Action`、`Hit`、`LinkDown` 六个外层状态。
- `Idle` 表示没有跟随目标的待机状态。
- `Follow` 在有跟随目标时每帧调用 `AllyFollowMotor.TickFollow(deltaTime)`，实际移动由跟随移动组件负责。
- `Assist` 是队友助战大状态，内部先接近目标，进入攻击距离后持续攻击；目标拉开距离后在 Assist 内部回到接近阶段。
- `Assist` 当前内部使用轻量 `Approach / Attack` 阶段，后续可以替换为行为树。
- `Action` 是队友通用动作状态，用于承接队友主动技能；当前不绑定键盘，进入时执行一次 `CombatActionDefinition`，动作窗口结束后回到 Assist 或 Follow / Idle。
- 目标死亡、目标丢失或主控距离过远时，助战流程会取消并回到 Follow / Idle。
- `Hit` 表示队友受击硬直状态，可打断 Follow、Assist 和 Action；结束后优先恢复被打断前的 Assist，目标失效或主控过远时回到 Follow / Idle。
- `LinkDown` 是队友生命归零后的链接中断状态，不再响应跟随、助战、动作和受击请求；队友不按普通死亡消失，后续会接救助交互和半透明漂浮表现。
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
- `Assets/_EndLink/Ally/AllyLinkDownState.cs`
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
- `AllyFollowMotor` 是队友跟随移动执行组件，当前优先使用 `NavMeshAgent` 做地形寻路，同时保留原有直线移动回退。
- 支持使用 `CharacterController.Move` 移动；如果队友没有 `CharacterController`，则直接修改 `Transform.position`。
- 跟随移动已补上最小贴地重力和向下附着力，队友在坡道、平台边缘和高低差过渡处会稳定贴地，不再长时间悬空滑行。
- NavMesh 分支现在实际使用导航结果的 3D 位移与高度，不再只把 NavMesh 当作平面朝向参考，因此坡道和平台跟随会按路径高度移动。
- `AllyStateMachine` 负责保存跟随目标并同步给 `AllyFollowMotor`。
- `PartyManager` 通过 `PartyFollowSettings` 统一配置两个队友的跟随参数，并在初始化时写入各自的 `AllyFollowMotor`。
- `AllyFollowState` 每帧调用 `TickFollow(deltaTime)`，因此 Assist、Action、Hit、LinkDown 状态不会继续抢跟随移动。
- 助战接近状态会调用 `TickMoveToPosition(position, arriveDistance, deltaTime)`，让队友临时移动到敌人附近而不修改主控跟随目标；在有 NavMesh 时会按目标所在高度采样，减少坡面和高台接近失真。
- 队友会移动到主控的本地队形偏移范围，移动时面向移动方向，停下后的朝向由 `idleFacingMode` 决定。
- 支持 `arrivalSmoothTime` 平滑加减速，降低接近队形点时的机械感。
- 支持主控冲刺同步：主控在 Move 状态按住冲刺时，队友 Follow 状态下的跟随速度会乘以 `sprintSyncSpeedMultiplier`。
- 支持 `catchUpDistance` 和 `catchUpSpeedMultiplier`，队友落后较远时会加速追上。
- 支持 `teleportDistance`，队友极端远离队形点时会直接归位，避免长距离丢失。
- 支持 `followSlotSoftness`，队友进入队形点周围软半径后就算到位，不强制踩死精确坐标。
- 支持 `followDeadZoneRadius`，每个队友站定后会以自己的站位作为死区中心；主控仍在该半径内移动时不会触发该队友重新跟随，也不会跟随主控转向；走出半径后才更新队形点和朝向。
- 当前死区对明显高低差做了额外处理：主控已稳定落地到另一层高度时，会提前打破死区，让队友开始上坡或上平台跟随。
- 归位过程中会同步更新死区圆心，避免动态槽位或重新归位后残留旧死区中心。
- `AllyFollowMotor` 会常驻绘制跟随死区 Gizmo，运行时以该队友当前死区中心为圆心，非运行时以队友自身为圆心；当前使用深蓝色常态显示，不再依赖选中状态。
- 支持第一版简易避让：离主控太近时会被推开，配置 `avoidanceLayerMask` 后也能对其他队友做局部排斥。
- 支持接收敌人推挤和移动平台带来的三维外部位移，并在位移后同步跟随死区与 NavMeshAgent。
- `formationOffset` 由 `PartyManager` 的队友槽位统一配置，并写入 `AllyFollowMotor`。
- `SetFormationOffset` 在偏移未变化时不会重复触发重新归位，降低动态槽位评估带来的抖动。
- 当前的跟随规则层仍然由 `AllyFollowMotor` 自己负责死区、追赶、瞬移归位、局部避让和动态站位；`NavMeshAgent` 只负责把这些目标点转成可走路径，避免队友直穿坡体、跑进空中或贴着障碍走直线。

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
