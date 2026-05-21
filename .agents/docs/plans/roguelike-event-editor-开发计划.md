# Roguelike事件编辑器 — 开发计划

> **版本**: v2.0  
> **日期**: 2026-05-21  
> **状态**: 待执行  
> **关联设计**: [roguelike-event-editor-design.md](../design/roguelike-event-editor-design.md)  
> **主计划**: [roguelike-map-gameplay-开发计划.md](../plans/roguelike-map-gameplay-开发计划.md)

---

## TL;DR

开发一个**可见即所得（WYSIWYG）**的事件编辑器工具。策划在Unity Editor中拖拽节点图来创建Roguelike事件，实时预览效果，一键导出JSON供游戏运行时使用。

**并行独立性**: 此工具可**完全独立**于主Roguelike地图玩法开发，两个计划的Task互不阻塞。

**预估工期**: 3-4周（单人）

---

## Background

### 当前问题
- 事件内容需要手工编写JSON，易出错、效率低
- 策划无法直观看到事件在游戏中的表现
- 事件数据结构没有可视化验证手段
- 大量事件（30+）的手工管理成本高

### 目标
开发一个可视化事件编辑器工具，让策划人员可以：
1. 拖拽节点图创建事件逻辑
2. 实时预览事件UI效果
3. 一键导出标准JSON
4. 导入已有JSON进行修改

### 预期收益
- 事件创建效率提升10倍+
- 数据结构一致，零JSON语法错误
- 策划可自主迭代事件内容，不依赖开发

---

## Scope

### In Scope
1. **Editor Window**: UI Toolkit 三列布局（事件列表/节点画布/属性面板）
2. **节点图系统**: 基于GraphView的拖拽编辑
3. **节点类型**: Start / Option / Check / Success / Failure / Branch / End
4. **属性编辑**: 选中节点在Inspector面板中编辑
5. **实时预览**: 底部面板模拟运行事件UI
6. **JSON导出**: 单文件导出 + 批量导出
7. **JSON导入**: 导入已有事件编辑
8. **撤销/重做**: Ctrl+Z/Y

### Out of Scope
- 运行时编辑器（只在Editor模式下工作）
- 多用户协作/版本控制集成
- 事件数值平衡分析工具
- 事件测试自动运行
- 非Roguelike事件的通用编辑器

---

## Tasks

### Phase 1: 基础框架（Week 1）

#### Task 1: 编辑器窗口框架

- **目标**: 创建Editor Window和基础三列布局
- **输出**:
  - `RoguelikeEventEditorWindow.cs` — 主窗口
  - UXML/USS 布局文件
- **新增文件**:
  - `Assets/Tactics/Editor/RoguelikeEventEditor/RoguelikeEventEditorWindow.cs`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/EditorResources/EditorWindow.uxml`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/EditorResources/EditorWindow.uss`
- **验收标准**:
  - [ ] 通过 Tools > Roguelike > Event Editor 菜单打开窗口
  - [ ] 窗口分三列：左(事件列表) / 中(节点画布) / 右(属性面板)
  - [ ] 底部有预览面板区域
  - [ ] 窗口可缩放，布局自适应

#### Task 2: 事件列表面板

- **目标**: 实现事件列表的树形显示和管理
- **输出**:
  - `EventBlackboard.cs` — 事件列表面板
- **新增文件**:
  - `Assets/Tactics/Editor/RoguelikeEventEditor/EventBlackboard.cs`
- **验收标准**:
  - [ ] 显示每个事件的基本信息（ID、标题、选项数）
  - [ ] 支持新建事件（点击+按钮，弹出新建对话框）
  - [ ] 支持删除事件（右键菜单→删除）
  - [ ] 支持搜索/筛选事件
  - [ ] 点击事件在中央画布加载其节点图

#### Task 3: 节点图基础（GraphView）

- **目标**: 实现节点图画布的基础功能
- **输出**:
  - `EventGraphView.cs` — 节点图画布
  - 基础节点类
- **新增文件**:
  - `Assets/Tactics/Editor/RoguelikeEventEditor/EventGraphView.cs`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/Nodes/StartNode.cs`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/Nodes/OptionNode.cs`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/Nodes/ResultNode.cs` (Success/Failure共用)
  - `Assets/Tactics/Editor/RoguelikeEventEditor/Nodes/EndNode.cs`
- **验收标准**:
  - [ ] 支持从节点库拖入新节点
  - [ ] 节点可拖拽移动
  - [ ] 节点端口(Port)可连线
  - [ ] Start节点有输出端口 → 连接Option节点
  - [ ] Option节点有输出端口 → 连接Result节点
  - [ ] Result节点有输出端口 → 连接End节点
  - [ ] 画布支持缩放和平移
  - [ ] 选中节点高亮显示

#### Task 4: 属性面板

- **目标**: 实现选中节点的属性编辑
- **输出**:
  - `EventInspectorPanel.cs` — 属性面板
- **新增文件**:
  - `Assets/Tactics/Editor/RoguelikeEventEditor/EventInspectorPanel.cs`
- **验收标准**:
  - [ ] 选中Start节点：显示eventId(文本)、title(文本)、description(多行文本)
  - [ ] 选中Option节点：显示text(文本)、attribute(下拉:力量/敏捷/体质/智力/魅力)、successRate(数值)
  - [ ] 选中Result节点：显示type(下拉:gold/item/equip/buff/damage/heal/battle/exp/nothing)、amount(数值)、text(文本)
  - [ ] 选中End节点：显示summaryText(文本)
  - [ ] 属性修改实时同步到节点显示
  - [ ] 未选中节点时显示"请选择一个节点"

#### Task 5: 实时预览面板

- **目标**: 实现底部预览面板，模拟事件UI
- **输出**:
  - `EventPreviewPanel.cs` — 预览面板
- **新增文件**:
  - `Assets/Tactics/Editor/RoguelikeEventEditor/EventPreviewPanel.cs`
- **验收标准**:
  - [ ] 预览面板渲染事件标题和描述文本
  - [ ] 渲染选项按钮（显示文本 + 属性 + 成功率）
  - [ ] 点击选项模拟判定，展示成功/失败结果
  - [ ] 节点修改后预览面板自动刷新
  - [ ] 预览UI风格接近游戏内的实际表现

---

### Phase 2: 节点图增强（Week 2）

#### Task 6: Check节点和Branch节点

- **目标**: 添加Check(属性判定)和Branch(条件分支)节点
- **输出**:
  - `CheckNode.cs` — Check节点
  - `BranchNode.cs` — Branch节点
- **新增文件**:
  - `Assets/Tactics/Editor/RoguelikeEventEditor/Nodes/CheckNode.cs`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/Nodes/BranchNode.cs`
- **验收标准**:
  - [ ] Check节点自动从Option继承属性设定，显示成功率
  - [ ] Check节点有两个输出端口：Success(绿色)和Failure(红色)
  - [ ] Branch节点支持条件类型(职业/等级/金币)，分支出多条路径
  - [ ] Check和Branch节点在预览面板中展示判定效果
  - [ ] 完整节点流：Start → Option → Check → Success/Failure → End

#### Task 7: JSON导出功能

- **目标**: 实现事件导出为标准JSON
- **输出**:
  - `EventGraphSerializer.cs` — 序列化器
- **新增文件**:
  - `Assets/Tactics/Editor/RoguelikeEventEditor/Serialization/EventGraphSerializer.cs`
- **验收标准**:
  - [ ] 导出时移除编辑器专属数据（position坐标等）
  - [ ] 导出JSON格式符合数据模型定义
  - [ ] 可选导出路径（默认`Assets/Tactics/Resources/Events/`）
  - [ ] 导出成功后弹出提示，AssetDatabase自动刷新
  - [ ] 支持批量导出（勾选多个事件→一键导出所有）
  - [ ] 导出前验证必填字段，缺少字段报错

#### Task 8: JSON导入功能

- **目标**: 导入已有JSON事件到编辑器
- **输出**:
  - `EventGraphDeserializer.cs` — 反序列化器
- **新增文件**:
  - `Assets/Tactics/Editor/RoguelikeEventEditor/Serialization/EventGraphDeserializer.cs`
- **验收标准**:
  - [ ] 从JSON文件还原完整的节点图和位置
  - [ ] 导入后节点自动排列
  - [ ] 支持拖拽JSON文件到编辑器窗口导入
  - [ ] 导入时验证JSON格式，不合法则报错
  - [ ] 支持导入旧版本JSON（版本兼容检查）

#### Task 9: 撤销/重做

- **目标**: 实现编辑器的撤销/重做功能
- **输出**: 修改EventGraphView和相关类
- **验收标准**:
  - [ ] Ctrl+Z撤销上一步操作
  - [ ] Ctrl+Y重做
  - [ ] 支持的操作：添加/删除节点、连接/断开连线、属性修改
  - [ ] 撤销/重做后节点位置正确恢复
  - [ ] 撤销栈上限为50步

---

### Phase 3: 完善（Week 3）

#### Task 10: 节点自动布局

- **目标**: 一键整理节点位置
- **输出**: 修改EventGraphView
- **验收标准**:
  - [ ] 工具栏有"自动布局"按钮
  - [ ] 节点按拓扑顺序排列（Start左→End右或上→下）
  - [ ] 节点不重叠
  - [ ] 自动布局后可手动微调

#### Task 11: 事件模板

- **目标**: 提供预设事件模板快速创建
- **输出**: 模板系统
- **新增文件**:
  - `Assets/Tactics/Editor/RoguelikeEventEditor/Templates/` 目录
  - 3-5个模板JSON文件
- **验收标准**:
  - [ ] "新建事件"时可选模板
  - [ ] 模板包括：单属性抉择模板、多属性分工模板、团队协作模板
  - [ ] 从模板创建后可按需修改
  - [ ] 支持用户自定义模板（保存为模板）

#### Task 12: 完整节点流验证

- **目标**: 验证节点连接的完整性和正确性
- **输出**: 验证系统
- **验收标准**:
  - [ ] 验证Start节点必须连接至少1个Option
  - [ ] 验证每个Option必须连接Check
  - [ ] 验证每个Check必须有Success和Failure两条路径
  - [ ] 验证所有路径最终连接到End
  - [ ] 导出时自动验证，不通过则阻止导出并提示
  - [ ] 编辑器中有"验证"按钮可手动触发

#### Task 13: 汇总测试与文档

- **目标**: 编辑器整体可用性达标
- **输出**:
  - 使用文档
  - 测试用例
- **验收标准**:
  - [ ] 完整流程可用：新建→编辑→预览→导出→导入→修改→导出
  - [ ] 每个节点类型的功能完整
  - [ ] 创建3个示例事件（不同类型）
  - [ ] 撰写"事件编辑器使用指南"存入 `.agents/docs/usage/`
  - [ ] 所有操作不产生Unity报错

---

## 依赖关系

```
Task 1 (编辑器窗口框架)
  ├── Task 2 (事件列表面板) — 并行，独立
  └── Task 3 (节点图基础)  
        ├── Task 4 (属性面板) — 依赖Task 3
        ├── Task 5 (实时预览) — 依赖Task 3
        ├── Task 6 (Check/Branch节点) — 依赖Task 3
        ├── Task 7 (JSON导出) — 依赖Task 3、Task 6
        ├── Task 8 (JSON导入) — 依赖Task 7
        ├── Task 9 (撤销/重做) — 依赖Task 3
        ├── Task 10 (自动布局) — 依赖Task 3
        ├── Task 11 (事件模板) — 依赖Task 3
        └── Task 12 (验证) — 依赖Task 3、Task 6

Task 13 (汇总测试) — 依赖所有
```

### 并行执行建议

```
Wave 1: Task 1(独立) + Task 2(独立) 并行
    ↓
Wave 2: Task 3(核心) + Task 4(依赖3) + Task 5(依赖3) 并行
    ↓
Wave 3: Task 6 + Task 7 + Task 9 并行
    ↓
Wave 4: Task 8 + Task 10 + Task 11 + Task 12 并行
    ↓
Wave 5: Task 13(汇总)
```

---

## 与主计划的关系

### 依赖关系

```
主计划 (roguelike-map-gameplay-开发计划)
├── Task 1-2: 地图/节点状态（独立）
├── Task 3: 非战斗节点（独立）
├── Task 6-8: 事件系统/属性判定/事件UI → 依赖事件编辑器导出JSON
│     ↑                                     可以用手工JSON先行开发
│     └── 事件编辑器 (独立计划) ←────────────┘
├── Task 9-13: 商店/休息站/内容/平衡/Boss（独立）
```

### 核心策略

| 方案 | 说明 |
|------|------|
| **方案A（推荐）** | 事件编辑器与主计划**完全并行开发**。主计划前期(Phase 1)用手工编写少量JSON进行事件系统开发，编辑器完成后切换为导出。两队/两个Agent无阻塞。 |
| **方案B** | 先完成编辑器 MVP，再开始主计划的事件系统开发。适合人员串行。 |

### 文件交付对接

```
事件编辑器输出 → Assets/Tactics/Resources/Events/*.json
事件系统读取 ← 同一路径
```

接口协议：事件JSON数据结构已在设计文档中定义，双方独立开发，接口稳定后联调。

---

## Risks & Open Questions

| 风险 | 等级 | 缓解 |
|------|------|------|
| GraphView API学习曲线 | 中 | 参考Unity官方示例(UnityEditor.Experimental.GraphView) |
| 节点图性能（50+节点） | 低 | 编辑器工具，非运行时，性能容忍度高 |
| 与主计划对接延迟 | 低 | 接口协议固定，可分别自测后联调 |
| 策划使用门槛 | 低 | 提供模板和使用文档，培训1小时即可上手 |

### Open Questions

1. **节点图引擎**: 使用Unity的GraphView（推荐）还是自定义IMGUI/UI Toolkit实现？
   → 建议GraphView，与Shader Graph同体系，生态成熟
2. **Visual Scripting集成**: 使用Unity Visual Scripting包还是纯自定义？
   → 建议纯自定义GraphView，避免包依赖和版本兼容问题
3. **事件JSON版本兼容**: 是否需要版本号字段？
   → 建议v1加入version字段，便于未来兼容

---

## Assumptions

1. 使用Unity 2022+的GraphView API（`UnityEditor.Experimental.GraphView`）
2. 不依赖Unity Visual Scripting包，使用纯自定义实现
3. 所有Editor代码放在 `Assets/Tactics/Editor/` 目录，不影响运行时
4. 事件JSON数据模型稳定后双方（编辑器和事件系统）按接口开发
5. 编辑器不处理运行时逻辑，只负责数据创建和编辑

---

## 整体验收标准

- [ ] 打开编辑器：Tools > Roguelike > Event Editor
- [ ] 新建事件：选择模板或从空白创建
- [ ] 编辑节点：拖拽添加、连线、编辑属性
- [ ] 实时预览：底部面板同步显示事件UI
- [ ] 导出JSON：导出到Resources/Events/目录
- [ ] 导入JSON：导入已有文件编辑
- [ ] 撤销/重做：Ctrl+Z/Y正常工作
- [ ] 自动布局：一键整理节点
- [ ] 节点验证：不完整结构阻止导出并报错
- [ ] 独立于主计划运行，版本对接无冲突
