# Battle UI Skill Panel 重构

## 任务目标

1. 将 Skill1Button 和 Skill2Button 收束到一个独立的 SkillPanel 中，与 MoveButton 垂直排列
2. SkillPanel 内部按钮水平排列
3. 添加 Skill3 和 Skill4 按钮
4. 为将来基于 Unit 身上的 skill 动态创建对应数量的 skill 按钮做准备

## 当前结构

### Battle.uxml (当前)
```
BottomPanel (row)
├── LeftActionPanel (column)
│   ├── MoveButton
│   ├── Skill1Button
│   └── Skill2Button
└── RightActionPanel (column)
    └── EndTurnButton
```

### BattleUIController.cs (当前)
- 单独查询 `_skill1Button` 和 `_skill2Button`
- 单独绑定点击事件

## 目标结构

### Battle.uxml (目标)
```
BottomPanel (row)
├── ActionPanel (column)
│   ├── MoveButton
│   └── SkillPanel (row)
│       ├── Skill1Button
│       ├── Skill2Button
│       ├── Skill3Button
│       └── Skill4Button
└── RightActionPanel (column)
    └── EndTurnButton
```

### BattleUIController.cs (目标)
- 新增 `_skillPanel: VisualElement` 字段
- 将 Skill 按钮统一管理，为动态创建预留接口
- 保留 Skill1-4 的硬编码引用（过渡方案）
- 新增 `CreateSkillButtons(IUnit unit)` 方法框架（将来实现动态创建）

## 实施步骤

### Step 1: 修改 Battle.uxml

**文件:** `Assets/Tactics/Arts/UI/Battle.uxml`

修改 LeftActionPanel 结构：
- 将 LeftActionPanel 改名为 ActionPanel（更准确的语义）
- 在 MoveButton 下方新增 `SkillPanel` 容器（class: `skill-panel`）
- 将 Skill1Button 和 Skill2Button 移入 SkillPanel
- 新增 Skill3Button 和 Skill4Button 到 SkillPanel

### Step 2: 修改 Battle.uss

**文件:** `Assets/Tactics/Arts/UI/Battle.uss`

新增样式：
```css
.skill-panel {
    flex-direction: row;
    gap: 8px;
}
```

修改 `.left-panel` 为 `.action-panel`（名称变更）

### Step 3: 修改 BattleUIController.cs

**文件:** `Assets/Tactics/Scripts/UI/BattleUIController.cs`

#### 3.1 字段变更
- 移除: `_skill1Button`, `_skill2Button`
- 新增: `_skillPanel: VisualElement`
- 新增: `_skillButtons: List<Button>` 用于管理所有技能按钮

#### 3.2 WireButtons 变更
- 查询 `SkillPanel` 而非单独的 Skill1Button 和 Skill2Button
- 通过 `Q<Button>("Skill1Button")`、`Q<Button>("Skill2Button")` 等查询按钮
- 使用 `Q<Button>("Skill3Button")`、`Q<Button>("Skill4Button")` 查询新按钮
- 将所有 skill 按钮添加到 `_skillButtons` 列表
- 绑定点击事件，使用统一的 `OnSkillButtonClicked(int skillIndex)` 处理

#### 3.3 UnwireButtons 变更
- 遍历 `_skillButtons` 列表解绑事件

#### 3.4 事件处理变更
- 移除 `OnSkill1Clicked` 和 `OnSkill2Clicked` 方法
- 新增 `OnSkillButtonClicked(int skillIndex)` 方法，接收技能索引参数

#### 3.5 动态创建预留
- 新增 `CreateSkillButtonsForUnit(IUnit unit)` 方法框架（空实现，带 TODO 注释）
- 该方法将来会：
  1. 清空 SkillPanel 现有内容
  2. 遍历 `unit.GetNonMoveAbilities()`
  3. 为每个 ability 动态创建 Button 并添加到 SkillPanel
  4. 绑定对应的点击事件

## 注意事项

1. **USS 类名变更**: `left-panel` → `action-panel`，需要同步修改 uxml 和 uss
2. **按钮文本**: 当前阶段 Skill1-4 使用占位文本（"技能1"-"技能4"），动态创建功能实现后从 ability 配置读取
3. **事件绑定**: 当前阶段所有 skill 按钮点击输出 Debug.Log，将来与具体 ability 绑定
4. **当前范围**: 本次仅重构 UI 布局和代码结构，动态创建功能预留接口，不在本次实施

## 验收标准

1. [ ] BattleUI 正常显示，MoveButton 在上方，SkillPanel 在下方水平排列
2. [ ] Skill1 和 Skill2 点击功能正常（输出 Debug.Log）
3. [ ] Skill3 和 Skill4 按钮可见且可点击
4. [ ] BottomPanel 布局正确，ActionPanel 和 RightActionPanel 分别位于左右两侧
5. [ ] 代码结构支持将来扩展为动态创建技能按钮（预留 `CreateSkillButtonsForUnit` 方法框架）
