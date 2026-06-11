# EndLink Art Pipeline

本文档记录 EndLink 工程内美术资源的目录、锁文件、导入、元数据、Addressables、预览和 Review 方案。

当前阶段的目标是先形成一套轻量、清晰、可执行的协作规则，保证后续角色、敌人、场景、特效和 UI 美术资源进入工程时不会把目录、Prefab 和导入设置搞乱。

## 基本原则

1. 美术资源使用独立根目录 `Assets/Art`，与 `Assets/_EndLink` 的玩法代码和系统资源分开。不要放成 `Assets/_EndLink/Art`。
2. `Assets/Art` 主要放 Unity 需要导入和引用的资源，例如 FBX、贴图、材质、VFX、纯视觉 Prefab、UI 图片。
3. 带玩法脚本、状态机、碰撞和战斗组件的逻辑 Prefab 仍优先放在 `Assets/_EndLink` 对应模块中。
4. 视觉资源通过 `Visuals` 子物体或视觉 Prefab 挂接到逻辑根物体，避免美术改模型时影响玩法组件。
5. 源工程文件当前不纳入主仓库；如果以后确实要纳入，必须走 Git LFS，并重新讨论锁文件策略。
6. 文件名和目录名优先使用英文、数字、下划线。中文适合用于显示名、策划文档、备注和 Inspector 文案，不建议作为资源文件名。

## 目录结构

`Assets/Art` 与 `Assets/_EndLink` 同级，具体目录以工程实际结构为准。

目录用途：

| 目录 | 用途 |
|---|---|
| `Characters` | 我方角色视觉资源。 |
| `Enemies` | 敌人视觉资源。 |
| `Environments` | 关卡、地形、建筑、道具和场景装饰资源。 |
| `VFX` | 战斗特效、状态特效、环境特效和 Visual Effect Graph 资源。 |
| `UI` | HUD 图标、技能图标、血条、锁定标识和界面贴图。 |
| `Shared` | 多处共用的材质、贴图、Shader、Render Texture 等。 |
| `_Incoming` | 临时导入区，只用于还没整理命名和导入设置的新资源。整理后应移动到正式目录。 |

## 命名建议

文件名建议使用稳定前缀：

| 类型 | 前缀 | 示例 |
|---|---|---|
| 角色模型 | `CH_` | `CH_Player_Main_Body.fbx` |
| 敌人模型 | `EN_` | `EN_Aberrant_Wanderer_Body.fbx` |
| 场景物件 | `ENV_` | `ENV_DemoArena_Wall_A.fbx` |
| 材质 | `MAT_` | `MAT_Player_Main_Body.mat` |
| 贴图 | `T_` | `T_Player_Main_Body_BC.png` |
| 视觉 Prefab | `PF_` | `PF_Enemy_Aberrant_Visual.prefab` |
| 特效 | `VFX_` | `VFX_Hit_Overclock.prefab` |
| UI 图标 | `UI_` | `UI_Action_Overclock.png` |

贴图后缀建议：

| 后缀 | 含义 |
|---|---|
| `_BC` | Base Color / Albedo |
| `_N` | Normal |
| `_M` | Metallic |
| `_R` | Roughness |
| `_AO` | Ambient Occlusion |
| `_E` | Emission |
| `_Mask` | 通道打包遮罩 |


## Art 分支接入流程

美术资源使用独立 `art` 分支提交，程序主开发仍以 `dev` 分支为准。`art` 分支进入 `dev` 前，需要先经过整理分支。

推荐流程：

```text
art -> art-integration -> dev
```

当需要同步美术资源时，可以让 Agent 执行固定流程：

1. 检查 `dev...art` 的变更范围。
2. 如果变更超出 `Assets/Art`，先停止并报告，不直接合并。
3. 从当前 `dev` 建立 `art-integration` 分支。
4. 只整理 `Assets/Art` 内资源。
5. 按本文档命名规则统一改名和移动目录。
6. 文件重命名或移动时保留对应 `.meta`，避免 Unity 引用丢失。
7. 检查是否缺少 `.meta`、是否有中文临时命名、是否有不规范目录。
8. 检查是否存在明显丢引用或误挂玩法脚本到视觉 Prefab 的风险。
9. 输出资源清单、改名映射和风险报告。
10. 用户确认后，再由用户决定是否合并进 `dev`。

边界规则：

- Agent 不直接把 `art` 合进 `dev`。
- Agent 不擅自修改 `Assets/_EndLink`、`ProjectSettings`、`Packages` 或 `.unity` 场景文件。
- 美术可以在 `art` 分支使用中文临时命名，但进入 `dev` 前需要统一为英文工程命名。
- 如果必须修改非 `Assets/Art` 文件，先说明原因并等待确认。

## 推荐提交流程

当前阶段美术资源进入工程时，先按下面流程处理：

1. 新资源先进入 `Assets/Art/_Incoming` 或单独工作分支。
2. 整理命名和目录，移动到正式分类目录。
3. 检查导入设置，例如贴图类型、模型 scale、材质引用。
4. 视情况和需求继续。

## 逻辑 Prefab 与视觉 Prefab

Prefab 按职责放置，不建立全局大 `Prefabs` 文件夹。

核心规则：纯视觉资源跟随 `Assets/Art`，带玩法逻辑的 Prefab 跟随 `Assets/_EndLink` 对应模块。逻辑根物体通过 `Visuals` 子物体引用视觉 Prefab。

| Prefab 类型 | 推荐位置 |
|---|---|
| 只有模型、Renderer、Animator、材质引用、特效挂点 | `Assets/Art/.../Prefabs` |
| 挂了战斗、状态机、生命、碰撞、AI、输入、UI 逻辑脚本 | `Assets/_EndLink/<Module>/Prefabs` |
| Hitbox、Projectile、战斗判定类逻辑 Prefab | `Assets/_EndLink/Combat/Prefabs` |
| HUD、血条、动作槽位等运行时 UI Prefab | `Assets/_EndLink/UI/Prefabs` |
| 纯 UI 图片、图标、装饰图 | `Assets/Art/UI` |


## 锁文件策略

当前阶段不启用 Git LFS Lock 等强制锁定机制，只通过沟通说明正在编辑的高风险资源，避免多人同时修改同一个二进制或高冲突资源。

需要提前沟通的资源主要是角色、敌人、场景块、共享材质、Prefab、`.unity` 场景文件，以及未来可能纳入仓库的 `.fbx`、`.psd`、`.blend`、`.spp` 等源或二进制资源。

如果后续多人美术协作变多、同类资源频繁冲突，再考虑对特定二进制资源启用 Git LFS Lock。

## 导入规则自动化

第一阶段先用人工规范和少量检查，等资源量上来后再做 `AssetPostprocessor`。

未来可以自动化的内容：

| 类型 | 自动化目标 |
|---|---|
| 贴图 | 按目录设置 sRGB、Normal Map、最大尺寸、压缩格式、MipMap。 |
| 模型 | 统一 scale、法线切线、材质导入策略、动画导入开关。 |
| UI 图片 | 设置 Sprite 类型、透明度、压缩、Pixels Per Unit。 |
| VFX 资源 | 检查命名、目录、引用材质和贴图是否在合法路径。 |
| Prefab | 检查是否存在 `Visuals` 根、是否误挂玩法脚本到 `Assets/Art` 视觉 Prefab。 |

建议阶段：

1. 手动导入 + 文档规范。
2. 增加 Editor 检查工具，只提示问题，不自动修改。
3. 增加 `AssetPostprocessor`，对明确规则自动修正。
4. 在提交前或 CI 中跑资源检查。

当前不要过早自动改资源，因为导入规则还没有稳定，强自动化容易误伤。

## 资产目录与元数据

资源数量增加后，需要一个轻量资产目录，记录“这个资源是什么、属于谁、用在哪里、当前状态如何”。

第一版可以是文档或表格，不急着写系统。后续如果需要进入 Unity 工作流，可以做 ScriptableObject 资产目录。

建议元数据字段：

| 字段 | 含义 |
|---|---|
| `assetId` | 稳定资源 ID，例如 `enemy.aberrant.wanderer.visual`。 |
| `displayName` | 中文显示名，例如“异常程序游离体”。 |
| `category` | 角色、敌人、场景、特效、UI。 |
| `status` | 计划中、白模、制作中、可用、需重做、废弃。 |
| `owner` | 当前负责人。 |
| `sourcePath` | 源工程文件路径。 |
| `unityPath` | Unity 内资源路径。 |
| `previewPath` | 预览图或截图路径。 |
| `usedBy` | 被哪些 Prefab、场景或系统使用。 |
| `notes` | 备注、限制和后续处理事项。 |

如果后续做工具，建议放在 `Assets/_EndLink/Editor` 中，数据资产放在 `Assets/_EndLink/Data` 或 `Assets/Art/_Catalog`，具体位置到时再定。

## Addressables 策略

当前 Demo 阶段不接入 Addressables，资源先以直接引用为主。本节只作为未来方向记录。

以后出现资源量明显增大、需要异步加载、按关卡/角色包拆分资源，或需要远程更新内容时，再考虑接入 Addressables。届时 key 应使用稳定资源 ID，而不是直接依赖文件名。

## 自动预览与 Review

当前阶段以人工检查为主，自动预览只作为未来方向记录。

资源提交前先确认：路径和命名符合规范、引用没有丢失、视觉 Prefab 没有误挂玩法脚本、运行后没有明显材质丢失或比例错误。

以后资源量增加后，可以补 Editor 工具自动生成视觉 Prefab 预览图，并检查缺失引用、目录命名和导入设置；再往后可接入提交前脚本或 CI 输出资源检查报告。