# 冻结迁移证据规约

## 当前边界

- Godot 4.7 C# 是唯一产品、编辑、生成与运行权威；不得恢复已退役工程、MCP、Editor 工具或资产目录作为实现旁路。
- 历史行为只从 `unity-final-2026-08-08`、FrozenOracle、Golden、DTO、receipt、许可证证据和 retirement manifest 读取。
- 冻结来源只用于审计与确定性回归，不进入生产运行时，也不因当前实现变化而重写。
- 新内容生成只消费已有冻结输入，并通过 Application typed draft、ResourceSaver/PackedScene、Catalog/UID、幂等与回滚门禁进入 Godot。

## 验证边界

- 当前修改使用 `Tools/godot/Verify-GodotProject.ps1`，不调用任何已退役引擎或 MCP。
- 自动等价、固定 Seed 和 Golden 只证明声明的逻辑合同，不替代真实 Editor Reload、视觉、操作手感或干净 Windows 启动。
- 删除 FrozenOracle、Golden、receipt、许可证或 retirement evidence 必须获得用户单独确认。
