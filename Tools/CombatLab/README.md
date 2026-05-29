# EndLink Combat Lab

EndLink 战斗系统实验测试工具。当前版本先覆盖标签反应和基础伤害流程，后续可以继续承接伤害测算、连携规则、资源循环等实验。

打开方式：

1. 用浏览器打开 `Tools/CombatLab/index.html`。
2. 配置中心目标的最大血量、当前血量和已有 Tag。Tag 支持层数写法，例如 `Fire:3`。
3. 配置下方三名角色的 Skill / Link 伤害与施加 Tag，多个 Tag 用英文逗号分隔。
4. 点击 Skill 或 Link，观察目标血量、目标 Tag 和时间线变化。

规则写法：

- `Fire:3 => Burning`
- `Break, Shock => Stun`
- `Stun, Launch => Airborne`
- `Shield:3 => Clear`，结果为空时只消耗条件 Tag，不生成新 Tag

当前版本不读取 Unity `.asset` 数据，也不写回项目资产。它只用于快速验证战斗系统设想。

测试：

```powershell
node Tools\CombatLab\tests\reaction-engine.test.js
```
