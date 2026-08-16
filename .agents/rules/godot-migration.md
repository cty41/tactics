# Godot 迁移验证规约

## Unity 源快照验证边界

- Unity → Godot 迁移阶段不构建或启动 Unity Windows Standalone。
- Unity Windows Standalone Smoke Test 不属于 Unity 终版冻结、迁移启动或迁移批次的阻塞门禁。
- Unity 源快照使用 Editor 编译、定向 EditMode/PlayMode 测试、固定探针场景人工验证、OKF 校验和依赖审计作为迁移证据。
- 纯 C# 逻辑、测试和文档增量不因缺少 Unity Standalone 验证而暂停迁移。

## 产品目标边界

- Windows/Steam 仍是产品目标；本规约只取消 Unity 侧中间 Standalone 验证，不改变最终产品平台选择。
- Godot Windows 导出包的发布验收不自动继承本规约，是否执行以及何时执行必须在 Godot 发布阶段单独决定。
- 不得用未执行的 Unity Standalone Smoke Test 宣称已完成 Windows 发布验收。

## 例外与记录

- 如未来用户或已批准计划明确要求 Unity Standalone 验证，必须先说明构建成本、目标提交和验证范围，再单独执行。
- 任何未执行的构建验证都要在迁移记录中标明为“未执行”，不得写成“通过”。
