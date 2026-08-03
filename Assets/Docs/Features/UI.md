# UI Features

运行时 HUD、动作槽位和通用 UI 组件详情。

主索引见 [FEATURES.md](../FEATURES.md)。

<a id="feature-combat-ui-foundation"></a>

### Feature：战斗 UI 基础

<details>
<summary>展开详情</summary>

功能说明：
- `HUDCombatController` 是战斗 HUD 总入口，负责绑定 `PartyManager`、控制 HUD 显隐，并驱动下属 UI 模块刷新。
- 旧版小队动作槽、成员头像和终链奥义显示组件暂时归档在 `UI/PartyUI`，供后续恢复小队战斗 UI 时复用；新版单人战斗 UI 不继续依赖这些组件。
- `UIPartyCombatAction` 是旧小队主动技能动作栏管理器；队友主动技能停用后仅作为归档组件保留。
- `UICombatActionSlot` 是单个动作槽位组件，槽位绑定的是小队命令槽，例如 `PlayerSkill`、`AllySlotASkill`、`AllySlotBSkill`，而不是固定动作资产。
- `UICombatActionSlot` 通过 `PartyManager` 解析当前角色，通过角色 CombatDriver 读取当前槽位动作和该动作自己的冷却。
- `UICombatActionSlot` 通过 `PartyManager.CombatRouter` 读取该槽位当前键位显示文本。
- `UICombatActionSlot` 不读取输入、不释放动作、不判断战斗规则。
- 冷却中直接把图标染成配置颜色，冷却结束后恢复图标原色。
- 动作栏每帧只刷新冷却这类连续变化显示；键位文本通过 `PartyCombatRouter.KeyBindingsChanged` 事件刷新。
- `UIPartyMemberPortrait` 是归档的小队成员头像 UI，当前只保留成员可用状态与 Link Down / 死亡灰化，不再包含连携键位和窗口高亮配置。
- `UIPartyUltimateBar` 是终链奥义条 UI，负责显示协同率进度、`PartyCombatRouter` 当前奥义键位和奥义就绪颜色。
- `HUDDebugLogPanel` 是运行时 HUD 调试日志面板，监听 `CombatEventsBus`、`AllyDebugLog` 和 `PartyCombatRouter.CommandRequested`，可按 Combat、Ally、Party、Damage、Tag 筛选显示最近日志。
- `UIHealthBar` 是通用血条组件，支持 `CharacterHealth` 和正式敌人的 `EnemyHealth`，可用于主角、队友和敌人头顶血条。
- 左上主角/队友状态区里的 `Health_Main`、`Health_AllyA`、`Health_AllyB` 会按默认命名自动绑定到 `PartyManager` 当前的小队生命组件；如果场景里有 `HUDCombatController`，也可以由它统一重绑。
- `UIHealthBar` 支持 `Image.fillAmount`、可选血量文本、满血隐藏、死亡隐藏、无生命来源隐藏和运行时绑定生命来源。
- `UIHealthBar` 优先监听生命事件刷新，`autoRefresh` 只作为兜底刷新开关。
- `UIBalanceBar` 是通用平衡条组件，只依赖 `IBalanceSource`；当前可直接绑定 `EnemyBalance`，后续主角平衡组件实现同一接口后可以复用。
- `UIBalanceBar` 显示当前剩余平衡值，优先监听 `BalanceChanged` 事件刷新，不负责削减平衡、进入失衡或判断处决。
- `UIAimReticle` 是 B 形态射击瞄准的居中十字准星，只监听 `PlayerAimController` 的瞄准状态并控制 `CanvasGroup` 显隐，不读取输入或计算射线。
- `UIWeaponForm` 是主角 A/B/C 武器形态显示，只监听 `PlayerWeaponController.FormChanged` 更新中央字母；上下箭头只提示 Q/E 或方向键切换关系，不承担按钮输入。
- `UIHealthBar` 和 `UIEnemyHealthBar` 不会在 `Awake` / `OnValidate` 里修改 `CanvasGroup` 显隐，首次显示刷新延后到 `Start`，避免编辑器生命周期 warning。
- `UIEnemyHealthBar` 是敌人头顶血条控制器，负责 World Space 跟随、面向相机、绑定 `EnemyHealth` 和套用默认半透明暗红色样式。
- 敌人头顶血条预制体使用小尺寸世界单位 RectTransform，避免拖入场景时因为缩放重置变成巨大半透明面片；显隐刷新只在运行期改 `CanvasGroup`。
- `CombatHUDPrefabGenerator` 提供 `EndLink > UI > Combat HUD` 菜单入口，当前生成运行时调试面板、敌人头顶血条、瞄准准星和武器形态显示。
- 旧版小队状态、动作槽与奥义 Prefab 继续作为归档资产保留，但不会再被生成工具创建或覆盖；新版单人战斗 UI 后续使用新的接口和生成流程。

对应脚本：
- `Assets/_EndLink/UI/HUDCombatController.cs`
- `Assets/_EndLink/UI/PartyUI/UIPartyCombatAction.cs`
- `Assets/_EndLink/UI/PartyUI/UICombatActionSlot.cs`
- `Assets/_EndLink/UI/PartyUI/UIPartyMemberPortrait.cs`
- `Assets/_EndLink/UI/PartyUI/UIPartyUltimateBar.cs`
- `Assets/_EndLink/UI/HUDDebugLogPanel.cs`
- `Assets/_EndLink/UI/UIHealthBar.cs`
- `Assets/_EndLink/UI/UIBalanceBar.cs`
- `Assets/_EndLink/UI/UIEnemyHealthBar.cs`
- `Assets/_EndLink/UI/UIAimReticle.cs`
- `Assets/_EndLink/UI/UIWeaponForm.cs`
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
- 平衡条物体
  - `UIBalanceBar`
  - `Image` 填充图
  - 可选 `CanvasGroup` 和 `TextMeshProUGUI` 数值文本
- 敌人头顶血条物体
  - `Canvas`，Render Mode 为 World Space
  - `CanvasGroup`
  - `UIEnemyHealthBar`
  - `UIHealthBar`
  - 半透明暗红色背景与填充 `Image`
- 瞄准准星
  - `CanvasGroup`
  - `UIAimReticle`
  - 居中十字线 `Image`
- 武器形态显示
  - `UIWeaponForm`
  - 浅灰菱形背景与 A/B/C `TextMeshProUGUI`
  - 上下切换提示箭头
- 生成工具产物
  - `Assets/_EndLink/UI/Prefabs/PF_DebugPanel.prefab`
  - `Assets/_EndLink/UI/Prefabs/PF_EnemyHealthBar.prefab`，运行生成菜单后创建
  - `Assets/_EndLink/UI/Prefabs/PF_AimReticle.prefab`
  - `Assets/_EndLink/UI/Prefabs/PF_WeaponFormUI.prefab`
  - `Assets/_EndLink/UI/Generated/UI_Square64.png`

关键配置：
- `HUDCombatController.partyManager`：小队管理器，空时自动查找
- `HUDCombatController.partyCombatAction`：小队动作栏 UI 管理器
- `HUDCombatController.partyMemberPortraits`：小队成员头像 UI 集合
- `HUDCombatController.partyUltimateBar`：终链奥义条 UI
- `HUDCombatController.mainHealthBar / allySlotAHealthBar / allySlotBHealthBar`：主角与两个队友的状态血条；为空时按默认子物体名自动查找并接线
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
- `UIBalanceBar.balanceSource`：实现 `IBalanceSource` 的平衡来源；当前正式敌人使用 `EnemyBalance`
- `UIBalanceBar.fillImage`：平衡条填充图，推荐 Image Type 使用 Filled
- `UIBalanceBar.hideWhenFull`：满平衡时是否隐藏；主角和 Boss HUD 通常关闭
- `UIBalanceBar.autoRefresh`：事件刷新之外的兜底刷新开关，默认关闭
- `UIEnemyHealthBar.enemyHealth`：要显示的正式敌人生命组件；为空时可从父物体查找
- `UIEnemyHealthBar.worldOffset`：血条相对敌人锁定点或生命组件位置的世界偏移
- `UIEnemyHealthBar.backgroundColor` / `fillColor`：敌人血条背景和填充颜色，默认半透明暗红色
- `UIEnemyHealthBar.hideWhenFull` / `hideWhenDead`：满血和死亡时是否隐藏敌人头顶血条
- `UIAimReticle.aimController`：主角根物体上的 `PlayerAimController`；空时运行期查找一次
- `UIWeaponForm.weaponController`：主角根物体上的 `PlayerWeaponController`；空时运行期查找一次

</details>
