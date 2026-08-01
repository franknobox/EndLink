# World Features

本文件记录灰盒地图、机关和世界交互相关的已完成功能。

<a id="feature-world-interaction"></a>

## Feature: 世界交互底座

### 当前状态
已完成接口一致性版。

### 功能说明
建立一个通用的小型世界交互底座，用于后续门、电梯、开关、测试机关、拾取物等对象接入。

第一版只解决基础链路：
- 可交互对象统一暴露“是否可交互、交互提示、交互点、执行交互”。
- 扫描、当前目标、变化事件和玩家桥接统一使用 `IWorldInteractable`，支持直接实现接口或继承 `WorldInteractable`。
- 交互者在半径内低频扫描候选对象，自动选中最近的可用对象。
- 候选距离统一通过 `GetInteractionPoint()` 计算，不再直接依赖 Collider 最近点。
- 执行缓存目标前会重新验证目标存活、`CanInteract`、交互半径和可选遮挡，防止刷新间隔内对已经离开的对象交互。
- 玩家通过已有新版 Input System 的 `Player/Interact` 动作触发当前交互。
- 玩家默认只在 Idle / Move 状态允许交互，避免攻击、闪避、受击过程中误触机关。
- 普通机关可直接挂 `WorldInteractable` 用事件测试；专用机关通过继承基类扩展具体行为。

### 对应脚本
- `Assets/_EndLink/World/Interactable/IWorldInteractable.cs`
- `Assets/_EndLink/World/Interactable/WorldInteractable.cs`
- `Assets/_EndLink/World/Interactable/WorldInteractor.cs`
- `Assets/_EndLink/World/Interactable/PlayerInteractor.cs`
- `Assets/_EndLink/Control/PlayerInputReader.cs`

### 相关物体 / 配置
- 玩家根物体可挂 `WorldInteractor` 和 `PlayerInteractor`。
- 可交互机关根物体可直接挂 `WorldInteractable`、继承它的专用机关脚本，或挂直接实现 `IWorldInteractable` 的组件，并需要有可被扫描到的 Collider。
- `WorldInteractor` 的 `Interactable Layers` 后续建议指向专用 Interactable Layer，避免扫描无关碰撞体。
- `Player/Interact` 已存在于 Input Actions 中，当前默认绑定键盘 `F` 单击和手柄 `buttonNorth`；系统只消费输入，不手工维护生成文件。

<a id="feature-weapon-object-interaction"></a>

## Feature: 武器物体交互

### 当前状态
已完成第一版。

### 功能说明
- `ObjInteractable` 作为通用武器交互入口，推荐挂在具体功能物体的交互子物体上，与接收 Hitbox 的 Collider 配合使用。
- 固定提供触发装置、网络结构、远程节点、可破坏物、重型物体和受力机关六种类型，并分别固定对应 A、A、B、C、C、C 武器形态。
- 网络结构和可破坏物支持配置累计命中次数；其余类型在单次合法命中后尝试执行功能。
- 交互组件只负责形态检查、命中进度、最短命中间隔和一次性触发，具体功能统一交给父级 `IObjFunction`。
- 玩家 Hitbox 会独立检测 `ObjInteractable`，不要求对象属于战斗目标 Layer 或实现 `IHitReceiver`；同一对象如果也是战斗目标，仍会继续进入正常伤害流程。
- 门、电梯平台和检查点已接入 `IObjFunction`；原有 F 键交互入口继续保留，便于场景逐步迁移。

### 对应脚本
- `Assets/_EndLink/World/Interactable/ObjInteractionType.cs`
- `Assets/_EndLink/World/Interactable/ObjInteractionContext.cs`
- `Assets/_EndLink/World/Interactable/IObjFunction.cs`
- `Assets/_EndLink/World/Interactable/ObjInteractable.cs`
- `Assets/_EndLink/Combat/Hitbox/HitboxBase.cs`
- `Assets/_EndLink/Combat/Hitbox/HitboxProjectile.cs`

### 相关物体 / 配置
- 功能组件挂在稳定父物体上；交互子物体挂 Collider 和 `ObjInteractable`，`Function Target` 可留空自动向父级查找。
- `Required Hit Count` 只对网络结构和可破坏物生效；`Min Hit Interval` 用于避免同一段攻击的重叠判定被重复累计。
- 触发后不应再次使用的物体可开启 `Trigger Once`，需要在机关或场景重置时调用 `ResetInteraction()` 恢复。
- 交互 Collider 所在 Layer 必须允许与武器 Hitbox 所在 Layer 产生 Trigger 回调；世界交互分支不读取 Hitbox 的战斗目标 LayerMask。
- 当前未自动修改场景，门、电梯或检查点需要按“功能父物体 + 交互子物体”结构手动添加 `ObjInteractable`。

<a id="feature-world-spawn-checkpoint"></a>

## Feature: 通用出生点与检查点

### 当前状态
已完成第一版。

### 功能说明
- `WorldSpawnPoint` 只保存稳定 ID、世界位置、朝向、建议半径和用途，不直接生成任何对象。
- 出生点用途支持开场出生、检查点和敌人生成点多选；未来玩家存档与敌人生成器可以引用同一位置类型，但各自保留独立执行逻辑。
- `WorldCheckpoint` 继承现有世界交互基类，首次交互会切换当前复活点，再次交互可休整；默认战斗中不可使用。
- 检查点休整会恢复玩家生命、清除战斗标签、软锁/硬锁目标和残留战斗上下文。
- `WorldRespawnManager` 维护默认出生点和当前检查点，监听玩家死亡，并使用不受时间缩放影响的延迟执行复活。
- 玩家根物体低于 `fallDeathHeight` 时会立即触发死亡并沿用正常复活流程；当前默认世界下界为 `Y=-50`。
- 复活会安全传送玩家、恢复生命、清理战斗标签与目标，并重置动作、连段、防御、输入缓冲、移动速度、重力和战斗击退。
- 第一版不写入磁盘存档，也不生成或重置敌人；相关系统后续通过出生点 ID、用途和检查点事件接入。

### 对应脚本
- `Assets/_EndLink/World/WorldSpawnPoint.cs`
- `Assets/_EndLink/World/WorldCheckpoint.cs`
- `Assets/_EndLink/World/WorldRespawnManager.cs`
- `Assets/_EndLink/Control/PlayerController.cs`
- `Assets/_EndLink/Player/StateMachine/PlayerStateMachine.cs`

### 相关物体 / 配置
- 场景系统物体挂一个 `WorldRespawnManager`，拖入玩家根物体和默认 `WorldSpawnPoint`；同一场景只能启用一个调度器。
- `fallDeathHeight`：玩家坠落死亡使用的世界 Y 高度，当前默认 `-50`。
- 开场出生点挂 `WorldSpawnPoint` 并选择 `PlayerStart`，朝向箭头表示玩家出生朝向。
- 可交互检查点根物体挂 `WorldCheckpoint` 和可被 `WorldInteractor` 扫描的 Collider，并放在 Interactable Layer。
- 推荐在检查点旁创建独立的安全落点子物体并挂 `WorldSpawnPoint`，再拖入 `WorldCheckpoint`；如果出生点与检查点根物体重合，也可以挂在同一物体自动读取。
- `WorldCheckpoint` 会自动为关联出生点补充 `Checkpoint` 用途；其调度器引用可留空，运行时使用场景中的活动调度器。
- 复制出生点后需要保证 `Point Id` 唯一；可以通过组件菜单“重新生成出生点 ID”处理重复 ID。

<a id="feature-elevator-platform"></a>

## Feature: 两层移动电梯

### 当前状态
已完成第一版。

### 功能说明
- 玩家站在平台乘客范围内时，可以通过现有世界交互输入让电梯在上下两层间往返。
- 电梯使用运动学 `Rigidbody.MovePosition` 驱动，并在起步和到站阶段自动缓入缓出。
- 移动中拒绝重复运行请求，停靠后根据当前位置显示“上行”或“下行”。
- 普通 Rigidbody 实体由物理接触带动；实现 `IExternalDisplacementReceiver` 的角色和普通 `CharacterController` 会获得平台三维位移补偿。
- 提供开始运行、抵达下层和抵达上层事件，后续可以接电梯门、音效、灯光或关卡逻辑。

### 对应脚本
- `Assets/_EndLink/World/ObjFunction/ElevatorPlatform.cs`
- `Assets/_EndLink/World/ObjFunction/ElevatorInteractable.cs`
- `Assets/_EndLink/Control/IExternalDisplacementReceiver.cs`

### 相关物体 / 配置
- 电梯移动根物体挂 `Rigidbody`、`ElevatorPlatform` 和 `ElevatorInteractable`；脚本会把 Rigidbody 配置为 Kinematic。
- ProBuilder 平台需要保留实体 Collider，并额外准备覆盖平台上方乘客区域的 Trigger Collider。
- `Lower Stop` 和 `Upper Stop` 必须是电梯根物体之外的固定 Transform；电梯只读取它们的世界 Y 高度，平台 X/Z 始终保持进入场景时的初始值。
- 乘客 Trigger 所在 Layer 需要包含在玩家 `WorldInteractor` 的 `Interactable Layers` 中。
- 第一版只支持上下两个停靠点，不处理多楼层、外部呼叫队列和电梯门状态机。

<a id="feature-world-door"></a>

## Feature: 通用开关门

### 当前状态
已完成第一版。

### 功能说明
- 继承 `WorldInteractable`，通过玩家现有 `F` 交互输入切换开启和关闭。
- 支持本地坐标平移门和绕门板 Pivot 旋转的门。
- 使用平滑缓入缓出运动，支持运行中再次交互反向。
- 可选使用门板上的运动学 Rigidbody 驱动碰撞，也可在灰盒阶段直接移动 Transform。
- 提供开门开始、完全开启、关门开始和完全关闭事件。

### 对应脚本
- `Assets/_EndLink/World/ObjFunction/DoorInteractable.cs`

### 相关物体 / 配置
- 推荐使用稳定的门根物体挂 `DoorInteractable` 和交互 Trigger，独立门板子物体拖入 `Moving Part`。
- 升降闸门选择 `Slide`，通过 `Open Local Offset` 设置开启偏移；平开门选择 `Rotate`，并把门板 Pivot 放在门轴位置。
- 门框和活动门板不能是同一个不可分离的 ProBuilder Mesh，否则会整体移动。
