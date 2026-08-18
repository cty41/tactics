# Godot Authoring Spec → MCP 工作流

Agent 只能用 TypeScript compiler 生成规范化作者批次；它不能直接写 `.tres`、Catalog 或 UID。正式变更必须由 canonical Godot Editor 的 Tactics Authoring MCP 事务执行。

## 支持范围

`run-map`、`event`、`treasure`、`encounter`、`battle-layout`、`ai`、`skill` 与原生 `presentation` Profile。Poison Spear 专用图保持 Workbench-only；Audio 不在作者编译链内。

八类最小输入见 `../../Tools/gameplay-test-spec/examples/authoring/minimal-assets.json`，跨 Layout/Encounter/Event 输入见 `../../Tools/gameplay-test-spec/examples/authoring/cross-asset-batch.json`。

## 固定流程

1. 通过 MCP `list/get/reference_audit` 取得现有 identity、document revision 与 reference revision。
2. 编写 `AuthoringAssetSpecV1`；update/delete/rebind 必须携带对应 revision。
3. 先运行 `npm --prefix Tools/gameplay-test-spec run build`，再执行只校验：`node Tools/gameplay-test-spec/dist/src/cli.js validate-authoring-spec --spec <input.json>`。
4. 编译：`node Tools/gameplay-test-spec/dist/src/cli.js compile-authoring-spec --spec <input.json> --out <batch.json>`。
5. 将完整 batch 交给 `tactics_authoring_validate`，通过后原样交给 `tactics_authoring_apply`。
6. 检查 created/modified/deleted、UID、revision、路径和 typed reload evidence。

Create/Duplicate 的 `initialSnapshot` 与 lifecycle 属于同一事务、同一个 Editor Undo action。保存、Catalog、UID、引用或 revision 任一步失败时，Editor 服务负责整体回滚。Event 图只投影当前 flat option/check/outcome 语义；图布局保存稳定节点 ID 与坐标，不保存第二份语义边。
