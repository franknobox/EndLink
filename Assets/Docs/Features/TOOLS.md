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
- `Asset List` 标签页会列出三个数据目录下已有的数据资产。
- 创建资产后会自动选中并 Ping 到 Project 窗口，具体字段继续在 Inspector 中编辑。
- 工具会确保目标目录存在，当前固定使用项目约定的数据路径。

对应脚本：
- `Assets/_EndLink/Editor/CombatDataToolWindow.cs`

相关数据目录：
- `Assets/_EndLink/Data/CombatData/Actions`
- `Assets/_EndLink/Data/CombatData/Tags/Definitions`
- `Assets/_EndLink/Data/CombatData/Tags/CombinationRules`

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
