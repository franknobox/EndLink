# Ally Features

队友状态机、助战、跟随表现、小队管理、战斗上下文和连携窗口详情。

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
- `AllyCombatDriver` 当前使用自动助战动作槽位，并根据 `CombatActionDefinition` 生成 Hitbox、写入伤害、击退、战斗标签和标签持续时间；旧主动技能与连携动作槽暂不配置。
- 队友进入助战流程只要求配置了 `Assist Action`；动作冷却只影响实际出手时间，冷却未结束时会在 Assist 内等待，而不是放弃助战。
- `AllyCombatDriver` 按 `CombatActionDefinition` 分别记录动作冷却。
- `AllyCombatDriver` 支持数据或动画事件时序，并在助战取消、受击、链接中断或组件禁用时关闭当前普通判定；Projectile 继续独立运行。
- 动画事件动作可以在一次执行中重复开启多组独立 Hitbox 窗口，用于队友的多段攻击；整套动作仍只记录一次动作冷却。
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
- `Assets/_EndLink/Ally/Party/PartyCombatContext.cs`
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

<a id="feature-party-link-context"></a>

### Feature：连携触发与窗口

<details>
<summary>展开详情</summary>

功能说明：
- 标签组合规则成功执行后会广播 `ReactionTriggered`，事件携带触发来源、反应目标、主要结果标签和对应的 `CombatTagCombinationRule`。
- `PartyLinkContext` 监听协议反应事件，并为主控和两个队友同时开启一个全队共享的 4 秒连携窗口。
- 窗口期内再次触发协议反应会把剩余时间刷新为完整 4 秒，并继续记录新的反应目标。
- 当前主动释放入口已停用：`PlayerInputReader` 不再读取连携请求，`PartyCombatRouter` 不再配置 1/2/3 或手柄连携键位。
- 只有一个有效反应目标时默认攻击该目标；记录了多个反应目标时优先攻击主控当前软锁目标。
- 反应目标死亡或失效不会关闭窗口；没有有效反应目标时会回退到当前软锁目标，没有任何有效目标时保留窗口但拒绝本次释放。
- 主控和队友 CombatDriver 上的 `LinkAction` 配置已移除；窗口、目标解析和协同率数据接口仅作为后续恢复连携机制的基础保留。
- 协同率达到 100% 后，`PartyUltimateContext` 标记终链奥义可释放；当前第一版按 `PartyCombatRouter` 配置的奥义键只消耗就绪状态并广播事件，不要求目标，也不执行具体奥义表现。
- `PartyLinkContext` 暴露窗口是否开启、剩余时间、归一化剩余时间和目标解析接口，供后续连携 UI 使用。
- `PartyUltimateContext` 暴露当前协同率、协同率上限、归一化进度、奥义就绪事件和奥义消耗事件，供后续 UI、镜头和奥义表现接入。

对应脚本：
- `Assets/_EndLink/Ally/Party/PartyLinkContext.cs`
- `Assets/_EndLink/Ally/Party/PartyUltimateContext.cs`
- `Assets/_EndLink/Ally/Party/PartyCombatRouter.cs`
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
  - `PartyUltimateContext`

关键配置：
- `PartyLinkContext.linkWindowDuration`：协议反应触发后的共享连携窗口，默认 4 秒
- `PartyUltimateContext.maxSynergyRate`：协同率上限，默认 100
- `CombatActionDefinition.SynergyGainOnLink`：连携动作成功释放后增加的协同率，只对 `LinkAttack` 类型动作生效
- `PartyCombatRouter.partyUltimateKey`：尝试释放全队终链奥义，默认 V

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
- `Assets/_EndLink/Ally/Party/PartyCombatContext.cs`
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

<a id="feature-party-manager"></a>

### Feature：固定三人小队管理

<details>
<summary>展开详情</summary>

功能说明：
- `PartyManager` 是固定三人小队的场景级管理入口。
- 第一版只支持固定主控 + 2 个固定队友，不做主控切换、入队离队或复杂编队。
- `PartyFormationSlot` 保存单个队友槽位，包含槽位名、队友状态机和队形偏移。
- 队友根物体或 `AllyStateMachine` 被禁用时，槽位会保留配置但暂时视为非运行成员；跟随初始化、动态站位、战斗路由和队友 UI 会自动跳过该队友。
- 初始化时，`PartyManager` 会把 `mainCharacter` 设置为两个队友的跟随目标。
- 初始化时，`PartyManager` 会把两个槽位的 `formationOffset` 写入各自队友的 `AllyFollowMotor`。
- 初始化时，`PartyManager` 会把统一的 `PartyFollowSettings` 写入两个队友的 `AllyFollowMotor`。
- 支持两个队友的动态站位槽位交换：运行中会比较当前分配和交换后的移动代价，在收益足够且主控不处于队友死区内时交换左右后方槽位。
- `PartyManager` 提供轻量队伍查询：主控引用、已配置队友数量、存活/有效队友数量、存活队友列表和队友是否存活。
- `PartyManager` 提供最小小队表现调度入口：`NotifyCombatStarted`、`NotifyCombatEnded`、`NotifyMemberDead`，供后续 UI、镜头、语音和站位表现监听。
- `PartyManager` 持有 `PartyCombatRouter` 引用，供战斗 UI 和后续小队系统读取当前键位路由。
- `PartyCombatContext` 作为小队战斗状态上下文，供队友目标选择、战斗 UI 和后续连携系统读取当前主目标、已知敌人和战斗状态。
- `PartyCombatRouter` 负责把 `PlayerInputReader` 中的战斗输入翻译成主控、队友 A、队友 B 或全队的命令请求。
- `PartyCombatRouter` 不直接生成 Hitbox，不处理伤害或标签；当前命令类型中已移除连携入口。
- 旧主控/队友 `Skill` 命令、动作引用和键位当前均停用；路由代码壳暂时保留，供后续单人技能方案或小队实验重新设计时评估。
- `PartyCombatRouter` Inspector 当前只保留旧技能空键位和全队终链奥义键位；连携键位配置已移除。
- `PartyUltimateContext` 继续保留协同率数据能力，但当前没有连携释放入口为其充能。
- 后续队友 AI、连携规则或调试工具需要知道“谁是主控，谁是队友”时，可以从 `PartyManager` 查询。

对应脚本：
- `Assets/_EndLink/Ally/Party/PartyManager.cs`
- `Assets/_EndLink/Ally/Party/PartyFormationSlot.cs`
- `Assets/_EndLink/Ally/Party/PartyFollowSettings.cs`
- `Assets/_EndLink/Ally/Party/PartyCombatRouter.cs`
- `Assets/_EndLink/Ally/Party/PartyCombatContext.cs`
- `Assets/_EndLink/Ally/Party/PartyLinkContext.cs`
- `Assets/_EndLink/Ally/Party/PartyUltimateContext.cs`
- `Assets/_EndLink/Ally/AllyFollowMotor.cs`
- `Assets/_EndLink/Ally/AllyStateMachine.cs`

相关物体：
- 场景管理物体
  - `PartyManager`
  - `PartyCombatRouter`
  - `PartyLinkContext`
  - `PartyUltimateContext`
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
- `playerSkillKey` / `allySlotASkillKey` / `allySlotBSkillKey`：旧主动技能键位，当前全部为 `None`
- `partyUltimateKey`：全队终链奥义键位，默认 V
- `PartyUltimateContext.maxSynergyRate`：终链奥义协同率上限，默认 100
- `CombatActionDefinition.SynergyGainOnLink`：各连携技自己的协同率收益
- `logInitialization`：是否打印小队初始化日志
- `logCommands`：是否打印小队战斗命令路由日志

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
- `Action` 是队友通用单次动作状态，当前主要为连携等非自动助战动作保留；队友主动技能暂时停用，动作窗口结束后回到 Assist 或 Follow / Idle。
- 目标死亡、目标丢失或主控距离过远时，助战流程会取消并回到 Follow / Idle。
- `Hit` 表示队友受击硬直状态，可打断 Follow、Assist 和 Action；结束后优先恢复被打断前的 Assist，目标失效或主控过远时回到 Follow / Idle。
- 离开 `Assist` / `Action`、进入 `Hit` 或 `LinkDown` 时会取消当前动作，避免状态切换后残留 Hitbox。
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
- `Assets/_EndLink/Ally/Party/PartyFollowSettings.cs`
- `Assets/_EndLink/Ally/Party/PartyManager.cs`

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
