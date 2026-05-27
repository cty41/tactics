# RoguelikeMap Editor 手动验收流程

## 前置条件
1. Unity Editor 已打开
2. 项目中有至少一个 `RoguelikeMapConfig` 资产

## 测试步骤

### 1. 基本功能
- [ ] 打开编辑器：`Tactics` → `RoguelikeMap Editor`
- [ ] 窗口正常显示，三栏布局（左配置、中画布、右属性）

### 2. 生成地图
- [ ] 点击 `Generate` 按钮
- [ ] 地图正常生成，节点显示在画布上
- [ ] 右侧面板显示 "Select a node"

### 3. 节点编辑
- [ ] 点击选中一个节点
- [ ] 右侧面板显示节点属性
- [ ] 修改节点位置，画布上节点同步移动
- [ ] 修改节点类型，节点颜色同步变化

### 4. 连接编辑
- [ ] 手工添加连接（右键菜单或拖拽）
- [ ] 连接线正常显示
- [ ] 手工删除连接
- [ ] 点击 `Rebuild Connections` 按钮，按距离重建连接

### 5. Treasure/Store 配置
- [ ] 选中 Treasure 节点
- [ ] 修改 goldMin/goldMax
- [ ] 添加/删除 equipmentEntries
- [ ] 选中 Store 节点
- [ ] 添加/删除 storeGoods

### 6. Mystery 节点联动
- [ ] 选中 Mystery 节点
- [ ] 设置 eventId
- [ ] 双击 Mystery 节点
- [ ] Event Editor 打开并定位到对应事件

### 7. 保存/加载
- [ ] 点击 `Save` 保存到默认目录
- [ ] 关闭编辑器
- [ ] 重新打开编辑器
- [ ] 点击 `Load` 加载保存的地图
- [ ] 所有数据完整恢复

### 8. Export/Import
- [ ] 点击 `Export` 导出到指定路径
- [ ] 清空编辑器
- [ ] 点击 `Load` 导入刚才导出的 JSON
- [ ] 所有数据完整恢复

### 9. 校验
- [ ] 点击 `Validate` 按钮
- [ ] 无错误时显示 "All checks passed!"
- [ ] 有错误时显示具体错误列表

### 10. 清理
- [ ] 点击 `Clear` 清空编辑器
- [ ] 所有面板重置
- [ ] 脏标记清除

## 预期结果
- 所有操作响应正常，无报错
- 数据 round-trip 完整
- UI 同步更新

## 自动化测试
运行 `Window` → `General` → `Test Runner` → `EditMode` → `RoguelikeMapEditorTests` 验证核心逻辑。
