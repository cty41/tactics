# RoguelikeMap Editor 编译后误弹 `Select Config` 修复计划

> **版本**: v1.0
> **日期**: 2026-05-28
> **状态**: 已完成
> **关联模块**: `Assets/Tactics/Scripts/Editor/RoguelikeMapEditor/RoguelikeMapEditorWindow.cs`

---

## Background

### 当前问题

每次 Unity 重新编译脚本后，`RoguelikeMap Editor` 都会再次弹出 `Select Config` 对话框。

这不是预期行为。用户在同一次编辑会话里已经选过 config，编译后应当静默恢复，而不是再次打断。

### 原因分析

当前触发链路已经明确：

1. `RoguelikeMapEditorWindow.OnEnable()` 每次窗口启用都会调用 `LoadDefaultConfig()`
2. Unity 脚本重新编译后会触发 domain reload / 编辑器窗口重建
3. `LoadDefaultConfig()` 在项目里检测到多个 `RoguelikeMapConfig` 时，会直接调用 `EditorUtility.DisplayDialogComplex("Select Config", ...)`
4. 代码中没有任何 `EditorPrefs` / `SessionState` / GUID 持久化逻辑去记住上一次用户选中的 config

因此，根因不是“编译主动弹窗”，而是“编译导致窗口重新启用，而窗口启用时总是重新要求选 config”。

### 目标

在不修改运行时代码的前提下，调整 `RoguelikeMap Editor` 的配置恢复策略：

- 记住上一次用户选择的 config
- 编译后自动恢复
- 只有当记住的 config 已失效，或用户主动要求切换时，才重新弹 `Select Config`

---

## Scope

### In Scope

1. `RoguelikeMapEditorWindow` 内部的 config 恢复逻辑
2. “自动恢复”与“用户主动切换 config”两条路径的职责分离
3. 多 config、单 config、无 config、失效 config 的兜底行为

### Out of Scope

1. RoguelikeMap Editor 的节点编辑、保存、导出逻辑
2. 运行时代码
3. `RoguelikeMapConfig` 资产结构
4. 编辑器窗口的未保存文档关闭提示机制

---

## Tasks

### Task 1: 引入上次选中 Config 的持久化

- **目标**: 让编辑器窗口能够记住上次选择的 `RoguelikeMapConfig`
- **输入**: 用户在多 config 情况下的选择结果
- **输出**: 一个稳定的 editor 级持久化键，保存上次选中的 config GUID
- **验收标准**:
  - [x] 持久化内容使用 config 的 GUID，而不是 name 或 path
  - [x] 首次选择后会立即写入持久化
  - [x] 后续窗口重开或脚本重编译后可读取该值

### Task 2: 重写默认 Config 加载顺序

- **目标**: 把 `LoadDefaultConfig()` 改成"恢复优先"而不是"每次重新询问"
- **输入**: 当前项目中的 `RoguelikeMapConfig` 列表、上次选中的 GUID
- **输出**: 稳定的 config 选择顺序
- **验收标准**:
  - [x] 若存在有效的"上次选中 config"，直接加载，不弹窗
  - [x] 若没有历史选择且只有一个 config，直接加载它
  - [x] 若没有历史选择且有多个 config，首次才弹 `Select Config`
  - [x] 若历史选择已失效，自动回退到重新选择逻辑

### Task 3: 分离自动恢复与手动切换行为

- **目标**: 避免 `OnEnable()` 和 `Reload Config` 共用同一条"会弹窗"的逻辑
- **输入**: 窗口自动启用场景、用户点击切换/重载 config 的场景
- **输出**: 两条职责明确的配置加载路径
- **验收标准**:
  - [x] `OnEnable()` 只做静默恢复，不主动弹多 config 选择框（除首次/失效情况）
  - [x] `Reload Config` 或专门的 `Select Config` 操作可以显式重新选择
  - [x] 用户手动切换后会更新"上次选中 config"记录

### Task 4: 补齐失效与边界兜底

- **目标**: 确保历史 config 丢失或项目状态变化时，窗口行为仍然可预测
- **输入**: config 被删除、改名、移动、类型失效、项目中 config 数量变化
- **输出**: 完整的兜底行为定义
- **验收标准**:
  - [x] 若保存的 GUID 已失效，会清理旧记录并重新选择
  - [x] 若项目中无 config，不弹 `Select Config`，仅保留 warning/空状态
  - [x] 若项目中只剩一个 config，自动加载它并更新持久化

### Task 5: 回归验证

- **目标**: 验证本次修复只改变 config 恢复体验，不影响编辑器其他主流程
- **输入**: 多 config / 单 config / 无 config / 删除 config / 编译重载场景
- **输出**: 可复现的回归验证结果
- **验收标准**:
  - [ ] 多个 config：首次打开弹一次，之后编译不再弹
  - [ ] 手动切换 config 后，下一次编译恢复到新选择的 config
  - [ ] 删除已记住的 config 后，会重新提示选择
  - [ ] 生成、保存、加载、导出流程不受影响

---

## Assumptions

1. 本次只落计划，不实现代码
2. 该问题属于 Unity Editor 使用体验问题，修复范围限定在 `RoguelikeMapEditorWindow`
3. “记住上次选中的 config” 使用 `EditorPrefs` 即可，不需要把选择写入项目资产或 repo 文件
