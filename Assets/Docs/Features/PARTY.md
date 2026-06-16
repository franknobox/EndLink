# Party Features

固定三人小队、小队战斗上下文和连携窗口详情。

主索引见 [FEATURES.md](../FEATURES.md)。

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
- 成功释放连携技后，`PartyUltimateContext` 会读取本次 `LinkAction` 的 `SynergyGainOnLink`，增加全队协同率。
- 协同率达到 100% 后，`PartyUltimateContext` 标记终链奥义可释放；当前第一版按 `PartyCombatRouter` 配置的奥义键只消耗就绪状态并广播事件，不要求目标，也不执行具体奥义表现。
- `PartyLinkContext` 暴露窗口是否开启、剩余时间、归一化剩余时间和目标解析接口，供后续连携 UI 使用。
- `PartyUltimateContext` 暴露当前协同率、协同率上限、归一化进度、奥义就绪事件和奥义消耗事件，供后续 UI、镜头和奥义表现接入。

对应脚本：
- `Assets/_EndLink/Party/PartyLinkContext.cs`
- `Assets/_EndLink/Party/PartyUltimateContext.cs`
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
  - `PartyUltimateContext`

关键配置：
- `PartyLinkContext.linkWindowDuration`：协议反应触发后的共享连携窗口，默认 4 秒
- `PartyUltimateContext.maxSynergyRate`：协同率上限，默认 100
- `CombatActionDefinition.SynergyGainOnLink`：连携动作成功释放后增加的协同率，只对 `LinkAttack` 类型动作生效
- `PlayerCombatDriver.LinkAction`：主控连携技动作
- `AllyCombatDriver.LinkAction`：对应队友连携技动作
- `PartyCombatRouter` 的 `1` / `2` / `3` 键位：分别选择主控、队友 A、队友 B 的连携技
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
- `PartyCombatRouter` Inspector 中可以覆盖 Q/E/F、1/2/3 和全队终链奥义对应的键位。
- `LinkAttack` 命令只有在 `PartyLinkContext` 窗口开启时才会被接受；请求成功后由对应角色状态机执行 `LinkAction` 并消费共享窗口。
- `PartyUltimateContext` 维护全队协同率；成功释放连携技会按 `CombatActionDefinition.SynergyGainOnLink` 充能，奥义键在满值后消耗奥义就绪状态。
- 后续队友 AI、连携规则或调试工具需要知道“谁是主控，谁是队友”时，可以从 `PartyManager` 查询。

对应脚本：
- `Assets/_EndLink/Party/PartyManager.cs`
- `Assets/_EndLink/Party/PartyFormationSlot.cs`
- `Assets/_EndLink/Party/PartyFollowSettings.cs`
- `Assets/_EndLink/Party/PartyCombatRouter.cs`
- `Assets/_EndLink/Party/PartyCombatContext.cs`
- `Assets/_EndLink/Party/PartyLinkContext.cs`
- `Assets/_EndLink/Party/PartyUltimateContext.cs`
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
- `playerSkillKey` / `allySlotASkillKey` / `allySlotBSkillKey`：主控和两个队友主动技能键位，默认 Q / E / F
- `playerLinkAttackKey` / `allySlotALinkAttackKey` / `allySlotBLinkAttackKey`：主控和两个队友连携请求键位，默认 1 / 2 / 3
- `partyUltimateKey`：全队终链奥义键位，默认 V
- `PartyUltimateContext.maxSynergyRate`：终链奥义协同率上限，默认 100
- `CombatActionDefinition.SynergyGainOnLink`：各连携技自己的协同率收益
- `logInitialization`：是否打印小队初始化日志
- `logCommands`：是否打印小队战斗命令路由日志

</details>
