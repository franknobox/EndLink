# World Features

本文件记录灰盒地图、机关和世界交互相关的已完成功能。

## Feature: 世界交互底座

### 当前状态
已完成第一版。

### 功能说明
建立一个通用的小型世界交互底座，用于后续门、电梯、开关、测试机关、拾取物等对象接入。

第一版只解决基础链路：
- 可交互对象统一暴露“是否可交互、交互提示、交互点、执行交互”。
- 交互者在半径内低频扫描候选对象，自动选中最近的可用对象。
- 玩家通过已有新版 Input System 的 `Player/Interact` 动作触发当前交互。
- 玩家默认只在 Idle / Move 状态允许交互，避免攻击、闪避、受击过程中误触机关。
- 具体门、电梯逻辑暂不写死，第一版可直接挂 `WorldInteractable` 用事件测试，后续再通过继承基类扩展。

### 对应脚本
- `Assets/_EndLink/World/IWorldInteractable.cs`
- `Assets/_EndLink/World/WorldInteractable.cs`
- `Assets/_EndLink/World/WorldInteractor.cs`
- `Assets/_EndLink/World/PlayerInteractor.cs`
- `Assets/_EndLink/Control/PlayerInputReader.cs`

### 相关物体 / 配置
- 玩家根物体可挂 `WorldInteractor` 和 `PlayerInteractor`。
- 可交互机关根物体可直接挂 `WorldInteractable`，或挂继承自它的专用机关脚本，并需要有可被扫描到的 Collider。
- `WorldInteractor` 的 `Interactable Layers` 后续建议指向专用 Interactable Layer，避免扫描无关碰撞体。
- `Player/Interact` 已存在于 Input Actions 中，当前系统只消费输入，不手工维护生成文件。
