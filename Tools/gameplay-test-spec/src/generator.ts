import type { GenerationResult, ScenarioSpec } from "./schema.js";
import { validateScenarioSpec } from "./validator.js";

function includesAny(text: string, words: string[]): boolean {
  return words.some(word => text.includes(word.toLowerCase()));
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function extractNumberAfterKeywords(text: string, keywords: string[], fallback: number): number {
  for (const keyword of keywords) {
    const match = text.match(new RegExp(`${escapeRegExp(keyword)}[^\\d]*(\\d+(?:\\.\\d+)?)`, "i"));
    if (match) {
      return Number(match[1]);
    }
  }

  return fallback;
}

function extractHealthTransition(text: string, fallbackFrom: number, fallbackTo: number): { from: number; to: number } {
  const match = text.match(/(?:hp|血量|生命)[^\d]*(\d+(?:\.\d+)?)[^\d]+(?:到|至|->|变成|提升到|降到)[^\d]*(\d+(?:\.\d+)?)/i);
  if (!match) {
    return { from: fallbackFrom, to: fallbackTo };
  }

  return {
    from: Number(match[1]),
    to: Number(match[2])
  };
}

export function generateScenarioSpec(text: string): GenerationResult {
  const normalized = text.trim().toLowerCase();
  const diagnostics = [];

  if (!normalized) {
    return {
      diagnostics: [{
        code: "MissingInput",
        severity: "error",
        message: "Natural language input is empty."
      }],
      needsClarification: true,
      missingFields: ["input"],
      ambiguousFields: []
    };
  }

  let spec: ScenarioSpec | undefined;

  if (includesAny(normalized, ["非法", "无终点", "no terminal", "invalid"])) {
    spec = createInvalidGraphSpec();
  } else if (includesAny(normalized, ["蓝量不足", "mana不足", "mana不够", "没蓝", "没有蓝", "mana too low"])) {
    spec = createManaInsufficientSpec();
  } else if (includesAny(normalized, ["蓝量足够", "mana足够", "蓝量够", "足够释放", "mana enough"])) {
    spec = createManaSuccessSpec();
  } else if (includesAny(normalized, ["超出射程", "射程外", "out of range", "range too far"])) {
    spec = createTargetOutOfRangeSpec();
  } else if (includesAny(normalized, ["没有任何有效目标", "没有有效目标", "无有效目标", "no valid target", "no target"])) {
    spec = createNoValidTargetSpec();
  } else if (includesAny(normalized, ["反击", "counter", "反伤"])) {
    spec = createCounterRetaliationSpec();
  } else if (includesAny(normalized, ["标记", "mark", "marked"])) {
    spec = createMarkedDamageSpec();
  } else if (includesAny(normalized, ["范围伤害", "群体", "aoe", "area damage", "半径内"]) ) {
    spec = createAreaDamageSpec();
  } else if (includesAny(normalized, ["击退", "knockback", "推开", "吹飞"])) {
    spec = createKnockbackSpec();
  } else if (includesAny(normalized, ["友军", "盟友", "ally"]) && includesAny(normalized, ["治疗", "heal", "回复"])) {
    const healAmount = extractNumberAfterKeywords(normalized, ["恢复", "治疗", "heal", "回复"], 4);
    spec = createAllyHealSpec(healAmount);
  } else if (includesAny(normalized, ["增益", "buff", "强化"])) {
    const duration = extractNumberAfterKeywords(normalized, ["持续", "duration", "回合", "turn"], 2);
    spec = createApplyBuffSpec(duration);
  } else if (includesAny(normalized, ["治疗", "自愈", "heal"])) {
    const hp = extractHealthTransition(normalized, 6, 10);
    spec = createSelfHealSpec(hp.from, hp.to);
  } else if (includesAny(normalized, ["伤害", "damage", "打一个", "攻击"])) {
    const hp = extractHealthTransition(normalized, 10, 3);
    spec = createSingleTargetDamageSpec(hp.from, hp.to);
  }

  if (!spec) {
    return {
      diagnostics: [{
        code: "UnrecognizedIntent",
        severity: "error",
        message: "Input did not match MVP supported skill test intents."
      }],
      needsClarification: true,
      missingFields: ["scenarioIntent"],
      ambiguousFields: []
    };
  }

  const validation = validateScenarioSpec(spec);
  diagnostics.push(...validation.diagnostics);

  return {
    spec: validation.spec,
    diagnostics,
    needsClarification: !validation.valid,
    missingFields: [],
    ambiguousFields: []
  };
}

function createSelfHealSpec(initialHealth: number, expectedHealth: number): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "SelfHealSkillRaisesCasterHealth",
    tags: ["mvp", "skill", "heal"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      { kind: "createSkillGraph", parameters: { alias: "graph", graphKind: "selfHeal", healAmount: expectedHealth - initialHealth } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: initialHealth, maxHealth: expectedHealth } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } }
    ],
    actions: [
      { kind: "executeSkillGraph", parameters: { graphAlias: "graph", casterAlias: "caster" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Completed", parameters: {} },
      { kind: "unitHealthEquals", target: "caster", expected: expectedHealth, parameters: {} }
    ]
  };
}

function createManaSuccessSpec(): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "ManaConsumedOnSuccessfulAbilityUse",
    tags: ["mvp", "skill", "mana", "ability", "heal"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      { kind: "createSkillGraph", parameters: { alias: "graph", graphKind: "selfHeal", healAmount: 4 } },
      { kind: "createSkillAbilityConfig", parameters: { alias: "ability", graphAlias: "graph", manaCost: 3, targetRange: 1 } },
      { kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 6, maxHealth: 10, mana: 10, cellAlias: "casterCell" } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } },
      { kind: "createSkillAbility", parameters: { alias: "abilityImpl", configAlias: "ability", ownerAlias: "caster" } },
      { kind: "selectAbility", parameters: { abilityAlias: "abilityImpl" } }
    ],
    actions: [
      { kind: "executeAbilityOnTarget", target: "caster", parameters: { abilityAlias: "abilityImpl" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Completed", parameters: {} },
      { kind: "unitManaEquals", target: "caster", expected: 7, parameters: {} },
      { kind: "unitHealthEquals", target: "caster", expected: 10, parameters: {} },
      { kind: "stepMessageContains", expected: "Completed", parameters: {} }
    ]
  };
}

function createManaInsufficientSpec(): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "ManaInsufficientPreventsAbilityUse",
    tags: ["mvp", "skill", "mana", "ability", "heal"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      { kind: "createSkillGraph", parameters: { alias: "graph", graphKind: "selfHeal", healAmount: 4 } },
      { kind: "createSkillAbilityConfig", parameters: { alias: "ability", graphAlias: "graph", manaCost: 8, targetRange: 1 } },
      { kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 6, maxHealth: 10, mana: 5, cellAlias: "casterCell" } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } },
      { kind: "createSkillAbility", parameters: { alias: "abilityImpl", configAlias: "ability", ownerAlias: "caster" } },
      { kind: "selectAbility", parameters: { abilityAlias: "abilityImpl" } }
    ],
    actions: [
      { kind: "executeAbilityOnTarget", target: "caster", parameters: { abilityAlias: "abilityImpl" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Failed", parameters: {} },
      { kind: "unitManaEquals", target: "caster", expected: 5, parameters: {} },
      { kind: "lastErrorContains", expected: "Not enough mana", parameters: {} },
      { kind: "stepMessageContains", expected: "Failed", parameters: {} }
    ]
  };
}

function createTargetOutOfRangeSpec(): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "TargetOutOfRangePreventsAbilityUse",
    tags: ["mvp", "skill", "mana", "ability", "damage", "range"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      { kind: "createSkillGraph", parameters: { alias: "graph", graphKind: "singleTargetDamage", baseDamage: 7 } },
      { kind: "createSkillAbilityConfig", parameters: { alias: "ability", graphAlias: "graph", manaCost: 3, targetRange: 2 } },
      { kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } },
      { kind: "createCell", parameters: { alias: "targetCell", x: 4, y: 0 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, mana: 10, cellAlias: "casterCell" } },
      { kind: "createUnit", parameters: { alias: "target", playerNumber: 1, health: 10, maxHealth: 10, cellAlias: "targetCell" } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } },
      { kind: "createSkillAbility", parameters: { alias: "abilityImpl", configAlias: "ability", ownerAlias: "caster" } },
      { kind: "selectAbility", parameters: { abilityAlias: "abilityImpl" } }
    ],
    actions: [
      { kind: "executeAbilityOnTarget", target: "target", parameters: { abilityAlias: "abilityImpl" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Failed", parameters: {} },
      { kind: "unitManaEquals", target: "caster", expected: 10, parameters: {} },
      { kind: "lastErrorContains", expected: "Target out of range", parameters: {} },
      { kind: "stepMessageContains", expected: "Failed", parameters: {} }
    ]
  };
}

function createNoValidTargetSpec(): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "NoValidTargetPreventsAbilityUse",
    tags: ["mvp", "skill", "mana", "ability", "damage", "target"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      { kind: "createSkillGraph", parameters: { alias: "graph", graphKind: "singleTargetDamage", baseDamage: 7 } },
      { kind: "createSkillAbilityConfig", parameters: { alias: "ability", graphAlias: "graph", manaCost: 3, targetRange: 2 } },
      { kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } },
      { kind: "createCell", parameters: { alias: "targetCell", x: 1, y: 0 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, mana: 10, cellAlias: "casterCell" } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } },
      { kind: "createSkillAbility", parameters: { alias: "abilityImpl", configAlias: "ability", ownerAlias: "caster" } },
      { kind: "selectAbility", parameters: { abilityAlias: "abilityImpl" } }
    ],
    actions: [
      { kind: "executeAbilityOnCell", target: "targetCell", parameters: { abilityAlias: "abilityImpl" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Failed", parameters: {} },
      { kind: "unitManaEquals", target: "caster", expected: 10, parameters: {} },
      { kind: "lastErrorContains", expected: "No valid target in range", parameters: {} },
      { kind: "stepMessageContains", expected: "Failed", parameters: {} }
    ]
  };
}

function createSingleTargetDamageSpec(initialHealth: number, expectedHealth: number): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "SingleTargetDamageReducesTargetHealth",
    tags: ["mvp", "skill", "damage"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      { kind: "createSkillGraph", parameters: { alias: "graph", graphKind: "singleTargetDamage", baseDamage: initialHealth - expectedHealth } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, cell: { x: 0, y: 0 } } },
      { kind: "createUnit", parameters: { alias: "target", playerNumber: 1, health: initialHealth, maxHealth: initialHealth, defenceFactor: 0, cell: { x: 1, y: 0 } } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } }
    ],
    actions: [
      { kind: "executeSkillGraph", parameters: { graphAlias: "graph", casterAlias: "caster" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Completed", parameters: {} },
      { kind: "unitHealthEquals", target: "target", expected: expectedHealth, parameters: {} }
    ]
  };
}

function createApplyBuffSpec(duration: number): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "ApplySelfBuff",
    tags: ["mvp", "skill", "buff", "status"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      {
        kind: "createSkillGraph",
        parameters: {
          alias: "buffGraph",
          graphKind: "applyBuff",
          selectionKind: "self",
          buffName: "Might",
          buffEffectType: "None",
          triggerTiming: "None",
          duration
        }
      },
      { kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, cellAlias: "casterCell" } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } }
    ],
    actions: [
      { kind: "executeSkillGraph", parameters: { graphAlias: "buffGraph", casterAlias: "caster" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Completed", parameters: {} },
      { kind: "unitHasBuff", target: "caster", expected: "Might", parameters: {} },
      { kind: "unitBuffDurationEquals", target: "caster", expected: duration, parameters: { buffName: "Might" } }
    ]
  };
}

function createAreaDamageSpec(): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "AreaDamageHitsAllTargetsInRadius",
    tags: ["mvp", "skill", "damage", "aoe"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      {
        kind: "createSkillGraph",
        parameters: {
          alias: "areaGraph",
          graphKind: "areaDamage",
          baseDamage: 3,
          radius: 1,
          maxRange: 4
        }
      },
      { kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } },
      { kind: "createCell", parameters: { alias: "targetPointCell", x: 1, y: 1 } },
      { kind: "createCell", parameters: { alias: "targetCellA", x: 1, y: 0 } },
      { kind: "createCell", parameters: { alias: "targetCellB", x: 0, y: 1 } },
      { kind: "createCell", parameters: { alias: "safeCell", x: 4, y: 4 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, cellAlias: "casterCell" } },
      { kind: "createUnit", parameters: { alias: "targetA", playerNumber: 1, health: 10, maxHealth: 10, cellAlias: "targetCellA" } },
      { kind: "createUnit", parameters: { alias: "targetB", playerNumber: 1, health: 10, maxHealth: 10, cellAlias: "targetCellB" } },
      { kind: "createUnit", parameters: { alias: "safeTarget", playerNumber: 1, health: 10, maxHealth: 10, cellAlias: "safeCell" } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } }
    ],
    actions: [
      { kind: "executeSkillGraph", parameters: { graphAlias: "areaGraph", casterAlias: "caster", targetPointAlias: "targetPointCell" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Completed", parameters: {} },
      { kind: "unitHealthEquals", target: "targetA", expected: 7, parameters: {} },
      { kind: "unitHealthEquals", target: "targetB", expected: 7, parameters: {} },
      { kind: "unitHealthEquals", target: "safeTarget", expected: 10, parameters: {} }
    ]
  };
}

function createKnockbackSpec(): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "KnockbackMovesTargetAwayFromCaster",
    tags: ["mvp", "skill", "movement", "knockback"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      {
        kind: "createSkillGraph",
        parameters: {
          alias: "knockbackGraph",
          graphKind: "knockback",
          distance: 1,
          maxRange: 1
        }
      },
      { kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } },
      { kind: "createCell", parameters: { alias: "targetCell", x: 1, y: 0 } },
      { kind: "createCell", parameters: { alias: "landingCell", x: 2, y: 0 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, cellAlias: "casterCell" } },
      { kind: "createUnit", parameters: { alias: "target", playerNumber: 1, health: 10, maxHealth: 10, cellAlias: "targetCell" } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } }
    ],
    actions: [
      { kind: "executeSkillGraph", parameters: { graphAlias: "knockbackGraph", casterAlias: "caster", primaryTargetAlias: "target" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Completed", parameters: {} },
      { kind: "unitCellEquals", target: "target", expected: { x: 2, y: 0 }, parameters: {} }
    ]
  };
}

function createAllyHealSpec(healAmount: number): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "AllyHealRestoresFriendlyUnitHealth",
    tags: ["mvp", "skill", "heal", "ally"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      {
        kind: "createSkillGraph",
        parameters: {
          alias: "allyHealGraph",
          graphKind: "allyHeal",
          healAmount,
          maxRange: 1
        }
      },
      { kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } },
      { kind: "createCell", parameters: { alias: "allyCell", x: 1, y: 0 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, cellAlias: "casterCell" } },
      { kind: "createUnit", parameters: { alias: "ally", playerNumber: 0, health: 6, maxHealth: 10, cellAlias: "allyCell" } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster", "ally"] } }
    ],
    actions: [
      { kind: "executeSkillGraph", parameters: { graphAlias: "allyHealGraph", casterAlias: "caster" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Completed", parameters: {} },
      { kind: "unitHealthEquals", target: "ally", expected: 10, parameters: {} }
    ]
  };
}

function createMarkedDamageSpec(): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "MarkedTargetTakesCriticalDamage",
    tags: ["mvp", "skill", "buff", "mark", "damage"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      {
        kind: "createSkillGraph",
        parameters: {
          alias: "markGraph",
          graphKind: "applyBuff",
          selectionKind: "enemy",
          buffName: "Marked",
          buffEffectType: "Marked",
          triggerTiming: "BeforeAttacked",
          duration: 2,
          maxRange: 1
        }
      },
      {
        kind: "createSkillGraph",
        parameters: {
          alias: "damageGraph",
          graphKind: "singleTargetDamage",
          baseDamage: 4,
          canCrit: false,
          isRanged: false
        }
      },
      { kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } },
      { kind: "createCell", parameters: { alias: "targetCell", x: 1, y: 0 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, cellAlias: "casterCell" } },
      { kind: "createUnit", parameters: { alias: "target", playerNumber: 1, health: 10, maxHealth: 10, cellAlias: "targetCell" } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } }
    ],
    actions: [
      { kind: "executeSkillGraph", parameters: { graphAlias: "markGraph", casterAlias: "caster", primaryTargetAlias: "target" } },
      { kind: "executeSkillGraph", parameters: { graphAlias: "damageGraph", casterAlias: "caster", primaryTargetAlias: "target" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Completed", parameters: {} },
      { kind: "unitHasBuff", target: "target", expected: "Marked", parameters: {} },
      { kind: "unitBuffDurationEquals", target: "target", expected: 2, parameters: { buffName: "Marked" } },
      { kind: "unitHealthEquals", target: "target", expected: 2, parameters: {} }
    ]
  };
}

function createCounterRetaliationSpec(): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "CounterBuffRetaliatesWhenDamaged",
    tags: ["mvp", "skill", "buff", "counter", "damage"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      {
        kind: "createSkillGraph",
        parameters: {
          alias: "counterGraph",
          graphKind: "applyBuff",
          selectionKind: "enemy",
          buffName: "Counter",
          buffEffectType: "None",
          triggerTiming: "DamageTaken",
          duration: 2,
          maxRange: 1
        }
      },
      {
        kind: "createSkillGraph",
        parameters: {
          alias: "damageGraph",
          graphKind: "singleTargetDamage",
          baseDamage: 4,
          canCrit: false,
          isRanged: false
        }
      },
      { kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } },
      { kind: "createCell", parameters: { alias: "targetCell", x: 1, y: 0 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, cellAlias: "casterCell" } },
      { kind: "createUnit", parameters: { alias: "target", playerNumber: 1, health: 10, maxHealth: 10, luck: 0, cellAlias: "targetCell" } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } }
    ],
    actions: [
      { kind: "executeSkillGraph", parameters: { graphAlias: "counterGraph", casterAlias: "caster", primaryTargetAlias: "target" } },
      { kind: "executeSkillGraph", parameters: { graphAlias: "damageGraph", casterAlias: "caster", primaryTargetAlias: "target" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Completed", parameters: {} },
      { kind: "unitHasBuff", target: "target", expected: "Counter", parameters: {} },
      { kind: "unitBuffDurationEquals", target: "target", expected: 2, parameters: { buffName: "Counter" } },
      { kind: "unitHealthEquals", target: "target", expected: 6, parameters: {} },
      { kind: "unitHealthEquals", target: "caster", expected: 9, parameters: {} }
    ]
  };
}

function createInvalidGraphSpec(): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "InvalidGraphWithoutTerminalIsRejected",
    tags: ["mvp", "skill", "validation"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      { kind: "createSkillGraph", parameters: { alias: "graph", graphKind: "invalidSelfHeal", healAmount: 5 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10 } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } }
    ],
    actions: [
      { kind: "executeSkillGraph", parameters: { graphAlias: "graph", casterAlias: "caster" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Aborted", parameters: {} },
      { kind: "validationErrorCodeIncludes", expected: "NoTerminalNode", parameters: {} }
    ]
  };
}
