# C# 分层合同

```text
Tactics.Core <- Tactics.Application <- Godot Runtime / Godot Editor / CLI
```

- Core：状态、规则、命令、事件、ContentId、确定性 RNG。
- Application：Draft 编译、Snapshot、Diagnostics，后续承载 ChangeSet/Revision/Preview service。
- Godot Adapter：Node/Resource/UID/Editor API，翻译到纯 .NET 边界。
- Dev-only：GdUnit、godot-ai、EditorPlugin、迁移 DTO，不得进入 Release runtime。

验证依赖时以 `.csproj` ProjectReference 和源码 `using` 为准，不凭目录名推断。
