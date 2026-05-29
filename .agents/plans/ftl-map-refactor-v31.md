# FTL 地图重构 v3.1：网格布局

## TL;DR

> **核心目标**：将地图生成算法从列式布局重构为网格布局（m×n 单元），每个网格单元内放置一个节点，节点位置在单元区域内随机。
>
> **关键变更**：
> - 配置新增 `gridColumns=5`, `gridRows=4` 参数
> - 节点位置从"列内随机"改为"网格单元内随机"
> - 连接规则保持不变（前向+侧向+后向）
> - `nodeCount = gridColumns × gridRows`
>
> **交付物**：
> - 更新的 `RoguelikeMapConfig`（新增网格参数）
> - 重构的 `RoguelikeMapGenerator`（网格布局算法）
> - 更新的 `RoguelikeMapEditorWindow`（适配新参数）
>
> **预计工作量**：Medium（3-4 个任务）
> **并行执行**：YES - 2 波

---

## Context

### 设计来源
- **设计文档**: `.agents/docs/ftl-style-map-generation-algorithm.md` (v3.1)
- **核心变化**: 从列式布局改为网格布局

### 当前实现（v3.0 列式布局）
```csharp
// 当前算法
float columnWidth = maxReachableDistance * 0.8f;
float yRange = sqrt(maxReachableDistance² - columnWidth²) * 0.8f;
int numColumns = max(Ceil(Sqrt(nodeCount * 1.5)), Ceil((nodeCount-2) / maxNodesPerColumn) + 2);

// 节点放置
for each column:
    x = col * columnWidth + random(-columnWidth*0.3, columnWidth*0.3)
    y = random(-yRange, yRange)
```

### v3.1 设计（网格布局）
```
网格参数：gridColumns=5, gridRows=4
单元尺寸：cellWidth = 地图宽度 / gridColumns
          cellHeight = 地图高度 / gridRows

节点放置：
for each cell (col, row):
    x = col * cellWidth + random(0.1, 0.9) * cellWidth
    y = row * cellHeight + random(0.1, 0.9) * cellHeight
```

---

## Scope

### In Scope
- [x] RoguelikeMapConfig 新增 gridColumns/gridRows 字段
- [x] RoguelikeMapGenerator 重写为网格布局算法
- [x] RoguelikeMapEditorWindow 适配新参数
- [x] 连接规则保持不变（前向+侧向+后向+20%）

### Out of Scope
- 连接规则修改
- 视野系统修改
- 编辑器 UI 修改
- 存档系统

---

## Tasks

### Task 1: 更新 RoguelikeMapConfig

- **目标**：新增网格布局参数
- **输入**：当前 `RoguelikeMapConfig.cs`
- **输出**：更新的配置类
- **验收标准**：
  - [x] 新增 `gridColumns` 字段（int，默认 5）
  - [x] 新增 `gridRows` 字段（int，默认 4）
  - [x] `nodeCount` 改为只读属性：`get => gridColumns * gridRows`
  - [x] 编译通过

### Task 2: 重构 RoguelikeMapGenerator

- **目标**：实现网格布局算法
- **输入**：更新的配置
- **输出**：重构的生成器
- **验收标准**：
  - [x] 计算网格单元尺寸（cellWidth, cellHeight）
  - [x] 在每个网格单元内随机放置节点
  - [x] 起点固定在最左侧列某个单元
  - [x] Boss 固定在最右侧列某个单元
  - [x] 连接规则保持不变
  - [x] 验证逻辑保持不变
  - [x] 编译通过

### Task 3: 更新 RoguelikeMapEditorWindow

- **目标**：适配新配置参数
- **输入**：更新的配置
- **输出**：更新的编辑器窗口
- **验收标准**：
  - [x] 配置加载适配新字段
  - [x] 编译通过

### Task 4: 测试验证

- **目标**：验证网格布局生成正确
- **输入**：所有修改的文件
- **输出**：可运行的网格布局地图
- **验收标准**：
  - [x] 地图生成成功
  - [x] 节点分布在 5×4 网格中
  - [x] 连接数量合理（每节点 2-4 个）
  - [x] 编辑器显示正常

---

## Execution Strategy

### Wave 1: 配置和生成器（Task 1, 2）
- Task 1 和 Task 2 可并行
- 阻塞 Task 3, 4

### Wave 2: 编辑器和测试（Task 3, 4）
- 依赖 Wave 1
- Task 3 和 Task 4 可并行

---

## Commit Strategy

- **Task 1-2**: `refactor(roguelike-map): 网格布局配置和生成器`
- **Task 3-4**: `refactor(roguelike-map): 编辑器适配和测试`

---

## Success Criteria

### 验证命令
```bash
# Unity 编译通过
# 打开编辑器 Tactics/RoguelikeMap Editor
# 点击 Generate
# 验证：
# 1. 节点分布在 5×4 网格中
# 2. 每节点 2-4 个连接
# 3. 编辑器显示正常
```
