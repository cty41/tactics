# Roguelike 节点重新访问功能修复

## TL;DR

> **问题**: Roguelike 地图中已访问的节点无法再次点击进入
> 
> **原因**: `MapRevealSystem` 将已访问节点标记为"透明视野锚点"但不会重新设为 `Reachable`；`IsNodeClickable` 只允许 `Reachable` 状态可点击
> 
> **解决方案**: 修改 BFS 逻辑，让已访问的直接邻居节点也变成 `Reachable` 状态

---

## Context

### 问题描述

在 Roguelike 地图系统中，玩家访问过的节点无法再次点击进入。设计上应该支持往回走的功能，但目前实现存在以下问题：

1. **NodeState 枚举定义** (`RoguelikeMapNode.cs:13-19`):
   - `Visited` 状态注释为"半透明，**不可点击**"
   
2. **IsNodeClickable 方法** (`NodeStateManager.cs:108-114`):
   - 只允许 `Reachable` 状态的节点可点击
   
3. **MapRevealSystem.UpdateReveal** (`MapRevealSystem.cs:85-89`):
   - Visited 节点只作为"透明视觉锚点"继续传播视野
   - **不会**将已访问的邻居节点重新设为 `Reachable`

### 根因分析

```
当前流程：
玩家访问节点 A → A 变成 Visited
BFS 计算视野 → A 的邻居 B 变成 Reachable
玩家访问 B → B 变成 Visited
BFS 计算视野 → B 的邻居 A 仍是 Visited（不会变回 Reachable）
结果 → A 无法点击
```

---

## Work Objectives

### 核心目标

修复 Roguelike 地图节点重新访问功能，让玩家可以往回走到已访问的节点。

### 具体交付物

- 修改 `MapRevealSystem.cs` 的 BFS 逻辑
- 确保已访问的直接邻居（1 hop）可以重新变为 `Reachable` 状态

### 完成标准

- [ ] 已访问节点的直接邻居可以重新点击
- [ ] 非直接邻居的已访问节点保持不可点击（防止跳跃）
- [ ] 视野计算逻辑正常工作

### 必须实现

- 已访问的直接邻居节点变为 `Reachable` 状态
- 保持 BFS 视野传播的透明性（Visited 节点仍可穿透视野）

### 禁止实现

- 不能让所有已访问节点都可点击（只能是直接邻居）
- 不能破坏现有的视野揭示逻辑

---

## TODOs

- [x] 1. 修改 MapRevealSystem.UpdateReveal 方法

  **What to do**:
  - 修改 `MapRevealSystem.cs` 第 85-89 行的逻辑
  - 当 `neighborNode.state == NodeState.Visited` 时，检查是否为直接邻居（1 hop）
  - 如果是直接邻居，将状态设为 `Reachable`
  - 保持 BFS 队列的继续传播

  **Must NOT do**:
  - 不能让非直接邻居的已访问节点变为 Reachable
  - 不能破坏 Visited 节点的视野穿透特性

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 单文件修改，逻辑清晰，风险低
  - **Skills**: []
    - 无需特殊技能

  **References**:

  **Pattern References**:
  - `MapRevealSystem.cs:85-89` - 当前 Visited 节点处理逻辑（需要修改）
  - `MapRevealSystem.cs:91-95` - Reachable 状态设置逻辑（参考模式）

  **API/Type References**:
  - `NodeState` 枚举 - 状态定义
  - `RoguelikeMapNode.state` - 节点状态字段

  **Acceptance Criteria**:

  **QA Scenarios**:

  ```
  Scenario: 已访问节点的直接邻居可以重新点击
    Tool: Unity Editor Play Mode
    Preconditions: 玩家已访问节点 A，节点 B 是 A 的直接邻居且已访问
    Steps:
      1. 从节点 B 出发，计算视野
      2. 检查节点 A 的状态
      3. 验证 A 的状态为 Reachable
      4. 点击节点 A
      5. 验证可以成功进入节点 A
    Expected Result: 节点 A 可以被点击并进入
    Evidence: .sisyphus/evidence/task-1-reentry-test.png

  Scenario: 非直接邻居的已访问节点不可点击
    Tool: Unity Editor Play Mode
    Preconditions: 玩家已访问节点 A，节点 C 是 A 的间接邻居（2+ hops）且已访问
    Steps:
      1. 从当前位置计算视野
      2. 检查节点 C 的状态
      3. 验证 C 的状态为 Visited（不可点击）
    Expected Result: 节点 C 保持 Visited 状态，不可点击
    Evidence: .sisyphus/evidence/task-1-non-neighbor-test.png
  ```

  **Commit**: YES
  - Message: `fix(map): 修复已访问节点无法重新进入的问题`
  - Files: `Assets/Tactics/Scripts/RoguelikeMap/MapRevealSystem.cs`

---

## Final Verification Wave

- [ ] F1. **功能验证** - 在 Unity Editor 中测试节点重新访问功能
- [ ] F2. **回归测试** - 验证原有视野揭示逻辑正常工作

---

## Success Criteria

### 验证命令

```bash
# 在 Unity Editor 中运行游戏
# 1. 访问节点 A
# 2. 移动到节点 B（A 的邻居）
# 3. 尝试点击节点 A
# Expected: 节点 A 可以被点击并进入
```

### 最终检查清单

- [ ] 已访问的直接邻居可以重新点击
- [ ] 非直接邻居保持不可点击
- [ ] 视野计算逻辑正常
- [ ] 无编译错误
