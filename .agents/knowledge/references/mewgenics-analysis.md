---
type: External Reference
title: Mewgenics Analysis
description: Mewgenics 配置组织、敌人 AI 模式和本地反编译证据的版本固定参考。
tags: [reference, mewgenics, reverse-engineering, ai, combat]
timestamp: "2026-08-17T01:26:52+08:00"
status: archived
catalog_scope: mewgenics-reference-analysis
repo_paths:
  - .agents/docs/mewgenics-config-analysis.md
  - .agents/docs/mewgenics-runtime-reverse-engineering.md
  - Tools/reverse-engineering
source_fingerprint: sha256:f5de85135f37a5235f3d62d8af9c4bdbc74f245e4d6138902311217def729fda
---

# Summary

本参考将两类证据并列维护：外部提取的 GON 配置用于说明能力、职业、AI 与冒险内容如何组合；Ghidra 小范围反编译用于验证配置被哪些运行时模块解释。输入可执行文件、工具版本、目标地址和脱敏摘要由仓库内 manifest 固定，Ghidra 安装、project database、原始游戏文件和完整反编译输出留在仓库外。

# Current Findings

Mewgenics 的敌人决策同时存在基于权重重评估局势的 GenericBrain，以及按显式序列推进的 PatternBrain。能力几何、移动评分和决策模式相互配合；形态状态由 FormChanger 保存，变化条件由独立触发被动表达。`virtual_abilities` 当前被视为 AI 对真实能力的评估包装，此结论仍需完整调用链验证。

# Relationships

- 这些外部机制可为[Monster AI](../systems/monster-ai.md)的评分型与序列型决策提供设计参照。
- 目标合法性、AOE 地块过滤和成本/效果分阶段结论与[Battle System](../systems/battle.md)有关。
- 内容池、战后升级和事件组合可为[Roguelike Run](../systems/roguelike-run.md)提供结构参考。
- 能力模板与运行时解释边界可与[SkillGraph](../systems/skill-graph.md)对照，但不构成兼容性要求。

# Evidence Boundary

本页作为设计研究历史公开保留。原始二进制、反编译导出、函数索引和 `Tools/reverse-engineering` 执行工具只存在于私有历史归档，不属于公开源代码或可复现构建输入；公开工程不得按下述旧路径重新执行逆向流程。

函数地址与自动名称仅对 manifest 中 SHA-256 固定的 `Mewgenics.exe` 有效。Ghidra 推断类型、完整 C 伪代码以及未经交叉验证的时序不进入本知识概念。bonus turn 精确调度和 virtual ability unwrap 路径仍是未决问题。

# Verification

历史验证曾使用私有归档中的只读 headless 导出工具。公开仓库只保留经过人工综合的机制结论；新增外部研究必须重新完成合法来源审查，不得依赖已移除的脚本或数据 dump。

# Citations

[1] Repository source: `.agents/docs/mewgenics-config-analysis.md`

[2] Repository source: `.agents/docs/mewgenics-runtime-reverse-engineering.md`

[3] Repository source: `Tools/reverse-engineering/README.md`
