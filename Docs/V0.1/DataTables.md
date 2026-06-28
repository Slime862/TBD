# 读表与表结构设计

## 1. 目标
这套表结构设计的目标是：
- 方便你直接看表、筛表、改数值。
- 尽量复用 `Project_CyberSousa` 现成的导表与 RefData 读取框架。
- 让 v0.1 的英雄、技能、怪物、地点都能数据驱动。
- 给后续“技能触发条件下拉配置”预留扩展空间。

## 2. 参考的现成框架约定
根据 `D:\Projects\Project_CyberSousa` 当前实现，可直接沿用以下约定：
- Excel 源文件目录：`Assets/Resources/RefData/Excel`
- 导出文本目录：`Assets/Resources/RefData/ExportTxt`
- 每个 sheet 导出为一个同名 `.txt`
- 第一行为字段名
- `#` 前缀列为整列注释，导出时跳过
- `~` 前缀单元格可作为整行竖向注释，解析时跳过
- 列表使用 `;` 分隔
- `Vector2/Vector3` 使用 `:` 分隔
- 空列表使用 `*`

这意味着 v0.1 最好继续沿用“简单字段 + 列表字段 + 多表关联”的方式，不要一开始就把大量 JSON 塞进单元格。

## 3. 建议的数据组织方式
- 一个概念一张主表。
- 用 `id` 做主键，统一用 `long`。
- 需要多效果、多单位波次、多可选项时，拆从表，不把一整串复杂结构塞在一列里。
- UI 展示文本和战斗数值尽量在同一批表中可查到，避免后期一半在代码、一半在表里。

## 4. 建议的核心表

### 4.1 `global_config`
用途：
- 放全局常量和 v0.1 公共参数。

建议字段：
- `id`
- `battleSlotCount`
- `teamSizeLimit`
- `travelMoveInterval`
- `skillSlowMotionScale`
- `skillSlowMotionDuration`
- `battleIntroDelay`
- `eliteTintStrength`
- `bossTintStrength`

### 4.2 `hero`
用途：
- 定义英雄静态模板。

建议字段：
- `id`
- `code`
- `name`
- `desc`
- `baseHp`
- `baseAtk`
- `baseDef`
- `baseSpeed`
- `defaultPosition`
- `passiveId`
- `normalAttackPool`
- `skillPool`
- `branchPool`
- `portraitRes`
- `battleRes`

字段说明：
- `normalAttackPool` 用 `;` 存普攻技能 id 列表。
- `skillPool` 用 `;` 存技能 id 列表。
- `branchPool` 用 `;` 存职业分支 id 列表。

### 4.3 `hero_level`
用途：
- 定义英雄升级成长。

建议字段：
- `id`
- `heroId`
- `level`
- `hpBonus`
- `atkBonus`
- `defBonus`
- `speedBonus`
- `unlockSkillIds`
- `unlockTrinketIds`

说明：
- 如果 v0.1 升级只改数值，也可以先只保留数值列。

### 4.4 `hero_branch`
用途：
- 定义职业分支。

建议字段：
- `id`
- `heroId`
- `name`
- `desc`
- `statModifierHp`
- `statModifierAtk`
- `statModifierDef`
- `statModifierSpeed`
- `extraPassiveId`
- `availableSkillIds`
- `triggerOptionGroupIds`

说明：
- `availableSkillIds` 允许分支限制技能池。
- `triggerOptionGroupIds` 预留给后续“不同分支对应不同触发条件选项集合”。

### 4.5 `passive`
用途：
- 定义英雄被动、怪物特性、分支额外被动。

建议字段：
- `id`
- `name`
- `desc`
- `triggerTiming`
- `effectType`
- `effectValue1`
- `effectValue2`
- `effectValue3`

说明：
- v0.1 可以先只支持少量被动类型，不必一次做成超大系统。

### 4.6 `skill`
用途：
- 定义英雄与怪物共用的技能模板。

建议字段：
- `id`
- `code`
- `name`
- `desc`
- `skillCategory`
- `priority`
- `cooldown`
- `castPositionMask`
- `targetTeam`
- `targetPositionMask`
- `targetSelectRule`
- `targetCount`
- `triggerType`
- `triggerParam1`
- `triggerParam2`
- `triggerOptionGroupId`
- `iconRes`
- `castAnimKey`
- `hitAnimKey`

字段说明：
- `skillCategory` 可区分普攻/主动技能/怪物技能/Boss 技能。
- `castPositionMask` 建议用 `1111` 这类字符串或位掩码表达可施法站位。
- `targetPositionMask` 表达可命中目标站位。
- `triggerOptionGroupId` 为未来玩家自定义触发条件留口。

### 4.7 `skill_effect`
用途：
- 一条技能对应多段效果。

建议字段：
- `id`
- `skillId`
- `order`
- `effectType`
- `value1`
- `value2`
- `value3`
- `targetOverride`
- `moveOffset`
- `applyChance`

说明：
- 例如一个技能先造成伤害再推位，就拆成两行效果。
- `moveOffset` 可用正负值表达前推/后拉。

### 4.8 `trigger_option`
用途：
- 定义未来可供玩家选择的触发条件模板。

建议字段：
- `id`
- `groupId`
- `name`
- `desc`
- `triggerType`
- `triggerParam1`
- `triggerParam2`
- `uiOrder`

说明：
- v0.1 即使 UI 不开放修改，也建议先把结构留好。

### 4.9 `trinket`
用途：
- 定义饰品。

建议字段：
- `id`
- `name`
- `desc`
- `slotType`
- `rarity`
- `effectType`
- `effectValue1`
- `effectValue2`
- `effectValue3`
- `iconRes`

### 4.10 `monster`
用途：
- 定义怪物与其精英形态。

建议字段：
- `id`
- `code`
- `name`
- `desc`
- `monsterKind`
- `baseMonsterId`
- `baseHp`
- `baseAtk`
- `baseDef`
- `baseSpeed`
- `defaultPosition`
- `skillIds`
- `passiveId`
- `isElite`
- `portraitRes`
- `battleRes`

字段说明：
- 普通怪 `isElite = false`。
- 精英怪可单独一行，并通过 `baseMonsterId` 关联普通怪。

### 4.11 `stage`
用途：
- 定义地点和地点预览信息。

建议字段：
- `id`
- `name`
- `desc`
- `recommendLevel`
- `travelBgRes`
- `waveGroupId`
- `bossMonsterId`
- `rewardPreview`

说明：
- 营地界面可直接读取这里展示“即将经历什么”。

### 4.12 `stage_wave`
用途：
- 定义地点中的波次顺序。

建议字段：
- `id`
- `waveGroupId`
- `waveIndex`
- `waveType`
- `title`
- `desc`
- `enemyIds`
- `enemyPositions`

字段说明：
- `waveType` 区分 normal / elite / boss。
- `enemyIds` 用 `;` 存怪物 id。
- `enemyPositions` 用 `;` 存对应站位，例如 `1;2;4`。

如果后续一波里需要更多额外字段，再拆 `stage_wave_unit` 从表。

## 5. v0.1 最小落地表集合
如果要尽快推进实现，第一批只需要这些表：
- `global_config`
- `hero`
- `hero_branch`
- `passive`
- `skill`
- `skill_effect`
- `trinket`
- `monster`
- `stage`
- `stage_wave`

`hero_level` 和 `trigger_option` 可以先建表头，内容后补。

## 6. 字段设计建议

### 6.1 关于站位
- 尽量把“可施法站位”和“可命中站位”做成独立字段。
- 不要只写“前排/后排”文本，否则后面做推拉位移时会不够精确。

### 6.2 关于触发条件
- v0.1 先以“预设触发类型 + 2~3 个参数”解决，不要先做表达式解释器。
- 常见触发可先支持：
  - 敌方前排存在时
  - 自身血量低于 X
  - 相邻友军血量低于 X
  - 有多个目标可命中时
  - 技能冷却结束且可命中目标存在时

### 6.3 关于效果类型
- v0.1 建议优先支持：
  - `Damage`
  - `Heal`
  - `Shield`
  - `Buff`
  - `Debuff`
  - `Push`
  - `Pull`
  - `SelfMove`
  - `Mark`

不要一开始就扩太多。

## 7. RefData 代码接入建议
如果直接复用 `Project_CyberSousa` 的链路，建议结构如下：
- 每张表一个 `XXXRefData` 类
- 多行表走 `SCRefDataList<T>`
- 单行全局表走 `SCRefDataCore`
- 新增一个本项目自己的 `SCRefDataMgr` 或同类管理器统一持有引用

这样后面查表和初始化都比较稳定。

## 8. 推荐目录方案
如果后续要正式接这套表，建议预留：

```text
Assets/Resources/RefData/
  Excel/
  ExportTxt/

Assets/Scripts/Runtime/RefData/
  HeroRefData.cs
  HeroBranchRefData.cs
  PassiveRefData.cs
  SkillRefData.cs
  SkillEffectRefData.cs
  TrinketRefData.cs
  MonsterRefData.cs
  StageRefData.cs
  StageWaveRefData.cs
```

## 9. 风险提醒
- 如果 `skill` 表里同时塞触发、目标、效果、演出、分支限制的所有复杂结构，很快会变成难维护大表。
- 如果地点和波次不拆表，后面预览界面与战斗生成都会变得很难读。
- 如果英雄当前配置也放回静态表里，后续营地修改和存档会很别扭。

## 10. 建议的下一步
- 先建空表与表头。
- 先填 2 名英雄、4 种怪、1 个地点的最小数据。
- 再按这些表头去写 RefData 类和运行时数据结构。

这样能最快把文档变成真正可落地的工程结构。
