# Godot 研究指南

本指南处理未知、版本相关或生态相关的 Godot 问题。它定义调查方法，不复制整页官方文档。

## 触发条件

- Godot API、生命周期或编辑器行为不确定；
- C# 与 GDScript 行为看似不同；
- 涉及 4.x 小版本差异；
- EditorPlugin、Assembly Reload、Resource UID、headless、export 或插件问题；
- 本地出现无法从项目代码解释的引擎错误；
- 需要判断生态是否已有可用插件或实现。

## 固定调查顺序

1. 本地代码、完整日志、项目设置和最小复现；
2. Context7 的 Godot 4.7 官方文档；
3. Godot 官方 Class Reference、C# 文档、升级说明；
4. Godot 官方源码，优先精确 `4.7.1-stable` tag；
5. Godot GitHub issue、PR、proposal、discussion；
6. 相关插件的官方仓库、release、issue 和源码；
7. Godot Forum、Reddit、Stack Overflow 等社区内容。

社区内容只提供线索。采用前必须确认 Godot 4.x/具体版本，区分 C#/GDScript/GDExtension，区分 Editor/Runtime/headless/export，检查 PR 是否已发布，并至少用官方资料、源码或本地复现交叉验证一项。

## 查询组成

搜索关键词应组合：精确错误签名、`Godot 4.7`/`4.7.1`、语言绑定、执行上下文、Windows、相关类名/方法名。不要只搜索概括性现象。

## 证据等级

| 等级 | 含义 |
|---|---|
| `verified_local` | 本地 4.7.1 精确复现或测试通过 |
| `official_docs` | 官方 4.7 文档明确说明 |
| `upstream_source` | 官方源码或已经合并的实现 |
| `upstream_open` | 仍开放的 issue/PR/proposal |
| `community_lead` | 未经交叉验证的社区线索 |
| `inference` | 根据多项证据推断，尚无直接证明 |

本地精确版本与文档冲突时，以本地复现作为当前项目行为依据，同时记录官方预期和差异；workaround 不得写成 Godot 通用规则。

## 研究记录最小字段

- URL、查询日期、对应版本；
- 支持的具体结论；
- 证据等级；
- 是否本地验证；
- Editor/Runtime/headless/export 与语言范围；
- 失效条件或开放上游状态。

完整日志放 Incident，当前验证结论放 OKF，重复流程才进入 Skill。
