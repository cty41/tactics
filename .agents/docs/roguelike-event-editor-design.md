# Roguelike 事件编辑器当前设计

## 定位

事件编辑器是 Unity Editor 内的 UI Toolkit 图编辑工具，负责编辑 `SerializableEventData` 并与运行时事件 JSON 往返。当前入口为 `Tactics/Event Editor`。

它不是运行时事件执行器；运行时状态修改仍由 Roguelike 事件与 `RewardResult` 链路负责。

## 当前界面

- 左侧 Blackboard：事件列表、新建、删除和选择。
- 中央 Graph：节点创建、拖动、连接、删除和网格背景。
- 右侧 Inspector：编辑当前事件或节点字段。
- 下方 Preview：实时显示标题、描述和选项结果预览。
- Toolbar：New、Import、Export。

图区域支持：

- Space 或空白处交互打开模糊搜索；
- 从端口拖到端口建立连接；
- 从端口拖到空白处创建节点并自动连接；
- Delete 键或节点右键菜单删除节点；
- 节点移动时实时重绘连接；
- 单个输入/输出端口保持单连接语义。

## 数据契约

### 根对象

`SerializableEventData`：

| 字段 | 说明 |
|---|---|
| `eventId` | 稳定事件 ID |
| `title` / `description` | 展示信息 |
| `region` | `DarkForest`、`BurialGrounds` 或 `Monastery` |
| `version` | 当前默认 `1.0` |
| `nodes` | 节点列表 |
| `connections` | 显式有向连接 |

### 节点

当前图创建菜单和 Inspector 支持 `Start`、`Option`、`Check`、`Success`、`Failure`、`End`。每个节点包含稳定 `nodeId`、编辑器位置和按类型解释的 payload。

数据模型预留了 `Branch` 常量和 `branch_0` 等端口格式，但当前创建菜单、节点渲染与 Inspector 尚未形成完整 Branch 编辑链路，不能把它视为可用节点类型。

结果类型支持：`gold`、`item`、`equip`、`buff`、`damage`、`damage_all`、`heal`、`battle`、`exp`、`nothing`；目标支持 `self`、`random`、`all`。

连接记录 `from`、`to` 和端口名，例如 `out`、`success`、`failure` 或 `branch_0`。

## 数据流

```text
JSON
  -> EventGraphSerializer.Deserialize + Validate
  -> SerializableEventData
  -> Blackboard / Graph / Inspector / Preview
  -> SerializableEventData
  -> EventGraphSerializer.Serialize + Validate
  -> Assets/Tactics/Resources/Events/<region>/<eventId>.json
```

Import 当前扫描事件目录并导入有效 JSON；Export 当前导出选中的单个事件。地图编辑器可通过 Mystery 节点的 `eventId` 打开或创建对应事件。

## 当前校验

序列化器会拒绝：

- 空 `eventId`、`title` 或 `region`；
- 空节点列表；
- 空或重复节点 ID；
- 缺少 Start 或 End 节点。

连接端点存在性、不可达节点、循环、端口与节点类型兼容性等更强校验尚未成为完整契约，记录在 [项目已知缺口](project-known-gaps.md)。

## 已实现与未实现边界

已实现：核心图编辑、Inspector、实时预览、模糊搜索、拖拽连接、节点删除、JSON 导入导出、地图事件联动和基础结构校验。

未实现或未形成稳定证据：完整 Branch 编辑链路、Undo/Redo、可复用模板、自动布局、拖入 JSON 文件、显式版本迁移、批量导出、完整图语义校验和编辑器专用自动测试。

## 实现入口

- 窗口：`Assets/Tactics/Scripts/Editor/RoguelikeEventEditor/RoguelikeEventEditorWindow.cs`
- 图：`EventGraphView.cs`
- 模型：`EventDataModel.cs`
- 序列化：`Serialization/EventGraphSerializer.cs`
- Inspector/预览：`EventDataEditor.cs`、`EventPreviewPanel.cs`
