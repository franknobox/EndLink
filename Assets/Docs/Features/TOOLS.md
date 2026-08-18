# Tools Features

Editor 工具、数据创建工具和调试监视窗口详情。

主索引见 [FEATURES.md](../FEATURES.md)。

<a id="feature-combat-data-tool"></a>

### Feature：战斗数据编辑工具

<details>
<summary>展开详情</summary>

功能说明：
- 通过菜单 `EndLink > Combat Data Tool` 打开。
- 提供“动作”“标签定义”“组合规则”和“资源列表”四个标签页。
- 前三个标签页分别用于快捷创建 `CombatActionDefinition`、`CombatTagDefinition` 和 `CombatTagCombinationRule`。
- `Actions` 页支持选择动作类型，创建时会同步初始化 Action Id 和显示名称。
- `Tag Definitions` 页支持创建时填写 Tag Id、显示名称、等级、默认持续时间和最大层数。
- `Combination Rules` 页支持创建时填写输入标签、所需层数、优先级和第一条反应效果，并同步显示控制、资源、自定义效果所需的数值、半径或效果 ID 字段。
- `Asset List` 标签页会列出三个数据目录下已有的数据资产；动作摘要覆盖伤害类型、固定伤害、攻击倍率、命中强度、平衡伤害、动作时序、Root Motion、反馈、Hitbox 和连携协同率。
- 资源列表会提示重复 Action Id、重复 Tag Id、缺失 Hitbox 和无效组合规则，但不会自动修改数据资产。
- 创建资产后会自动选中并 Ping 到 Project 窗口，复杂字段继续在 Inspector 中编辑。
- 工具会确保目标目录存在，当前固定使用项目约定的数据路径。

对应脚本：
- `Assets/_EndLink/Editor/CombatDataToolWindow.cs`

相关数据目录：
- `Assets/_EndLink/Data/CombatData/Actions`
- `Assets/_EndLink/Data/CombatData/Tags/Definitions`
- `Assets/_EndLink/Data/CombatData/Tags/CombinationRules`

</details>

<a id="feature-probuilder-mesh-repair"></a>

### Feature：ProBuilder 网格修补

<details>
<summary>展开详情</summary>

功能说明：
- 通过 `EndLink > Level > ProBuilder Mesh Repair` 打开，扫描当前选择或手动指定的 `ProBuilderMesh`。
- 默认“面朝向”页查询与相邻面多数方向不一致的疑似反向面，并在 Scene View 显示面边缘与法线；可翻转勾选结果，也可直接翻转 ProBuilder 当前选面。
- “边界封口”页作为独立的手动拓扑工具，按共享顶点识别闭合边界、开放边链、歧义区域和非流形边；这些边界不计入默认问题数量，也不代表网格一定存在错误。
- 支持封闭一个或多个闭合边界：近平面开口生成单个多边形面，楼梯底部等空间开口按较短对角线拆成三角面组补齐；单面封口的平面误差阈值可调。
- 支持桥接两圈闭合且顶点数相同的边界，工具会按空间距离自动匹配两圈顶点。
- 新面方向可手动反转；修补操作支持 Unity Undo，并在完成后刷新同物体的 `MeshCollider`。
- 工具不会一键修复全部开放边界，也不会处理开放边链或带分叉的歧义拓扑，避免误封门洞、窗口和其他有意保留的开口。

对应脚本：
- `Assets/_EndLink/Editor/ProBuilderMeshRepairWindow.cs`

相关 Editor 工具：
- `EndLink > Level > ProBuilder Mesh Repair`

</details>

<a id="feature-input-binding-tool"></a>

### Feature：输入绑定工具

<details>
<summary>展开详情</summary>

功能说明：
- 通过 `EndLink > Input > Binding Tool` 打开，直接读取 `InputSystem_Actions.inputactions` 中的项目默认绑定。
- 提供“键盘与鼠标”和“手柄”两个子页面，按游戏操作与界面操作分组，以中文操作名和按键槽展示绑定。
- 点击按键槽即可监听修改；移动、界面导航等复合绑定会拆成上、下、左、右四行，并保留同一方向的主副绑定。
- 普通绑定和复合绑定的具体方向支持监听与清除；跨键鼠/手柄共用的系统默认槽位保持只读，避免单页修改同时破坏另一设备。
- 原始 Input System Path 默认隐藏，可通过“高级路径”展开并直接编辑普通槽位。
- 监听时只接收当前槽位对应的设备类型，鼠标点击不会误写到手柄槽位，特殊轴输入仍可通过原始路径编辑。
- 自动检查同一控制方案中的完全重复路径和父子路径占用，例如右摇杆整体与右摇杆方向同时绑定。
- 每个设备页分别显示冲突摘要，同时区分“冲突关系组数”和“受影响绑定槽位数”，黄色图标数量对应当前页受影响槽位。
- 修改先保存在窗口内存副本中，支持 Undo、放弃修改和未保存提示；只有点击“应用到 Input Actions”后才写回源资产并触发重导入。
- Play Mode 中只允许查看；工具不修改生成的 `InputSystem_Actions.cs`，也不负责未来玩家运行时改键的本地持久化。

对应脚本：
- `Assets/_EndLink/Editor/InputBindingToolWindow.cs`

相关 Editor 工具：
- `EndLink > Input > Binding Tool`

</details>

<a id="feature-combat-hud-generator"></a>

### Feature：战斗 HUD 生成工具

<details>
<summary>展开详情</summary>

功能说明：
- 通过菜单 `EndLink > UI > Combat HUD` 下的入口执行。
- 工具使用 Unity Editor API 生成 UGUI Panel Prefab，不手写 `.prefab` 文本。
- `Create Current Prefabs` 会生成当前仍在使用的运行时调试面板、敌人头顶血条、瞄准准星和武器形态显示，也可以分别生成单个 Prefab。
- `PF_DebugPanel` 挂载 `HUDDebugLogPanel`，用于显示运行时战斗、队友和小队命令日志。
- `PF_EnemyHealthBar` 使用 World Space Canvas、`UIEnemyHealthBar` 和 `UIHealthBar`，生成后可挂到正式敌人视觉层级。
- `PF_AimReticle` 是不带绝对屏幕坐标的居中 UGUI Panel，挂载 `UIAimReticle` 后按 B 形态瞄准状态显隐。
- `WeaponFormUI` 使用右下角推荐锚点，中央显示当前 A/B/C 形态，上下箭头只作切换方向提示。
- 旧版小队状态、动作槽和终链奥义 UI 已归档在 `UI/PartyUI`，生成器不再创建或覆盖对应 Prefab。
- 工具只生成 Prefab 资产，不直接修改当前场景；缺失基础方形 Sprite 时才会补建。

对应脚本：
- `Assets/_EndLink/Editor/CombatHUDPrefabGenerator.cs`
- `Assets/_EndLink/UI/HUDDebugLogPanel.cs`
- `Assets/_EndLink/UI/UIEnemyHealthBar.cs`
- `Assets/_EndLink/UI/UIHealthBar.cs`
- `Assets/_EndLink/UI/UIAimReticle.cs`
- `Assets/_EndLink/UI/UIWeaponForm.cs`

生成路径：
- `Assets/_EndLink/UI/Prefabs/PF_DebugPanel.prefab`
- `Assets/_EndLink/UI/Prefabs/PF_EnemyHealthBar.prefab`
- `Assets/_EndLink/UI/Prefabs/PF_AimReticle.prefab`
- `Assets/_EndLink/UI/Prefabs/PF_WeaponFormUI.prefab`
- `Assets/_EndLink/UI/Generated/UI_Square64.png`

相关 Editor 工具：
- `EndLink > UI > Combat HUD > Create Current Prefabs`
- `EndLink > UI > Combat HUD > Create Debug Panel`
- `EndLink > UI > Combat HUD > Create Enemy Health Bar`
- `EndLink > UI > Combat HUD > Create Aim Reticle`
- `EndLink > UI > Combat HUD > Create Weapon Form UI`

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

<a id="feature-art-asset-validator"></a>

### Feature：美术资源校验工具

<details>
<summary>展开详情</summary>

功能说明：
- `Assets/Art/_Incoming` 中的资源完成导入、删除或移动后，会自动延迟执行一次静态校验。
- 自动校验只报告问题，不会擅自替换 Shader、修改模型 Rig、调整贴图或重写 Prefab。
- `Error` 和 `Warning` 会自动输出到 Console；`Info` 只在窗口显示，避免普通命名提示刷屏。
- 通过 `EndLink > Validation > Asset Validator` 打开结果窗口，可以切换扫描 `_Incoming` 或整个 `Assets/Art`。
- 窗口支持按严重程度和关键字筛选、复制报告，并可直接选中和定位问题资源。
- 材质检查覆盖缺失 Shader、错误 Shader、不受支持 Shader，以及 URP 工程中的 Legacy/Built-in Shader。
- Prefab 检查覆盖 Missing Script、丢失材质、Renderer 材质 Shader；如果临时 Prefab 挂有任何外部或项目脚本，会汇总脚本类型并提示人工确认是否需要保留。
- FBX 检查覆盖动画导入开关、默认 Clip 名和无效 Humanoid Avatar；`Loop Time` 等动作使用配置不作为通用资源健康问题。
- 贴图检查覆盖 Normal Map 命名与导入类型不匹配、超大最大导入尺寸提示。
- `_Incoming` 临时命名检查覆盖非 ASCII 名称、空格路径和常见副本括号命名；这些提示用于转正前整理，不阻止临时导入。

对应脚本：
- `Assets/_EndLink/Editor/EndLinkAssetValidation.cs`
- `Assets/_EndLink/Editor/EndLinkIncomingAssetPostprocessor.cs`
- `Assets/_EndLink/Editor/EndLinkAssetValidatorWindow.cs`

相关 Editor 工具：
- `EndLink > Validation > Asset Validator`

</details>

<a id="feature-scene-doctor"></a>

### Feature：场景配置体检

<details>
<summary>展开详情</summary>

功能说明：
- 通过 `EndLink > Validation > Scene Doctor` 打开，只扫描当前活动场景。
- 第一版只报告问题，不会添加组件、修改 Layer、重写引用或标记场景为已修改。
- 通用检查覆盖 Missing Script、已丢失的序列化对象引用，以及违反 `DisallowMultipleComponent` 的重复组件。
- 玩家与镜头检查覆盖固定主控关键组件、生命/索敌/连段/射击瞄准/格挡/Animator 桥接、三种武器形态动作组、Main Camera、Cinemachine Brain 和视角模式入口；不再检查旧按键交互组件。
- 战斗与敌人检查覆盖场景反馈调度器、正式敌人生命/平衡/标签/目标/状态/感知/移动/动作接线、视觉根和敌人 Collider Layer。
- 数据与导航检查覆盖场景实际引用的 Action Id、Hitbox Prefab、重复 Action Id、推荐 Layer、NavMesh 数据和启用但未落在 NavMesh 上的 Agent。
- 白盒 Prefab 检查允许 `BOX_` 根节点自由摆放，只警告子节点额外的 Position/Rotation/Scale Override，以及接近零、负数或异常巨大的子节点变换。
- 几何检查会校验同物体 `MeshFilter` 与 `MeshCollider` 的网格引用，并比较激活对象的 Renderer/非 Trigger Collider Bounds；Trigger、隐藏 Renderer、CharacterController 和 `ObjInteractable` 检测体不参与空间对齐检查。
- 场景归类检查按 `_Blockout/Env`、`_Blockout/Interact`、`_Gameplay/SpawnPoints`、`Checkpoints`、`CombatZones` 和 `_Systems` 约定报告放错区域的明确玩法组件，只提供 Warning 和定位。
- Layer/Tag 检查要求 `_Blockout/Env` 实体碰撞使用 `Environment`、`ObjInteractable` 碰撞子物体使用 `Interactable`，普通白盒保持 `Untagged`；玩家、敌人与 MainCamera 延续既有规则。
- 窗口支持按严重程度、类别和关键字筛选，问题对象可直接定位，并可复制完整文本报告。

对应脚本：
- `Assets/_EndLink/Editor/SceneValidationIssue.cs`
- `Assets/_EndLink/Editor/SceneValidator.cs`
- `Assets/_EndLink/Editor/SceneDoctorWindow.cs`

相关 Editor 工具：
- `EndLink > Validation > Scene Doctor`

</details>
