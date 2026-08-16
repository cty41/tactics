---
type: Operational Playbook
resource: https://github.com/cty41/tactics/tree/unity-final-2026-08-08
title: Archived Unity Agent Workflow
description: Historical Unity authoring and validation workflow retained only as retirement provenance.
tags: [operations, unity, archive, provenance]
timestamp: "2026-08-17T01:26:55+08:00"
status: archived
catalog_scope: unity-agent-workflow
repo_paths:
  - .agents/docs/unity-retirement-audit.md
  - Tools/migration/manifest/retirement/unity-governance-retirement-v1.json
  - Tools/migration/manifest/retirement/unity-retirement-inventory-v1.json
verified_revision: 168d1934
source_fingerprint: sha256:5bf94f785b90fa600243743b4b6d6715e7dfdb1fd6b0167fb268bd0444d6dc28
---

# Archived state

Unity 不再是当前项目的编辑、生成、运行或 Agent 工作流权威。原 `Resources.Load`、`GameAssetManager`、Unity YAML/MCP、`refresh_unity`、AssetDatabase、`.meta` 配对和 Unity Editor 测试规则只适用于最终 Tag 中的历史工程。

当前主线使用 [Godot Agent Workflow](godot-agent-workflow.md)。Unity-only rules、skills、MCP、工具与历史 Gameplay Specs 已由 `unity-governance-retirement-v1.json` 记录文件路径、Git blob、SHA-256 和退役原因；完整 Unity 工程由 `unity-retirement-inventory-v1.json` 保留文件级分类。

任何历史追溯必须从 `unity-final-2026-08-08`、FrozenOracle、Golden 或 receipt 读取，不得恢复 live Unity 目录作为当前实现旁路。
