# EndLink 问题记录

本文档用于记录开发中出现过的系统性问题、技术债、设计债和长期工程注意事项。

遇到“越改越乱”“多个系统互相牵连”“问题反复修补”的情况时，优先在这里补一条记录，避免同类问题反复消耗时间。

## 已收束问题

### 队友跟随、敌人移动碰撞与索敌配置边界

- 早期队友跟随死区、动态站位、避让、敌人碰撞推挤和敌人索敌配置曾连续互相影响。
- 当前边界已基本收束：
  - `PartyManager` 负责小队关系、槽位和统一跟随参数。
  - `AllyFollowMotor` 负责队友跟随移动、死区、局部避让和队友 NavMesh 接线。
  - `EnemyMotorBase` 负责敌人自身移动，以及敌人移动时挤开挡路角色。
  - `EnemyStateMachine` 负责敌人大状态和敌人集中配置。
  - `EnemyTargetSensor` 只执行索敌检测，不再持有主要 Inspector 配置。
- 后续修改移动/碰撞相关逻辑时，仍需要回归验证：
  - 队友死区是否稳定。
  - 队友是否会因主角轻微移动而大幅调整站位。
  - 队友和玩家是否仍不能反向顶动敌人。
  - 敌人正常移动是否能挤开挡路玩家或队友。
  - 敌人和队友是否还会异常浮空、穿坡或停在空中。

### 动作时序第一版接入

- `startup / active / recovery` 的第一版动作时序已经接入玩家、队友和敌人的 CombatDriver。
- 动作开始后会先进入 `startup`，前摇结束时再生成 Hitbox；`active` 可以覆盖本次非弹体 Hitbox 的运行时生命周期。
- 这部分不再作为“未完成动作时序”问题记录；剩余问题转入 Hitbox 池化、动画事件校正和动作执行入口统一。

## 冻结债务

### 队友系统方向冻结

- 当前战斗方向更偏单人动作，优先做好主角战斗、敌人战斗和核心 3C。
- 队友短期保持白模和功能实验单位定位，用于验证连携、标签反应、AI 助战和小队 UI，不追求完整表现。
- 队友系统短期只修阻断问题，不主动做大规模表现完善或结构重构。

### AllyFollowMotor 过大

- `AllyFollowMotor` 当前同时承担跟随、死区、动态归位、冲刺同步、追赶、传送、避让、NavMesh、战斗位置移动、击退接收、重力、朝向和 Gizmo 绘制。
- 该问题仍然存在，并且已经接入 NavMesh，属于明确技术债。
- 当前不立即拆分，避免重新引入已经稳定过的跟随问题。
- 后续重新推进队友表现、复杂队友 AI 或特色 AI 实验时，再拆为更小模块：
  - 跟随目标与死区。
  - 队形槽位解析。
  - NavMesh / 地形移动。
  - 局部避让。
  - 战斗位置移动。
  - Gizmo 与调试显示。

### PartyCombatContext 集合优化

- `PartyCombatContext` 当前用 `List<Transform>` 保存已知敌人，并在清理目标时使用线性查找。
- 当前白模阶段敌人数量少，不是性能瓶颈。
- 后续如果一场战斗中已知敌人数量明显增加，再改为 `HashSet<Transform>` 或 `HashSet<ICombatTarget>`，并保留必要的有序主目标列表。

## 仍有效技术债

### 格挡与 Hitbox 后续效果拦截

- 第一版格挡弹反已在 `CharacterHealth` 扣血前处理伤害倍率和击退，并避免普通格挡进入玩家 Hit 状态。
- `HitLanded` 广播和 Hitbox 附带标签目前仍按既有命中流程执行；后续正式设计防御、异常状态和完美弹反时，需要统一定义哪些命中后效果应被格挡或弹反阻断。

### Hitbox 池化与统一创建入口

- 玩家、队友和敌人的动作执行仍会直接 `Instantiate` Hitbox。
- Hitbox 生命周期结束后仍直接 `Destroy`，高频动作或多敌人压测时可能带来 GC 和 CPU 抖动。
- 后续需要建立 Hitbox 池化和统一创建入口，优先覆盖近战波与远程飞行 Hitbox。
- 接入 Animator 后，允许动画事件覆盖或校正 `CombatActionDefinition` 的数据时序。

### CombatDriver 重复

- `PlayerCombatDriver`、`AllyCombatDriver`、`EnemyCombatDriver` 都包含动作冷却、Hitbox 生成、事件上报和动作时序推进等相似逻辑。
- 当前玩家、队友、敌人的动作表现和后续动画事件接线仍未完全稳定，暂不抽基类。
- 后续做 Hitbox 池化、动画事件和敌人攻击表现统一时，再抽 `CombatExecutionUtility` 或 `CombatDriverBase`。

### 自动化验证缺位

- 项目已保留 `com.unity.test-framework`，但当前还没有稳定的 Runtime/EditMode 测试程序集和基础回归测试。
- 后续优先补 `DamageCalculator`、`CombatTagContainer`、`PartyLinkContext`、`ActionCooldownTracker` 等纯逻辑或低场景依赖测试。
- 正式补测试前，先建立清晰的 `.asmdef` / `.asmref` 边界，避免测试目录继续依赖默认 `Assembly-CSharp`。

### Camera.main 与全局查找收敛

- 当前 `PlayerTargeting`、`EnemyStateMachine` 和 `UIEnemyHealthBar` 的目标/状态/头顶 UI 朝向逻辑仍会在运行时访问 `Camera.main`。
- 部分 HUD / UI 脚本仍保留 `FindFirstObjectByType` 兜底查找。
- 白模阶段可以接受；后续镜头系统、HUD 和头顶 UI 稳定后，应优先提供显式 `viewReference` 或集中绑定入口，未配置时再 fallback 到缓存主相机。

## 长期工程注意

### 静态事件订阅规范

- `CombatEventsBus`、`PartyCombatRouter.CommandRequested` 等静态事件存在长期订阅风险。
- 新增订阅者必须保持订阅和退订成对出现，通常在 `OnEnable` / `OnDisable` 或窗口打开 / 关闭中处理。
- 如果事件总线订阅者明显增多，再考虑统一订阅封装或弱引用方案。

### 移动、碰撞与战斗位移边界

- 敌人挤开挡路角色、角色受击击退、吸聚、冲撞、霸体和外部位移属于不同规则，不应混用同一个接口表达所有位移。
- 新增击退、吸聚、霸体、冲撞等战斗位移时，需要先确认：
  - 位移来源是谁。
  - 被移动者是否可抵抗。
  - 是否影响 CharacterController / NavMeshAgent。
  - 是否应该打断当前状态。
  - 是否和敌人正常移动推挤互相叠加。

### Unity 生成文件

- `InputSystem_Actions.cs` 等 Unity 生成文件不手工维护。
- 输入动作和绑定以 `.inputactions` 为源头，修改后在 Unity 中重新 Generate C# Class。
