# Tools Features

Editor 工具、数据创建工具和调试监视窗口详情。

主索引见 [FEATURES.md](../FEATURES.md)。

<a id="feature-combat-data-tool"></a>

### Feature：战斗数据编辑工具

<details>
<summary>展开详情</summary>

功能说明：
- 通过菜单 `EndLink > Combat Data Tool` 打开。
- 提供 `Actions`、`Tag Definitions`、`Combination Rules` 和 `Asset List` 四个标签页。
- 前三个标签页分别用于快捷创建 `CombatActionDefinition`、`CombatTagDefinition` 和 `CombatTagCombinationRule`。
- `Actions` 页支持选择动作类型，创建时会同步初始化 Action Id 和显示名称。
- `Tag Definitions` 页支持创建时填写 Tag Id、显示名称、等级、默认持续时间和最大层数。
- `Combination Rules` 页支持创建时填写输入标签、所需层数、优先级和第一条反应效果。
- `Asset List` 标签页会列出三个数据目录下已有的数据资产，并显示动作类型、伤害类型、固定伤害、攻击力倍率、连携协同率、Tag 等级、规则优先级和效果数量等摘要。
- 创建资产后会自动选中并 Ping 到 Project 窗口，复杂字段继续在 Inspector 中编辑。
- 工具会确保目标目录存在，当前固定使用项目约定的数据路径。

对应脚本：
- `Assets/_EndLink/Editor/CombatDataToolWindow.cs`

相关数据目录：
- `Assets/_EndLink/Data/CombatData/Actions`
- `Assets/_EndLink/Data/CombatData/Tags/Definitions`
- `Assets/_EndLink/Data/CombatData/Tags/CombinationRules`

</details>

<a id="feature-combat-hud-generator"></a>

### Feature：战斗 HUD 生成工具

<details>
<summary>展开详情</summary>

功能说明：
- 通过菜单 `EndLink > UI > Combat HUD` 下的入口执行。
- 工具使用 Unity Editor API 生成 UGUI Panel Prefab，不手写 `.prefab` 文本。
- `Create All Panels` 会一次生成全部战斗 HUD 面板；也可以分别生成单个 Panel，方便手动拖到 Canvas 下调整位置。
- Panel Prefab 本身只保留推荐锚点和尺寸，根 RectTransform 的位置与 Z 会在生成时归零，拖到 Canvas 后再手动调整摆放。
- 左上生成 `PF_PartyStatusPanel`，包含生命条占位、主控头像和两个队友头像。
- 成员头像挂载 `UIPartyMemberPortrait`，用于显示 1/2/3 连携键位、连携窗口高亮和 Link Down 灰化。
- 左下生成 `PF_SkillPanel`，挂载 `UIPartyCombatAction`，并自动绑定 Q/E/F 三个主动技能槽。
- 右下生成 `PF_UltimatePanel`，挂载 `UIPartyUltimateBar`，显示当前奥义键位、协同率和终链奥义就绪颜色。
- 右上生成 `PF_DebugPanel`，挂载 `HUDDebugLogPanel`，用于按分类筛选显示运行时战斗、队友和小队命令日志。
- 生成的可见占位文案默认不放英文说明文字，只保留键位和运行时动态内容，避免白模阶段默认 HUD 出现无用英文标签。
- 生成的主角/队友状态条默认采用 `#659F67` 一档绿色；头像占位保持中性灰，技能槽保留区分配色，奥义条默认使用黄色充能。
- 工具会在缺失时创建基础圆形和方形 UI Sprite，便于白模阶段直接看到 HUD 结构。
- 工具只生成 Panel Prefab 资产，不直接修改当前场景。

对应脚本：
- `Assets/_EndLink/Editor/CombatHUDPrefabGenerator.cs`
- `Assets/_EndLink/UI/UIPartyMemberPortrait.cs`
- `Assets/_EndLink/UI/UIPartyUltimateBar.cs`
- `Assets/_EndLink/UI/HUDDebugLogPanel.cs`

生成路径：
- `Assets/_EndLink/UI/Prefabs/PF_PartyStatusPanel.prefab`
- `Assets/_EndLink/UI/Prefabs/PF_SkillPanel.prefab`
- `Assets/_EndLink/UI/Prefabs/PF_UltimatePanel.prefab`
- `Assets/_EndLink/UI/Prefabs/PF_DebugPanel.prefab`
- `Assets/_EndLink/UI/Generated/UI_Circle64.png`
- `Assets/_EndLink/UI/Generated/UI_Square64.png`

相关 Editor 工具：
- `EndLink > UI > Combat HUD > Create All Panels`
- `EndLink > UI > Combat HUD > Create Party Status Panel`
- `EndLink > UI > Combat HUD > Create Skill Panel`
- `EndLink > UI > Combat HUD > Create Ultimate Panel`
- `EndLink > UI > Combat HUD > Create Debug Panel`

</details>

<a id="feature-combat-lab"></a>

### Feature：EndLink Combat Lab

<details>
<summary>展开详情</summary>

功能说明：
- `EndLink Combat Lab` 是浏览器端战斗实验工具，通过打开 `Tools/CombatLab/index.html` 使用。
- 界面采用固定三栏布局，页面本身不滚动，Tag/规则/时间线等长内容只在各自列表区域内滚动。
- 工具用于脱离 Unity Play Mode 快速验证标签定义、标签层数、默认持续时间、反应规则、反应效果、基础伤害和连携窗口。
- 中心区域显示一个可配置血量的目标，目标身上可以手动添加和移除 Tag。
- 下方固定三名角色，每名角色有攻击力、一个 Skill 和一个 Link，动作伤害按 `Flat + Atk * Multiplier` 计算，并支持 `StructuralDamage` 与 `RuntimeDamage`。
- 左侧可以维护实验用 Tag 定义，包括 ID、显示名、等级、默认持续时间和最大层数。
- 左侧可以维护实验用反应规则，包括 `Tag A`、`Tag B`、所需层数、优先级和 `reactionEffects`。
- `reactionEffects` 支持 `ApplyTag`、`RemoveTag`、`DealDamage`，并保留 `SpreadTag`、`ApplyControl`、`InterruptAction`、`ModifyResource` 和 `CustomEvent` 作为实验入口。
- 触发协议反应后会打开 4 秒连携窗口，窗口期内可选择一名角色释放 Link，窗口关闭后 Link 按钮不可释放。
- 当前版本不读取 Unity `.asset`，也不写回项目资源，只作为战斗规则和策划设想的快速验证工具。

对应文件：
- `Tools/CombatLab/index.html`
- `Tools/CombatLab/app.js`
- `Tools/CombatLab/reaction-engine.js`
- `Tools/CombatLab/styles.css`
- `Tools/CombatLab/tests/reaction-engine.test.js`

验证命令：
- `node Tools\CombatLab\tests\reaction-engine.test.js`

</details>

<a id="feature-ally-monitor"></a>

### Feature：队友调试监视窗口

<details>
<summary>展开详情</summary>

功能说明：
- `AllyDebugLog` 是队友专用调试事件流，运行时代码只负责上报状态切换、事件响应、助战阶段、冷却等待和攻击执行等关键行为。
- `AllyMonitorWindow` 是 Editor 队友监视窗口，通过 `EndLink > Debug > Ally Monitor` 打开。
- 窗口上半部分显示当前场景所有 `AllyStateMachine` 的状态快照，包括状态、跟随目标、助战目标、到目标 Collider 表面的距离、攻击距离、重接近距离、冷却和当前 Action。
- 窗口下半部分显示队友行为日志，可以按队友对象和 `State / Brain / Assist / Combat / Follow` 分类过滤。
- `Capture` 控制是否采集队友调试事件，`Console` 控制是否同时镜像到 Unity Console，默认建议只看窗口避免刷屏。

对应脚本：
- `Assets/_EndLink/Ally/AllyDebugLog.cs`
- `Assets/_EndLink/Editor/AllyMonitorWindow.cs`
- `Assets/_EndLink/Ally/AllyStateMachine.cs`
- `Assets/_EndLink/Ally/AllyBrain.cs`
- `Assets/_EndLink/Ally/AllyAssistState.cs`
- `Assets/_EndLink/Ally/AllyCombatDriver.cs`

相关 Editor 工具：
- `EndLink > Debug > Ally Monitor`

</details>
