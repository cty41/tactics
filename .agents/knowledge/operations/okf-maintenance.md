---
type: Operational Playbook
resource: https://github.com/cty41/tactics/tree/main/Tools/okf
title: OKF Maintenance
description: 将工作区变更映射到 catalog_scope，并由 Agent 同步受影响知识概念的维护流程。
tags: [agent, okf, knowledge, automation]
timestamp: "2026-07-14T21:25:00+08:00"
status: active
catalog_scope: okf-maintenance
repo_paths:
  - .agents/knowledge/catalog-scopes.yaml
  - .agents/rules/knowledge-maintenance.md
  - .agents/skills/knowledge-maintenance/SKILL.md
  - Tools/okf/catalog_impact.py
  - Tools/okf/validate_bundle.py
verified_revision: d5f1730d3527
source_fingerprint: sha256:8e4038c1ec510f34d13190724394d01a2edceea63d9821e017694f419fe58e70
---

# Current State

`catalog-scopes.yaml` 保存仓库路径到 `catalog_scope` 的多对多映射。Agent 修改代码、设计、计划、规则或工具后，使用 `catalog_impact.py report --worktree` 找出受影响概念，核对真实差异并更新知识正文，再使用 `sync --worktree --scope <scope> --write` 刷新来源指纹、时间和根日志。

这一流程由 Agent 规则触发，不依赖 Git hook 或远端 CI。未映射但位于受监控目录的路径会显示为警告，Agent 必须判断它应加入已有 scope、建立新概念，还是明确保持不受 OKF 管理。

# Relationships

- [Unity Agent Workflow](unity-agent-workflow.md)规定代码、资产、文档和验证的通用安全边界。
- [Open Knowledge Format v0.1](../references/okf-v0.1.md)定义 bundle、概念、索引和日志的基础格式。

# Verification Guidance

运行影响检测、bundle 校验和 `Tools/okf` 下的全部单元测试。同步命令必须限定为本任务实际影响的 scope，避免吸收工作区中已有的无关修改。
