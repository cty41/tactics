# RoguelikeMap Editor 手动验收流程

## 使用原则

- 本清单验证 Unity Editor 动态行为，不以截图作为正确性证据。
- 记录实际通过/失败项和 Console 错误；失败后只为真实复现问题建立修复任务。
- 自动测试先行，再执行本清单。

## 前置条件

1. Unity Editor 已打开且项目编译无错误。
2. 项目中至少存在一个 `RoguelikeMapConfig`。
3. 已运行 EditMode `RoguelikeMapEditorTests`。

## 地图编辑器

### 打开与布局

- [ ] 通过 `Tactics/RoguelikeMap Editor` 打开窗口。
- [ ] 配置区、画布和 Inspector 正常显示。
- [ ] 打开窗口不会无条件弹出配置选择框。

### 生成与节点编辑

- [ ] `Generate` 生成有效地图，节点和连接可见。
- [ ] 选中节点后 Inspector 显示对应数据。
- [ ] 修改节点位置后画布和文档同步。
- [ ] 修改节点类型后颜色与类型字段同步。
- [ ] 新增和删除节点不会留下悬空连接。

### 连接

- [ ] 可手工新增、删除连接。
- [ ] 拖动节点时连接线持续跟随。
- [ ] `Rebuild Connections` 明确覆盖现有连接，并按距离重建双向连接。

### 节点配置

- [ ] Treasure 的金币、装备和 Buff 条目可编辑并 round-trip。
- [ ] Store 商品配置可编辑并 round-trip。
- [ ] Mystery 的 `eventId` 可编辑。
- [ ] 双击 Mystery 节点会打开 Event Editor，并定位或创建对应事件。

### 保存、加载与校验

- [ ] Save 后关闭并重开窗口，Load 能恢复节点、位置、连接和节点配置。
- [ ] Export 后重新加载 JSON，数据保持一致。
- [ ] Validate 对合法数据报告通过，对重复 ID、无起点或断链给出明确错误。
- [ ] Clear 清空数据、选择状态和脏标记。

## Pure Run 主流程回归

- [ ] 新开 Pure Run 后只能选择当前层后继节点。
- [ ] 已访问节点不可再次点击。
- [ ] 非战斗节点结果不会因重新显示地图而回滚。
- [ ] 战斗胜利返回后，当前位置、胜场、节点状态和后继可达性正确。
- [ ] 战斗失败不会提交胜场或角色升级。
- [ ] Boss 胜利后正常结束当前 run。

## 独立战斗回归

- [ ] 非 Roguelike 战斗不会误开 RoguelikeMap。
- [ ] 非 Roguelike 战斗不会误进入 Pure Run 结束流程。
- [ ] 通用结算的经验、属性和技能选择仍可使用。

## 结果记录

验收记录至少包含：Unity 版本、测试日期、使用的配置资产、自动测试结果、失败步骤、错误文本和复现条件。
