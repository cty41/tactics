# RoguelikeMap Editor 审计后收尾计划

## Summary

当前仓库代码已经完成 `RoguelikeMap Editor` 先前计划中的主要建设项，这份计划的目标不再是继续实现核心功能，而是把现状固定为可交接文档，并把后续工作收束到验证与真实残项。

成功标准：

- 后续执行者可以直接依据本计划理解当前完成度与剩余动作
- 不再重复实现已落地的数据模型、连接编辑、Inspector、事件联动、序列化 round-trip
- 只在动态验证失败时开出小范围修复项

## Current State

- `MapEditorDocument` 已是编辑器唯一数据源，`Load/Edit/Save/Export` 数据流已收束
- `SerializableMapData` 已支持显式连接、`eventId`、Treasure、Store 字段
- `RoguelikeMapEditorWindow` 已具备 Save/Load/Export/Validate、脏状态提示、Mystery 节点事件联动
- `RoguelikeEventEditorWindow` 已支持按 `eventId` 打开或创建事件
- `RoguelikeMapEditorTests` 与 `.agents/docs/roguelike-map-editor-manual-test.md` 已提供自动与手动验证基础
- 仍缺动态证据：尚需实际运行 Unity 编译、Editor 测试与手动验收

## Relevant Context

- 关键入口
  - `Assets/Tactics/Scripts/Editor/RoguelikeMapEditor/MapEditorDocument.cs`
  - `Assets/Tactics/Scripts/Editor/RoguelikeMapEditor/RoguelikeMapEditorWindow.cs`
  - `Assets/Tactics/Scripts/Common/SerializableMapData.cs`
- 事件联动
  - `Assets/Tactics/Scripts/Editor/RoguelikeEventEditor/RoguelikeEventEditorWindow.cs`
- 验证资产
  - `Assets/Tactics/Tests/Editor/RoguelikeMapEditorTests.cs`
  - `.agents/docs/roguelike-map-editor-manual-test.md`

## Implementation Changes

### 1. 文档更新

- 将旧 `.sisyphus` 计划改写为“审计后状态与收尾计划”
- 明确核心实现已完成，剩余动作只保留验证与真实残项

### 2. 动态验证

- 在 Unity 中确认 C# 编译通过，无新增控制台错误
- 运行 `RoguelikeMapEditorTests`
- 按手动验收文档跑通生成、编辑、连接、事件、保存、重开、导出流程

### 3. 残项收束

- 若动态验证全部通过，将该主题标记为已审计完成
- 若存在问题，只记录真实失败点、复现方式、影响范围，不重新扩展大计划

## Interfaces / Data Flow

- `Load`: JSON → `SerializableMapData` → `MapEditorDocument`
- `Edit`: `MapGraphView` / `MapInspectorPanel` → `MapEditorDocument`
- `Save/Export`: `MapEditorDocument` → `SerializableMapData` → JSON
- Mystery 节点双击：`RoguelikeMapEditorWindow` → `RoguelikeEventEditorWindow.OpenEvent/CreateNewEvent`

## Test Plan

- 自动验证
  - Unity 编译通过
  - `RoguelikeMapEditorTests` 通过
- 手工验证
  - 生成地图
  - 新增/删除节点
  - 修改节点类型与位置
  - 手工改连接
  - 设置 Mystery `eventId`
  - 保存、关闭、重开、加载、导出
- 回归判定
  - 全部通过则结束本主题
  - 失败则仅补真实缺陷

## Risks / Open Questions

- 当前结论主要来自静态代码审计，动态验证可能暴露 Editor 生命周期或 UI 交互细节问题
- 如果未来同时维护 `.sisyphus` 与 `.agents/docs/plans/` 两份同主题文档，需要明确后者为稳定交接真相源

## Assumptions

- 默认接受“原实施计划主要任务已完成”的审计结论
- 默认不再为已落地能力重开实现任务
- 默认后续只做验证与残项收尾

## Handoff Notes

- 新 session 先读 `MapEditorDocument.cs`、`RoguelikeMapEditorWindow.cs`、`RoguelikeMapEditorTests.cs`
- 先跑编译与测试，再决定是否还有真实残项
- 不要重新设计编辑器数据模型，也不要把已完成项重新当成待办
