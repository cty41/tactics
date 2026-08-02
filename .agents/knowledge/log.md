# Tactics Knowledge Update Log

## 2026-08-01
* **Sync**: `project-documentation` 已同步到来源指纹 `sha256:07a592fba3f9ce26eea1990aae4e30e8873274fba231ab1138ac083b93003c3f`。
* **Sync**: `pure-run-artwork` 已同步到来源指纹 `sha256:9efa57e5d37dab9188590e9498d6ecf631d635e50959135c16af79d196fb8c60`。
* **Sync**: `skill-graph` 已同步到来源指纹 `sha256:9c5bb7f2aaa67ce42b9b5d02c8992d2de27641a835d44f967e40ee3e3d7940ee`。
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:870a8cc20de52924fa1f0a2618d1d5005269833e5f961599f97b84412f9f6fa1`。
* **Update**: 新增独立 `Tactics/Pure Run/Tween Preview`，以隔离 PreviewRenderUtility 舞台复用运行时单位 Sequence、投射物 Renderer/材质/轨迹与尾迹构建；支持十种动作组合、四方向、距离、循环、倍速、时间拖动和可 Undo 的 Profile 沙盒。该窗口只标记 Release/ProjectileImpact，Skill Recipe 仍由独立 VFX Preview 检查，蝙蝠动画继续延期。
* **Update**: 程序化火球、骨矛和突刺已完成可玩验收，但定位为传统美术特效逐技能替换前的临时视觉基线；Tween 长期保留角色/投射物运动、受击等简单动画，简单光环、闪光和短尾迹仍可程序化，复杂技能不再默认扩展有限原语。
* **Fix**: 废弃 Cast 整张人物 `GlowOverlay` 白膜，改为人物与阴影后方的非阻塞 `CastCharge` 径向光环；所有 Cast 都有默认蓝色 Recipe，骨矛与火球分别覆写苍白青和暖橙红，主 Sprite 的 Sprite、Material 和 Color 保持不变。
* **Validation**: Cast 光环与 Pure Run Tween 资产 EditMode 48/48，三职业技能、骨矛清理和战斗倍速联合 PlayMode 72/72，Sprite 严格校验 38 文件/0 失败。
* **Update**: Battle 全局播放倍率新增 `0.5×`，按钮循环固定为 `1× → 2× → 4× → 0.5× → 1×`；初始默认仍为 `1×`，暂停恢复与跨场景保留继续由 `GameTimeService` 统一负责。
* **Update**: 经人工确认的骨矛 `v01` 已接入独立运行时 Sprite；使用中心 Pivot、`128 PPU`、`Scale=1`、切线旋转与最多两个非阻塞短残影，不再复用死灵飞行能量球。
* **Fix**: Sprite 投射物和残影只在 Profile 显式提供 Material 时覆盖 `SpriteRenderer` 默认材质，修复空 Material 导致骨矛显示为洋红错误线的问题；程序化 VFX Material 继续只用于 Mesh 原语。
* **Validation**: Pure Run VFX 资产 EditMode 46/46、相关技能与 VFX PlayMode 48/48 通过；测试同时验证默认/显式 Sprite Material、骨矛残影上限和取消清理。

## 2026-07-31
* **Sync**: `project-documentation` 已同步到来源指纹 `sha256:ad6f9994669477d3e6d6b351856b95832b7a2d9840c5061e411bcc8c007062c9`。
* **Sync**: `skill-graph` 已同步到来源指纹 `sha256:89818c025b2730669f248d438f8dd025425a1a4ae274339f1a574aecb298cc52`。
* **Sync**: `pure-run-artwork` 已同步到来源指纹 `sha256:32ba7eb650697ee3ec72986dbc10a04198773974cecf9f4137d72a3b693b5ca8`。
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:3ff4e929873881ea8d07d3c2b6cddc0561321ab135a1446c920781be2caa501e`。
* **Update**: SkillGraph 新增五种语义 Cue、六种有限原语 Recipe、接触关键帧阻塞、程序化火球与三技能命中反馈；骨矛新 Sprite 保持人工确认门禁，未接入运行时。
* **Update**: Pure Run 技能 VFX 预览支持 Recipe/Cue/等级/路径/命中数、固定种子及可拖动时间轴；突刺端点不再被中途敌人的通用 LOS 误裁剪。
* **Sync**: `monster-ai` 已同步到来源指纹 `sha256:8026bb89e61794a34ef8dd68a329f94839923089a9e0f35de765607789bedbf2`。
* **Sync**: `roguelike-run` 已同步到来源指纹 `sha256:e1c57e802eb3635bbb0ed25157f98b55681f44a2e036ca52042edc38903dd526`。
* **Sync**: `okf-maintenance` 已同步到来源指纹 `sha256:9781161bcccb96d44d673207cf1f050e91c471289fb41fb2f3e31382fc2a2866`。
* **Sync**: `gameplay-test-framework` 已同步到来源指纹 `sha256:c0399c6a5e22f83268d07bd35e260f2e2de0d1eb881a184fdeb28b96cb34cbcb`。
* **Sync**: `project-architecture` 已同步到来源指纹 `sha256:cc2b86d403d34d9da429396e6e348cd4ba003f307f412cf30a0aef716b07806a`。

## 2026-07-30
* **Sync**: `pure-run-artwork` 已同步到来源指纹 `sha256:085379df987e40c0fceed1ac7b5456c08a516e49e7146abcff95d09b862556fc`。
* **Update**: Pure Run 方向图改为先用排除耳、口鼻、手脚和装备的纯核心主体蒙版校准；无手臂角色的手掌必须以多像素接触面直接嵌入主体边缘。
* **Sync**: `project-documentation` 已同步到来源指纹 `sha256:2d92453e876618b718cde40cef55d235774620335f74114e1e2efd52b8e2a48a`。
* **Sync**: `roguelike-run` 已同步到来源指纹 `sha256:0820098ef9c4869b66ff00e787d4a378c95f467f043bf77e1a8ccc2c0c372571`。
* **Sync**: `okf-maintenance` 已同步到来源指纹 `sha256:9781161bcccb96d44d673207cf1f050e91c471289fb41fb2f3e31382fc2a2866`。
* **Sync**: `project-architecture` 已同步到来源指纹 `sha256:2ad6b06e6d022bb041402d5a1e68816c7870669ce1ed2f0c7a8c8cd87256e00c`。
* **Sync**: `skill-graph` 已同步到来源指纹 `sha256:a2d637e9e22612d4785cc4ab6da340a3ca016f4c2d2c255f48a2af50ade27333`。
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:a2c370f95a6dffa0c91fc182418dc7fd375f32992c1f529a484ec189023b27de`。

## 2026-07-29
* **Sync**: `pure-run-artwork` 已同步到来源指纹 `sha256:44a4fde38bd8dd0859970e34d2ba54ce2067651d3dd526dc43b5d470f001be7f`。
* **Sync**: `unity-agent-workflow` 已同步到来源指纹 `sha256:0550c020533163fae8da57c9b22961a6989fa574dbef5c3ae3583d285e9179e4`。
* **Sync**: `project-documentation` 已同步到来源指纹 `sha256:5c32ee885b8ac4fca82b6f06a8c5414c23ee21161118e31e2a438ba64bcfa4df`。
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:e14364ded10298b483585c30fa2c6c4d0f3a257357adfb2914faf69044493202`。
* **Creation**: 新增 `pure-run-artwork` scope，沉淀角色美术尺寸、生成、去幕、Review、资产状态和提交边界。
* **Sync**: `roguelike-run` 已同步到来源指纹 `sha256:3246c857bc4055e840180cf5e1d52aab73c142bb636d61060e6da8ccf9542`。

## 2026-07-28
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:f094c7ad4afd293f4ad9475c73cd07d5a3a2545903900bbb52cb0747f54e93d1`。
* **Sync**: `roguelike-run` 已同步到来源指纹 `sha256:a75c15825a5b87fd5a7963a55f6a4b3f6d7bcc66fe5c9895de8843940577f5bc`。
* **Sync**: `gameplay-test-framework` 已同步到来源指纹 `sha256:f30f1cf05214a2c1c56c8bdfa8e5f229541093fcf3026f38a38e3546cae81a0c`。
* **Sync**: `project-documentation` 已同步到来源指纹 `sha256:1f4f15f36833e48bdb6de2116f69b3c4f262e88b3527c39f3072fba9db969412`。
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:27b7fdd07a1478edb0a38d15493f0286419bb5c581fbd78804297a9ae6266b87`。
* **Sync**: `skill-graph` 已同步到来源指纹 `sha256:dd33f78e40c74d2171da787945125ea8a3f001f381ab4509b70b96c818261f98`。
* **Sync**: `monster-ai` 已同步到来源指纹 `sha256:dc40a65a2b1a2a545f3f28b252b65ddf48b2bf3e28e85072499365875ae4ff25`。
* **Update**: 同步 `MaxUsesPerTurn` cantrip 的权威设计、Unit 回合计数、怪物 AI 统一可用性与正式技能数值。
* **Sync**: `unity-agent-workflow` 已同步到来源指纹 `sha256:feea3f5c79eed1429db582bf4f26c0a988afa724f8858af325e20548c47bfd26`。

## 2026-07-26
* **Sync**: `project-documentation` 已同步到来源指纹 `sha256:1917edf6608a303005ad92b48408f13063bb9a9f668bfc07486f9819fdb524a9`。
* **Sync**: `skill-graph` 已同步到来源指纹 `sha256:42e98c00ac118c735de6590654f4994ee19a0eab335656e688ba439b4c523dac`。
* **Sync**: `roguelike-run` 已同步到来源指纹 `sha256:c5b464ba43f093f7b028b92c2549ea172445b830088e21ee32e81d14babf21d3`。
* **Sync**: `monster-ai` 已同步到来源指纹 `sha256:064d37473ec009a0676a68352ee8b1833069ea81c807fcc0e8234d6a325ea5b6`。
* **Sync**: `gameplay-test-framework` 已同步到来源指纹 `sha256:b28b51a81457cc21f4269b053dfc1c97ac00db461265c2dc4ddacf353d9e3bfa`。
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:cf6cd093cfef1fe67a0c14a77a0a0f4d976ae40f588f564abe411d9f78c37f2c`。
* **Sync**: `unity-agent-workflow` 已同步到来源指纹 `sha256:7d5067a04ea4e6a4fc8b22e3fb17b5271f05d1f2c93a26d7826000898fa307f2`。

## 2026-07-25
* **Sync**: `roguelike-run` 已同步到来源指纹 `sha256:f4ff8c314f92d6bb6bcb10383d04574592d3a72678da6175258a6e46c6ae48c3`。
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:4892a9a9396799870c0a69551d433f30f60b18818b679e0abd4b61621e11a238`。
* **Sync**: `skill-graph` 已同步到来源指纹 `sha256:42e98c00ac118c735de6590654f4994ee19a0eab335656e688ba439b4c523dac`。
* **Sync**: `monster-ai` 已同步到来源指纹 `sha256:b01fbffe3d7cccf7d798bb61f39fc0ce682bd0ecabb8294a584e5bfe393f555a`。

## 2026-07-24
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:e87b82a09bf59a49f8a43ab453206406fe0ddcbfb990ff3b049534e819c06b67`。
* **Sync**: `project-documentation` 已同步到来源指纹 `sha256:d79372a22b9cc60b64d08bd17d8d5e8c0c1076d0154104103352f095cb6bff0e`。
* **Sync**: `gameplay-test-framework` 已同步到来源指纹 `sha256:03505c18a1c58168b7b69b6739f5794c7f9ac538425ec2b88c709b3fd6a7432d`。
* **Sync**: `skill-graph` 已同步到来源指纹 `sha256:64546eadc8cd66ef56356e6177ec1e90d0cbd4f1f48fcab51586030e12acd3b8`。

## 2026-07-23
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:0822cab4e0392c57db64cde9c07c55db1809a716e439321e26184326495b582e`。
* **Sync**: `project-documentation` 已同步到来源指纹 `sha256:0cee9620e2a4067589eda282545a7b591e2da1e0fb52c26af437edb1c2806e49`。
* **Sync**: `skill-graph` 已同步到来源指纹 `sha256:216702530bf8315f80c2facf3d1a459290d3716cff964da0b5143f6019ea7f8a`。
* **Sync**: `gameplay-test-framework` 已同步到来源指纹 `sha256:03505c18a1c58168b7b69b6739f5794c7f9ac538425ec2b88c709b3fd6a7432d`。
* **Sync**: `monster-ai` 已同步到来源指纹 `sha256:15d9e0d25e7ada8a91d98f94c48038ffd7b97736f996ff251bdc204decf8c8f7`。
* **Sync**: `project-architecture` 已同步到来源指纹 `sha256:38f691018a7cb17afb64c79bfa5b59000283f3fb2dad67c45104a7e05daeb3e1`。
* **Sync**: `roguelike-run` 已同步到来源指纹 `sha256:c58451ad405dde6f30a64fc4d37a25f5e989f8fd7e274b997c1af39b418b3a89`。
* **Sync**: `project-known-gaps` 已同步到来源指纹 `sha256:67cfe68ebcea5cdba1230c8c18ae935dc45a2b2a7f7c5d1acfbf983ded90ef1b`。

## 2026-07-22
* **Sync**: `monster-ai` 已同步到来源指纹 `sha256:c93f60b02efda1ca3f5243350b87e02d0d09f3d8fa31d1e89bfd5aefad5112c9`。
* **Sync**: `gameplay-test-framework` 已同步到来源指纹 `sha256:ea72a5a682d55be31a4eeda5f33226ab18ac54956d4880bcb16fb7f9882b39ec`。
* **Sync**: `skill-graph` 已同步到来源指纹 `sha256:061429ee5ba7a3b0810df7b8166aebae636559ede5c520a4bb312eec7fb8a97b`。
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:f50ef54e80612750e3d0e2b0fc7fa88d5d65d85c589ebf0605cc34c2077885b0`。
* **Sync**: `project-documentation` 已同步到来源指纹 `sha256:6a46949cebe9e135082a5972fbdb75e94bc529449f92d1fc2f1688c6e641b8e1`。
* **Sync**: `roguelike-run` 已同步到来源指纹 `sha256:1884f4fc252a540b3c156f036c0ec0a17d0e4305ac415b62612081b343c2775c`。

## 2026-07-19
* **Sync**: `project-documentation` 已同步到来源指纹 `sha256:42d4ec5e191aa824a2b25b1e71df5d6dc808a1f340c9677e5a79c368de5dc75a`。

## 2026-07-17
* **Sync**: `project-documentation` 已同步到来源指纹 `sha256:9e3fc2abb8fc7203d0d7b0245c54634e1f0111c0f3146a493126e30ccf328827`。
* **Sync**: `skill-graph` 已同步到来源指纹 `sha256:376c3eda0531910d96d810798cee3cef07c1109c44aea3ef1c0983854931398a`。
* **Sync**: `roguelike-run` 已同步到来源指纹 `sha256:2da48e44bf7153720c3a6cd7ced3489e0cae28bcf4146e50d27e0fbe37a43276`。
* **Sync**: `project-known-gaps` 已同步到来源指纹 `sha256:84ef9e15d718d0fdc31f08006b393bd403b9d512467d0ec8b6afdea710cad933`。
* **Sync**: `monster-ai` 已同步到来源指纹 `sha256:55eb3c23fb677cae86b96acc5a1d3e095e957405aed599c369bdf35785554586`。
* **Sync**: `gameplay-test-framework` 已同步到来源指纹 `sha256:bc2f8ba872a7e43c1f73226ae9e9ea48fa9aa27110030f94ba0a14f97ab43330`。
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:7476716c4fe3e81b169e3ea7a7e7e7a1e1d5f6d89e01798c060ba427a732066a`。

## 2026-07-16
* **Sync**: `skill-graph` 已同步到来源指纹 `sha256:cfc076a4fb606f80df1f884ffa698befce7351d59a46120809c7517fadc046ee`。
* **Sync**: `roguelike-run` 已同步到来源指纹 `sha256:ff754942be80393dd1872043284cd1fcb7f54d3b0617dffb5005c60a03bb7e80`。
* **Sync**: `monster-ai` 已同步到来源指纹 `sha256:2e68e9c787a39b8ec3c98971f1ffa8a8241eb21aab656d7238040cb21d64566a`。
* **Sync**: `gameplay-test-framework` 已同步到来源指纹 `sha256:0831db66b3f9367493259c1263f785693a53809c89ed03782bb412b424828b2d`。
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:9c29b0335f80fdcbd610adf374a97dfb0f8051e643fbfd573b100a150763d502`。

## 2026-07-15
* **Sync**: `okf-maintenance` 已同步到来源指纹 `sha256:adbe32e3cf9f575b3f59f7f38d4666d01640762723f8b7eb58ad9bcd96cfe774`。
* **Sync**: `project-documentation` 已同步到来源指纹 `sha256:7225f0f8f97854f10d0461539683e1ea1fc01e388e9bbbf75958ffdc4a8c2902`。
* **Sync**: `unity-agent-workflow` 已同步到来源指纹 `sha256:6617411637c9911034d298186c5d4f0c70ea6e65ea82f1cfe9692ed512d5d3c4`。
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:49c60c62589897f441d456ae2d98e125358bd3fc888f14e7d98ed8edb0786222`。
* **Sync**: `gameplay-test-framework` 已同步到来源指纹 `sha256:ac0c40ecf08a3791382ed3df8e4423f1995d9efab28c61384760bf8d7a649343`。
* **Sync**: `project-known-gaps` 已同步到来源指纹 `sha256:9c77cfbcdf7eaaabc5b94dfba923530905c98277735689a9512652bb115e2128`。
* **Sync**: `roguelike-run` 已同步到来源指纹 `sha256:d8e53a4c62644be88e719c29d56fbdedba90d06d4885e03ebdf5199ad4829edb`。
* **Sync**: `skill-graph` 已同步到来源指纹 `sha256:a19edfe2bdecc307735299939024a40b4d2a6071bf7b4c6d5a86407b86f76498`。
* **Creation**: 添加项目文档生命周期与统一已知缺口概念。
* **Update**: 将当前设计收敛为权威文档，并移除已经完成、过期或重复的 docs/plans。
* **Deprecation**: 三职业首切计划已完成知识迁移，概念状态改为 archived。
* **Sync**: `monster-ai` 已同步到来源指纹 `sha256:c4c7f7adc1fa8276fd7db81be3db016ba55f2f540895be24963a0ae46a508529`。

## 2026-07-14
* **Sync**: `mewgenics-reference-analysis` 已同步到来源指纹 `sha256:5440f1243da2099b84fb0adceb31384a23f2972b2828a31b623b58ab897e7bc3`。
* **Sync**: `skill-graph` 已同步到来源指纹 `sha256:c5bb833c80c14524000a378200da1320e749d264ae36b4f1855ed2f7d7473a49`。
* **Sync**: `roguelike-run` 已同步到来源指纹 `sha256:fa9001e6f81de2611327d6d630cc6221d897541d1d6d9958b5dc344a9945351b`。
* **Sync**: `monster-ai` 已同步到来源指纹 `sha256:6aeee3da30a2d0fe4073e3151e98c97c09a8c74d828c2b39efe55ea53a75edaa`。
* **Sync**: `gameplay-test-framework` 已同步到来源指纹 `sha256:5d4ab233e01cba7e6371f498b746679e238e8583282891667409a3e92611ad37`。
* **Sync**: `first-slice-three-class-skills` 已同步到来源指纹 `sha256:36a0cf2d06c6997b451c0fe52f1d6e2261e02b40255f42d6193c5358dd9f7a95`。
* **Sync**: `battle-system` 已同步到来源指纹 `sha256:a73a6fe393e8f3d04d86b9af95cfa3cdb417f053b8897d5b5e79f3e9b3dea7a3`。
* **Creation**: 添加 Gameplay Test Framework 概念及其 compiler、runtime adapters 和 specs 影响范围。
* **Update**: 同步 Pure Run 7 层只前进地图、单人成长、高级技能保底、显式遭遇配方和 Mew 风格怪物 AI 职责边界。
* **Sync**: `okf-maintenance` 已同步到来源指纹 `sha256:8e4038c1ec510f34d13190724394d01a2edceea63d9821e017694f419fe58e70`。
* **Sync**: `project-architecture` 已同步到来源指纹 `sha256:0d4feed7888853a0ca5f469716af441f37f1f2e8ce95009e80e8c6d96cf06637`。
* **Sync**: `unity-agent-workflow` 已同步到来源指纹 `sha256:35c9b0a981ee8d162b4d6961a9544cf4b78543d8917ab597cb65f80847bd4710`。
* **Creation**: 添加路径到 `catalog_scope` 的影响映射、Agent worktree 检测与 OKF 自动同步流程。
* **Creation**: 建立符合 OKF v0.1 与 Tactics Profile v0.1 的独立知识 bundle。
* **Creation**: 添加项目架构、SkillGraph、怪物 AI、战斗、Roguelike Run、三职业首切计划和 Unity Agent 工作流概念。
* **Creation**: 登记 OKF v0.1 规范与 Karpathy LLM Wiki 方法来源。
* **Lint**: 建立仓库内 frontmatter、链接、index、状态和 `catalog_scope` 校验工具。
