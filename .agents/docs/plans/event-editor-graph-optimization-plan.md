# 事件编辑器Graph交互优化计划

> **版本**: v1.0  
> **日期**: 2026-05-13  
> **状态**: 待确认  
> **参考**: Unity Visual Scripting 编辑器模式

---

## Background

### 当前问题

| 问题 | 现状 | 影响 |
|------|------|------|
| **节点创建方式单一** | 固定位置工具栏按钮，节点强制刷在(100,100)等硬编码位置 | 无法在画布任意位置创建，效率低 |
| **连线交互简陋** | 点击端口A→点击端口B，无拖拽预览线，无取消机制 | 体验生硬，误操作无法撤销 |
| **节点移动单向通知薄弱** | OnNodeMoved只触发数据刷新，画布/连线更新依赖走全图重算 | 大图性能隐患 |
| **无右键上下文** | 无右键菜单（删除节点/复制/创建节点） | 操作路径过长 |
| **无网格/吸附** | 画布纯色背景，节点随意放置 | 难以对齐，视觉杂乱 |
| **创建连线中无视觉反馈** | pendingPort仅变色，无跟随鼠标的虚线 | 用户不知道正在"连接模式" |

### 目标

参考 Unity Visual Scripting 的编辑器模式，优化节点创建和连线交互体验。

---

## 参考：VS 关键模式 （已确认）

| VS 特性 | 实现方式 | 可借鉴度 |
|---------|---------|---------|
| **右键创建节点** | `FuzzyWindow` — 弹出模糊搜索窗口，输入关键词实时筛选节点类型，`Enter` 在鼠标位置创建 | ⭐⭐⭐⭐⭐ |
| **拖拽连线** | `FlowDragAndDropUtility` — 从端口 MouseDown 开始拖线，`GraphGUI.DrawConnection` 画贝塞尔曲线跟随鼠标，松手到目标端口完成连接 | ⭐⭐⭐⭐⭐ |
| **连线预览** | `GraphGUI.Hermite()` — 三次 Hermite 插值绘制平滑曲线，`DrawOverlay` 在画布层之上画临时的拖拽线 | ⭐⭐⭐⭐⭐ |
| **画布缩放/平移** | `CanvasControlScheme` — Ctrl+滚轮缩放，中键拖拽平移，视口限制 | ⭐⭐⭐⭐ |
| **端口定义** | `IUnitPort` / `ValueInput` / `ValueOutput` — 强类型端口，有 type 和 label | ⭐⭐⭐⭐ |
| **端口兼容** | `CanConnect()` — 检查端口类型、方向（输入/输出）、数据类型是否匹配 | ⭐⭐⭐⭐ |
| **图序列化** | `IGraphData` / `IGraphElementData` — 运行时和序列化分离，支持 Undo/Redo | ⭐⭐⭐ |
| **选择系统** | `GraphSelection` + `ISelected` 接口 — 统一管理节点/连线的选中状态 | ⭐⭐⭐ |
| **删除节点** | `Delete` 键 + `GraphElement.Delete()` — 自动删除关联连线 | ⭐⭐⭐⭐⭐ |
| **右键菜单** | `ContextualMenu` — 右键节点弹出"删除"/"复制"/"编辑"菜单 | ⭐⭐⭐⭐ |

---

## 优化任务

### Task 1: 连线拖拽交互（VS 参考：FlowDragAndDropUtility + GraphGUI.DrawConnection）

**目标**: 从端口拖拽出线，实时预览贝塞尔曲线，松手到目标端口完成连接

**具体实现**:
- 端口 `PointerDownEvent` 开始拖线，记录源端口
- `PointerMoveEvent` 中从源端口 center 到鼠标位置画临时曲线（复用 `ConnectionElement` 的贝塞尔算法，改为虚线 + 半透明）
- `PointerUpEvent` 检测鼠标下是否有另一端口，有则 `CreateConnection`，无则取消
- 拖线期间源端口和目标候选端口高亮（变色）
- `KeyDownEvent(Escape)` 取消拖线
- 连线成功/失败有回弹动画（可延后）

**涉及文件**: `EventGraphView.cs`（新增 `ConnectionDragHandler` 内部类或方法）

### Task 2: Fuzzy Finder 节点创建（VS 参考：FuzzyWindow）

**目标**: 右键画布或按 `Space` 弹出模糊搜索窗口，快速创建节点

**具体实现**:
- 右键空白画布 → 弹出搜索框 + 节点类型列表（Start/Option/Check/Success/Failure/End）
- 输入关键词实时筛选（如输入 "op" → 只显示 "Option"）
- `Enter` 或点击 → 在鼠标位置创建节点
- `Escape` 关闭搜索框
- 上下箭头选择候选项，高亮当前候选项
- 复用 `AddNodeToCanvas(type, mouseX, mouseY)`

**涉及文件**: `EventGraphView.cs`（新增 `ShowFuzzyFinder` 方法），新增 `FuzzySearchPopup.cs`

### Task 3: 右键上下文菜单（VS 参考：ContextualMenu）

**目标**: 右键节点弹出操作菜单，右键空白弹出创建菜单

**具体实现**:
- 使用 Unity 内置 `ContextualMenuManipulator`（简化版）或自定义 `DropdownMenu`
- 空白画布右键 → "Create Node..."（打开 Fuzzy Finder）/ "Paste"
- 节点右键 → "Delete" / "Duplicate"
- `Delete` 键删除选中节点 + 关联连线
- `Ctrl+C/V` 复制粘贴（Phase 2）

**涉及文件**: `EventGraphView.cs`, `EventNodeElement.cs`

### Task 4: 节点创建位置优化（附带改进）

**目标**: 节点出现在合理位置

**具体实现**:
- 工具栏按钮 → 画布视口中央（考虑 panOffset/zoom）
- 右键/Fuzzy Finder → 鼠标在画布上的世界位置
- 新节点做简单碰撞检查，避免完全覆盖已有节点（y+80偏移直到空位）

### Task 5: 连线过程中的预览虚线（VS 参考：DrawOverlay 临时线）

**目标**: 点击端口后（非拖拽模式），从选中端口到鼠标画预览虚线

**具体实现**:
- `HandlePortClick` 选中端口 → 开始跟踪鼠标
- 在 `PointerMoveEvent` 中从端口画半透明虚线到鼠标（`generateVisualContent` 或直接操控一个 VisualElement）
- 点击目标端口或空白画布结束预览 → 创建连线或取消
- 视觉：浅蓝色半透明虚线，区别于已完成的实线

**涉及文件**: `EventGraphView.cs`

### Task 6: 画布背景网格

**目标**: 添加网格背景辅助对齐

**具体实现**:
- 使用 `generateVisualContent` 绘制浅灰色网格线（小格20px，大格100px）
- 网格随 zoom/pan 变换缩放
- 可选：节点移动结束时吸附到最近网格点（Snap to Grid，按住 Alt 临时禁用）

### Task 7: 删除键 + 选择系统

**目标**: Delete 键删除选中节点及其连线

**具体实现**:
- 保持选中节点引用 `_selectedNode`
- `KeyDownEvent(Delete)` → 移除节点 + 移除所有关联 `_connections` + 触发 `OnGraphChanged`
- 支持多选（Shift+Click）可延后

---

## 实现优先级

| 优先级 | 任务 | VS 启发 | 工作量 | 状态 |
|--------|------|---------|--------|------|
| **P0** | Task 1 连线拖拽 | FlowDragAndDropUtility | 中 | ✅ 已完成 |
| **P0** | Task 2 Fuzzy Finder | FuzzyWindow | 中 | ✅ 已完成 |
| **P0** | Task 7 删除键 | GraphElement.Delete | 小 | ✅ 已完成 |
| **P1** | Task 3 右键菜单 | ContextualMenu | 小 | ✅ 已完成 |
| **P1** | Task 5 预览虚线 | DrawOverlay | 小 | ✅ 已完成 |
| **P1** | Task 4 节点位置优化 | — | 小 | ✅ 已完成 |
| **P2** | Task 6 画布网格 | — | 中 | ✅ 已完成 |

---

## 风险

| 风险 | 缓解 |
|------|------|
| 自定义画布（非 GraphView）实现拖拽连线复杂 | 参考 VS 源码简化实现，避免过度设计 |
| 右键菜单在不同 OS 行为有差异 | 使用 Unity 内置 `ContextualMenuPopulateEvent` |
| 大量节点时 draw 性能 | 限制可见区域外连线不绘制（viewport culling） |

---

## 待补充

- [x] VS 源码探索结果（package cache 分析）
- [x] 用户确认优先级和范围
