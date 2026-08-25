# Codex 接力任务：Pure Run single run 美术资产生产（feat/gd2 worktree）

你是被调度来继续 Pure Run 美术资产生产的 codex 会话。工作目录：`D:\codes\tactics-worktrees\feat-gd2\feat-gd2`（git 分支 feat/gd2）。所有路径以此为根。

## 目标范围（已与用户确认的共享理解）

为 single run 补齐 tilemap 层美术与物品图标：

- **第一批（进行中）**：物品图 5 张 — 幸运戒指 / 银戒指 / 生命药剂 / 魔法药剂 / 净化药水。5 张的源图已生成（codex 内置 ImageGen，256×256 纯 #00FF00 绿幕），已走完 ingest → prepare(去幕, chroma=00ff00 tolerance=24) → validate(通过) → render-review。全部处于 `review_pending`，**等用户逐张确认后 approve → promote**。
- **第二批（合同/jobs 已建，未生成）**：节点类型标识 8 张 — Start / Battle / Elite / Boss / Rest / Store / Mystery / Treasure。输出目录 `Tools/artworks/pure_run/node_icons/`。
- **第三批（合同/jobs 已建，未生成）**：tile 道具 7 张 — 篝火 / 商人立姿 / 商人交易反馈 / 宝箱闭合 / 宝箱打开 / 堕落祭坛 / 出口传送门。输出目录 `Tools/artworks/pure_run/props/`。
- **第四批（合同/jobs 已建，未生成）**：村民 NPC 5 姿态 — idle DR/UL、hit DR/UL、death。输出目录 `Tools/artworks/pure_run/villager/`。村民替代战斗护送 NPC 现占位的法师图（`ProtectedNpcBattleConfig` 用 `unit.pure-run.mage`）。

明确不做：武器/防具装备图（系统未完善）、宝箱怪敌人（无“开箱→伤人→开战”流程）、Buff 图标 ×14、运行时地砖接入、领队棋盘新图（复用战斗图）。

## 管线事实（必须遵守）

- 机器权威是 `Tools/artworks/pipeline/` 状态机：`create-contract → create-job → retry(建 attempt) → ingest → prepare → validate → render-review → approve → promote`。所有转换必须经 CLI，禁止手改 registry/report/receipt。
- CLI：`python .agents/skills/pure-run-artwork-pipeline/scripts/artwork_pipeline.py --root <repo> <cmd> ...`
- 门禁：`check --strict` 当前 385 项全绿；每次改动后重跑。`validate_sprite_assets.py --root Tools/artworks --strict --review-examples`。
- 物品/道具/图标是非胶囊资产：合同用 `kind=projectile` + `--no-mask-required`，不设 anchor，规格见各 `output_spec.md`。村民是胶囊 NPC：`kind=ground_character` + `--no-arms`，需语义蒙版与基线 236（尚未 attach-mask/calibrate-core）。
- 尺寸契约见 `.agents/skills/pure-run-artwork-pipeline/references/sprite-size-contract.md`；ImageGen 单图迭代、去幕流程见同目录 `imagegen-iteration.md`、`chroma-key-validation.md`；物品样式约定见 `Tools/artworks/pure_run/items/output_spec.md`（256×256、基线 y=236、无圆盘底、粗轮廓扁平）。
- 历史证据：`generation-invocations` 记录 provider `openai-imagegen`；实际生图通过 **codex 内置 image_gen 工具**（`codex exec` 会加载 `.codex/skills/.system/imagegen/SKILL.md`）。生成要求：256×256、纯 #00FF00 背景、物品不出现该色；生成后校验尺寸/基线/绿幕纯净。

## 已建合同与 jobs（勿重复创建）

第一批合同（kind=projectile, maskRequired=false, requiresInvocation=false）：
- 幸运戒指 contract-1150e3da1a2d2be8 / job-8c0260edd8abdb68（attempt a002 review_pending，a001 technical_failed 是容差 0 的失败记录）
- 银戒指 contract-cb930ed8c7e3be56 / job-7064b8a37678e1ef（a001 review_pending）
- 生命药剂 contract-00312112a8b9abd1 / job-ee7658e70053b988（a001 review_pending）
- 魔法药剂 contract-f95fb7a5dda7d076 / job-14f14757b75a7df7（a001 review_pending；已知物品底部 y=235 差 1px，待用户决定是否接受）
- 净化药水 contract-a882a622c8b31082 / job-79e7d50a9481d774（a001 review_pending）

第二批 8 合同（node-*）见 `Tools/artworks/pipeline/contracts/`（contract-ac55b4fcce71ab3b=start, 6a33901196f24295=battle, 8eaaabdbeb095b4d=elite, d93945212229587b=boss, b6f858dea2887581=rest, a099d095d70c37b1=store, 160685e1d5585913=mystery, 9e642c8f3046bc59=treasure），对应 jobs 在 `Tools/artworks/pipeline/jobs/`；prompt 在 `Tools/artworks/pure_run/node_icons/prompt_*.md`。
第三批 7 合同（prop-*）contracts：campfire=7817278687c899ce, merchant_idle=530b97d5974ad506, merchant_trade=8b6f8ad03d5c32c2, chest_closed=9bfbb6902c974153, chest_open=70409ef4eb9f1c9c, altar=8f4bb94cff066ab0, exit_portal=9b234ede7cb21822；prompt 在 `Tools/artworks/pure_run/props/prompt_*.md`。
第四批 5 合同（villager-*）contracts：idle_dr=f7f0f3b1e939015b, idle_ul=ac9f54a6b4d9ceae, hit_dr=ca3cd64d23954c17, hit_ul=c10214d889e5247e, death=700692c962b15db2；prompt 在 `Tools/artworks/pure_run/villager/prompt_*.md`。

合同→job 映射与 attempt 明细可用 `Tools/artworks/pipeline/` 下的 JSON 核对；用只读方式读取，状态转换只走 CLI。

## 下一步执行顺序

1. **第一批收尾**：把 5 张确认图路径展示给用户（`Tools/artworks/pure_run/items/reviews/` 汇总图 + `Tools/artworks/pipeline/reviews/job-*-a00X/` 单图），请用户逐张确认；确认后用 `approve --reviewer cty41 --reason ... --decided-at <ISO>` + `promote`。魔法药剂 1px 基线问题请用户表态。
2. **第二~四批生成**：每张用 `codex exec`（或如果你本身是 codex 会话，直接用内置 image_gen）生成绿幕源图到对应 `concepts/`（如 `pure_run_node_battle_v01_chroma_source_v01.png`），随后 `retry --job-id <job>` 建 attempt → ingest → prepare(chroma=00ff00, tolerance=24) → validate（失败分析 issue，必要时调整容差或重生成）→ render-review → 请用户确认 → approve/promote。
3. **村民（第四批）注意**：胶囊 NPC 需要语义蒙版（`attach-mask`）与 `calibrate-core`（需 approved anchor mask）。当前无已批准村民母图——首个 idle DR 生成后，先用户确认，再按管线制作核心蒙版与锚点；hit/death 以其为锚。若流程遇到 anchor 缺失，向用户说明并按其决定执行。
4. **收尾**：`check --strict`、`validate_sprite_assets.py`、OKF（`Tools/okf/catalog_impact.py report --worktree` + sync scope pure-run-artwork）、git 精确暂存（排除 `tmp/` 与未授权运行时文件），先展示暂存清单等用户确认再提交。

## 铁律

- 不手写/机械修改 `.tres/.tscn`；不手改 pipeline registry。
- 每次 ImageGen 先 `begin-generation` 记录（非高风险合同 requiresInvocation=false 可跳过，但普通创建源图后 ingest 时若合同要求 invocation 则必须提供）。
- 单张生成、单张 Review、明确确认后再推进下一张；不批量“碰运气”。
- 不修改 Godot Resource / 运行时代码，除非用户明确授权运行时美术接入。
- 提交前 exact staged scope + `git diff --cached --check`；先展示暂存清单等用户确认。
- 中文回复；标识符遵循 .NET 命名规范。