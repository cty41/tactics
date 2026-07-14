---
type: External Reference
resource: https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/d44368c15e38e7c92481c5992e4f9b5b421a801d/okf/SPEC.md
title: Open Knowledge Format v0.1
description: Markdown、YAML frontmatter、索引、日志、链接和引用的开放知识格式草案。
tags: [okf, knowledge, markdown, specification]
timestamp: "2026-07-14T00:00:00+08:00"
status: active
catalog_scope: okf-v0.1-reference
---

# Summary

OKF 将知识表示为层级目录中的 UTF-8 Markdown 概念文档。概念通过 YAML frontmatter 提供结构化字段，通过正文 Markdown 表达解释、关系和引用。

# Conformance Used by Tactics

- 每个非保留 Markdown 概念具有可解析 frontmatter 和非空 `type`。
- `index.md` 用于渐进披露，`log.md` 用于按日期记录更新。
- 关系使用标准 Markdown 链接。
- 外部论据使用 `# Citations`。
- 未知类型和扩展字段必须被容忍和保留。

Tactics Profile 在此基础上额外要求 `title`、`description`、`timestamp`、状态和实现证据字段，并把 bundle 内断链视为 lint 错误。

# Related Method

[Karpathy LLM Wiki](karpathy-llm-wiki.md)解释了为何让 LLM 持续维护这种文件化知识库。

# Citations

[1] [Open Knowledge Format v0.1 specification](https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/d44368c15e38e7c92481c5992e4f9b5b421a801d/okf/SPEC.md)
[2] [Open Knowledge Format README](https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/d44368c15e38e7c92481c5992e4f9b5b421a801d/okf/README.md)
