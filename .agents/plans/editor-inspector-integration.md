# Editor Inspector 集成计划

## Background

- **当前问题**：RoguelikeMap Editor 和 Event Editor 的属性编辑面板（Inspector Panel）内嵌在各自的 EditorWindow 右侧，占用窗口空间，且与 Unity 原生 Inspector 分离，体验不统一。
- **目标**：将两个 Editor 的属性面板迁移到 Unity Inspector 区域，选中节点时在 Inspector 显示属性，取消选择时恢复默认 Inspector。
- **预期收益**：
  - Editor 窗口空间释放，GraphView 可获得更大显示区域
  - 属性编辑体验与 Unity 原生行为一致
  - 两个 Editor 共用同一套 Inspector 区域，减少界面切换

## 关键发现

**节点数据类型**：
- `RoguelikeMapNode` - 普通 C# 类（非 ScriptableObject）
- `EventNodePayload` - 普通 C# 类（非 ScriptableObject）

**数据容器类型**：
- `RoguelikeMap` - 普通 C# 类，使用 JSON 文件持久化
- `EventDataModel` - 普通 C# 类，使用 JSON 文件持久化

**结论**：需要创建 `ScriptableObject` 包装类来持有这些数据，然后通过 `[CustomEditor]` 绑定。采用延迟保存策略优化性能。

## 保存策略设计

### 分层保存机制

```
┌─────────────────────────────────────────────────────────┐
│  Layer 1: Inspector 修改 (实时)                          │
│  → 直接修改内存中的 RoguelikeMapNode / EventNodePayload  │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│  Layer 2: EditorWindow 标记 dirty (实时)                 │
│  → 设置 _isDirty = true，不触发文件写入                   │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│  Layer 3: JSON 文件写入 (延迟)                           │
│  → EditorWindow 关闭 / 点击 Save 按钮时才触发            │
└─────────────────────────────────────────────────────────┘
```

### 优点
- Inspector 修改实时反映到内存数据，用户体验流畅
- 避免频繁文件 I/O，提升性能
- 与现有 Save 按钮机制兼容

## Scope

### In Scope

- 创建包装类（ScriptableObject）以支持 CustomEditor 绑定
- RoguelikeMap Editor 节点选中时，在 Inspector 显示节点属性
- Event Editor 节点选中时，在 Inspector 显示节点属性
- Inspector 中的修改能正确回写到内存数据（延迟保存到 JSON）
- 取消选择时恢复 Unity 默认 Inspector
- 移除两个 EditorWindow 中原有的内嵌 InspectorPanel 区域

### Out of Scope

- 运行时游戏逻辑修改
- GraphView 节点本身的显示样式调整
- Inspector 面板之外的其他 Editor 功能改动

## 包装类设计

### MapNodeDataWrapper

```csharp
public class MapNodeDataWrapper : ScriptableObject
{
    [HideInInspector] public RoguelikeMapNode NodeData;
    [HideInInspector] public Action OnDataChanged;  // 回调到 EditorWindow

    // Inspector 修改时调用
    public void NotifyDataChanged()
    {
        OnDataChanged?.Invoke();  // 通知 EditorWindow 标记 dirty
    }
}
```

### EventNodeDataWrapper

```csharp
public class EventNodeDataWrapper : ScriptableObject
{
    [HideInInspector] public EventNodePayload NodeData;
    [HideInInspector] public Action OnDataChanged;

    public void NotifyDataChanged()
    {
        OnDataChanged?.Invoke();
    }
}
```

### CustomEditor 实现要点

```csharp
[CustomEditor(typeof(MapNodeDataWrapper))]
public class MapNodeDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var wrapper = (MapNodeDataWrapper)target;
        var node = wrapper.NodeData;

        EditorGUI.BeginChangeCheck();

        // 绘制属性（从 MapInspectorPanel 迁移）
        node.NodeName = EditorGUILayout.TextField("Node Name", node.NodeName);
        // ... 其他属性

        if (EditorGUI.EndChangeCheck())
        {
            wrapper.NotifyDataChanged();  // 通知 EditorWindow 标记 dirty
            // 同步更新 GraphView 节点显示
        }
    }
}
```

## Tasks

### Task 1: 创建包装类和 RoguelikeMap 节点的 CustomEditor

- **目标**：实现 `MapNodeDataWrapper` 包装类和 `MapNodeDataEditor`，使 Inspector 能显示并编辑节点属性
- **输入**：`RoguelikeMapNode` 类定义、`RoguelikeMap` 类定义、当前 `MapInspectorPanel` 的属性绘制逻辑
- **输出**：
  - `MapNodeDataWrapper.cs` - ScriptableObject 包装类 ✅
  - `MapNodeDataEditor.cs` - CustomEditor 实现 ✅
- **验收标准**：
  - [x] 选中包装对象时，Inspector 显示自定义属性界面
  - [x] 属性字段（名称、类型、连接等）可正常编辑
  - [x] 修改后调用 `NotifyDataChanged()`，内存数据实时更新
  - [x] GraphView 节点显示同步更新

### Task 2: 创建 Event 节点的包装类和 CustomEditor

- **目标**：实现 `EventNodeDataWrapper` 包装类和 `EventDataEditor`，使 Inspector 能显示并编辑节点属性
- **输入**：`EventNodePayload` 类定义、`EventDataModel` 类定义、当前 `EventInspectorPanel` 的属性绘制逻辑
- **输出**：
  - `EventNodeDataWrapper.cs` - ScriptableObject 包装类 ✅
  - `EventDataEditor.cs` - CustomEditor 实现 ✅
- **验收标准**：
  - [x] 选中包装对象时，Inspector 显示自定义属性界面
  - [x] 属性字段（事件类型、参数、条件等）可正常编辑
  - [x] 修改后调用 `NotifyDataChanged()`，内存数据实时更新
  - [x] GraphView 节点显示同步更新

### Task 3: 实现 GraphView 节点选择时触发 Selection 更新

- **目标**：在两个 GraphView 中，点击节点时创建/更新包装对象并设置为 `Selection.activeObject`
- **输入**：`MapGraphView`、`EventGraphView` 的节点选择回调
- **输出**：修改两个 GraphView 的 `SelectNode` 方法
- **实现细节**：
  ```csharp
  // MapGraphView 中
  private MapNodeDataWrapper _selectionWrapper;
  private bool _isDirty = false;

  void SelectNode(MapNode node)
  {
      // 创建或复用包装对象
      if (_selectionWrapper == null)
          _selectionWrapper = ScriptableObject.CreateInstance<MapNodeDataWrapper>();

      _selectionWrapper.NodeData = node.NodeData;
      _selectionWrapper.OnDataChanged = MarkDirty;

      Selection.activeObject = _selectionWrapper;
  }

  void MarkDirty()
  {
      _isDirty = true;  // 仅标记，不触发文件写入
  }

  void DeselectAll()
  {
      Selection.activeObject = null;
  }
  ```
- **验收标准**：
  - 点击 Map 节点时，Inspector 显示对应的 `MapNodeDataEditor`
  - 点击 Event 节点时，Inspector 显示对应的 `EventDataEditor`
  - 取消选择时 `Selection.activeObject` 设为 null，Inspector 恢复默认
  - 包装对象在 EditorWindow 关闭时正确销毁

### Task 4: 实现延迟保存机制

- **目标**：在 EditorWindow 中实现 dirty 标记和延迟保存逻辑
- **输入**：现有 Save 按钮逻辑、OnDisable 生命周期
- **输出**：修改两个 EditorWindow，添加 dirty 标记和延迟保存
- **实现细节**：
  ```csharp
  // EditorWindow 中
  private bool _isDirty = false;

  void MarkDirty()
  {
      _isDirty = true;
  }

  // 关闭窗口时检查
  void OnDisable()
  {
      if (_isDirty)
      {
          SaveToJson();  // 延迟保存
      }
  }

  // 点击 Save 按钮
  void OnSaveClicked()
  {
      SaveToJson();
      _isDirty = false;
  }
  ```
- **验收标准**：
  - [x] Inspector 修改后 `_isDirty` 标记为 true
  - [x] 关闭窗口时自动保存
  - [x] 点击 Save 按钮时保存
  - [x] 保存后 `_isDirty` 重置为 false

### Task 5: 移除 EditorWindow 中的内嵌 InspectorPanel

- **目标**：删除两个 EditorWindow 中原有的 InspectorPanel 区域，释放窗口空间
- **输入**：`RoguelikeMapEditorWindow`、`EventEditorWindow` 中的 InspectorPanel 相关代码
- **输出**：移除 InspectorPanel 的引用和绘制逻辑，GraphView 扩展至全窗口
- **验收标准**：
  - [x] EditorWindow 中不再显示内嵌 InspectorPanel
  - [x] GraphView 正常显示，无布局错误
  - [x] 编译通过，无报错

---

## Assumptions

- 当前 InspectorPanel 的属性绘制逻辑可迁移到 `CustomEditor.OnInspectorGUI()`，无需大幅重构
- 现有 Save 按钮逻辑可复用，仅需添加 dirty 标记检查

## Risks & Open Questions

- **数据同步**：Inspector 修改后需要通知 GraphView 刷新节点显示，需实现回调机制
- **多窗口冲突**：如果同时打开多个 EditorWindow，包装对象的生命周期需正确管理
- **序列化兼容性**：`RoguelikeMapNode`/`EventNodePayload` 的字段需支持 Unity 序列化（或使用 `SerializedObject` 手动处理）
