# UI Features

运行时 HUD、动作槽位和通用 UI 组件详情。

主索引见 [FEATURES.md](../FEATURES.md)。

<a id="feature-combat-ui-foundation"></a>

### Feature：战斗 UI 基础

<details>
<summary>展开详情</summary>

功能说明：
- `HUDCombatController` 是战斗 HUD 总入口，负责绑定 `PartyManager`、控制 HUD 显隐，并驱动下属 UI 模块刷新。
- `UIPartyCombatAction` 是小队动作栏管理器，当前负责绑定主控、队友 A、队友 B 的主动技能槽。
- `UICombatActionSlot` 是单个动作槽位组件，槽位绑定的是小队命令槽，例如 `PlayerSkill`、`AllySlotASkill`、`AllySlotBSkill`，而不是固定动作资产。
- `UICombatActionSlot` 通过 `PartyManager` 解析当前角色，通过角色 CombatDriver 读取当前槽位动作和该动作自己的冷却。
- `UICombatActionSlot` 通过 `PartyManager.CombatRouter` 读取该槽位当前键位显示文本。
- `UICombatActionSlot` 不读取输入、不释放动作、不判断战斗规则。
- 冷却中直接把图标染成配置颜色，冷却结束后恢复图标原色。
- 动作栏每帧只刷新冷却这类连续变化显示；键位文本通过 `PartyCombatRouter.KeyBindingsChanged` 事件刷新。
- `UIPartyMemberPortrait` 是小队成员头像 UI，负责显示主控和两个队友头像、1/2/3 连携键位、连携窗口高亮，以及队友 Link Down / 主控死亡后的灰化。
- `UIPartyUltimateBar` 是终链奥义条 UI，负责显示协同率进度、V 键位和奥义就绪颜色。
- `HUDDebugLogPanel` 是运行时 HUD 调试日志面板，监听 `CombatEventsBus`、`AllyDebugLog` 和 `PartyCombatRouter.CommandRequested`，可按 Combat、Ally、Party、Damage、Tag 筛选显示最近日志。
- `UIHealthBar` 是通用血条组件，支持 `CharacterHealth` 和正式敌人的 `EnemyHealth`，可用于主角、队友和敌人头顶血条。
- `UIHealthBar` 支持 `Image.fillAmount`、可选血量文本、满血隐藏、死亡隐藏、无生命来源隐藏和运行时绑定生命来源。
- `UIHealthBar` 优先监听生命事件刷新，`autoRefresh` 只作为兜底刷新开关。
- `CombatHUDPrefabGenerator` 提供 `EndLink > UI > Combat HUD` 菜单入口，可以一键生成所有面板，也可以单独生成某个 Panel Prefab。
- 生成的 HUD 面板当前包含左上小队状态区、头像连携区、左下 Q/E/F 主动技能区、右下终链奥义条和右上临时调试信息区。

对应脚本：
- `Assets/_EndLink/UI/HUDCombatController.cs`
- `Assets/_EndLink/UI/UIPartyCombatAction.cs`
- `Assets/_EndLink/UI/UICombatActionSlot.cs`
- `Assets/_EndLink/UI/UIPartyMemberPortrait.cs`
- `Assets/_EndLink/UI/UIPartyUltimateBar.cs`
- `Assets/_EndLink/UI/HUDDebugLogPanel.cs`
- `Assets/_EndLink/UI/UIHealthBar.cs`
- `Assets/_EndLink/Editor/CombatHUDPrefabGenerator.cs`

相关物体：
- 战斗 UI Canvas / HUD 根物体
  - `HUDCombatController`
- 技能栏或动作栏父物体
  - `UIPartyCombatAction`
- 主动技能圆形动作槽位
  - `UICombatActionSlot`
  - `Image` 图标
  - 可选 `TextMeshProUGUI` 键位文本
- 小队成员头像
  - `UIPartyMemberPortrait`
  - `Image` 头像
  - `Image` 连携高亮
  - 可选 `TextMeshProUGUI` 连携键位
- 终链奥义条
  - `UIPartyUltimateBar`
  - `Image` 协同率填充
  - 可选 `TextMeshProUGUI` 键位和百分比文本
- 运行时调试日志面板
  - `HUDDebugLogPanel`
  - `TextMeshProUGUI` 日志文本
- 血条物体
  - `UIHealthBar`
  - `Image` 填充图
  - 可选 `TextMeshProUGUI` 血量文本
- 生成工具产物
  - `Assets/_EndLink/UI/Prefabs/PF_PartyStatusPanel.prefab`
  - `Assets/_EndLink/UI/Prefabs/PF_SkillPanel.prefab`
  - `Assets/_EndLink/UI/Prefabs/PF_UltimatePanel.prefab`
  - `Assets/_EndLink/UI/Prefabs/PF_DebugPanel.prefab`
  - `Assets/_EndLink/UI/Generated/UI_Circle64.png`
  - `Assets/_EndLink/UI/Generated/UI_Square64.png`

关键配置：
- `HUDCombatController.partyManager`：小队管理器，空时自动查找
- `HUDCombatController.partyCombatAction`：小队动作栏 UI 管理器
- `HUDCombatController.partyMemberPortraits`：小队成员头像 UI 集合
- `HUDCombatController.partyUltimateBar`：终链奥义条 UI
- `UIPartyCombatAction.autoCollectChildSlots`：是否自动从子物体收集动作槽
- `UIPartyCombatAction.driveChildSlotsManually`：是否由动作栏统一驱动子槽刷新
- `UICombatActionSlot.slot`：该 UI 对应的键位槽，例如 PlayerSkill、AllySlotASkill、AllySlotBSkill
- `UICombatActionSlot.iconImage`：技能图标 Image，可拖子物体上的 Icon
- `UICombatActionSlot.keyLabelText`：键位显示文本
- `UICombatActionSlot.cooldownTintColor`：冷却染色颜色，默认半透明灰色
- `UIPartyMemberPortrait.memberSlot`：头像对应 MainCharacter、AllySlotA 或 AllySlotB
- `UIPartyMemberPortrait.highlightImage`：连携窗口开启时显示的高亮图
- `UIPartyUltimateBar.fillImage`：协同率填充 Image，建议 Image Type 使用 Filled
- `HUDDebugLogPanel.logText`：运行时调试日志文本
- `HUDDebugLogPanel.showCombat / showAlly / showParty / showDamage / showTag`：日志分类筛选
- `HUDDebugLogPanel.maxLines`：最多保留的 HUD 日志行数
- `UIHealthBar.health`：要显示的 `CharacterHealth`，主角和队友通常使用它
- `UIHealthBar.enemyHealth`：要显示的 `EnemyHealth`，正式敌人头顶血条通常使用它
- `UIHealthBar.fillImage`：血条填充 Image，建议 Image Type 使用 Filled
- `UIHealthBar.valueText`：可选血量文本
- `UIHealthBar.hideWhenFull` / `hideWhenDead`：满血和死亡时是否隐藏
- `UIHealthBar.autoRefresh`：事件刷新之外的兜底刷新开关，默认关闭

</details>
