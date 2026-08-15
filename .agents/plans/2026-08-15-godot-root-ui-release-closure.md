# Godot Root Goal：Gameplay QA、正式 UI 与 Release Candidate 收口

## 目标

以 `migration/godot` 当前实现为基线，连续完成仍可自动验证的迁移工作，并在唯一的人工 UI/Release 验收闸门停下：

1. 收口 Gameplay Spec Godot Runner，保证生产输入链、隔离存档、watchdog、清理和报告可信。
2. 使用 Godot 原生 `Theme`/`Control` 等价表达 Unity UI Toolkit 的项目视觉语言。
3. 将 Home、Options、Pause、Battle HUD、Rogue Map、Progression、Inventory、Settlement、节点页和 Summary 从功能占位布局收敛为一致的正式 UI。
4. 补齐 UI/表现自动证据、完整 Run 旅程、进程重载和 Compatibility/Forward+ 回归。
5. 在本机建立可复现的 Windows Release Candidate 验证边界；缺失模板或首次远程 runner 属于外部闸门，不伪装为通过。

不 push、不建 PR、不切换 worktree、不修改 Unity 主线、不消费真实用户存档，不以自动测试替代人工视觉验收。

## Checkpoints

### 1. Gameplay Spec Runner 可信闭环

- 一个 action budget 只对应一个正式 gameplay transition。
- Pointer action 使用经 viewport/canvas 转换后的确切坐标，不依赖 headless 全局鼠标状态。
- Godot Scenario 隔离存档、timeout、no-progress、清理和报告保持 fail-closed。
- 运行 Runner 定向测试、完整 GdUnit 和统一 verifier。

提交：

```text
fix: preserve Godot gameplay input coordinates
```

### 2. 统一 Godot UI Theme 与组件合同

- 从 Unity `Home/Menu/Options/Battle/Inventory/Map/Progression/Summary` 的 UXML/USS 冻结结构和视觉语义；不复制 UI Toolkit 文件。
- 建立 Adapter-owned Godot Theme：近黑背景、半透明面板、橙色强调边、白/灰文本、hover/pressed/disabled/focus 状态。
- 建立 page shell、panel、card、slot、primary/secondary/danger/compact/action button 等复用构件。
- Theme 从根 Control 传播，局部只表达语义 variation；不让页面继续散落重复颜色和 StyleBox。
- Theme/PackedScene 不进入 gameplay Catalog，Catalog 保持 131。

提交：

```text
feat: establish the Godot Pure Run UI theme
```

### 3. Run Shell 与导航页面

- Home、New Run Setup、Options、Pause 使用统一层级、居中内容和安全区。
- Rogue Map、Rest、Store、Mystery、Elite/Boss 准备页和 Summary 使用一致的标题、资源摘要、主操作和返回语义。
- Pause 保留 Pause/Resume、Step、0.5x/1x/2x/4x 和 CheatConsole；不恢复额外流程按钮。
- 1600x900 为逻辑画布，非 16:9 使用 `canvas_items + keep` 等比适配。

提交：

```text
feat: style the Godot Pure Run shell and route pages
```

### 4. Battle HUD

- 顶部中央 Round/Turn Order，左上单位信息，左下 Move/Consumable/技能，右下 End Turn，右上播放控制。
- 操作按钮使用简短正式名称和第二行消耗；disabled/selected/targeting 状态清楚。
- CheatConsole 继续作为诊断覆盖层，鼠标选择/复制不穿透。
- 棋盘、HUD、Hover、HP/MP、状态、数字和表现层共享取景与安全区合同。

提交：

```text
feat: finish the Godot battle HUD presentation
```

### 5. Progression、Inventory 与结算页面

- New Run 严格职业三选一；Progression 保持属性分配到技能三选一的原子流程。
- 当前技能、描述、门槛、Learn/Upgrade 和起始分支 guarantee 使用清晰卡片层级。
- Inventory 显示角色、基础/bonus/total、装备槽、背包、物品详情及 Equip/Replace/Unequip/Carry/Unload。
- Settlement、Defeated/BossVictory Summary 和 Return Home 使用一致的奖励与结果层级。

提交：

```text
feat: finish Godot progression inventory and settlement UI
```

### 6. UI 自动证据与完整旅程

- GdUnit 验证 Theme 状态、Control bounds、层级、输入阻断、页面重入、临时节点清理。
- Gameplay Specs 覆盖 Inventory 战斗投影、Defeat、Damage Number、process reload/Continue。
- Compatibility 与 Forward+ 执行 Main journey；截图只作为同环境结构证据，不替代人工观感。
- 完整统一 verifier、OKF impact/sync、敏感信息与 whitespace 门禁全绿。

提交：

```text
test: harden Godot UI and gameplay acceptance journeys
```

### 7. Windows Release Candidate

- 复核 pinned Godot 4.7.1 editor/templates SHA-512、export preset、Release build 和输出白名单。
- 本机模板可用时导出 EXE/PCK 并做无 Editor 启动 smoke；模板缺失则保留为明确外部 gate，不下载或伪造结果。
- 产物、provenance、许可和真实用户数据边界可审计。

提交（有实际改动时）：

```text
test: validate the Godot Windows release candidate
```

## 自动关闭条件

- 每个 checkpoint code review 无 Critical/Important。
- 定向测试和 `Tools/migration/Verify-GodotMigration.ps1` 全绿。
- 每次仅 scoped staging，不包含已知 `.meta` 行尾、`project.godot` 归一化、缓存、临时 hash 或无关生成状态。
- 权威设计和 OKF 已同步；完成的旧 active plans 在长期知识迁移后删除。

最终状态：

```text
Generated/UnityOwned + manual_ui_and_release_qa_pending
```

只保留必须由用户判断的 UI 可读性、动画观感、真实 Editor Assembly Reload 和首次 Windows RC/干净机体验。
