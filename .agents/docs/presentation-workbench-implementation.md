# Pure Run Presentation Workbench 实现说明

## 入口与职责

唯一菜单为 `Tactics/Pure Run/Presentation Workbench`。工作台以 `BattlePresentationGraph` 为根，左侧编辑节点与边，中间运行隔离 Preview Stage，右侧编辑 Graph、Preview Scenario、节点及其叶资产，底部沿用统一时间线与诊断。

打开 Graph 时会创建 `HideAndDontSave` 克隆。节点、边、Scenario 和叶资产参数先进入沙盒；浏览、校验、拖动时间轴与预览不写正式资产。`Apply All` 使用一个 Unity Undo group 写回，`Revert All` 放弃会话修改。关闭脏窗口使用 Unity `SaveChanges`/`DiscardChanges`/Cancel 提示。

Projectile、Visual Cue 与 Skill VFX Recipe 节点会显示沙盒 Inspector。共享叶资产显示全部 Presentation Graph 引用数；`Duplicate & Rebind Current Node` 只在会话中建立待创建资产，直到 `Apply All` 才创建并绑定。Material、Prefab、Sprite 等引用只通过 ObjectField 替换，不在工作台内深层编辑。

## 共享执行计划

`PresentationExecutionPlanCompiler.Compile(graph, cue)` 输出纯数据树：

- `PresentationSequenceStep`：顺序子步骤。
- `PresentationParallelStep`：Fork 到指定 Join 的并行分支。
- `PresentationLeafStep`：引用原节点记录。

禁用节点透传，Finish 截断当前路径；Fork 分支在 Join 前停止，Join 后只继续一次。Runtime 和 Preview 使用不同叶执行器消费同一计划，计划结构不引用 DOTween、Editor 或玩法状态。

## MCP 工具

- `list_presentation_graphs`
- `get_presentation_graph`
- `validate_presentation_changeset`
- `apply_presentation_changeset`
- `preview_presentation`

`get` 返回 Graph GUID/路径、规范化 SHA-256 revision、完整 Preview Scenario、节点、边、依赖与诊断；`leafAssets` 同时列出每个可编辑叶资产的 GUID、路径、类型、revision 和 Graph/node 引用者，节点内的资产引用也携带同一 revision。UnitTween 节点使用 Preview Actor 上的 `UnitTweenVisual.Profile`，因此对应 `StandardUnitTweenProfile` 也进入叶清单和节点引用。Agent 只需一次 `get` 即可取得后续更新或复制需要的全部 revision。Graph 与叶 revision 独立；revision 只覆盖规范化的可编辑字段和稳定 GUID 身份，不受路径、dirty 状态、Unity 名称/HideFlags 或 JSON 属性顺序影响。

`validate` 在临时克隆上运行且不写资产；`apply` 要求 Graph 与每个被更新/复制叶资产的 `expectedRevision` 匹配。创建 Graph 时继续用顶层 `graphPath`，并传入与 `expectedRevision` 互斥的 `createGraph`，其中可声明显示名、版本、默认 Entry、Preview Prefab 和完整 Scenario。MCP 默认拒绝无效结果，只有 `allowInvalidDraft=true` 可保存结构无效草稿；revision、路径冲突和 typed 字段错误始终拒绝。

单图请求直接传 `graphPath`、`expectedRevision`、`operations` 与 `assetChanges`；跨 Graph 原子修改使用顶层 `changeSets` 数组。批处理会先汇总所有隐藏叶副本，使同一批次创建的共享叶资产可在多张 Graph 中绑定，再完成所有 Graph/叶 revision、路径与 typed operation 预检；通过后才进入同一 Undo batch 和唯一一次成功路径 `SaveAssets`，任一子事务失败会恢复既有资产并移除新资产与空目录。未显式给出 node/edge ID 时按规范化 operation 与序号确定性派生，保证独立 validate/apply 的预测 revision 一致。

Graph operation 的 `kind` 支持：

- `setGraph`、`replacePreviewScenario`
- `addNode`、`updateNode`、`moveNode`、`removeNode`
- `addEdge`、`reconnectEdge`、`removeEdge`
- `bindNodeAsset`、`unbindNodeAsset`

`assetChanges` 的 `kind` 支持 `createLeafAsset`、`copyLeafAsset` 与 `updateLeafAsset`。叶类型限于 `StandardUnitTweenProfile`、`ProjectileVisualProfile`、`VisualCueProfile` 和 `SkillVfxRecipe`。每种类型只接受 facade 白名单中的语义字段，不接受 SerializedProperty 路径；Recipe 通过 `replaceRecipeBindings` 类型化整表替换 cue bindings 与 primitive layers，并校验 cue 唯一性、枚举、时间、尺寸、颜色、Blocking Marker 和资产引用。

最小更新示例：

```json
{
  "graphPath": "Assets/Tactics/Arts/PureRun/Presentation/Example.asset",
  "expectedRevision": "<sha256>",
  "operations": [
    {
      "kind": "updateNode",
      "nodeId": "<node-id>",
      "speed": 12.0,
      "emitImpactMarker": true
    }
  ],
  "assetChanges": []
}
```

`preview_presentation` 使用统一 scope：`fullScenario`、`phase + phaseIndex`、`entry + cue`、`leaf + nodeId` 或 `forkRegion + forkNodeId`；兼容顶层 scope 字符串和嵌套 scope 对象。隐藏 Workbench Stage 通过共享执行计划构建对应真实 DOTween/Projectile/VFX 轨道，返回 `requestedScope`、`resolvedScope`、固定 seed、PNG、尺寸，以及实际 `NodeStart/NodeEnd`、Release、PoseRestore、Impact、Blocking、Hit、PhaseAdvance 时间、lane、phase、duration、Marker、诊断与真实 fallback。请求 Entry、Leaf 或 Fork Region 不会再回退成默认完整场景。Stop、窗口销毁、渲染异常和程序集重载都会恢复 RenderTexture 状态并清理 Tween、粒子、隐藏对象与 Preview Scene。

交互式 Preview Stage 由独立 `PresentationPreviewRenderController` 驱动，不再从 `IMGUIContainer.OnGUI` 调用 `EndAndDrawPreview()`。控制器只在 `EditorApplication.update` 中按需渲染：静止状态合并 dirty 请求，播放时最高 30 FPS；`PreviewRenderUtility.EndPreview()` 的结果通过 GPU copy 写入工作台持有的固定 `1280×720` RenderTexture，UI Toolkit `Image` 始终显示该持久纹理。窗口布局变化只缩放 Image，不重建内部 RenderTexture，也不执行交互路径 `ReadPixels()`。

外部窗口尺寸变化会立即进入 `ResizeSuspended`；尺寸连续稳定 500ms 且至少经过三个 update 后才恢复，并只补渲染最新状态。两处分栏拖动通过明确的 `BeginResize()`/`EndResize()` 使用相同门禁；暂停期间保留最后一个有效帧并显示提示层，播放时间继续推进。Stop、关闭、异常和程序集重载会解除 update/Tween 订阅并释放 RenderTexture、`PreviewRenderUtility`、粒子与隐藏对象。右侧第三方兼容 Inspector 可以保留低频 IMGUI，但中央预览、播放控制、时间线和 GraphView 高亮不得依赖 IMGUI repaint。

顶部 scope 控件与 GraphView 选择保持上下文一致；Leaf 与 Fork 会切到合法的 Leaf/Fork Region 范围。底部节点事件带来自同一实际时间线，点击事件会跳到对应时刻并反选 Graph 节点；播放时 GraphView 高亮当前活动节点。中央沙盒不再提供局部 Apply/Revert，所有人工写入统一走顶部 Apply All，并在异常时撤销完整 Undo group。

## 验证边界

自动化覆盖共享计划、既有 Marker/Fork/Join/视觉尾段回归、dry-run revision、菜单唯一性、离屏 PNG，以及 RenderController 的 dirty 合并、30 FPS 限频、resize 暂停/恢复、固定纹理身份和 Dispose。真实 D3D11 设备上的外部窗口/分栏连续拖动、Battle Camera 构图与美术观感仍属于人工视觉验收，不得用字段存在、Null 图形设备测试或离屏 PNG 非空替代。
