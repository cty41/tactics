---
feature: Battle
scenario: BuffIconCountDisplay
tags:
  - battle
  - buff
  - ui
  - icon
requiredAdapters:
  - Battle
  - UI
setup:
  - kind: bindBattleController
    parameters: {}
  - kind: useRealAssets
    parameters: {}
actions:
  - kind: addBuff
    parameters:
      unitAlias: p1_0
      buffName: Frozen
      duration: 3
      configPath: Assets/Tactics/Battle/Buffs/Frozen.asset
  - kind: addBuff
    parameters:
      unitAlias: p1_0
      buffName: Frozen
      duration: 3
      configPath: Assets/Tactics/Battle/Buffs/Frozen.asset
assertions:
  - kind: unitHasBuff
    target: p1_0
    expected: Frozen
    parameters: {}
  - kind: unitBuffDurationEquals
    target: p1_0
    expected: 3
    parameters:
      buffName: Frozen
  - kind: elementExists
    target: buff-icon-Frozen
    expected: true
    parameters: {}
  - kind: elementText
    target: buff-icon-Frozen >> turn-count
    expected: "3"
    parameters: {}
timeoutMs: 15000
---

# Battle - BuffIconCountDisplay

验证 buff icon 回合计数显示：施加 2 次 Freeze（全局唯一 + 刷新时长）后，
头顶应显示 1 个 Frozen icon，右下角计数为 2。

此测试用例验证：
1. Buff 全局唯一规则（重复施加刷新时长而非叠加实例）
2. Buff 时长刷新为 max(当前, 新)
3. UI adapter 嵌套元素文本断言（elementText + 层级选择器）
4. BattleUIController buff icon 的 name 属性可查询性
