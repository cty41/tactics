# Godot Windows RC：GitHub Actions 构建、包审计与干净机交接

## Summary

以 `migration/godot` 当前 ownership 收口成果为基线，完善现有 Windows GitHub Actions 测试构建，使其成为可下载、可追溯、可在无 Unity/Godot 环境验证的内部 RC 包。

本计划只覆盖自动化构建与交付证据；人工玩法、UI、Editor Assembly Reload 和干净机主观体验统一延后到 artifact 生成之后。

本版本明确不接入 Audio payload：现有 Audio bus/settings/cue 框架保留但保持静默，缺少音频素材不再阻断 Windows RC。`MQA-GODOT-AUDIO-ASSETS` 转为 `deferred`，不作为本计划的成功条件。

父计划：[`2026-08-15-godot-ownership-closure-unity-removal.md`](2026-08-15-godot-ownership-closure-unity-removal.md)。本计划不会删除 Unity；它先证明 Godot 可以从裁剪后的 ownership 边界独立构建和交付，最终删除仍需独立确认。

## Current State

仓库已有：

- `.github/workflows/godot-windows-build.yml`：`main` push 与 `workflow_dispatch` 触发，Windows hosted runner，最小 `contents: read` 权限，并按 workflow/ref 取消旧运行。
- `Tools/migration/Build-GodotWindows.ps1`：固定 Godot/.NET 版本检查、Core/Application 测试、headless scan、Compatibility smoke、Release 导出、测试依赖排除和构建清单。
- `godot/export_presets.cfg`：`Windows Desktop` Release，输出独立 EXE/PCK，排除 tests 和 godot-ai addon。
- `Tools/migration/manifest/godot-tooling.json`：固定 Godot 4.7.1 Mono 编辑器、export templates URL/SHA-512 和 .NET SDK 9.0.312。
- `Test-GodotOwnedWithoutUnity.ps1` 与 `Verify-GodotMigration.ps1 -GodotOwned`：已证明 Unity 根目录物理不存在时 Godot-owned 自动门禁可通过。

当前缺口：

- Actions 仍从完整 checkout 构建，没有把 Godot ownership 裁剪边界作为 RC 输入。
- 导出后只检查文件存在与测试 DLL，不验证包白名单、PE/PCK、Unity payload、来源清单或真实 EXE 启动。
- 失败时没有独立诊断 artifact；成功时只有包内文件 hash，没有 workflow/toolchain/provenance 总清单。
- 尚无首次 hosted runner 的成功 run、artifact digest、下载复核和无 Godot/Unity 机器启动证据。

## Implementation Changes

### Checkpoint 1：收紧 RC 输入、触发器与工具链

保留单一 workflow，不建立平行发布流水线：

- `workflow_dispatch` 作为内部 RC 的权威触发器；`main` push 继续执行同一验证，但不创建 GitHub Release、tag 或永久附件。
- 给 push 增加 Godot/Core/Application/build-tooling 相关 path filter，文档或 Unity-only 变更不浪费 Windows export runner。
- 权限保持 `contents: read`；不请求 `packages`、`releases`、`attestations`、`id-token` 或 secrets。
- 所有 `uses:` 固定完整 commit SHA，并在注释中保留对应版本。
- concurrency 保持 `${{ github.workflow }}-${{ github.ref }}` 与 `cancel-in-progress: true`。
- checkout 继续 `fetch-depth: 1`、`lfs: false`；只 materialize `godot/**` 的 LFS，并拒绝残留 LFS pointer。
- .NET/NuGet 与官方 Godot archive 可缓存；禁止缓存 `.godot/`、`Build/`、导入输出或用户存档，避免陈旧 UID/import 进入 RC。
- Godot Mono/editor、export templates 和 .NET SDK 继续由 tooling manifest 固定；下载后必须验证 SHA-512 与实际 `--version`。

新增 RC source staging：

- 从 tracked `HEAD` 建立位于 runner temp 的干净 staging tree。
- 明确排除 `Assets/`、`Packages/`、`ProjectSettings/`、`src/Tactics.UnityOracle.Tests`、本地 `.codex`、缓存、artifact 和用户存档。
- 只从该 staging tree 调用 Godot-owned verifier 与 Windows build；完整 checkout 只负责准备受信输入，不能成为实际导出根。
- staging manifest 记录 commit、纳入/排除根目录及 tracked-file SHA-256；出现未声明根或 Unity runtime 引用时 fail-fast。
- 生产工作区与 staging 在构建前后都必须 tracked-clean；构建脚本不得刷新 Resource、receipt 或 migration state。

Checkpoint commit：

```text
ci: harden Godot Windows RC source and toolchain

- build release candidates from the Godot-owned source boundary
- pin and verify the hosted Windows export toolchain
```

### Checkpoint 2：RC 导出、包内容审计与可追溯清单

扩展 `Build-GodotWindows.ps1`，保持它是 CI 与本地共用的唯一只读构建入口：

1. `dotnet restore --locked-mode`。
2. `dotnet build` 使用 `-m:1`，避免共享 obj/bin 竞争。
3. 执行 Godot-owned Core/Application/GdUnit/Gameplay Spec、Release、Compatibility/Forward+ 和 OKF 门禁；Unity Oracle 和活动 AssetDatabase 导出不进入 RC 构建。
4. headless Editor scan 后执行 `Windows Desktop` Release export。
5. 输出只允许位于 `Build/Godot/Windows`，删除目标前先解析并验证绝对路径仍位于仓库 Build 根下。

导出后增加审计：

- 必须存在非空 `Tactics.exe`、`Tactics.pck`、managed assemblies 和必要 Godot runtime 文件。
- 解析 EXE PE header/architecture，要求 Windows x86_64；PCK 不得为空。
- 禁止出现 GdUnit、Microsoft.TestPlatform、testhost、test fixtures、godot-ai、UnityEngine、UnityEditor、Unity `.meta`、migration temp、source DTO、真实存档或 secrets。
- 输出根使用显式 allowlist；意外新增顶层文件或目录直接失败，而不是静默上传。
- 对每个文件写入相对路径、长度与 SHA-256。
- `rc-manifest.json` 额外记录 commit、workflow run/ref、Godot、.NET、Configuration、renderer smoke、source manifest hash、Audio=`deferred_no_payload` 和生成时间。
- 生成 `SHA256SUMS.txt`，允许用户下载后独立核对。
- 同一 commit/toolchain 的文件集合与内容 hash 必须确定性一致；仅时间、run ID 等 provenance 字段允许变化，并与 semantic manifest 分离。

Checkpoint commit：

```text
ci: build and inspect Godot Windows RC artifacts

- validate exported Windows binaries and package boundaries
- emit deterministic hashes and release-candidate provenance
```

### Checkpoint 3：导出 EXE 启动 smoke 与失败诊断

在 Windows hosted runner 上对已导出的 `Tactics.exe` 执行有界启动 smoke：

- 使用临时用户数据目录，禁止访问生产 `user://`；启动前后记录目录内容。
- 使用 Godot 官方命令行支持的 headless/quit 边界启动导出程序，并设置固定最长运行时间。
- 要求进程正常启动、无立即崩溃、无 missing assembly/resource/UID/duplicate type 错误，并在预定边界退出。
- 同时运行 Compatibility 和默认 renderer 的最小启动验证；如果 hosted runner 不支持图形 renderer，必须以结构化 `renderer_unavailable` 失败或显式降级证据处理，不能把未执行写成通过。
- 收集 stdout/stderr、Godot log、process exit code、启动耗时和临时用户目录 diff。
- 启动超时先尝试正常终止，再清理精确进程；不得杀死 runner 上无关 Godot/PowerShell 进程。

失败交付：

- `if: always()` 上传独立 `tactics-godot-windows-rc-diagnostics-*`，包含构建摘要、工具版本、source/RC manifest、测试结果、导出/启动日志和失败阶段。
- 诊断包不得包含完整源码、用户存档、环境变量转储、token 或未筛选的 runner 信息。
- 生产 RC package 只在全部门禁成功后上传；`if-no-files-found: error`。

Checkpoint commit：

```text
test: validate Godot Windows RC launch and provenance

- smoke the exported executable in isolated user data
- preserve bounded diagnostics without publishing failed packages
```

### Checkpoint 4：Artifact、摘要与首次 hosted-run 闸门

成功 artifact：

- 名称：`tactics-godot-windows-rc-<short-sha>-<run-number>`。
- 内容：完整可运行包、`rc-manifest.json`、semantic manifest、source manifest 与 `SHA256SUMS.txt`。
- 保留 14 天；用于内部验收，不自动创建 Release。
- compression level 保持中等，避免 EXE/PCK 上传时间异常。
- Job Summary 展示 commit/ref、run attempt、Godot/.NET、包大小、artifact digest、semantic manifest hash、启动 smoke 结果、Audio deferred 状态、下载链接和到期时间。

首次 hosted-run 是独立外部闸门：

1. 本地完成 review、自动门禁和三个 scoped commits。
2. 因当前约束为“不 push”，此处暂停，等待用户授权 push/触发 Actions；不能把本地模拟冒充 hosted-run。
3. Actions 成功后记录 run URL/ID、artifact digest 和日志摘要。
4. 下载 artifact，核对 GitHub digest、`SHA256SUMS.txt` 与 manifest。
5. 在未安装 Godot/Unity 的 Windows 环境启动一次；人工玩法验收在此后单独执行。

首次 hosted-run 证据提交：

```text
docs: record first Godot Windows RC evidence

- bind the hosted workflow run and artifact digest
- hand off the downloaded package for deferred manual acceptance
```

## Workflow Structure

建议保持一个 workflow、两个 job：

```text
validate-owned-source
  ├─ checkout + Godot LFS
  ├─ pinned toolchain
  ├─ clean Godot-owned staging
  └─ Godot-owned verifier
          ↓ needs
export-windows-rc
  ├─ read-only Release export
  ├─ package/provenance audit
  ├─ exported EXE smoke
  ├─ success RC artifact
  └─ always-on bounded diagnostics
```

`validate-owned-source` 与 `export-windows-rc` 不并行写共享 obj/bin；若通过 artifact 在 job 间传 staging，应验证 staging artifact digest，且不包含 Unity 根目录。优先保留同一 Windows job 的串行构建，只有运行时长或诊断隔离证明值得时才拆 job。

## Public Interfaces

- `.github/workflows/godot-windows-build.yml` 增加 RC staging、审计、启动 smoke、诊断 artifact 和摘要字段。
- `Build-GodotWindows.ps1` 增加可选 RC manifest/output audit/startup smoke 参数；默认本地调用仍保持兼容。
- 可新增小型只读脚本：
  - `New-GodotOwnedRcSource.ps1`
  - `Test-GodotWindowsPackage.ps1`
  - `Test-GodotWindowsLaunch.ps1`
- `godot-tooling.json` 继续作为版本与 archive hash 权威；不把版本复制到多处。
- 不修改 Core、Application、Save、Catalog、玩法、UI 或 Audio runtime。

## Test Plan

自动验证：

- Workflow YAML/schema 与 PowerShell 语法通过。
- 所有 actions 使用完整 SHA；workflow token 仅 `contents: read`。
- 无效 Godot/.NET 版本、错误 archive SHA、缺失 templates、残留 LFS pointer 均 fail-fast。
- RC staging 中物理不存在 Unity 根与 Unity Oracle；Godot-owned verifier 全绿。
- 构建前后 tracked tree 不变，输出只位于验证后的 Build 子目录。
- Release export 精确产生 EXE/PCK 和必要 managed runtime。
- 包中无测试、Unity、godot-ai、migration、source、save 或 secret payload。
- tamper 任一输出后 manifest/hash 审计失败。
- EXE 正常启动和退出；缺 DLL、损坏 PCK、错误 UID 能被 smoke 捕获。
- Compatibility/default renderer 状态被明确记录，不允许 skipped 被写成 passed。
- 成功只上传 RC artifact；失败只上传经过筛选的 diagnostics。
- Artifact retention=14，名称包含 commit 与 run number，Job Summary 包含 digest/download 信息。
- 完整运行 `Verify-GodotMigration.ps1 -GodotOwned`、agent policy、OKF impact/sync、敏感信息和 whitespace 门禁。

Hosted-run 后验证：

- GitHub Actions run 绿色且没有 warning-as-error/NuGet vulnerability source 网络假失败；依赖恢复失败应保留明确网络诊断。
- 下载后的 artifact digest 与 GitHub 输出一致。
- `SHA256SUMS.txt` 与本地文件全部一致。
- 无 Godot/Unity 机器可以启动 EXE 并写入隔离 user data。
- 人工 UI/Gameplay/Editor QA 继续按 `manual-acceptance.md` 单独执行，不由 CI 自动标记 passed。

## Status and Closing

自动实现、review 与本地门禁完成后：

`Generated/GodotOwned + hosted_windows_rc_pending`

首次 hosted-run 与下载核验完成后：

`Generated/GodotOwned + manual_windows_rc_acceptance_pending`

Audio 状态：

`deferred_for_this_version_no_audio_payload`

人工验收完成后才允许把 Windows RC 标为 passed。Unity 删除不属于本计划；必须在 Windows RC、人工账本和最终删除确认全部满足后执行独立 destructive plan。

## Assumptions

- 本版本允许无音频发布候选；静默是预期行为，不是缺陷。
- 内部 RC 使用短期 GitHub Actions artifact，不自动创建 GitHub Release。
- GitHub-hosted runner 是首次真实 export-template/Windows 环境验证；本地缺少 Windows templates 不等价于 CI 失败。
- 当前不 push、不建 PR；workflow 实现完成后需要用户另行授权 push 才能产生 hosted-run。
- 不使用 artifact attestation 写权限；内部 RC 以 pinned actions、SHA-512 toolchain、GitHub artifact digest 和仓内 SHA-256 manifest 提供足够来源证据。
- 失败时停在最后一个绿色 checkpoint，不上传可供人工测试的失败 RC。
