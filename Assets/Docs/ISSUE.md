# EndLink 问题记录

本文档用于记录开发中出现过的系统性/功能性等各类问题、排查结论和后续处理，也是一个技术债文档。

遇到“越改越乱”“多个系统互相牵连”“问题反复修补”的情况时，优先在这里补一条记录，避免同类问题反复消耗时间。

## 2026-06-01：队友跟随、敌人移动碰撞与索敌配置边界混乱

### 现象
- 队友跟随死区、动态站位、避让和敌人碰撞推挤连续互相影响。
- 为了解决一个表现问题，牵动了 `AllyFollowMotor`、`PartyManager`、`EnemyMotorBase`、`EnemyTargetSensor`、`EnemyStateMachine` 等多个系统。
- 主要依赖 Play Mode 手感测试反馈，缺少明确的单点验证步骤，导致改动范围不断扩大。

### 当前判断
- 问题不只是某一个参数，而是系统职责边界需要更清晰。
- 队友跟随、动态槽位、避让和敌人移动碰撞都属于“角色位移控制”，第一版同时叠加太多规则时容易互相干扰。
- 索敌配置放在 `EnemyTargetSensor` 上会让敌人配置入口分散，已调整为由 `EnemyStateMachine` 集中暴露，`EnemyTargetSensor` 只执行检测。

### 处理原则
- 暂停继续堆叠新功能时，先做收束验证。
- 优先确认每个组件只负责自己的边界：
  - `PartyManager`：小队关系、槽位、统一跟随参数。
  - `AllyFollowMotor`：队友跟随移动、死区、局部避让。
  - `EnemyMotorBase`：敌人自身移动，以及敌人移动时挤开挡路角色。
  - `EnemyStateMachine`：敌人大状态和敌人集中配置。
  - `EnemyTargetSensor`：执行索敌检测，不持有 Inspector 配置。
- 如果跟随手感继续不稳定，优先关闭或简化动态槽位，先保证固定站位 + 死区 + 基础避让稳定。
- 每次修改移动/碰撞相关逻辑后，至少验证：
  - 队友死区是否稳定。
  - 队友是否还会轻微移动就大幅调整站位。
  - 队友是否不能顶动敌人。
  - 敌人正常移动是否能挤开挡路队友。
  - 敌人是否还会异常浮空。

### 后续行动
- 做新功能前，先整理一次队友跟随和敌人移动碰撞的职责边界。
- 后续新增击退、吸聚、霸体等战斗位移时，不复用“敌人挤开挡路角色”的接口，应单独设计角色受力/外部战斗位移入口。

## 2026-06-03：Hitbox 池化与动作时序

### 当前状态
- `startup / active / recovery` 的第一版动作时序已经接入玩家、队友和敌人的 CombatDriver。
- 动作开始后会先进入 `startup`，在前摇结束时再真正生成 Hitbox；`active` 会覆盖本次 Hitbox 的运行时生命周期。
- 当前仍直接 `Instantiate` / `Destroy` Hitbox，尚未做池化。

### 后续行动
- 建立 Hitbox 池化和统一创建入口，优先覆盖近战波与远程飞行 Hitbox。
- 后续接 Animator 后，允许动画事件覆盖或校正动作时序。

## 2026-06-03：代码审查后确认的工程技术债

### 静态事件订阅规范
- `CombatEventsBus`、`PartyCombatRouter.CommandRequested` 等静态事件存在长期订阅风险。
- 当前主要订阅者基本已在 `OnDisable` 或窗口关闭时退订，暂未发现明确泄漏，但后续新增订阅者必须保持“订阅和退订成对出现”。
- 后续如果事件总线订阅者明显增多，再考虑统一订阅封装或弱引用方案。

### AllyFollowMotor 过大
- `AllyFollowMotor` 当前同时承担跟随、死区、动态归位、冲刺同步、追赶、传送、避让、战斗位置移动和 Gizmo 绘制。
- 现在不立即拆分，避免刚稳定的跟随表现重新引入问题。
- 后续开始正式做队伍表现、NavMesh 或更复杂队友 AI 前，再拆为跟随行为、避让行为、队形槽位解析等更小模块。

### CombatDriver 重复
- `PlayerCombatDriver`、`AllyCombatDriver`、`EnemyCombatDriver` 都包含动作冷却、Hitbox 生成、事件上报等相似逻辑。
- 当前动作执行规则、技能时序和 Hitbox 池化都还没稳定，暂不抽基类。
- 后续统一动作时序和 Hitbox 池化时，再抽 `CombatExecutionUtility` 或 `CombatDriverBase`。

### PartyCombatContext 集合优化
- `PartyCombatContext` 当前用 `List<Transform>` 保存已知敌人，并在清理目标时使用 `Contains`。
- 当前白模阶段敌人数量很少，不是性能瓶颈。
- 后续如果一场战斗中已知敌人数量变多，再改为 `HashSet<Transform>` 或 `HashSet<ICombatTarget>`，并保留有序主目标列表。

### 自动化验证缺位
- 项目已保留 `com.unity.test-framework`，但当前还没有稳定的 Runtime/EditMode 测试程序集和基础回归测试。
- 后续优先补 `DamageCalculator`、`CombatTagContainer`、`PartyLinkContext`、`ActionCooldownTracker` 这类纯逻辑或低场景依赖测试。
- 正式补测试前，先建立清晰的 `.asmdef` / `.asmref` 边界，避免测试目录继续依赖默认 `Assembly-CSharp`。

### Camera.main 访问收敛
- 当前 `PlayerTargeting`、`EnemyStateMachine` 和 `UIEnemyHealthBar` 的目标/状态/头顶 UI 朝向逻辑仍会在运行时访问 `Camera.main`。
- 部分 HUD / UI 脚本仍保留 `FindFirstObjectByType` 兜底查找。白模阶段可接受，正式 Prefab 应尽量由 HUD 或场景管理器集中显式绑定。
- Unity 6 会缓存 `MainCamera` 标签对象，但访问 `Camera.main` 仍有小 CPU 开销，且依赖场景中正确配置 `MainCamera` 标签。
- 白模阶段可以接受；后续镜头系统复杂后，应优先提供显式 `viewReference`，未配置时再 fallback 到缓存的主相机。
- 建议处理方式：
  - `PlayerTargeting`、敌人头顶状态点等世界空间标识统一支持 `viewReference`。
  - `Awake/OnEnable` 缓存一次主相机 Transform，避免每帧直接访问 `Camera.main`。
  - 切换相机或重建相机时提供刷新入口，而不是依赖每帧全局查询。
