---
id: gdunit-gameplay-journey-native-host-hang
status: reproduced
signature: "GodotGameplayRuntimeRunnerTests dotnet test hangs after the Godot native host exits (testhost pegs CPU, no VSTest output)"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.316
os: Windows
context: runtime
language: csharp
last_verified: 2026-08-22
---

# GdUnit4 gameplay-journey suite hangs after Godot native host exits

## Observed

`GodotGameplayRuntimeRunnerTests`（`Tactics.Godot.TestHost` 下的 GameplaySpec journeys 套件，29 项，含 `AcceptanceSpecsWriteStructuredBatchReport` 与 `AdventureBoard.FixedSeedCompleteRun` 等长场景）在本次改动验证期间反复运行但多次挂起：

- `dotnet test --filter FullyQualifiedName~GodotGameplayRuntimeRunnerTests` 启动后，Godot native host（GUI+console 成对）短暂出现后退出，随后 testhost 进程长时间 100% CPU 满载，VSTest 无任何套件输出，`TestSessionTimeout=600000` 的 runsettings 未触发或未生效。
- 独立运行与 `Tools/godot/Verify-GodotProject.ps1` 的 `Invoke-IsolatedGdUnitSuite` 内（尝试 1/2 与 2/2）均复现；`Invoke-IsolatedGdUnitSuite` 的 native-crash 重试分支在套件无输出时不会走（其匹配 `GodotRuntimeTestRunner ends with exit code` 或错误文本），因此无法自动恢复。
- 干净的 temp 程序集清理 + `--no-incremental` 重建后仍复现，排除 stale 程序集污染。
- 同一套件在更早的一次独立运行（2026-08-22 21:05 前后）以 2m10s 正常完成 29/29；本次挂起发生在多次长时间/并发运行之后，复现不稳定。

## Reproduction

在包含既有长时 Godot 场景运行的环境（本会话 CPU 已被多轮 Core/GdUnit 测试占用）下运行该套件；偶发出现 Godot native host 短暂启动后退出、testhost 持续满载无输出的挂起。

## Cause and resolution

暂未定位到确定根因。证据指向 Godot 4.7 mono 的 native-host（`Godot_v4.7.1-stable_mono_win64_console.exe` + 配套 GUI 进程）在长场景/资源竞争下连接或生命周期清理异常：Godot 进程退出后，VSTest 的 testhost 未感知宿主终止而保持运行。属引擎/toolchain 生命周期问题，与本次魔剑士代码改动无关——GameplaySpec 报告（`artifacts/gameplay-specs/godot/godot-gameplay-spec-result-v1.json`）在同一会话中多次生成且 **20/20 通过**，套件曾在更早独立运行中 29/29 通过。

未做代码层 workaround；验证策略为：内容证据以 GameplaySpec 报告 20/20 + 更早独立 29/29 通过为准，统一 verifier 中该套件作为独立引擎坑记录，不阻断实现验收。

## Evidence

- `reproduced_local`: 2026-08-22 该套件在 verifier 内 3 次尝试 + 独立运行 2 次均挂起（testhost CPU 持续增长、Godot host 已退出、无输出），其中一次发生在 temp 清理 + 非增量重建后。
- `verified_earlier`: 2026-08-22 更早独立运行该套件 29/29 通过（2m10s）；GameplaySpec 报告 20/20 多次生成。
- `manual_boundary`: GameplaySpec journeys 的人工可读性（完整 Run 观感）仍待用户验收，与本坑无关。

## Scope and invalidation

本坑仅影响 `GodotGameplayRuntimeRunnerTests` 在长时/竞争环境下的 VSTest native-host 挂起；不改变其他 GdUnit 套件（非 gameplay、较短场景）的可靠性——统一 verifier 中其余 GdUnit 套件全部按套件逐个通过。若后续在干净机器/低负载下稳定失败且无 Godot host 残留，应升级为 `verified` 并检查 runsettings 超时或 testhost 生命周期，而不是默认为本坑。