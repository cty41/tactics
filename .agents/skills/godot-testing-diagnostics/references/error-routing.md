# Godot 错误路由

先使用精确签名在 `.agents/incidents/godot/index.md` 查找，不扫描全部历史。

| 签名/现象 | 首选 Incident |
|---|---|
| `doesn't inherit from SceneTree or MainLoop` | script-mainloop-entry |
| `same key has already been added` + `ScriptTypeBiMap.Add` | csharp-assembly-reload-duplicate-type |
| Dock 瞬间关闭/插件退出 | editor-dock-lifecycle |
| typed Resource 在 reload 后失效 | typed-resource-reload |
| 找不到 tooling/资产但 build 通过 | wrong-project-root |
| `obj`/DLL 被另一进程占用 | parallel-build-obj-contention |
