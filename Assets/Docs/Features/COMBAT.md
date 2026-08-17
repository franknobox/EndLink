# Combat Features

战斗数据、目标、伤害、受击、Hitbox、标签和事件系统详情。

主索引见 [FEATURES.md](../FEATURES.md)。

<a id="feature-character-health"></a>

### Feature：通用生命值与角色受击接线

<details>
<summary>展开详情</summary>

功能说明：
- `CharacterHealth` 是玩家、队友和后续更多角色可以复用的通用生命组件。
- `CharacterHealth.Kill()` 提供无视临时免伤的直接死亡入口，供坠落出界等世界规则使用；普通战斗伤害仍统一经过伤害管线。
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
- `AllyHealth` 是队友生命桥接层，订阅 `CharacterHealth` 后把受伤和生命归零转发给 `AllyStateMachine.RequestHit()` / `RequestLinkDown()`。
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
- `AllyHealth.requestLinkDownOnHealthDepleted`：队友生命归零时是否请求进入 LinkDown 状态

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
- 动作配置包含固定伤害 `FlatDamage`、攻击力倍率 `AtkPowerMultiplier`、伤害类型、击退、命中强度 `HitStrength`、平衡伤害 `BalanceDamage`、`CombatTagDefinition` 命中标签、标签持续时间、标签层数、连携协同率收益、冷却、前摇、有效时间、后摇、动画根位移开关与倍率、Hitbox prefab、Hitbox 生成位置、AI 有效攻击距离和可选命中反馈。
- 动作伤害基础公式为 `FlatDamage + AttackPower × AtkPowerMultiplier`，因此可配置纯固定伤害、纯倍率伤害或两者混合。
- 动作配置包含 `TimingSource`：`DataDriven` 使用 `startup / active / recovery` 推进；`AnimationEventDriven` 由动画事件控制判定开始、结束、取消窗口和动作结束。
- 动画事件模式仍使用数据总时长作为安全超时；缺少 `ActionEnd` 时会结束动作并警告，缺少 `HitboxStart` 时不会自动补一次命中。
- `UseRootMotion` 按动作决定是否让角色实体接收动画水平根位移，`RootMotionScale` 用于校正动画原始位移距离；垂直位移和动画旋转暂不接入。
- `SynergyGainOnLink` 只在动作类型为 `LinkAttack` 且连携技成功释放时由 `PartyUltimateContext` 读取，用于提升全队终链奥义协同率。

对应脚本：
- `Assets/_EndLink/Combat/CombatActionDefinition.cs`
- `Assets/_EndLink/Control/Animation/CombatActionTimingSource.cs`

相关资产：
- `CombatActionDefinition` 数据资产：可通过 `Create > EndLink > Combat > Combat Action Definition` 创建
- 推荐存放路径：`Assets/_EndLink/Data/CombatData/Actions`

关键类型：
- `BasicAttack`：普通攻击
- `Skill`：普通技能
- `LinkAttack`：连携攻击
- `Ultimate`：大招

</details>

<a id="feature-player-weapon-forms"></a>

### Feature：三种武器形态基础

<details>
<summary>展开详情</summary>

功能说明：
- `PlayerWeaponController` 是主角武器形态的唯一运行时入口，固定包含 `A`、`B`、`C` 三种形态；当前设计定位分别为标准、射击、重刃。
- A 形态作为基础形态始终解锁；B、C 形态可分别配置进入场景时是否已解锁，当前默认开启以保持 Demo 现有流程。
- 指定形态切换和 LT 射击快捷入口都会经过解锁校验；上一/下一形态切换会自动跳过锁定形态，不会停在不可用动作组上。
- `IsFormUnlocked` 提供统一查询，`UnlockForm` 供后续关卡奖励、角色进度或调试工具在运行时解锁形态；`FormUnlocked` 向 UI 和提示层广播首次解锁。
- 每种形态分别配置独立的普攻连段，具体伤害、平衡伤害、Hitbox 与动作时序继续复用 `CombatActionDefinition`。
- 当前 A 形态使用三段默认近战普攻；B 形态使用原远程弹体动作；C 形态使用原 Q 键近战技能动作。B、C 暂时都作为对应形态的基础攻击执行。
- `PlayerAimController` 承接 B 形态射击瞄准：手柄按住 LT 时会先快捷切换到 B 形态再进入瞄准；鼠标右键只在已经处于 B 形态时进入瞄准，不改变 A/C 形态。屏幕中心射线决定瞄准点，角色持续面向该方向；瞄准时会清除硬锁并临时切换到更近的越肩镜头。
- B 形态必须先瞄准才能执行基础攻击。射击开始时会冻结本次世界空间瞄准点，Projectile 从实际生成位置重新计算三维方向，降低越肩相机与枪口视差；成功射击后退出瞄准，松开瞄准键后才可再次进入。
- A/C 形态继续使用鼠标右键格挡；手柄格挡固定为 LB，不与 LT 瞄准冲突。
- 武器形态动作组只保存普攻连段，不再包含旧主动技能槽位；形态攻击统一从对应连段入口执行。
- `PlayerComboController` 会在连段开始时读取并锁定当前形态的动作列表；`PlayerCombatDriver` 会读取当前形态的起手动作。
- 第一版直接切换只允许在 `Idle`、`Move` 或状态机尚未初始化时进行，避免攻击中途直接替换动作组；后续战斗内切换由玩家状态机在合法取消窗口或形态派生流程中授权。
- 提供指定形态、上一形态和下一形态请求，以及 `FormChanged` 变化事件；当前由 `PlayerInputReader` 提供切换意图，Q / D-Pad Up 切到上一形态，E / D-Pad Down 切到下一形态。
- `PlayerAnimatorDriver` 会向 Animator 写入 `WeaponForm` Int 参数：0 为 A、1 为 B、2 为 C。
- 未挂载 `PlayerWeaponController` 时，现有 `PlayerComboController` 和 `PlayerCombatDriver` 配置继续作为兼容回退，当前场景不会因新增基础组件失效。

对应脚本：
- `Assets/_EndLink/Combat/Weapon/PlayerWeaponController.cs`
- `Assets/_EndLink/Player/ActCombat/PlayerComboController.cs`
- `Assets/_EndLink/Player/ActCombat/PlayerCombatDriver.cs`
- `Assets/_EndLink/Player/ActCombat/PlayerAimController.cs`
- `Assets/_EndLink/Control/PlayerAnimatorDriver.cs`
- `Assets/_EndLink/Control/Animation/CombatAnimatorParams.cs`

相关物体：
- 主角根物体
  - `PlayerWeaponController`
  - `PlayerComboController`
  - `PlayerCombatDriver`
  - `PlayerAimController`

关键配置：
- `initialForm`：进入场景时默认使用的形态
- `formBInitiallyUnlocked`：B 形态进入场景时是否已解锁，默认开启
- `formCInitiallyUnlocked`：C 形态进入场景时是否已解锁，默认开启
- `formA`：A 形态普攻连段，当前配置三段默认近战普攻
- `formB`：B 形态普攻连段，当前配置远程弹体攻击
- `formC`：C 形态普攻连段，当前配置原近战动作
- `PlayerAimController.aimCamera`：用于屏幕中心瞄准射线的实际渲染相机，空时运行期缓存 Main Camera
- `PlayerAimController.aimLayerMask`：瞄准射线可命中的 Layer，建议包含 Enemy、Environment 和 Interactable，不包含 Player
- `PlayerAimController.maxAimDistance`：没有命中物体时的最远瞄准距离
- `PlayerViewController.aimDistance / aimFieldOfView / aimShoulderOffset`：瞄准期间的距离、视场角和越肩构图，只覆盖当前镜头，不改写两套常驻视角预设

</details>

<a id="feature-combat-feedback"></a>

### Feature：通用战斗反馈

<details>
<summary>展开详情</summary>
功能说明：
- `CombatFeedbackDefinition` 是可复用的反馈数据资产，一份配置可以同时描述 Hitstop、镜头冲击、手柄震动、一次性音效和命中 VFX。
- `CombatFeedbackBus` 是通用反馈请求入口。Hitbox、格挡、协议反应和奥义等玩法系统只提交配置、位置、方向、来源与目标，不直接控制具体表现组件。
- `CombatFeedbackDispatcher` 是场景唯一执行器，统一消费请求；重复启用第二个调度器时会自动禁用并给出警告。
- `CombatActionDefinition.HitFeedback` 允许每个 Action 引用一份反馈资产；由该 Action 生成的 Hitbox 成功接触有效目标后自动提交反馈请求。
- Hitstop 使用不受 `Time.timeScale` 影响的真实时间计时；重叠请求保留更强的停顿并延长到最晚结束时间，结束时尽量避免覆盖暂停等外部时间控制。
- Cinemachine Impulse 的单次强度由反馈资产配置，波形、持续时间、传播和通道由场景中的 `CinemachineImpulseSource` 统一配置。
- 手柄震动驱动当前 `Gamepad` 的高低频马达；调度器禁用、失去焦点或震动到期时会主动归零。
- 音效通过调度器的统一 `AudioSource.PlayOneShot` 播放；VFX 以 GameObject prefab 形式生成在命中点，因此可兼容 ParticleSystem 和 Visual Effect Graph prefab。
- 当前 VFX 仍使用 `Instantiate/Destroy`，尚未接入对象池；正式高频特效接入时与 Hitbox 池化一起处理。
- 当前 Action 的 `HitFeedback` 只在 `HitResolution` 为 `Applied` 时触发；格挡、弹反、闪避、免疫和拒绝不会误用普通命中反馈，专用反馈后续从各自规则入口提交。

对应脚本：
- `Assets/_EndLink/Combat/Feedback/CombatFeedbackDefinition.cs`
- `Assets/_EndLink/Combat/Feedback/CombatFeedbackBus.cs`
- `Assets/_EndLink/Combat/Feedback/CombatFeedbackDispatcher.cs`
- `Assets/_EndLink/Combat/CombatActionDefinition.cs`
- `Assets/_EndLink/Combat/Hitbox/HitboxBase.cs`

场景配置：
- 创建一个全局 `CombatFeedback` 物体并挂载 `CombatFeedbackDispatcher`；Unity 会同时补充 `AudioSource` 和 `CinemachineImpulseSource`。
- 在实际使用的 `CinemachineCamera` 上添加 `Cinemachine Impulse Listener` 扩展，否则镜头不会响应 Impulse。
- 通过 `Create > EndLink > Combat > Combat Feedback Definition` 创建反馈资产，并拖到 Action 的 `Hit Feedback` 字段。
- 没有配置 `Hit Feedback` 的 Action 保持原行为，不会产生额外反馈。

关键配置：
- `hitstopDuration` / `hitstopTimeScale`：停顿的真实时长与相对时间倍率
- `cameraImpulseForce`：单次镜头冲击强度
- `lowFrequencyRumble` / `highFrequencyRumble` / `rumbleDuration`：手柄双马达强度和持续时间
- `audioClip` / `audioVolume`：一次性命中音效
- `vfxPrefab` / `vfxLifetime` / `vfxLocalOffset`：命中 VFX 与生命周期
- `CombatFeedbackDispatcher` 的通道开关：可以在场景中独立关闭某类反馈进行对照调试

代码入口：
- `CombatFeedbackBus.Raise(...)`：供格挡、协议反应、奥义和后续特殊表现主动提交反馈
- `CombatFeedbackDispatcher.Play(...)`：供调试工具直接执行完整反馈请求

</details>

<a id="feature-combat-action-executor"></a>

### Feature：统一 Action 执行接口

<details>
<summary>展开详情</summary>

补充更新：
- `PlayerCombatDriver`、`AllyCombatDriver`、`EnemyCombatDriver` 同时支持数据时序和动画事件时序。
- 数据时序跨过 `startup` 后生成判定；动画时序响应 `HitboxStart`、`HitboxEnd`、`CanCancel` 和 `ActionEnd`。
- 动画事件时序允许同一个 Action 重复配置多组 `HitboxStart / HitboxEnd`，每组生成独立 Hitbox 并可以再次命中同一目标；整套动作仍只记录一次冷却和一次动作锁。
- 多个判定窗口复用同一套 Action 伤害、击退、标签与生成参数，因此动作伤害表示每次命中的伤害，不是整套动作总伤害。
- 动作被受击、死亡、状态退出或对象禁用打断时，会清理动作运行时和普通驻留 Hitbox；已经发射的 Projectile 继续独立运行。
- 当前仍由各 Driver 自己持有时序、生成 Hitbox 和记录冷却；后续结合池化与 Hitbox Socket 稳定后再抽公共层。

功能说明：
- `ICombatActionExecutor` 统一提供 `CanExecute`、`TryExecute`、`GetCooldownRemaining` 和 `GetCooldownNormalized`。
- `PlayerCombatDriver`、`AllyCombatDriver`、`EnemyCombatDriver` 均实现该接口，具体 Hitbox 生成、朝向、日志和事件播报仍由各自 Driver 负责。
- 状态机负责角色当前是否允许进入动作状态；执行接口只检查动作配置、Hitbox 资源和动作自身冷却。
- 玩家、队友和敌人都按 `CombatActionDefinition` 独立记录冷却，普攻、技能和连携技不会互相覆盖冷却。
- 玩家和队友状态机在接受动作请求前先检查 `CanExecute`，避免进入状态后才发现动作仍在冷却或缺少资源。
- 战斗 UI 通过统一接口查询对应动作槽位的冷却，不再分别调用不同 Driver 的冷却方法。

对应脚本：
- `Assets/_EndLink/Combat/ICombatActionExecutor.cs`
- `Assets/_EndLink/Player/ActCombat/PlayerCombatDriver.cs`
- `Assets/_EndLink/Ally/AllyCombatDriver.cs`
- `Assets/_EndLink/Enemies/Abilities/EnemyCombatDriver.cs`
- `Assets/_EndLink/UI/PartyUI/UICombatActionSlot.cs`

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
- `IHitReceiver.ReceiveHit` 返回最小 `HitResolution`，统一表达 `Applied`、`Blocked`、`Parried`、`Dodged`、`Immune` 和 `Rejected`。
- `AppliedDamage` 表示目标生命值的实际减少量；过量伤害只记录目标剩余生命，不把公式伤害误报为实扣伤害。
- `Applied`、`Blocked`、`Parried`、`Immune` 视为成立命中并广播 `HitLanded`；`Dodged`、`Rejected` 只广播完整的 `HitResolved`。
- Action 普通命中反馈仅由 `Applied` 触发；战斗标签当前只允许在 `Applied` 和 `Blocked` 时附加。
- 同一个 Hitbox 的六种结算结果都会消耗本次目标记录，避免持续碰撞在单个判定窗口内反复尝试；后续持续伤害区域需要使用独立的间隔命中规则。
- `IHitInterceptor` 允许格挡、弹反和临时护盾动态接入生命结算，返回命中结果、伤害倍率与击退许可。

对应脚本：
- `Assets/_EndLink/Combat/Hitbox/HitResolution.cs`
- `Assets/_EndLink/Combat/Hitbox/IHitReceiver.cs`
- `Assets/_EndLink/Combat/Hitbox/IHitInterceptor.cs`
- `Assets/_EndLink/Combat/CharacterHealth.cs`
- `Assets/_EndLink/Enemies/Base/EnemyHealth.cs`
- Hitbox 实际造成伤害后，通过 `CombatKnockback` 统一计算并分发总击退位移；免伤、无伤害和击退距离为 `0` 时不会产生位移。
- `CharacterHealth` 初始化时自动登记同根物体上已启用的 `IHitInterceptor`，并提供动态注册与退订接口，供格挡、弹反、临时护盾和短暂无敌在运行时接入伤害结算。
- 动态拦截器按注册顺序处理；已禁用或已销毁的拦截器会在命中结算时自动移除，避免继续影响后续受击。
- 普通格挡产生的伤害结果会标记 `WasBlocked`，玩家生命桥接层不会因此进入 Hit 状态。
- 第一版最终击退距离为 `基础击退距离 × CharacterStats.KnockbackTakenMultiplier`，未挂载 `CharacterStats` 的目标默认按 `1` 倍处理。
- `CombatKnockback` 会先通过 `CombatTarget` 归一到目标 `RootTransform`，再向父级查找 `CharacterStats` 和 `ICombatKnockbackReceiver`，避免命中子 Collider 时击退丢失。
- `ICombatKnockbackReceiver` 只负责攻击命中的战斗击退，与敌人移动碰撞使用的外部推挤接口保持分离。
- `CombatKnockbackMotion` 会把总击退距离按先快后慢的曲线拆成逐帧水平位移，并保证累计位移不变；连续受击会叠加剩余位移并刷新持续时间。
- `PlayerController` 和 `EnemyMotorBase` 默认在 `0.12` 秒内执行衰减击退；敌人执行时会停止当前路径并同步 `NavMeshAgent`。
- `AllyFollowMotor` 仍实现同一击退接口，但暂时保留瞬时位移，等队友战斗表现继续开发时再接入共享衰减运动。
- 当前击退只处理 XZ 平面，不包含击飞或持续物理受力。
- 当前不新增硬直等级、可打断规则或额外受击组件；现有 Hit 状态行为保持不变。
- 死亡后的 Collider 开关逻辑保持现状，本次没有修改。

对应脚本：
- `Assets/_EndLink/Combat/Hitbox/CombatKnockback.cs`
- `Assets/_EndLink/Combat/Hitbox/IHitInterceptor.cs`
- `Assets/_EndLink/Combat/Hitbox/ICombatParryReceiver.cs`
- `Assets/_EndLink/Combat/Stats/CharacterStats.cs`
- `Assets/_EndLink/Combat/CharacterHealth.cs`
- `Assets/_EndLink/Enemies/Base/EnemyHealth.cs`
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
- `CombatTagCombinationRule` 描述 A + B 触发反应效果的规则，可配置源标签所需层数、规则优先级，以及一组 `CombatTagReactionEffect`。
- 多条规则同时满足时，`CombatTagContainer` 会选择优先级最高的规则触发；优先级相同时保持配置列表顺序。
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

<a id="feature-combat-events-bus"></a>

### Feature：战斗事件总栈

<details>
<summary>展开详情</summary>

补充更新：
- `HitResolved` 会携带 `HitResolution`，六种结果都会广播，适合调试、统计和后续单人战斗流程判断。
- `HitLanded` 只在正常命中、格挡、弹反和免疫时广播，继续供战斗上下文和既有响应逻辑使用。
- `Combat Monitor` 增加命中结算筛选与结果列，Console/HUD 调试日志也会显示结果和实际伤害。

功能说明：
- `CombatEventsBus` 是全局战斗事件广播入口。
- `CombatEvent` 是一条战斗事件的数据结构，包含事件类型、来源、目标、动作配置、战斗标签、伤害、命中信息和时间戳。
- `CombatEventType` 目前包含 `ActionStarted`、`HitResolved`、`HitLanded`、`Damaged`、`Dead`、`TagAdded`、`TagRemoved`、`TagExpired`、`ReactionTriggered`。
- `CombatEventLog` 是白模阶段用的 Console 日志监听器，默认不打印，必要时手动开启。
- `CombatMonitorWindow` 是 Editor 战斗事件监视窗口，通过 `EndLink > Debug > Combat Monitor` 打开；支持分别筛选动作、命中结算、成立命中、伤害、死亡、标签和协议反应，并可复制当前事件报告。
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
- `CombatEventsBus.RaiseHitResolved(...)`
- `CombatEventsBus.RaiseHitLanded(...)`
- `CombatEventsBus.RaiseDamaged(...)`
- `CombatEventsBus.RaiseDead(...)`
- `CombatEventsBus.RaiseTagAdded(...)`
- `CombatEventsBus.RaiseTagRemoved(...)`
- `CombatEventsBus.RaiseTagExpired(...)`
- `CombatEventsBus.RaiseReactionTriggered(...)`

</details>

<a id="feature-hitbox"></a>

### Feature：基础 Hitbox 配置

<details>
<summary>展开详情</summary>

补充更新：
- Hitbox 接触后先调用 `IHitReceiver.ReceiveHit`，取得最终 `HitResolution` 后再广播事件、播放普通反馈和附加标签。
- 六种结果都会记入当前 Hitbox 的已处理目标集合；普通反馈和 `onHit` 仅在 `Applied` 时触发，标签仅在 `Applied`、`Blocked` 时触发。

- 数据驱动的标准近战/驻留 Hitbox 使用动作 `ActiveTime` 作为本次生命周期。
- 动画驱动的标准 Hitbox 由 `HitboxEnd` 或 `ActionEnd` 主动关闭，同时保留动作总时长后的防泄漏超时。
- `HitboxProjectile` 不读取动作 `ActiveTime`，仍使用 prefab 自身的 `lifetime` 与 `maxDistance` 控制飞行寿命。
- 没有动作上下文时，Hitbox 仍回退使用 prefab 自身的 `lifetime`，方便独立测试和特殊用法。
- 玩家 Hitbox 会把当前 A/B/C 武器形态独立提交给 `ObjInteractable`，该世界交互分支不占用战斗目标 Layer 和伤害接口；远程弹体成功触发物体交互后也会遵循 `Destroy On Hit`。

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
- `Applied` 命中后触发 `UnityEvent<Collider>`，方便后续挂音效、特效或调试组件。
- 所有受击结果都会广播 `HitResolved`；只有正常命中、格挡、弹反和免疫会继续广播 `HitLanded`。
- 命中来自带 `HitFeedback` 的 Action 且结果为 `Applied` 时，会通过 `CombatFeedbackBus` 提交命中点反馈请求。

对应脚本：
- `Assets/_EndLink/Combat/Hitbox/HitboxBase.cs`
- `Assets/_EndLink/Combat/Hitbox/HitboxProjectile.cs`
- `Assets/_EndLink/Combat/Hitbox/HitboxHitInfo.cs`
- `Assets/_EndLink/Combat/Hitbox/HitResolution.cs`
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
- `knockbackForce`：基础总击退距离，最终位移会乘以受击者 `CharacterStats.KnockbackTakenMultiplier`
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
