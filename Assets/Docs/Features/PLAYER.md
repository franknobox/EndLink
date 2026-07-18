# Player Features

玩家、3C、玩家状态机和玩家侧战斗接线详情。

主索引见 [FEATURES.md](../FEATURES.md)。

<a id="feature-input-system"></a>

### Feature：新版 Input System 输入读取

<details>
<summary>展开详情</summary>

功能说明：
- 使用已生成的 `InputSystem_Actions` C# 包装类。
- 输入动作以 `InputSystem_Actions.inputactions` 为唯一源头；修改动作或绑定后，需要在 Unity 中重新 Generate C# Class 生成 `InputSystem_Actions.cs`。
- 玩家移动输入和相机输入分开读取，避免输入读取器承担移动或相机逻辑。
- 移动输入读取 `Player/Move`。
- 跳跃输入读取 `Player/Jump`，默认键位为键盘 `Space`、手柄 `buttonSouth`。
- 世界交互输入读取 `Player/Interact`，当前默认键位为键盘 `F` 单击和手柄 `buttonNorth`。
- 攻击输入读取 `Player/Attack`，由状态机统一捕获并写入短时攻击缓冲，再决定是否进入攻击状态。
- 防御输入读取 `Player/Guard`，默认键位为鼠标右键、手柄左扳机，只缓存当前是否按住。
- 目标锁定输入读取 `Player/TargetLock`，默认键位为鼠标中键、手柄右摇杆按下；输入层只缓存按下事件，是否建立硬锁由视角模式决定。
- 硬锁目标切换读取 `Player/Previous` 和 `Player/Next`；默认使用鼠标滚轮上/下或手柄右摇杆左/右，输入层不负责候选目标选择。
- 闪避输入读取 `Player/Dodge`，默认键位为键盘 `Left Ctrl`、手柄 `buttonEast`。
- 主控主动技能读取 `Player/PlayerSkill`，默认键位 Q。
- 队友主动技能读取 `Player/AllySlotASkill` 和 `Player/AllySlotBSkill`；当前不绑定键盘，路由配置入口和手柄绑定继续保留。
- 主控和队友连携请求读取 `Player/PlayerLinkAttack`、`Player/AllySlotALinkAttack`、`Player/AllySlotBLinkAttack`，默认键位 1 / 2 / 3；这些输入不会绕过连携机制直接释放动作。
- 全队终链奥义读取 `Player/PartyUltimate`，默认键位 V，可由 `PartyCombatRouter` 在运行时覆盖。
- 手柄第一版临时绑定：主控技能 `rightShoulder`，队友 A 技能 `leftShoulder`，队友 B 技能 `rightTrigger`，主控/队友连携为 D-Pad 上/左/右，全队极限技为 D-Pad 下；后续可根据实际手柄手感统一调整。
- 相机旋转读取 `Player/Look`。

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
- 暴露即时或指定速度面向世界方向的接口，供攻击开始转向和攻击期间软锁跟随共用。
- 支持移动方向参考，拖入 `Main Camera` 后可实现相机相对移动。
- 支持按住 `Left Shift` 冲刺；当前冲刺作为移动速度修饰，不单独进入状态机大状态。
- 支持基础单段跳，跳跃高度和冷却由 `PlayerController` 配置，垂直速度继续走现有手动重力。
- 支持状态机驱动的闪避位移，闪避期间由 `PlayerDodgeState` 决定方向、速度和持续时间。
- 提供复活和场景入口共用的安全传送入口，传送时会清除水平速度、重力、冲刺、朝向目标和战斗击退残留。
- 魂类硬锁建立后，移动改为目标相对方向：前后输入接近/远离目标，左右输入沿目标周围横移，并持续保持角色正面朝向锁定点。
- 硬锁闪避复用同一目标相对方向；没有移动输入时默认向远离目标的角色后方闪避，过程中不转背。
- 支持接收敌人推挤和移动平台带来的三维外部位移；敌人仍只传水平推挤，电梯可稳定带动玩家上下移动。
- 战斗命中产生的总击退距离会通过 `CombatKnockbackMotion` 在短时间内逐帧衰减执行，不再单帧瞬移。
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
- `jumpHeight`：单次跳跃目标高度
- `jumpCooldown`：两次跳跃之间的最短间隔
- `combatKnockbackDuration`：战斗击退的衰减持续时间，默认 `0.12` 秒
- `movementReference`：移动方向参考，通常拖 `Main Camera`

</details>

<a id="feature-third-person-camera"></a>

### Feature：第三人称视角模式

<details>
<summary>展开详情</summary>

功能说明：
- 基于 Cinemachine 3.1.6。
- 使用 `CinemachineCamera` 和 `CinemachineThirdPersonFollow`。
- 支持越肩、较高、较开阔的第三人称视角。
- 支持自由旋转。
- 支持 pitch 限制，避免镜头过低或过度俯视。
- 支持根据玩家状态机和 `PartyCombatContext` 自动切换镜头距离。
- 移动、攻击、技能、闪避、受击、死亡或处于战斗上下文时使用远景；脱战 Idle 时使用近景。
- 脱战 Idle 持续 `1` 秒后开始缓慢拉近；移动或战斗持续 `0.5` 秒后开始较快拉远，避免短时状态切换造成镜头反复伸缩。
- 启用 `CinemachineThirdPersonFollow` 内置避障，检测 `Default`、`Interactable` 和 `Environment` Layer；遇到障碍时快速拉近，离开障碍后平滑恢复距离。
- 支持鼠标锁定，方便第三人称自由视角操作。
- `PlayerViewController` 提供 `FastAction` 与 `SoulsLike` 两种局外可选模式，建议挂在 `EndLink_3rd_Camera`。
- `PlayerViewController` 独立保存 `FastAction` 与 `SoulsLike` 两套完整预设，切换模式时把所选参数交给 `ThirdPersonCameraController` 执行。
- `ThirdPersonCameraController` 的 Inspector 只保留对象引用和共享输入选项；已由预设接管的镜头参数保持序列化但不再重复显示。
- `FastAction` 使用偏高、偏远、旋转较快的预设；已有组件第一次升级时会自动从当前 `ThirdPersonCameraController` 捕获原调参。
- `SoulsLike` 使用偏近、偏低、旋转较慢的预设；中键建立硬锁后，相机平滑朝向目标锁定点。
- Play Mode 中修改当前模式的预设会立即重新应用，便于分别调教两套镜头。
- 魂类硬锁会同时把锁定点交给 `PlayerController`，统一驱动镜头跟随、角色面向、目标相对移动和定向闪避；解除硬锁或切回高速模式后恢复相机相对自由移动。

对应脚本：
- `Assets/_EndLink/Control/ThirdPersonCameraController.cs`
- `Assets/_EndLink/Control/PlayerCameraInputReader.cs`
- `Assets/_EndLink/Control/PlayerViewController.cs`

相关包：
- `com.unity.cinemachine`：当前版本 `3.1.6`

相关物体：
- `Main Camera`
  - `Camera`
  - `CinemachineBrain`
- 玩家子物体 `CameraTarget`
  - 建议位置在胸口到头部之间，例如本地高度约 `1.65`
- `EndLink_3rd_Camera`
  - `CinemachineCamera`
  - `CinemachineThirdPersonFollow`
  - `PlayerCameraInputReader`
  - `ThirdPersonCameraController`
  - `PlayerViewController`
  - 显式绑定场景中的 `PartyCombatContext`；`PlayerStateMachine` 可从 `followTarget` 自动获取

关键配置：
- `idleDistance`：脱战待机时的近景距离
- `activeDistance`：移动和战斗时的远景距离
- `idleDistanceSmoothTime`：镜头缓慢拉近的平滑时间
- `activeDistanceSmoothTime`：镜头较快拉远的平滑时间
- `minPitch` / `maxPitch`：上下视角限制
- `mouseYawSensitivity` / `mousePitchSensitivity`：鼠标灵敏度
- `gamepadYawSpeed` / `gamepadPitchSpeed`：手柄视角速度
- `targetWorldOffset`：相机目标点高度
- `shoulderOffset`：越肩偏移
- `verticalArmLength`：镜头高度和开阔感
- `cameraSide`：左肩、右肩或居中
- `fieldOfView`：视场角，影响画面开阔程度
- `viewMode`：选择高速自由视角或魂类硬锁视角
- `fastActionSettings`：高速动作模式独立的距离、高度、构图、FOV 与旋转灵敏度
- `soulsLikeSettings`：魂类模式独立的距离、高度、构图、FOV 与旋转灵敏度
- `hardLockRotationSmoothTime`：硬锁镜头跟随目标的平滑时间
- `Avoid Obstacles`：镜头避障开关；当前半径为 `0.25`，进入碰撞阻尼为 `0.05`，离开碰撞阻尼为 `0.5`


</details>

<a id="feature-player-state-machine"></a>

### Feature：玩家有限状态机

<details>
<summary>展开详情</summary>

功能说明：
- 使用代码状态机，不依赖 Animator StateMachine。
- 当前包含 `Idle`、`Move`、`Attack`、`Skill`、`Dodge`、`Guard`、`Hit`、`Dead` 八个状态。
- `Idle` 和 `Move` 会优先消费闪避输入，检查闪避冷却后切换到 `Dodge`。
- `Idle` 和 `Move` 会消费跳跃输入，满足贴地和冷却条件时由 `PlayerController` 写入向上的垂直初速度；第一版不单独进入空中状态。
- 状态机每帧统一捕获攻击输入，并写入默认 `0.15` 秒的短时缓冲；动作暂时不可执行时不会提前消费，成功进入攻击或超时后清空。
- `Idle` 和 `Move` 会在缓冲有效且普攻可执行时切换到 `Attack`。
- `Skill` 是通用技能状态，当前由 `PartyCombatRouter` 发起请求，状态机决定是否进入，进入状态后再调用 `PlayerCombatDriver` 执行技能表现和判定。
- 提供复活重置入口，统一清理动作、连段、防御、输入缓冲和闪避冷却后返回 `Idle`。
- `Attack` 状态按当前连段 Action 调用 `PlayerCombatDriver`；窗口内再次输入普攻会缓存下一段，当前段结束后继续执行。
- 动作开始后会锁定普通状态退出；动画事件动作由 `CanCancel` 打开取消窗口，数据驱动动作则在进入 Recovery 段时自动打开，`ActionEnd` 或数据时序结束负责自然收招。
- 第一版取消策略允许普攻与技能互相派生，并允许在取消窗口切入闪避或防御；具体许可可通过 `actionCancelOptions` 调整，`Hit` 和 `Dead` 始终可以强制打断。
- 动画事件动作不会被 `attackDuration` 或 `skillDuration` 提前切回移动状态。
- 攻击期间移动输入会乘以 `attackMoveInputScale`，当前默认可以做站桩攻击。
- 攻击期间会按 `attackTrackingRotationSharpness` 平滑跟随当前软锁点；目标失效或参数为 `0` 时保持当前朝向。
- `Guard` 由按住防御输入进入，松开后回到 `Move` 或 `Idle`；第一版允许从防御直接闪避。
- `Dodge` 状态负责主控闪避：自由移动时按相机相对输入闪避；硬锁时按目标相对方向定向闪避并保持面向目标；没有输入时默认向角色正后方后撤。
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
- `Assets/_EndLink/Player/StateMachine/PlayerGuardState.cs`
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
- `attackTrackingRotationSharpness`：攻击期间软锁跟随转向速度，`0` 表示关闭持续跟随
- `attackInputBufferDuration`：普攻输入缓冲时间，默认 `0.15` 秒
- `actionCancelOptions`：动作进入取消窗口后允许切入的普攻、技能、闪避和防御类型
- `guardMoveInputScale`：防御期间保留的移动输入倍率，默认 `0`
- `skillDuration`：通用技能状态持续时间
- `skillMoveInputScale`：技能期间移动输入倍率
- `dodgeDuration`：闪避状态持续时间
- `dodgeDistance`：一次闪避期望移动距离
- `dodgeCooldown`：闪避冷却时间
- `dodgeInvincibleDuration`：闪避开始后的临时免伤窗口
- `hitDuration`：受击硬直持续时间
- `hitMoveInputScale`：受击期间移动输入倍率

</details>

<a id="feature-player-act-combat"></a>

### Feature：玩家 ActCombat 基础

<details>
<summary>展开详情</summary>

功能说明：
- `PlayerComboController` 统一管理普攻段数、每段 Action、下一段输入窗口和每段攻击踏步；默认三个空槽会重复使用现有基础普攻，方便先验证三段节奏。
- 下一段已经排队但因配置、冷却或执行条件无法启动时，只会短暂重试；超过等待上限后结束连段并返回移动状态，避免永久停留在 Attack。
- 每一段可单独配置 `CombatActionDefinition`，后续可以逐步替换成不同伤害、Hitbox、前摇和后摇。
- 每段普攻开始时由 `PlayerComboController` 执行短距离前快后慢踏步；有软锁目标时按 Collider 表面距离停止，目标过远时只沿角色正前方移动。
- 攻击状态继续使用已有软锁平滑转向，并在每一段开始时重新取得当前有效目标。
- `PlayerGuardController` 只处理正面命中：进入防御后的短窗口判定为弹反，窗口结束后按配置倍率承受格挡伤害。
- `PlayerGuardController` 会随组件启用和禁用向 `CharacterHealth` 注册或退订命中拦截，关闭格挡能力后不会继续参与受击结算。
- 普通格挡不会触发玩家 Hit 状态，也不接收本次击退；成功弹反完全化解伤害，并让支持弹反反馈的敌人进入现有 Hit 状态。
- 没有正式动画和 VFX 时，防御期间会在玩家正面生成蓝色白模防御弧；普通格挡闪为青色，成功弹反闪为黄色并短暂放大。
- 提供防御开始、结束、普通格挡和弹反成功事件，后续可直接替换或叠加正式动画、音效和 VFX。
- 第一版不包含体力、架势条、攻击派生、格挡动画和独立敌人失衡状态。

对应脚本：
- `Assets/_EndLink/Player/ActCombat/PlayerComboController.cs`
- `Assets/_EndLink/Player/ActCombat/PlayerGuardController.cs`
- `Assets/_EndLink/Combat/Hitbox/IHitInterceptor.cs`
- `Assets/_EndLink/Combat/Hitbox/ICombatParryReceiver.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerAttackState.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerGuardState.cs`

相关物体：
- 玩家根物体
  - `PlayerComboController`
  - `PlayerGuardController`

关键配置：
- `comboActions`：按顺序执行的普攻 Action 列表
- `inputWindowStart` / `inputWindowEnd`：下一段输入窗口
- `queuedStepTimeout`：下一段已排队但无法执行时的最长等待时间，默认 `0.15` 秒
- `stepDistance` / `stepDuration`：每段攻击踏步距离与持续时间
- `targetStopDistance` / `maxTargetAssistDistance`：目标表面停止距离与最大辅助距离
- `parryWindowDuration`：进入防御后的弹反窗口
- `guardAngle`：正面可防御总角度
- `blockedDamageMultiplier`：普通格挡保留伤害倍率
- `showFallbackVisual`：是否显示运行时生成的白模防御弧
- `feedbackLocalOffset` / `feedbackRadius`：白模防御弧相对玩家的位置和大小

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
- 每次 Action 成功开始时同步 `ActionId`、`ActionType` 并触发 `ActionTrigger`，同一 Attack 状态内的后续连段也会重新触发动作动画。
- Animator 没有通用 `ActionTrigger` 参数时，进入 `Attack` / `Skill` 仍会回退使用原有状态 Trigger；`Hit` / `Dead` 保持状态触发。
- 参数名都可以在 Inspector 修改；Animator Controller 不存在对应参数时会安全跳过。
- `CombatAnimatorParams` 定义后续玩家、队友和敌人可共用的 Animator 参数名和 Hash，不绑定具体连段或动画状态名。
- `PlayerAnimatorDriver` 会在 Animator 同物体上自动确保存在 `CombatAnimationEventReceiver`，无需给每个视觉模型重复手动挂载。
- `CombatAnimationEventReceiver` 把 Clip 上的 `OnActionHitboxStart`、`OnActionHitboxEnd`、`OnActionCanCancel`、`OnActionEnd` 转发给角色 Driver。
- `ICombatRootMotionReceiver` 预留 Root Motion 位移接入口，后续可让动画驱动位移再交给角色移动层处理。
- `PlayerStateMachine` 已实现 `ICombatActionLockReceiver`，处理动作开始锁定、取消窗口、自然结束和外部打断。

对应脚本：
- `Assets/_EndLink/Control/PlayerAnimatorDriver.cs`
- `Assets/_EndLink/Control/Animation/CombatAnimatorParams.cs`
- `Assets/_EndLink/Control/Animation/CombatAnimationEventReceiver.cs`
- `Assets/_EndLink/Control/Animation/ICombatAnimationEventListener.cs`
- `Assets/_EndLink/Control/Animation/ICombatRootMotionReceiver.cs`
- `Assets/_EndLink/Control/Animation/ICombatActionLockReceiver.cs`
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
- `actionIdParameter` / `actionTypeParameter` / `actionTriggerParameter`：动作 ID、类型和通用动作触发参数名
- `attackTriggerParameter` / `skillTriggerParameter` / `hitTriggerParameter` / `deadTriggerParameter`：状态进入 Trigger 参数名
- `warnMissingParameters`：缺少 Animator 参数时是否打印警告

动画事件配置：
- 把 Action 的 `Timing Source` 改为 `AnimationEventDriven`。
- 在攻击 Clip 的接触开始帧添加 `OnActionHitboxStart`，接触结束帧添加 `OnActionHitboxEnd`。
- 可派生或取消的帧添加 `OnActionCanCancel`，收招结束帧添加 `OnActionEnd`。
- `startup + active + recovery` 总时长应大致覆盖动画长度，用作事件漏配时的安全退出基准。

</details>

<a id="feature-player-targeting"></a>

### Feature：玩家软锁与硬锁目标

<details>
<summary>展开详情</summary>

功能说明：
- `PlayerTargeting` 是玩家目标选择组件，放在战斗层，同时维护自动软目标和可选的手动硬锁目标。
- 负责按固定刷新间隔搜索并保存当前软锁目标。
- 硬锁建立后优先作为战斗系统的有效目标，不再随自动刷新切换；解除硬锁后立即回退到自动软目标。
- `PlayerViewController` 处于 `SoulsLike` 模式时，鼠标中键或手柄右摇杆按下会切换硬锁。
- 已硬锁时可用鼠标滚轮上/下或手柄右摇杆左/右切换到当前目标对应方向上最邻近的候选目标；没有该方向候选时保持当前目标。
- 硬锁目标会同步交给 `PlayerController`，使玩家移动和闪避使用目标相对方向并持续面向锁定点。
- 硬锁目标死亡、被设为不可选、离开搜索范围、Layer 不匹配或被销毁时会自动解除。
- 默认选择范围内距离玩家最近的敌人，也保留 `CameraForward` 模式用于后续偏动作游戏的视角优先设置。
- 最近目标按 `CombatTarget` 的 Collider 水平表面距离计算，不再按敌人根节点或模型中心距离计算。
- 有有效目标时会在目标朝向玩家一侧的身体表面显示一个最小白点；硬锁与软目标第一版共用该标识。
- `PlayerTargeting` 本身不控制相机、不生成 Hitbox、不决定攻击是否可以释放；相机响应由 `PlayerViewController` 协调。
- 默认搜索 `Enemy` Layer。
- 使用 `Physics.OverlapSphereNonAlloc` 搜索范围内目标，减少运行时 GC。
- `Nearest` 模式按距离最近自动刷新目标，刷新间隔默认 0.2 秒。
- `CameraForward` 模式会同时考虑视角/朝向夹角和距离。
- 当前目标离开搜索范围、Layer 不匹配、死亡、被设为不可选或被销毁时，会自动清除。
- 对外提供软目标获取，以及 `TryAcquireHardLock()`、`ToggleHardLock()`、`SwitchHardLockTarget()`、`ClearHardLock()` 等硬锁入口。
- `PlayerCombatDriver` 会读取当前有效目标；硬锁存在时优先使用硬锁目标，否则使用自动软目标。攻击开始先让玩家正面转向目标，Attack 状态期间继续平滑跟随。

对应脚本：
- `Assets/_EndLink/Player/PlayerTargeting.cs`
- `Assets/_EndLink/Control/PlayerViewController.cs`
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

<a id="feature-player-combat-driver"></a>

### Feature：玩家战斗驱动

<details>
<summary>展开详情</summary>

功能说明：
- `PlayerCombatDriver` 不读取输入，不决定是否能进入攻击状态。
- 状态机决定能否攻击，`PlayerCombatDriver` 只负责执行攻击表现和判定。
- 支持通过 `CombatActionDefinition` 配置普攻、主动技能、连携技的伤害、击退、`CombatTagDefinition` 标签、标签持续时间、冷却、Hitbox 和生成参数。
- `PlayerCombatDriver` 执行的动作必须来自 `CombatActionDefinition`。
- 支持 `DataDriven` 和 `AnimationEventDriven` 两种动作时序；后者由动画事件决定普通 Hitbox 的有效窗口和状态退出。
- 动画事件动作可以重复配置多组 `HitboxStart / HitboxEnd`，用于一招多段命中；每个窗口生成独立 Hitbox，整套动作仍只进入一次状态并记录一次冷却。
- 当前执行内容是生成指定 Hitbox prefab；有自动软锁目标时先让玩家正面转向目标，判定生成瞬间读取角色实时正前方，使前摇期间的跟随转向能同步影响 Hitbox 朝向。
- 普攻、主动技能和连携技按各自 `CombatActionDefinition` 独立记录冷却。
- 暴露只读动作冷却剩余时间、归一化冷却值，以及指定动作的冷却查询，供战斗 UI 区分普攻、技能和连携槽。
- 实现 `ICombatActionExecutor`，状态机通过统一 `CanExecute` / `TryExecute` 入口检查和执行动作。
- 支持通过动作资产中的 `Hitbox Spawn Distance` 和 `Hitbox Spawn Height` 调整 Hitbox 生成位置。
- 生成 Hitbox 后会调用 `HitboxBase.Initialize(gameObject)` 传入攻击者。
- 普通 Hitbox 会在数据有效段或动画判定结束事件后关闭；Projectile 仍按自身生命周期运行。
- 成功执行攻击后会通过 `CombatEventsBus` 广播 `ActionStarted`。
对应脚本：
- `Assets/_EndLink/Player/ActCombat/PlayerCombatDriver.cs`
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
- Hitbox prefab、生成距离、高度、冷却和时序来源从对应的 `CombatActionDefinition` 读取。
</details>

<a id="feature-architecture-boundary"></a>

### Feature：当前架构边界

<details>
<summary>展开详情</summary>

当前约定：
- 输入读取器只读并缓存原始输入，不做状态与动作决策；普攻缓冲有效期由 `PlayerStateMachine` 管理。
- `PlayerStateMachine` 决定当前状态，负责 Idle、Move、Attack 等流程切换。
- `PlayerController` 负责移动能力和朝向，不负责读取输入或判断是否允许移动。
- `PlayerAnimatorDriver` 只负责参数和事件桥接；动作合法性仍由状态机与 Driver 决定，动画事件只提交判定窗口和退出时机。
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
- 动画事件时序已接入，后续仍需补 Hitbox Socket、完整局部偏移和动画位移对齐。
- 战斗事件当前携带基础来源、目标、动作、单个标签和标签层数；后续如果连携规则需要更强表达，可扩展事件上下文或增加规则层数据结构。
- 当前 Hitbox 使用即时 Instantiate/Destroy，后续攻击频繁后建议切换对象池。

</details>
