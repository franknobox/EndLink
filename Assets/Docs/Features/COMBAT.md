# Combat Features

战斗数据、目标、伤害、受击、Hitbox、标签和事件系统详情。

主索引见 [FEATURES.md](../FEATURES.md)。

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
