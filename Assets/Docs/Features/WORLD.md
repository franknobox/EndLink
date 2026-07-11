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
- `Assets/_EndLink/World/IWorldInteractable.cs`
- `Assets/_EndLink/World/WorldInteractable.cs`
- `Assets/_EndLink/World/WorldInteractor.cs`
- `Assets/_EndLink/World/PlayerInteractor.cs`
- `Assets/_EndLink/Control/PlayerInputReader.cs`

### 相关物体 / 配置
- 玩家根物体可挂 `WorldInteractor` 和 `PlayerInteractor`。
- 可交互机关根物体可直接挂 `WorldInteractable`、继承它的专用机关脚本，或挂直接实现 `IWorldInteractable` 的组件，并需要有可被扫描到的 Collider。
- `WorldInteractor` 的 `Interactable Layers` 后续建议指向专用 Interactable Layer，避免扫描无关碰撞体。
- `Player/Interact` 已存在于 Input Actions 中，当前默认绑定键盘 `F` 单击和手柄 `buttonNorth`；系统只消费输入，不手工维护生成文件。

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
- `Assets/_EndLink/World/ElevatorPlatform.cs`
- `Assets/_EndLink/World/ElevatorInteractable.cs`
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
- `Assets/_EndLink/World/WorldDoor.cs`

### 相关物体 / 配置
- 推荐使用稳定的门根物体挂 `WorldDoor` 和交互 Trigger，独立门板子物体拖入 `Moving Part`。
- 升降闸门选择 `Slide`，通过 `Open Local Offset` 设置开启偏移；平开门选择 `Rotate`，并把门板 Pivot 放在门轴位置。
- 门框和活动门板不能是同一个不可分离的 ProBuilder Mesh，否则会整体移动。
