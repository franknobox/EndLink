# World Features

本文件记录灰盒地图、机关和世界交互相关的已完成功能。

<a id="feature-blockout-module-library"></a>

## Feature: 白盒关卡模块库

### 当前状态
已完成基础模块第一版。

### 功能说明
- 提供可重复使用的石块、柱体、边界、墙体、门洞、楼梯、坡道、垂直爬梯、天花板和数据方块白盒 Prefab，用于快速搭建关卡轮廓、通行结构和空间参照。
- 两种大型不规则柱体分别采用中段收窄和中段加粗轮廓，顶部保持平坦，可作为大树树干、石柱或大型环境支撑物占位。
- 不规则墙体和天花板使用多层三角切面、错位轮廓与不平整表面；墙体顶部保留轻微起伏但避免尖锐高峰，`BOX_DataBlock_02` 使用非对称框架、核心方块和离散体素表现复杂数据结构。
- 所有模块默认使用白色 ProBuilder 材质、`Environment` Layer 和实体 `MeshCollider`，不附带玩法脚本，可在场景中继续缩放和替换语义材质。
- 白盒关卡 Prefab 统一使用 `BOX_` 前缀，与普通玩法或视觉 Prefab 的 `PF_` 前缀区分。

### 相关物体 / 配置
- Prefab：`Assets/_EndLink/World/Prefab/Blockout`。
- Mesh：`Assets/Art/Environments/Blockout`。
- 当前包含三种 `BOX_Rock`、`BOX_Pillar_Narrow`、`BOX_Pillar_Thick`、`BOX_Boundary_X`、规则/不规则墙体、门洞、楼梯、坡道、垂直爬梯、不规则天花板和三种数据方块。
- 楼梯单级高度为 `0.25m`，用于基础 CharacterController 通行验证；垂直爬梯目前只有白盒外形和碰撞，不包含攀爬逻辑。

<a id="feature-weapon-object-interaction"></a>

## Feature: 武器物体交互

### 当前状态
已完成第一版。

### 功能说明
- `ObjInteractable` 作为通用武器交互入口，推荐挂在具体功能物体的交互子物体上，与接收 Hitbox 的 Collider 配合使用。
- 固定提供触发装置、网络结构、远程节点、可破坏物、重型物体和受力机关六种类型，并分别固定对应 A、A、B、C、C、C 武器形态。
- 网络结构和可破坏物支持配置累计命中次数；其余类型在单次合法命中后尝试执行功能。
- 交互组件只负责形态检查、命中进度、最短命中间隔和一次性触发，具体功能统一交给父级 `IObjFunction`。
- 功能成功执行后，交互子物体会通过 `MaterialPropertyBlock` 短暂闪白并恢复原材质属性，作为统一的生效确认反馈。
- 玩家 Hitbox 会独立检测 `ObjInteractable`，不要求对象属于战斗目标 Layer 或实现 `IHitReceiver`；同一对象如果也是战斗目标，仍会继续进入正常伤害流程。
- 门、电梯平台和检查点直接实现 `IObjFunction`，仅由交互子物体的武器命中触发。

### 对应脚本
- `Assets/_EndLink/World/Interactable/ObjInteractionType.cs`
- `Assets/_EndLink/World/Interactable/ObjInteractionContext.cs`
- `Assets/_EndLink/World/Interactable/IObjFunction.cs`
- `Assets/_EndLink/World/Interactable/ObjInteractable.cs`
- `Assets/_EndLink/Combat/Hitbox/HitboxBase.cs`
- `Assets/_EndLink/Combat/Hitbox/HitboxProjectile.cs`

### 相关物体 / 配置
- 功能组件挂在稳定父物体上；交互子物体挂 Collider 和 `ObjInteractable`，`Function Target` 可留空自动向父级查找。
- `Assets/_EndLink/World/PF_ObjInteractable_Test.prefab` 是测试用通用交互子物体，使用低模视觉、`Interactable` Layer、Trigger CapsuleCollider 和 `ObjInteractable`；拖到功能物体下后再按需求选择交互类型。
- `Required Hit Count` 只对网络结构和可破坏物生效；`Min Hit Interval` 用于避免同一段攻击的重叠判定被重复累计。
- 触发后不应再次使用的物体可开启 `Trigger Once`，需要在机关或场景重置时调用 `ResetInteraction()` 恢复。
- 交互 Collider 所在 Layer 必须允许与武器 Hitbox 所在 Layer 产生 Trigger 回调；世界交互分支不读取 Hitbox 的战斗目标 LayerMask。
- 当前场景中的门、电梯和检查点已按“功能父物体 + `ObjInteract` 交互子物体”结构完成接线。

<a id="feature-world-spawn-checkpoint"></a>

## Feature: 通用出生点与检查点

### 当前状态
已完成第一版。

### 功能说明
- `WorldSpawnPoint` 只保存稳定 ID、世界位置、朝向、建议半径和用途，不直接生成任何对象。
- 出生点用途支持开场出生、检查点和敌人生成点多选；未来玩家存档与敌人生成器可以引用同一位置类型，但各自保留独立执行逻辑。
- `WorldCheckpoint` 直接实现 `IObjFunction`，由 `ObjInteract` 子物体触发；首次触发会切换当前复活点，再次触发可休整，默认战斗中不可使用。
- 检查点成功激活或休整后触发自身事件，可继续驱动篝火启用、音效和特效，不依赖旧交互基类。
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
- 场景层级中的 `Checkpoints` 统一收纳 `PlayStart`、`RespawnPoint` 等玩家流程检查点；`SpawnPoints` 专门收纳能够生成敌人或其他实体的位置标记。
- `fallDeathHeight`：玩家坠落死亡使用的世界 Y 高度，当前默认 `-50`。
- 开场出生点挂 `WorldSpawnPoint` 并选择 `PlayerStart`，朝向箭头表示玩家出生朝向。
- 可交互检查点根物体挂 `WorldCheckpoint`，其 `ObjInteract` 子物体负责 Collider、交互类型和武器命中入口。
- 推荐在检查点旁创建独立的安全落点子物体并挂 `WorldSpawnPoint`，再拖入 `WorldCheckpoint`；如果出生点与检查点根物体重合，也可以挂在同一物体自动读取。
- `WorldCheckpoint` 会自动为关联出生点补充 `Checkpoint` 用途；其调度器引用可留空，运行时使用场景中的活动调度器。
- 复制出生点后需要保证 `Point Id` 唯一；可以通过组件菜单“重新生成出生点 ID”处理重复 ID。

<a id="feature-elevator-platform"></a>

## Feature: 两层移动电梯

### 当前状态
已完成第一版。

### 功能说明
- 玩家站在平台乘客范围内时，使用正确武器形态命中 `ObjInteract` 子物体即可让电梯在上下两层间往返。
- 电梯使用运动学 `Rigidbody.MovePosition` 驱动，并在起步和到站阶段自动缓入缓出。
- 移动中拒绝重复运行请求。
- 普通 Rigidbody 实体由物理接触带动；实现 `IExternalDisplacementReceiver` 的角色和普通 `CharacterController` 会获得平台三维位移补偿。
- 提供开始运行、抵达下层和抵达上层事件，后续可以接电梯门、音效、灯光或关卡逻辑。

### 对应脚本
- `Assets/_EndLink/World/ObjFunction/ElevatorPlatform.cs`
- `Assets/_EndLink/Control/IExternalDisplacementReceiver.cs`

### 相关物体 / 配置
- 电梯移动根物体挂 `Rigidbody` 和 `ElevatorPlatform`；交互入口使用独立 `ObjInteract` 子物体，脚本会把 Rigidbody 配置为 Kinematic。
- ProBuilder 平台需要保留实体 Collider，并额外准备覆盖平台上方乘客区域的 Trigger Collider。
- `Lower Stop` 和 `Upper Stop` 必须是电梯根物体之外的固定 Transform；电梯只读取它们的世界 Y 高度，平台 X/Z 始终保持进入场景时的初始值。
- 乘客 Trigger 用于确认触发者位于平台范围内；`ObjInteract` 的 Collider 需要与武器 Hitbox 产生 Trigger 回调。
- 第一版只支持上下两个停靠点，不处理多楼层、外部呼叫队列和电梯门状态机。

<a id="feature-world-door"></a>

## Feature: 通用开关门

### 当前状态
已完成第一版。

### 功能说明
- `DoorInteractable` 直接实现 `IObjFunction`，由 `ObjInteract` 子物体的合法武器命中切换开启和关闭。
- 支持本地坐标平移门和绕门板 Pivot 旋转的门。
- 使用平滑缓入缓出运动，支持运行中再次交互反向。
- 可选使用门板上的运动学 Rigidbody 驱动碰撞，也可在灰盒阶段直接移动 Transform。
- 提供开门开始、完全开启、关门开始和完全关闭事件。

### 对应脚本
- `Assets/_EndLink/World/ObjFunction/DoorInteractable.cs`

### 相关物体 / 配置
- 推荐使用稳定的门根物体挂 `DoorInteractable`，独立门板子物体拖入 `Moving Part`，另建 `ObjInteract` 子物体承接 Collider 和交互类型。
- 升降闸门选择 `Slide`，通过 `Open Local Offset` 设置开启偏移；平开门选择 `Rotate`，并把门板 Pivot 放在门轴位置。
- 门框和活动门板不能是同一个不可分离的 ProBuilder Mesh，否则会整体移动。
