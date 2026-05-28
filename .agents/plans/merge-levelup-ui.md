# 重构：合并属性加点与技能选择为统一升级界面

## 目标

新建 `LevelUpPanel.uxml` + `LevelUpPanelController.cs`，替换单独的 `AttributeAllocation` 和 `SkillSelection` 两个界面。

## 布局

```
┌──────────────────────────────────────────────────────┐
│  Lv.2 战士  剩余点数: 1                                │
├──────────────────────┬───────────────────────────────┤
│ 属性 (左)            │ 技能 (右)                       │
│                      │                               │
│ 力量   5  [+][-] +0  │ ○ 野蛮斩击  物理  Lv1          │
│ 敏捷   5  [+][-] +0  │   对前方敌人造成物理伤害          │
│ 体质   5  [+][-] +0  │                               │
│ 智力   5  [+][-] +0  │ ○ 冲锋      物理  Lv1          │
│ 精神   5  [+][-] +0  │   冲向目标并造成伤害              │
│ 幸运   5  [+][-] +0  │                               │
│                      │ ○ 旋风斩    物理  Lv1          │
│ 派生属性              │   对周围所有敌人造成伤害           │
│ 物理攻击: 10         │                               │
│ 魔法攻击: 10         │                               │
│ 生命上限: 50         │                               │
│ 法力上限: 0          │                               │
│ 速度: 5.0           │                               │
│ 物理防御: 1          │                               │
│ 魔法防御: 0          │                               │
│ 闪避: 0%            │                               │
│ 状态抗性: 0%         │                               │
├──────────────────────┴───────────────────────────────┤
│                    [确认] (加点完成+技能已选才可点)        │
└──────────────────────────────────────────────────────┘
```

## 实现

### Step 1: 创建 UXML
`Assets/Tactics/Arts/UI/LevelUpPanel.uxml` ✅
- 垂直 Flex 布局
- 顶部: CharacterNameLabel + PointsRemainingLabel
- 中部: 水平分割 → 左 AttributeRows(动态) + DerivedStats, 右 SkillList(动态)
- 底部: ConfirmButton

### Step 2: 创建 Controller
`Assets/Tactics/Scripts/UI/LevelUpPanelController.cs` ✅
- 继承 UIControllerBase
- SetCharacter(CharacterDefinition) + SetSkillOptions(List<SkillDefinition>)
- 派生属性计算: 物攻=Strength×2, 魔攻=Intelligence×2, 生命=Constitution×10, 法力=Charisma×10, 速度=Speed, 物防=DefenceFactor, 魔防=Charisma, 闪避=Agility×2%, 抗性=Charisma×2%
- 确认按钮: 属性点用尽 且 (无技能 或 技能已选) 才可启用
- OnConfirm 事件

### Step 3: 注册 UIManager
- UIId 新增 `LevelUp` ✅
- EnsureUIController 注册 ✅
- GetAssetPath 映射 ✅

### Step 4: 修改 BattleSettlementFlow
- 用单一 `ShowLevelUpAsync` 替换 `ShowAttributeAllocationAsync` + `ShowSkillSelectionAsync` ✅
