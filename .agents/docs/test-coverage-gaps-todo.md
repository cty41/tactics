# 测试能力缺失 TODO

基于 Freeze 永久冻结和 Charge 击退时机两个 bug 的排查，发现以下测试覆盖缺口。

## 一、断言能力缺失

### 1. 负向 Buff 断言
- **需求**: `unitDoesNotHaveBuff` — 断言单位不拥有指定 buff
- **场景**: 验证 buff 过期后被正确移除
- **当前状态**: 只有 `unitHasBuff`（正向），无法表达"buff 应该已消失"

### 2. 单位行为状态断言
- **需求**: `unitCanAct` 断言在 freeze 测试中使用
- **场景**: 验证冻结后单位不可行动，解冻后恢复
- **当前状态**: `unitCanAct` 断言已实现但从未在 freeze 测试中使用

## 二、测试场景缺失

### 3. 多回合 Buff 生命周期测试
- **需求**: apply → decrement → decrement → expire 完整生命周期
- **场景**: Frozen duration=2，advanceTurn 2 次后断言 buff 消失
- **当前状态**: 所有 buff 测试只 advanceTurn 1 次

### 4. 冻结单位回合递减测试
- **需求**: 验证 `OnTurnEnd` 对 `CanAct=false` 的单位也能被调用
- **场景**: 冻结单位的 buff duration 应随回合递减
- **当前状态**: 无此测试（正是本次 bug 的根因）

### 5. Charge 阶段顺序验证
- **需求**: 验证 charge 执行顺序：接近 → 碰撞 → 击退
- **场景**: 记录各阶段执行时间戳，断言顺序正确
- **当前状态**: 只检查最终位置和血量，不检查顺序

### 6. 击退动画存在性验证
- **需求**: 断言击退过程中有位移动画（非瞬移）
- **场景**: 检查击退前后位置变化是否经过插值
- **当前状态**: 无动画断言机制

## 三、测试基础设施改进

### 7. 顺序/行为断言框架
- **需求**: 支持记录操作序列并断言顺序
- **实现在**: `BattleGameplayStepAdapter` 或新增 `SequenceAssertion`
- **优先级**: 中

### 8. 动画完成断言
- **需求**: 支持等待动画完成后再断言
- **实现在**: `SkillGameplayStepAdapter` 新增 `animationCompleted` 断言
- **优先级**: 低（SkillGraphTestWorld 的 mock movement 是空操作）

## 优先级排序

| 优先级 | 编号 | 内容 |
|--------|------|------|
| P0 | 1, 3, 4 | 负向 buff 断言 + 多回合生命周期 + 冻结递减 |
| P1 | 2, 5 | unitCanAct 使用 + Charge 顺序验证 |
| P2 | 6, 7, 8 | 动画断言 + 顺序框架 + 动画完成断言 |
