# Roguelike事件编辑器 — 设计文档

> **版本**: v1.0  
> **日期**: 2026-05-12  
> **状态**: 设计完成  
> **关联设计**: [roguelike-map-gameplay-design.md](../design/roguelike-map-gameplay-design.md)  
> **关联计划**: [roguelike-event-editor-开发计划.md](../plans/roguelike-event-editor-开发计划.md)

---

## TL;DR

**一句话**: 用 UI Toolkit Editor + Visual Scripting 做一个**可见即所得的Roguelike事件编辑器**，策划拖拽节点图来创建事件，一键导出JSON供游戏运行时使用。

**核心价值**: 
- 策划无需编写代码即可创建复杂事件
- 实时预览事件在游戏中的表现
- 标准化事件数据结构，保证数据一致性

---

## 设计决策

| 决策项 | 选择 |
|--------|------|
| 编辑器框架 | Unity Editor Window（UI Toolkit） |
| 节点图引擎 | 自定义 Visual Scripting GraphView |
| 数据序列化 | JSON（策划可读可改） |
| 预览方式 | 内置预览面板，模拟游戏UI渲染 |
| 节点类型 | Start / Option / Check / Success / Failure / Branch / End |
| 导出路径 | `Assets/Tactics/Resources/Events/{RegionName}/*.json` |

---

## 编辑器界面布局

```
┌──────────────────────────────────────────────────────────────┐
│ [File]  [Edit]  [View]  [Export]                [帮助]       │  ← 菜单栏
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────┐  ┌─────────────────────────┐  ┌────────────┐  │
│  │ 事件列表  │  │     节点编辑画布          │  │ 属性面板   │  │
│  │          │  │                         │  │ (Inspector)│  │
│  │ 📁 黑暗森林│  │  [Start] ──→ [Option1] │  │           │  │
│  │   ├ 事件001│  │              ↓        │  │ 选项文本:  │  │
│  │   ├ 事件002│  │          [Check]      │  │ 暴力撬开   │  │
│  │   ├ 事件003│  │          ↙      ↘     │  │           │  │
│  │ 📁 墓地    │  │  [Success]  [Failure] │  │ 属性:     │  │
│  │   ├ 事件004│  │              ↘        │  │ [力量 ▼]  │  │
│  │   ├ 事件005│  │            [End]      │  │           │  │
│  │ 📁 修道院  │  │                         │  │ 成功率:   │  │
│  │   └ 事件006│  │                         │  │ 50%       │  │
│  │          │  │                         │  │           │  │
│  │ [+新事件] │  │  [缩放: 100% ████░]   │  │ 成功奖励: │  │
│  └──────────┘  └─────────────────────────┘  │ gold: 5   │  │
│                                             │           │  │
│  ┌─────────────────────────────────────────┐ │ 失败后果: │  │
│  │     实时预览面板 (Live Preview)          │ │ damage:8 │  │
│  │  ┌───────────────────────────────────┐  │ └──────────┘  │
│  │  │  [事件标题: 被诅咒的宝箱]           │  │              │
│  │  │  描述文本...                       │  │              │
│  │  │  [暴力撬开 力量65%] [解除陷阱...]   │  │              │
│  │  └───────────────────────────────────┘  │              │
│  └─────────────────────────────────────────┘              │
└──────────────────────────────────────────────────────────────┘
```

### 面板说明

**左侧 — 事件列表面板**:
- 按区域分组（黑暗森林/墓地/修道院）
- 支持新建、删除、重命名、拖拽排序
- 搜索/筛选功能
- 显示事件数量和每个事件的选项数

**中央 — 节点编辑画布**:
- GraphView 实现的节点图编辑器
- 拖拽添加节点、连线
- 节点自动布局功能（一键整理）
- 缩放/平移
- 迷你地图导航

**右侧 — 属性面板**:
- 选中节点后展示可编辑属性
- 下拉选择（属性类型、奖励类型等）
- 数值输入、文本输入
- 条件配置

**底部 — 实时预览面板**:
- 模拟游戏中的事件UI渲染
- 实时反映编辑中的修改
- 预览成功/失败结果动画

---

## 节点类型定义

### 节点类型总览

```
                ┌──────────┐
                │  START   │  事件入口：ID、标题、描述、区域
                └────┬─────┘
                     │
              ┌──────┼──────┐
              │      │      │
         ┌────▼─┐ ┌──▼──┐ ┌▼────┐
         │Option│ │Option│ │Option│  选项：文本、属性、成功率
         └──┬───┘ └──┬──┘ └──┬──┘
            │        │       │
         ┌──▼────┐ ┌─▼────┐ ┌▼────┐
         │ CHECK │ │CHECK │ │CHECK│  属性判定：自动基于Option属性
         └──┬─┬──┘ └──┬─┬──┘ └─┬─┬─┘
           /    \     /   \    /   \
       ┌──▼┐  ┌─▼┐ ┌▼──┐ ┌▼┐ ┌▼─┐ ┌▼─┐
       │Suc│  │Fai│ │Suc│ │F│ │Su│ │Fa│
       │ces│  │l  │ │ces│ │ai│ │cc│  │il│  成功/失败结果
       │s  │  │ure│ │s  │ │l │ │es│  │ur│
       └─┬─┘  └─┬─┘ └─┬─┘ └┬─┘ └┬─┘ └─┬─┘
         └──────┼──────┼────┼────┘
                └──────┴────┘
                   ┌─────┐
                   │ END │      结束：总结文本
                   └─────┘
```

### 节点属性说明

| 节点类型 | 必填属性 | 可选属性 | 输出连接 |
|----------|---------|---------|---------|
| **Start** | eventId, title, region | description, bgImage | → Option(s) |
| **Option** | text, attribute | condition (class/level), successRateOverride | → Check |
| **Check** | (auto from Option) | difficultyModifier | → Success / Failure |
| **Success** | type (reward), amount | text, itemId, buffId | → End / next Option |
| **Failure** | type (consequence), amount | text, damageType | → End / next Option |
| **Branch** | condition type | condition value | → two or more paths |
| **End** | summaryText | — | — |

### 奖励/后果类型

| 类型 | 参数 | 说明 |
|------|------|------|
| `gold` | amount: int | 获得金币 |
| `item` | itemId: string | 获得消耗品 |
| `equip` | equipId: string | 获得装备 |
| `buff` | buffId: string | 应用Buff |
| `damage` | amount: int, target: string | 单体伤害 |
| `damage_all` | amount: int | 全员伤害 |
| `heal` | amount: int | 恢复HP |
| `battle` | enemyGroupId: string | 强制战斗 |
| `exp` | amount: int | 获得经验 |
| `nothing` | — | 无结果 |

---

## 数据模型 — JSON事件结构

```json
{
  "eventId": "dark_forest_altar_001",
  "title": "艾尼弗斯之树的余晖",
  "description": "一棵巨大的古树矗立在空地中央，树皮上刻满了古老的符文...",
  "region": "DarkForest",
  "nodes": [
    {
      "nodeId": "start_1",
      "type": "Start",
      "position": {"x": 100, "y": 100}
    },
    {
      "nodeId": "opt_1",
      "type": "Option",
      "position": {"x": 100, "y": 250},
      "data": {
        "text": "解读符文",
        "attribute": "Intelligence",
        "successRate": null
      }
    },
    {
      "nodeId": "check_1",
      "type": "Check",
      "position": {"x": 100, "y": 400}
    },
    {
      "nodeId": "suc_1",
      "type": "Success",
      "position": {"x": 0, "y": 550},
      "data": {
        "type": "exp",
        "amount": 50,
        "text": "你成功解读了符文，获得了古代知识！"
      }
    },
    {
      "nodeId": "fail_1",
      "type": "Failure",
      "position": {"x": 200, "y": 550},
      "data": {
        "type": "damage",
        "amount": 8,
        "text": "符文反噬，你的大脑一阵刺痛！"
      }
    },
    {
      "nodeId": "end_1",
      "type": "End",
      "position": {"x": 100, "y": 700},
      "data": {
        "summaryText": "艾尼弗斯之树的低语渐渐消散..."
      }
    }
  ],
  "connections": [
    {"from": "start_1", "to": "opt_1", "port": "out"},
    {"from": "opt_1", "to": "check_1", "port": "out"},
    {"from": "check_1", "to": "suc_1", "port": "success"},
    {"from": "check_1", "to": "fail_1", "port": "failure"},
    {"from": "suc_1", "to": "end_1", "port": "out"},
    {"from": "fail_1", "to": "end_1", "port": "out"}
  ]
}
```

**数据说明**:
- `nodes`: 所有节点的扁平列表，含位置信息（用于编辑器恢复布局）
- `connections`: 节点间连接关系
- `Check` 节点自动继承上游 `Option` 的属性设定
- 编辑器导出时移除 `position` 字段（运行时不需要），但保留完整的 graph 结构

---

## 功能列表

### MVP（Phase 1）

1. ✅ 自定义 Editor Window（UI Toolkit）
2. ✅ 事件列表面板（树形结构 + 新建/删除）
3. ✅ 属性面板（选中节点后可编辑属性）
4. ✅ 节点图基础（GraphView + 节点拖拽 + 连线）
5. ✅ 5种基础节点（Start / Option / Success / Failure / End）
6. ✅ JSON导出功能（单文件/单事件）
7. ✅ 实时预览面板（模拟事件UI）

### Phase 2 — 增强

8. ⏳ Check节点 + 属性判定逻辑
9. ⏳ Branch条件分支节点
10. ⏳ 撤销/重做（Ctrl+Z/Y）
11. ⏳ 导入已有JSON事件进行编辑
12. ⏳ 批量导出（全选→一键导出）

### Phase 3 — 完善

13. 📋 节点自动布局
14. 📋 事件模板（从预设模板创建）
15. 📋 迷你地图导航
16. 📋 验证功能（检查节点连接完整性、必填字段）
17. 📋 本地化/多语言支持（JSON文本字段）

---

## 技术方案

### 文件结构

```
Assets/Tactics/Editor/
└── RoguelikeEventEditor/
    ├── RoguelikeEventEditorWindow.cs    ← Editor Window入口
    ├── EventGraphView.cs                ← 节点图画布
    ├── EventBlackboard.cs               ← 事件列表面板
    ├── EventInspectorPanel.cs           ← 属性面板
    ├── EventPreviewPanel.cs             ← 实时预览面板
    ├── Nodes/
    │   ├── StartNode.cs                 ← Start节点
    │   ├── OptionNode.cs                ← Option节点
    │   ├── CheckNode.cs                 ← Check节点
    │   ├── ResultNode.cs                ← Success/Failure节点
    │   ├── BranchNode.cs                ← Branch条件节点
    │   └── EndNode.cs                   ← End节点
    ├── Serialization/
    │   ├── EventGraphSerializer.cs      ← 图→JSON序列化
    │   └── EventGraphDeserializer.cs    ← JSON→图反序列化
    └── Utils/
        ├── NodeSearchWindow.cs          ← 搜索添加节点
        └── MiniMapController.cs         ← 迷你地图
```

### 关键类说明

| 类 | 继承自 | 职责 |
|----|--------|------|
| `RoguelikeEventEditorWindow` | `EditorWindow` | 主窗口，三列布局 |
| `EventGraphView` | `GraphView` | 节点画布，处理节点创建/连接/删除 |
| `EventBlackboard` | `VisualElement` | 事件列表树，搜索/筛选 |
| `EventInspectorPanel` | `VisualElement` | 属性编辑，根据选中节点动态切换 |
| `EventPreviewPanel` | `VisualElement` | 实时预览，监听节点变化 |
| `StartNode` / `OptionNode` / ... | `Node` | 各有自定义Port和属性字段 |

### 实时预览机制

```
节点属性变化
    ↓
EventPreviewPanel.OnDataChanged()
    ↓
重新渲染预览UI（模拟游戏内事件弹窗）
    ↓
展示选项按钮（含属性+成功率）
    ↓
点击选项 → 模拟判定 → 展示结果
```

---

## 导出流程

```
[导出按钮] 
    ↓
EventGraphSerializer.Serialize(graph)
    ↓
遍历所有nodes + connections
    ↓
构建JSON对象（移除position等编辑器专属数据）
    ↓
验证数据完整性（必填字段检查）
    ↓
保存到 Assets/Tactics/Resources/Events/{Region}/{eventId}.json
    ↓
AssetDatabase.Refresh()
    ↓
报告导出结果（成功/失败/警告）
```

---

## 与主计划的关系

```
┌──────────────────────────────────────────────┐
│          Roguelike地图玩法 主计划               │
│  .agents/docs/plans/roguelike-map-gameplay-   │
│  开发计划.md                                   │
│                                              │
│  Task 1-3: 区域系统/地图生成/节点状态           │
│  Task 5: 非战斗节点基础                        │
│  Task 6-8: 事件系统/属性判定/事件UI            │
│  Task 9-13: 商店/休息站/内容/平衡/Boss结算     │
│                          ↑                    │
│                          | 依赖                │
│  Task 事件编辑器 (独立) ──┘                    │
│  .agents/docs/plans/                          │
│  roguelike-event-editor-开发计划.md            │
└──────────────────────────────────────────────┘
```

**依赖关系**: 事件编辑器是**独立工具**，不依赖主计划的其他任务。事件系统（Task 6-8）可以使用手工编写的JSON先行开发，编辑器完成后切换为编辑器导出。

---

## 风险与缓解

| 风险 | 等级 | 缓解 |
|------|------|------|
| GraphView API复杂度高 | 中 | 先实现基础节点图（拖拽+连接），再逐步增强 |
| Visual Scripting集成复杂 | 高 | 改为纯自定义GraphView，不依赖Unity Visual Scripting包 |
| 实时预览实现成本 | 中 | MVP阶段预览可简化为静态文本渲染 |
| 编辑器专用代码量大 | 低 | 所有Editor代码放在 `Editor/` 目录，不影响运行时包体 |
