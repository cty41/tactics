---
type: External Reference
resource: https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f
title: Karpathy LLM Wiki
description: 使用LLM持续构建、交叉引用和维护持久Markdown知识库的方法模式。
tags: [llm-wiki, knowledge, agents, markdown]
timestamp: "2026-07-14T00:00:00+08:00"
status: active
catalog_scope: karpathy-llm-wiki-reference
---

# Summary

LLM Wiki 将原始来源、LLM 维护的 wiki 和约束维护方式的 schema 分为三层。新来源不会只被索引，而会被吸收进已有概念、摘要和关系，使知识在多次查询和会话之间累积。

# Tactics Adaptation

Tactics 使用现有 docs、plans、代码、Godot Resource 和测试作为来源层，以本 OKF bundle 作为综合 wiki，并通过 `AGENTS.md`、规则和 skill 约束维护流程。

普通查询不会自动写回；只有形成持久决策、实现状态变化或用户明确要求 ingest 时才修改 bundle，以控制仓库噪声。

# Format

具体交换格式采用 [Open Knowledge Format v0.1](okf-v0.1.md)。

# Citations

[1] [LLM Wiki](https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f)
