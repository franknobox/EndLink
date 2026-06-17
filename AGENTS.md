# EndLink Agent Guide

这是指导 Agent 执行 EndLink 游戏开发任务的文档。凡是代码、Unity 资源、工程文档相关的开发任务，开始前都要先看本文件。

## 开始任务前

1. 先看仓库工程文件结构，确认要改的内容属于哪个模块。
2. 再看必要的工程文档，避免重复设计或违背已有边界。
3. 不确定某个 Unity API、包功能、版本行为时，或者想参考学习，可以查看本机 Unity 官方文档：

```text
E:\Unity_repo\Engine\6000.3.15f1\Editor\Data\Documentation
```

## 工程结构

工程内容主要分为运行时代码、项目数据、美术资源和工程文档几块。
主要游戏代码和项目数据集中在：

```text
Assets/_EndLink/
├─ Ally/                 队友状态机、助战、跟随和队友战斗执行
├─ Combat/               战斗通用系统
│  ├─ Damage/            伤害上下文、伤害结果和伤害计算
│  ├─ Events/            战斗事件总线和事件数据
│  ├─ Hitbox/            Hitbox、Projectile、命中信息和受击接口
│  ├─ Stats/             角色战斗数值
│  ├─ Tags/              战斗标签、标签容器和反应规则
│  └─ Target/            Combat Target 标准化
├─ Control/              输入读取、玩家移动、相机输入和外部位移接口
├─ Data/                 游戏数据资产目录
│  ├─ CombatData/        战斗动作、标签定义和标签反应规则数据
│  └─ ScenesData/        场景关联数据，例如 NavMesh 烘焙资产
├─ Editor/               Unity Editor 工具窗口和自定义 Inspector
├─ Enemies/              正式敌人基底、感知、状态机和能力组件
│  ├─ Abilities/         敌人移动和战斗执行能力
│  └─ StateMachine/      敌人大状态机
├─ Party/                固定三人小队、小队上下文和战斗路由
├─ Player/               玩家战斗、生命、索敌和状态机
│  └─ StateMachine/      玩家状态机
├─ Tests/                临时或必要测试脚本
└─ UI/                   运行时 HUD、动作槽位和血条组件
```

美术资源集中在：

```text
Assets/Art/
├─ Characters/           角色相关视觉资源
├─ Enemies/              敌人相关视觉资源
├─ Environments/         场景、地形、关卡视觉资源
├─ VFX/                  特效资源
├─ UI/                   UI 视觉资源
├─ Shared/               多模块复用资源
└─ _Incoming/            新接入或待整理资源
```

## 工程文档

仓库文档在：
```text
Assets/Docs/
```

主要文档：
- `README.md`：Docs 目录说明。
- `ROADMAP.md`：后续开发路线和阶段目标。
- `FEATURES.md`：当前功能索引，只保留概览、状态表和跳转。
- `Features/`：功能详情子文档。
- `ISSUE.md`：已知问题、技术债、设计债和需要后续回看的事项。
- `ART.md`：美术资源协作的规则文档。
- `Design/`：面向程序实现的策划文档。

Feature 子文档：
- `Features/PLAYER.md`
- `Features/COMBAT.md`
- `Features/PARTY.md`
- `Features/ALLY.md`
- `Features/ENEMIES.md`
- `Features/UI.md`
- `Features/TOOLS.md`

## 行动准则

1. 不要自己新建子文件夹或命名空间，除非用户明确要求或同意。
2. 命名参考现有文件，尽量不要超过 4 个单词组合。
3. 需要写测试脚本时，放在 `Assets/_EndLink/Tests`。如果测试只是临时验证，用完后删掉。
4. 没有明确要求时，不要改 `.unity` 场景文件。
5. 不要回滚或覆盖用户已有改动。遇到无关的未提交改动，忽略即可。
6. 不要执行破坏性 Git 操作，例如 `git reset --hard`、强制 checkout 或删除用户文件，除非用户明确要求。
7. 代码优先遵循当前目录、命名空间和脚本拆分方式，不要为了“更标准”主动重构。
8. Unity 生成文件不要手工维护。比如 `InputSystem_Actions.cs` 应由 `.inputactions` 生成。
9. 如果一个问题可以靠简单的动手配置操作解决（比如某某组件我忘记添加了），就不用另外写代码。

其他工程实践、测试、调试、前端或协作流程，按 Agent 自身预设和所用 Skills 执行。

## 文档更新规则

任务做完后，如果新增或修改了功能，需要主动更新 Feature 文档：

1. 更新主索引：
```text
Assets/Docs/FEATURES.md
```
2. 更新对应子文档：
```text
Assets/Docs/Features/*.md
```
3. 不要主动更新 `ROADMAP.md`以及 Assets\Docs\Design 文件夹中的文档，除非用户明确要求。

4. 技术债、设计债、反复出现的问题，记录到：
```text
Assets/Docs/ISSUE.md
```

## 完成任务时

最终回复要说明：

- 做了哪些关键改动。
- 增/删/改了哪些主要文件。
- 运行了哪些验证。
- 如果没有运行测试或验证，需要明确说明原因。
