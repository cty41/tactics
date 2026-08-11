# Godot Phase 6B：Pure Run 三战斗流程与持久化迁移

以 `migration/godot`、HEAD `d5b5b1ef` 为基线，实现 N1→N2→N3 单槽可恢复 Run。冻结 Unity v5 语义但不导入 PlayerPrefs JSON；战斗中退出从战前检查点重开；胜场只记录待消费成长。Godot 使用 `user://` V1 envelope、payload SHA、temp/backup 恢复和损坏隔离。

本批不包含完整七层地图、N4–N6、Elite/Boss、Rest/Store/Mystery、成长消费、正式 UI/Input 或视觉载荷。按合同冻结、确定性 Runtime、Godot Resource/Persistence 三个 checkpoint 提交；完整门禁失败即停在最后一个绿色提交，不 push、不建 PR。
