# Tactics Godot 全量开源候选与公开根历史重建

## Summary

将完整 Godot 游戏、测试、迁移证据、Frozen Oracle、OKF 和 Agent 工作过程公开；排除完整 Unity 工程、第三方受限载荷、未确权素材、本地配置和秘密信息。公开仓库继续使用 `cty41/tactics`，但以单一新 root commit 开始。

- 自有代码、文档、测试、迁移工具和 `.agents`：Apache-2.0。
- 已确权游戏资产：CC BY 4.0，署名 `cty41`。
- 游戏名称、Logo 和官方身份：保留商标权利。
- 第三方依赖：保持原许可，进入通知和 SBOM。

公开硬门禁为许可扫描、干净 clone 全量验证、GitHub Actions Windows 导出和启动 smoke。人工 gameplay/presentation QA 作为已知状态公开记录，不阻断源码公开。

## Current State

- 基线：`migration/godot`，计划创建时 HEAD `f6ba703e`。
- 当前工作树已有用户改动，不得 reset、clean 或无条件覆盖：
  - `.agents/docs/manual-acceptance.md`
  - `Tests/gameplay-specs/compiled/pure-run-real-player-route.plan.json`
  - `Tools/migration/manifest/state/pure-run-full-seven-layer-v1.json`
  - `godot/project.godot`
  - `godot/default_bus_layout.tres`
- 私有 GitHub 归档和本地 bundle 已建立，仍需重新证明 refs、LFS 和冷恢复完整。
- 当前缺少根许可、资产许可、第三方通知、商标、贡献和安全文档。
- `project.godot` 仍有本地 `godot_ai` 引用，CI 仍依靠 RC staging 临时重写。
- Frozen Oracle、migration 和 OKF 可公开，但必须保持历史/测试工具身份，不进入生产运行时依赖。

## Implementation

### 1. 固化私有归档和净化前证据

- 校验本地 bare archive、bundle SHA、全部 refs、GitHub metadata 和 LFS 对象；从私有远端执行冷恢复。
- 分类现有 dirty/untracked 文件，保留有效产品改动，不静默丢弃。
- 净化前运行当前完整统一 verifier，记录通过范围和已知非产品阻断。
- 将最终净化前提交、LFS 和验证报告同步到 `tactics-legacy-private`；归档 Actions 保持关闭。

### 2. 建立许可、资产与公开分类合同

- 新增 `LICENSE`、`NOTICE`、`ASSET_LICENSE.md`、`THIRD_PARTY_NOTICES.md`、`TRADEMARKS.md`、`CONTRIBUTING.md` 和 `SECURITY.md`。
- 新增机器可读 provenance manifest、公开文件策略和依赖 SBOM。
- 项目自有文本默认 Apache-2.0；所有媒体和二进制必须显式登记路径、SHA-256、来源、权利人和许可证。
- 无法证明权属的运行资产以自有替代资产替换，不删除玩法。
- Frozen Oracle 逐文件审计；未闭合文件移除，对应规则迁入公共产品测试。
- `.agents` 默认公开；删除本地权限状态和秘密，历史计划及失效 Unity 规则标记 archived。
- 排除第三方截图、数据 dump、反编译源码、外部参考输入、未确权中间输出、Steamworks SDK、证书和密钥。

### 3. 让公开工程直接可运行

- 从 canonical `godot/project.godot` 永久移除 `godot_ai` plugin/helper。
- 保留 `tactics_tooling`；公开运行时不依赖 Codex、MCP、私有目录或本机绝对路径。
- Frozen Oracle、migration 和 OKF 仅作为测试/历史工具，不被 Core、Application 或游戏运行时引用。
- CI 直接从公开根构建；RC staging 只负责打包，不再生成另一份删减源码。
- 增加测试专用 startup-smoke 入口，输出稳定 ready marker 后正常退出；正常启动行为不变。
- README 说明运行时、Agent-first 工作流、历史证据、许可边界和人工 QA 状态。

### 4. 建立公开验证链和 GitHub Actions

- 串行执行 locked restore、Debug/Release build、NUnit、Frozen Oracle、migration、GdUnit、Gameplay Specs、Resource/UID/Catalog、OKF、Skill、许可、provenance、秘密和受限材料扫描。
- Windows Runner 从干净 checkout 导出包并执行 startup smoke。
- 上传 Windows 包、测试报告、SBOM、许可证报告和 provenance 报告。
- 任一硬门禁失败时仓库保持 private，不创建公开 Release。

### 5. 创建无历史公开候选并切换仓库

- 创建 orphan public candidate，并在独立临时 Git 仓库证明只有新的 root 历史。
- 先在当前 private repo 的 candidate branch 跑完整 Actions。
- 全绿后再次同步私有归档。
- 删除/重建远程仓库前请求最终 destructive cutover 确认。
- 获确认后以同名 private repo 推送单一 root commit，重跑 Actions；再次全绿后才切为 public。
- 配置默认分支、branch protection、Actions 最小权限、安全策略和 LFS。

## Public Interfaces

- 不改变 Core 战斗、Run、Save、AI、技能或 Catalog 行为。
- 新增许可、provenance、第三方依赖和公开文件分类 schema。
- Godot Adapter 仅增加测试专用 startup-smoke 入口。
- `.agents`、OKF、Frozen Oracle 和 migration evidence 公开但不属于生产运行时依赖。
- `Godot.ContentAuthoring` 暂留游戏仓库验证，不在本批拆分 addon。

## Test Plan

- 本地 bundle 和私有 GitHub 均可恢复全部 refs 与 LFS。
- 所有 dirty 文件有明确归属，无 reset/clean 丢失。
- 所有 tracked 文件通过许可分类；所有媒体在 provenance manifest 中。
- Frozen Oracle 无未确权冻结源，移除规则有公共测试接替。
- `.agents` 无真实 secret、本机用户路径或可执行的过期 Unity 指令。
- 干净 clone 不依赖 Unity、用户缓存、Codex 或 godot-ai。
- 完整 verifier、Windows export、startup smoke 与 GitHub Actions 全绿。
- 新公开仓库只有单一 root，不包含旧 Unity/Piloto/Steamworks 或秘密 blob。
- 切 public 后从匿名干净 clone 再做 restore、build、test 和 package hash 校验。

## Assumptions and Closing

- 用户拥有计划按 Apache-2.0 公开的项目源码；发现例外时只排除或替换具体文件。
- `cty41` 是 CC BY 4.0 资产默认署名。
- `.agents` 采用默认公开策略；完整 Unity 工程和旧历史只在私有归档。
- 人工 gameplay/presentation QA 不阻断源码公开，但必须如实标注。
- 远程删除重建是唯一需要再次人工确认的最终步骤；此前审计、修复、review、自动测试和本地 scoped commit 可连续执行。
- 完成后将长期结论并入权威设计，更新 OKF，删除本计划，由 Git 历史保留。
