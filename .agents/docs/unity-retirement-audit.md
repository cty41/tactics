# Unity 退役审计

## 结论

当前产品范围是三职业 Pure Run。Godot 已具备 Catalog 143、完整 Run、Save V6、Lv1–Lv3、Map/Treasure、Inventory/Progression、程序化表现、正式 UI、Content Workbench、Gameplay Spec Runner 和 Windows RC 自动证据。

Unity 工程中仍存在的 Barbarian、Hunter、Uppercut、Counter、Mark、Freeze、Frost Nova、Charge Heal 和 Melee Heal 属于历史原型，不进入当前产品；它们由最终 Unity Tag 和退役清单保留来源，不迁入 Godot Runtime。

## 冻结身份

- Annotated tag：`unity-final-2026-08-08`
- Tag object：`b881177a7a34eff2d4ef8bc3ca6e47c12f5a468d`
- Peeled source commit：`168d19345d7e0f7f22ce2516351eda9cef2e1cb1`
- 完整 tracked Unity inventory：`Tools/migration/manifest/retirement/unity-retirement-inventory-v1.json`
- Inventory 覆盖 9263 个文件、46501338 bytes，`unresolved=0`

## 主线切换与保留策略

- 远程 `main` 从 peeled Unity commit `168d19345d7e0f7f22ce2516351eda9cef2e1cb1` fast-forward 到包含本审计的 Godot tracked tree；禁止 force push。
- 永久保留现有 annotated tag `unity-final-2026-08-08`，不重打、不移动。
- 在切换前创建远程临时分支 `archive/unity-main-2026-08-16`，精确指向旧 `main` 的 peeled commit。
- 临时归档分支至少保留 30 天，最早于 2026-09-15 复核删除；删除不影响永久 Tag。
- 新 `main` 的 Windows CI 与人工下载 artifact smoke 通过后，才删除远程 `migration/godot`；在人工 clean-machine 闸门关闭前保留该分支。
- 本次切换不移动、删除或重绑本地 worktree。推送后失败采用 fix-forward；任何非 fast-forward 回滚需单独明确批准。

旧迁移 receipt 中的 `sourceCommit=b881177a...` 实际记录的是 annotated tag object。历史 receipt 不改写；新的退役与 Frozen Oracle 证据必须分别记录 tag object 和 peeled commit。

## 功能与工具映射

| Unity 领域 | Godot 当前权威 | 退役分类 |
|---|---|---|
| Battle/Core/Skill/Status/AI | `src/Tactics.Core`、`src/Tactics.Application`、Catalog 143 | `migrated_equivalent` |
| 三职业角色、召唤物、敌人与 Lv1–Lv3 | Godot content Resources、Frozen Oracle、NUnit/GdUnit | `migrated_equivalent` |
| 七层 Run、Rest/Store/Mystery/Treasure/Elite/Boss | Map Resource、Save V6、Gameplay Specs | `migrated_equivalent` |
| Home/Battle/Map/Inventory/Progression/Summary UI | Godot Theme、Control、`Main.tscn` | `replaced_by_godot_design` |
| Unity BattleTest Inspector | Encounter Fixture Workbench、validated checkpoint、Gameplay Spec Runner | `replaced_by_godot_design` |
| Map/Event/Skill/Presentation/AI authoring | Tactics Content Workbench | `replaced_by_godot_design` |
| Barbarian/Hunter 与旧技能 | 无当前产品入口 | `retired_legacy_prototype` |
| Piloto/Odin/TBSFramework/Unity packages | 不进入 Godot 发布包 | `excluded_third_party` |
| Audio payload | 静默 Audio framework，素材延期 | `deferred_audio_payload` |
| Unity PlayerPrefs import | Save V1–V6 只迁 Godot 格式 | `replaced_by_godot_design` |

## 删除结果

1. `Tactics.FrozenOracle.Tests` 已把 47 份源码、JSON 和 Shader 证据冻结到仓库内，记录原路径、Git blob 与 SHA-256；solution 和 verifier 不再编译 `Assets/Tactics/**`。
2. `godot-content-ownership-v1` 已把 142 项 canonical Catalog 及 13 个当前类别晋升为 `GodotOwned`；旧 batch/state 作为历史导出/生成证据保留原 ownership，不再充当当前权威。
3. `Tactics.Godot.slnx`、`Tactics.Godot.runsettings` 与 `Tools/godot/Verify-GodotProject.ps1` 已建立；Windows build/RC/config 入口已迁至 `Tools/godot/`，CI 不再调用旧迁移入口。
4. 精确删除清单 `unity-deletion-manifest-v1` 覆盖 9307 个文件、46909708 bytes；包含 Unity 四个工程根、Unity-only 治理/工具和三个已完成的过渡入口。两项 Unity `.meta` 工作树状态已明确记录，未被静默覆盖。
5. 删除清单先在系统临时副本物理应用并通过完整 Godot 主线 verifier；预演 receipt 为 `unity-deletion-dry-run-v1`。
6. 历史 Barbarian/Hunter Gameplay Spec 文本继续作为平台中立编译器 fixture 保留，不进入 Godot Catalog 或当前产品范围。
7. 用户于 2026-08-16 明确确认删除；产品工作区已严格删除清单内 9307 个 tracked 文件、46909708 bytes，并清理 1659 个经 Git 证明为 ignored 的 Unity 本地生成文件。`Assets/`、`Packages/`、`ProjectSettings/`、`UIElementsSchema/` 均已不存在。
8. 真实删除后 `Tools/godot/Verify-GodotProject.ps1` 通过 Core 103、Application 114、FrozenOracle 15、Gameplay Specs、GdUnit、Debug/Release、Compatibility/Forward+、Catalog/ownership、OKF 和 whitespace。结果记录于 `unity-deletion-result-v1`。
9. Godot 手工验收继续 pending，但按产品决定不阻断 Unity 源工程删除；Unity 源可由 annotated tag `unity-final-2026-08-08` 和 Git 历史恢复。

## 不得外推

- `GodotOwned` 只表示编辑、生成和运行真相源切换，不表示人工观感或发布验收通过。
- 历史 Unity 原型被退役，不应重新出现在 Godot Catalog。
- Git 删除不清除历史对象；Unity 源仍可由远程最终 Tag 恢复。
