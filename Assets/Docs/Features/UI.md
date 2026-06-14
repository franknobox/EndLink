# UI Features

运行时 HUD、动作槽位和通用 UI 组件详情。

主索引见 [FEATURES.md](../FEATURES.md)。

<a id="feature-combat-ui-foundation"></a>

### Feature：战斗 UI 基础

<details>
<summary>展开详情</summary>

功能说明：
- `HUDCombatController` 是战斗 HUD 总入口，负责绑定 `PartyManager`、控制 HUD 显隐，并驱动下属 UI 模块刷新。
- `UIPartyCombatAction` 是小队动作栏管理器，负责绑定主控、队友 A、队友 B 的主动技能槽、连携请求槽和全队终链奥义槽。
- `UICombatActionSlot` 是单个动作槽位组件，槽位绑定的是小队命令槽，例如 `PlayerSkill`、`AllySlotASkill`、`AllySlotBSkill`，而不是固定动作资产。
- `UICombatActionSlot` 通过 `PartyManager` 解析当前角色，通过角色 CombatDriver 读取当前槽位动作和该动作自己的冷却。
- `UICombatActionSlot` 通过 `PartyManager.CombatRouter` 读取该槽位当前键位显示文本。
- `UICombatActionSlot` 不读取输入、不释放动作、不判断战斗规则。
- 冷却中直接把图标染成配置颜色，冷却结束后恢复图标原色。
- 动作栏每帧只刷新冷却这类连续变化显示；键位文本通过 `PartyCombatRouter.KeyBindingsChanged` 事件刷新。
- `UIHealthBar` 是通用血条组件，支持 `CharacterHealth` 和正式敌人的 `EnemyHealth`，可用于主角、队友和敌人头顶血条。
- `UIHealthBar` 支持 `Image.fillAmount`、可选血量文本、满血隐藏、死亡隐藏、无生命来源隐藏和运行时绑定生命来源。
- `UIHealthBar` 优先监听生命事件刷新，`autoRefresh` 只作为兜底刷新开关。

对应脚本：
- `Assets/_EndLink/UI/HUDCombatController.cs`
- `Assets/_EndLink/UI/UIPartyCombatAction.cs`
- `Assets/_EndLink/UI/UICombatActionSlot.cs`
- `Assets/_EndLink/UI/UIHealthBar.cs`

相关物体：
- 战斗 UI Canvas / HUD 根物体
  - `HUDCombatController`
- 技能栏或动作栏父物体
  - `UIPartyCombatAction`
- 技能、连携、终链奥义等圆形动作槽位
  - `UICombatActionSlot`
  - `Image` 图标
  - 可选 `TextMeshProUGUI` 键位文本
- 血条物体
  - `UIHealthBar`
  - `Image` 填充图
  - 可选 `TextMeshProUGUI` 血量文本

关键配置：
- `HUDCombatController.partyManager`：小队管理器，空时自动查找
- `HUDCombatController.partyCombatAction`：小队动作栏 UI 管理器
- `UIPartyCombatAction.autoCollectChildSlots`：是否自动从子物体收集动作槽
- `UIPartyCombatAction.driveChildSlotsManually`：是否由动作栏统一驱动子槽刷新
- `UICombatActionSlot.slot`：该 UI 对应的键位槽，例如 PlayerSkill、AllySlotASkill、AllySlotBSkill
- `UICombatActionSlot.iconImage`：技能图标 Image，可拖子物体上的 Icon
- `UICombatActionSlot.keyLabelText`：键位显示文本
- `UICombatActionSlot.cooldownTintColor`：冷却染色颜色，默认半透明灰色
- `UIHealthBar.health`：要显示的 `CharacterHealth`，主角和队友通常使用它
- `UIHealthBar.enemyHealth`：要显示的 `EnemyHealth`，正式敌人头顶血条通常使用它
- `UIHealthBar.fillImage`：血条填充 Image，建议 Image Type 使用 Filled
- `UIHealthBar.valueText`：可选血量文本
- `UIHealthBar.hideWhenFull` / `hideWhenDead`：满血和死亡时是否隐藏
- `UIHealthBar.autoRefresh`：事件刷新之外的兜底刷新开关，默认关闭

</details>
