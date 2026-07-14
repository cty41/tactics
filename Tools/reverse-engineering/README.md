# Mewgenics reverse-engineering workspace

本目录保存可复现的分析入口、目标清单、输入指纹和脱敏证据。它不保存 Ghidra 安装、Ghidra project database、游戏可执行文件、完整反编译文本或原始导出。

## External layout

默认约定如下，所有路径都可通过脚本参数覆盖：

- Ghidra: `D:\Program Files\ghidra_12.1.2_PUBLIC`
- Ghidra project: `D:\Ghi\mew.gpr` 与 `D:\Ghi\mew.rep`
- raw exports: `D:\Ghi\export`
- headless logs: `D:\Ghi\logs`
- input binary: `D:\SteamLibrary\steamapps\common\Mewgenics\Mewgenics.exe`

`mew.rep` 是 Ghidra 的版本化项目数据库，包含程序字节、分析状态、符号、交叉引用和反编译缓存。它会迅速增长，而且包含本机状态，因此必须留在仓库外。

## Verify

```powershell
pwsh Tools/reverse-engineering/scripts/verify-environment.ps1
```

验证器只读检查 Ghidra、project、Java、输入文件哈希、目标清单和导出脚本。输入版本与 manifest 不同会直接失败，避免把旧地址误用于新版本程序。

## Export

先只检查将要执行的命令：

```powershell
pwsh Tools/reverse-engineering/scripts/export-mew.ps1 -ValidateOnly
```

确认后执行只读 headless 导出：

```powershell
pwsh Tools/reverse-engineering/scripts/export-mew.ps1
```

脚本以 `-readOnly -noanalysis` 打开外部 project，使用仓库内 `ExportFunctionBundle.java` 导出目标函数。原始 JSON 仍写到外部目录；仓库内的 `evidence/mewgenics-function-index.json` 仅保存签名、数量和人工结论等可审查摘要。

## Updating evidence

1. 记录新 `Mewgenics.exe` 的 SHA-256 和大小并更新 manifest。
2. 在 Ghidra GUI 中完成必要的导入、分析、重命名和类型恢复。
3. 更新 `targets/mewgenics-functions.json`，运行验证与导出。
4. 人工核对原始导出，只把不含完整反编译代码的摘要写入 `evidence/`。
5. 同步 `.agents/docs/` 中的结论、置信度和未决问题，再运行 OKF impact/sync/validate。
