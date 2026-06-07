# EndLink Combat Lab

EndLink 战斗系统实验测试工具。当前版本用于快速验证标签定义、标签层数、持续时间、协议反应、反应效果、基础伤害和连携窗口。

打开方式：

1. 用浏览器打开 `Tools/CombatLab/index.html`。
2. 左侧维护 Tag 定义。Tag 支持 ID、显示名、等级、默认持续时间和最大层数。
3. 左侧维护反应规则。规则由 `Tag A + Tag B + 所需层数 + 优先级 + Effects` 组成。
4. 中间配置目标血量和目标身上的 Tag。
5. 下方三名角色各有攻击力、一个 Skill 和一个 Link。动作伤害按 `Flat + Atk * Multiplier` 计算。
6. 触发协议反应后，右侧连携窗口会打开 4 秒；窗口关闭时 Link 按钮不可释放。

Tag 写法：

- `ovl`
- `ovl:3`
- `ovl:2@6`

Effect 写法：

- `ApplyTag:meltdown`
- `ApplyTag:ovl:2@6`
- `RemoveTag:ovl`
- `DealDamage:35:RuntimeDamage`
- `DealDamage:20:StructuralDamage`
- `SpreadTag:ovl`
- `ApplyControl`
- `InterruptAction`
- `ModifyResource`
- `CustomEvent:stream_cascade`

多个 Effect 用英文分号分隔：

```text
ApplyTag:meltdown; DealDamage:35:RuntimeDamage; RemoveTag:ovl
```

当前版本不读取 Unity `.asset` 数据，也不写回项目资源。它只用于快速验证战斗系统设想。

测试：

```powershell
node Tools\CombatLab\tests\reaction-engine.test.js
```
