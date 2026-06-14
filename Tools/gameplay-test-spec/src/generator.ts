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
  } else if (includesAny(normalized, ["冲锋", "charge", "突进"])) {
    spec = createChargeSpec();
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
      { kind: "createUnit", parameters: { alias: "targetA", playerNumber: 1, health: 10, maxHealth: 10, defenceFactor: 0, cellAlias: "targetCellA" } },
      { kind: "createUnit", parameters: { alias: "targetB", playerNumber: 1, health: 10, maxHealth: 10, defenceFactor: 0, cellAlias: "targetCellB" } },
      { kind: "createUnit", parameters: { alias: "safeTarget", playerNumber: 1, health: 10, maxHealth: 10, defenceFactor: 0, cellAlias: "safeCell" } },
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
            canCrit: true,
            isRanged: false
          }
        },
      { kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } },
      { kind: "createCell", parameters: { alias: "targetCell", x: 1, y: 0 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, cellAlias: "casterCell" } },
      { kind: "createUnit", parameters: { alias: "target", playerNumber: 1, health: 10, maxHealth: 10, defenceFactor: 0, cellAlias: "targetCell" } },
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

function createChargeSpec(): ScenarioSpec {
  return {
    feature: "SkillGraph",
    scenario: "ChargeStrikeMovesAndDamagesTarget",
    tags: ["mvp", "skill", "movement", "charge", "damage"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      {
        kind: "createSkillGraph",
        parameters: {
          alias: "chargeGraph",
          graphKind: "charge",
          collisionDamage: 1,
          maxRange: 3
        }
      },
      { kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } },
      { kind: "createCell", parameters: { alias: "pathCell", x: 1, y: 0 } },
      { kind: "createCell", parameters: { alias: "targetCell", x: 2, y: 0 } },
      { kind: "createCell", parameters: { alias: "retreatCell", x: 3, y: 0 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, cellAlias: "casterCell" } },
      { kind: "createUnit", parameters: { alias: "target", playerNumber: 1, health: 10, maxHealth: 10, cellAlias: "targetCell" } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } }
    ],
    actions: [
      { kind: "executeSkillGraph", parameters: { graphAlias: "chargeGraph", casterAlias: "caster", primaryTargetAlias: "target" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Completed", parameters: {} },
      { kind: "unitCellEquals", target: "caster", expected: { x: 2, y: 0 }, parameters: {} },
      { kind: "unitCellEquals", target: "target", expected: { x: 3, y: 0 }, parameters: {} },
      { kind: "unitHealthEquals", target: "target", expected: 9, parameters: {} }
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
      { kind: "createUnit", parameters: { alias: "target", playerNumber: 1, health: 10, maxHealth: 10, defenceFactor: 0, luck: 0, cellAlias: "targetCell" } },
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

// ═══════════════════════════════════════════
//  SkillGraphSpec 生成（NL → 技能图结构）
// ═══════════════════════════════════════════

export interface SkillGraphSpecNode {
  Id: string;
  Type: string;
  Parameters?: Record<string, unknown>;
}

export interface SkillGraphSpecEdge {
  Source: string;
  Target: string;
  Port?: string;
}

export interface SkillGraphSpecOutput {
  DisplayName: string;
  Description: string;
  Nodes: SkillGraphSpecNode[];
  Edges: SkillGraphSpecEdge[];
}

export interface SkillGraphSpecGenerationResult {
  spec?: SkillGraphSpecOutput;
  diagnostics: Array<{ code: string; severity: string; message: string }>;
  needsClarification: boolean;
  missingFields: string[];
  ambiguousFields: string[];
  questionsToAsk: string[];
}

export interface SkillDesignAnswers {
  displayName: string;
  description?: string;
  targetType: "single_enemy" | "self" | "ally" | "area";
  effects: Array<"damage" | "heal" | "buff" | "knockback" | "dash">;
  damageType?: "Physical" | "Magical";
  baseDamage?: number;
  healAmount?: number;
  buffName?: string;
  buffDuration?: number;
  buffIsUnique?: boolean;
  knockbackDistance?: number;
  isRanged?: boolean;
  canCrit?: boolean;
  maxRange?: number;
  areaRadius?: number;
  dashMaxRange?: number;
  dashCollisionDamage?: number;
}

export function generateSkillGraphSpec(text: string): SkillGraphSpecGenerationResult {
  const normalized = text.trim().toLowerCase();

  if (!normalized) {
    return {
      diagnostics: [{ code: "MissingInput", severity: "error", message: "Input is empty." }],
      needsClarification: true,
      missingFields: ["input"],
      ambiguousFields: [],
      questionsToAsk: []
    };
  }

  const questionsToAsk: string[] = [];
  const missingFields: string[] = [];

  // ── 意图识别 ──
  const hasArea = includesAny(normalized, ["范围", "周围", "群体", "aoe", "area", "半径"]);
  const hasDamage = includesAny(normalized, ["伤害", "damage", "攻击", "打击"]);
  const hasHeal = includesAny(normalized, ["治疗", "治愈", "heal", "回复", "恢复"]);
  const hasBuff = includesAny(normalized, ["buff", "增益", "强化", "冰冻", "冻结", "标记", "着火", "灼烧", "减速", "护盾"]);
  const hasKnockback = includesAny(normalized, ["击退", "knockback", "推开", "吹飞"]);
  const hasDash = includesAny(normalized, ["冲锋", "突进", "charge", "dash", "跳跃"]);
  const hasSelf = includesAny(normalized, ["自身", "自己", "自我", "self"]);
  const hasAlly = includesAny(normalized, ["友军", "盟友", "ally"]);
  const hasRanged = includesAny(normalized, ["远程", "投射", "弹道", "射击", "ranged", "projectile"]);
  const isMagical = includesAny(normalized, ["魔法", "法术", "magical", "魔力"]);

  // ── 提取数值 ──
  const baseDamage = extractNumberAfterKeywords(normalized, ["伤害", "damage", "攻击力"], 5);
  const healAmount = extractNumberAfterKeywords(normalized, ["治疗", "heal", "恢复"], 5);
  const buffDuration = extractNumberAfterKeywords(normalized, ["持续", "duration", "回合"], 2);
  const areaRadius = extractNumberAfterKeywords(normalized, ["半径", "radius", "范围"], 2);
  const maxRange = extractNumberAfterKeywords(normalized, ["距离", "射程", "range"], 3);

  // ── 确定目标类型 ──
  let targetType: SkillDesignAnswers["targetType"] = "single_enemy";
  if (hasArea) targetType = "area";
  else if (hasSelf) targetType = "self";
  else if (hasAlly) targetType = "ally";

  // ── 确定效果列表 ──
  const effects: SkillDesignAnswers["effects"] = [];
  if (hasDamage) effects.push("damage");
  if (hasHeal) effects.push("heal");
  if (hasBuff) effects.push("buff");
  if (hasKnockback) effects.push("knockback");
  if (hasDash) effects.push("dash");

  if (effects.length === 0) {
    questionsToAsk.push("这个技能的效果是什么？(造成伤害 / 治疗 / 施加状态 / 击退 / 位移)");
    missingFields.push("effects");
  }

  // ── 提取 Buff 名称 ──
  let buffName: string | undefined;
  if (hasBuff) {
    if (includesAny(normalized, ["冰冻", "冻结", "frozen"])) buffName = "Frozen";
    else if (includesAny(normalized, ["标记", "mark"])) buffName = "Marked";
    else if (includesAny(normalized, ["着火", "灼烧", "ignite"])) buffName = "Ignite";
    else if (includesAny(normalized, ["反击", "counter"])) buffName = "Counter";
    else if (includesAny(normalized, ["减速", "slow"])) buffName = "Slowed";
    else if (includesAny(normalized, ["护盾", "shield"])) buffName = "Shielded";
    else buffName = "Buff";
  }

  // ── 生成 SkillGraphSpec ──
  const displayName = text.split(/[,，。]/)[0].trim().replace(/\s+/g, "") || "NewSkill";
  const answers: SkillDesignAnswers = {
    displayName,
    description: text,
    targetType,
    effects,
    damageType: isMagical ? "Magical" : "Physical",
    baseDamage: hasDamage ? baseDamage : undefined,
    healAmount: hasHeal ? healAmount : undefined,
    buffName,
    buffDuration: hasBuff ? buffDuration : undefined,
    buffIsUnique: true,
    knockbackDistance: hasKnockback ? 1 : undefined,
    isRanged: hasRanged,
    canCrit: false,
    maxRange,
    areaRadius: hasArea ? areaRadius : undefined,
    dashMaxRange: hasDash ? maxRange : undefined,
    dashCollisionDamage: hasDash ? 1 : undefined
  };

  const spec = buildSkillGraphSpec(answers);

  return {
    spec,
    diagnostics: [],
    needsClarification: questionsToAsk.length > 0,
    missingFields,
    ambiguousFields: [],
    questionsToAsk
  };
}

export function generateSkillGraphSpecFromAnswers(answers: SkillDesignAnswers): SkillGraphSpecOutput {
  return buildSkillGraphSpec(answers);
}

function buildSkillGraphSpec(a: SkillDesignAnswers): SkillGraphSpecOutput {
  const nodes: SkillGraphSpecNode[] = [];
  const edges: SkillGraphSpecEdge[] = [];
  let prevId = "start";

  nodes.push({ Id: "start", Type: "Start" });

  // ── 目标选择 ──
  if (a.targetType === "self") {
    nodes.push({ Id: "select", Type: "SelectSelf" });
  } else if (a.targetType === "ally") {
    nodes.push({ Id: "select", Type: "SelectAlly", Parameters: { maxRange: a.maxRange ?? 2 } });
  } else if (a.targetType === "area") {
    nodes.push({ Id: "point", Type: "SelectTargetPoint", Parameters: { maxRange: a.maxRange ?? 3 } });
    edges.push({ Source: prevId, Target: "point" });
    prevId = "point";
    nodes.push({ Id: "collect", Type: "CollectTargetsInArea", Parameters: { radius: a.areaRadius ?? 2 } });
    edges.push({ Source: prevId, Target: "collect" });
    prevId = "collect";
    nodes.push({ Id: "loop", Type: "ForEachTarget" });
    edges.push({ Source: prevId, Target: "loop" });
    prevId = "loop";
  } else {
    // single_enemy
    if (a.isRanged) {
      nodes.push({ Id: "select", Type: "SelectPrimaryTarget", Parameters: { minRange: 2, maxRange: a.maxRange ?? 3 } });
    } else {
      nodes.push({ Id: "select", Type: "SelectPrimaryTarget", Parameters: { minRange: 1, maxRange: a.maxRange ?? 1 } });
    }
  }

  if (a.targetType !== "area") {
    edges.push({ Source: prevId, Target: "select" });
    prevId = "select";
  }

  // ── 位移效果 ──
  if (a.effects.includes("dash")) {
    nodes.push({ Id: "dash", Type: "DashToTarget", Parameters: { maxRange: a.dashMaxRange ?? 3, collisionDamage: a.dashCollisionDamage ?? 1 } });
    edges.push({ Source: prevId, Target: "dash" });
    prevId = "dash";
  }

  // ── 弹道 ──
  if (a.isRanged && a.targetType === "single_enemy") {
    nodes.push({ Id: "projectile", Type: "ProjectileLaunch", Parameters: { travelTime: 0.3, speed: 10 } });
    edges.push({ Source: prevId, Target: "projectile" });
    prevId = "projectile";
    nodes.push({ Id: "on_hit", Type: "OnHit" });
    edges.push({ Source: prevId, Target: "on_hit" });
    prevId = "on_hit";
  }

  // ── 效果节点 ──
  if (a.effects.includes("damage")) {
    const damageId = "damage";
    nodes.push({
      Id: damageId,
      Type: "ApplyDamage",
      Parameters: {
        baseDamage: a.baseDamage ?? 5,
        damageType: a.damageType === "Magical" ? 1 : 0,
        isRanged: a.isRanged ?? false,
        canCrit: a.canCrit ?? false
      }
    });
    edges.push({ Source: prevId, Target: damageId });
    prevId = damageId;
  }

  if (a.effects.includes("heal")) {
    const healId = "heal";
    nodes.push({ Id: healId, Type: "ApplyHeal", Parameters: { healAmount: a.healAmount ?? 5 } });
    edges.push({ Source: prevId, Target: healId });
    prevId = healId;
  }

  if (a.effects.includes("knockback")) {
    const kbId = "knockback";
    nodes.push({ Id: kbId, Type: "ApplyKnockback", Parameters: { distance: a.knockbackDistance ?? 1 } });
    edges.push({ Source: prevId, Target: kbId });
    prevId = kbId;
  }

  if (a.effects.includes("buff") && a.buffName) {
    const buffId = "buff";
    nodes.push({
      Id: buffId,
      Type: "ApplyBuff",
      Parameters: { buffName: a.buffName, duration: a.buffDuration ?? 2, isUnique: a.buffIsUnique ?? true }
    });
    edges.push({ Source: prevId, Target: buffId });
    prevId = buffId;
  }

  // ── 终止 ──
  nodes.push({ Id: "finish", Type: "Finish" });

  // ── 循环闭合 ──
  if (a.targetType === "area") {
    edges.push({ Source: prevId, Target: "loop" });
    edges.push({ Source: "loop", Target: "finish", Port: "OnComplete" });
  } else {
    edges.push({ Source: prevId, Target: "finish" });
  }

  return {
    DisplayName: a.displayName,
    Description: a.description ?? "",
    Nodes: nodes,
    Edges: edges
  };
}

// ═══════════════════════════════════════════
//  SkillGraphSpec → gameplay-test.md 转换
// ═══════════════════════════════════════════

export function generateGameplayTestFromSpec(spec: SkillGraphSpecOutput): ScenarioSpec {
  const analysis = analyzeSpec(spec);
  const setup = buildSetup(spec, analysis);
  const actions = buildActions(spec, analysis);
  const assertions = buildAssertions(spec, analysis);

  return {
    feature: "SkillGraph",
    scenario: `${spec.DisplayName.replace(/\s+/g, "")}Skill`,
    tags: buildTags(analysis),
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup,
    actions,
    assertions
  };
}

interface SpecAnalysis {
  graphKind: string;
  targetType: "single_enemy" | "self" | "ally" | "area";
  hasProjectile: boolean;
  hasDash: boolean;
  hasBuff: boolean;
  hasHeal: boolean;
  hasDamage: boolean;
  hasKnockback: boolean;
  damageType: number;
  baseDamage: number;
  healAmount: number;
  buffName: string;
  buffDuration: number;
  maxRange: number;
  areaRadius: number;
}

function analyzeSpec(spec: SkillGraphSpecOutput): SpecAnalysis {
  const types = new Set(spec.Nodes.map(n => n.Type));

  const hasProjectile = types.has("ProjectileLaunch");
  const hasDash = types.has("DashToTarget");
  const hasBuff = types.has("ApplyBuff");
  const hasHeal = types.has("ApplyHeal");
  const hasDamage = types.has("ApplyDamage");
  const hasKnockback = types.has("ApplyKnockback");
  const isArea = types.has("CollectTargetsInArea");
  const isSelf = types.has("SelectSelf");
  const isAlly = types.has("SelectAlly");

  let targetType: SpecAnalysis["targetType"] = "single_enemy";
  if (isArea) targetType = "area";
  else if (isSelf) targetType = "self";
  else if (isAlly) targetType = "ally";

  let graphKind = "singleTargetDamage";
  if (hasProjectile) graphKind = "projectile";
  else if (isArea) graphKind = "areaDamage";
  else if (isSelf && hasHeal) graphKind = "selfHeal";
  else if (isAlly && hasHeal) graphKind = "allyHeal";
  else if (hasDash) graphKind = "charge";
  else if (hasKnockback) graphKind = "knockback";
  else if (hasBuff && !hasDamage) graphKind = "applyBuff";

  const damageNode = spec.Nodes.find(n => n.Type === "ApplyDamage");
  const healNode = spec.Nodes.find(n => n.Type === "ApplyHeal");
  const buffNode = spec.Nodes.find(n => n.Type === "ApplyBuff");
  const selectNode = spec.Nodes.find(n => n.Type === "SelectPrimaryTarget");
  const collectNode = spec.Nodes.find(n => n.Type === "CollectTargetsInArea");

  return {
    graphKind,
    targetType,
    hasProjectile,
    hasDash,
    hasBuff,
    hasHeal,
    hasDamage,
    hasKnockback,
    damageType: (damageNode?.Parameters?.damageType as number) ?? 0,
    baseDamage: (damageNode?.Parameters?.baseDamage as number) ?? 5,
    healAmount: (healNode?.Parameters?.healAmount as number) ?? 5,
    buffName: (buffNode?.Parameters?.buffName as string) ?? "Buff",
    buffDuration: (buffNode?.Parameters?.duration as number) ?? 2,
    maxRange: (selectNode?.Parameters?.maxRange as number) ?? 3,
    areaRadius: (collectNode?.Parameters?.radius as number) ?? 2
  };
}

function buildSetup(spec: SkillGraphSpecOutput, analysis: SpecAnalysis): ScenarioSpec["setup"] {
  const setup: ScenarioSpec["setup"] = [
    { kind: "createSkillTestWorld", parameters: {} }
  ];

  const graphParams: Record<string, unknown> = {
    alias: "graph",
    graphKind: analysis.graphKind
  };

  if (analysis.hasDamage) graphParams.baseDamage = analysis.baseDamage;
  if (analysis.graphKind === "areaDamage") {
    graphParams.radius = analysis.areaRadius;
    graphParams.maxRange = analysis.maxRange;
  }
  if (analysis.graphKind === "charge") {
    graphParams.maxRange = analysis.maxRange;
  }
  if (analysis.graphKind === "selfHeal" || analysis.graphKind === "allyHeal") {
    graphParams.healAmount = analysis.healAmount;
  }
  if (analysis.graphKind === "applyBuff") {
    graphParams.buffName = analysis.buffName;
    graphParams.duration = analysis.buffDuration;
    graphParams.selectionKind = analysis.targetType === "self" ? "self" : "enemy";
  }

  setup.push({ kind: "createSkillGraph", parameters: graphParams });

  if (analysis.targetType === "area") {
    setup.push({ kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } });
    setup.push({ kind: "createCell", parameters: { alias: "targetPointCell", x: 1, y: 1 } });
    setup.push({ kind: "createCell", parameters: { alias: "targetCellA", x: 1, y: 0 } });
    setup.push({ kind: "createCell", parameters: { alias: "targetCellB", x: 0, y: 1 } });
    setup.push({ kind: "createCell", parameters: { alias: "safeCell", x: 4, y: 4 } });
    setup.push({ kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, cellAlias: "casterCell" } });
    setup.push({ kind: "createUnit", parameters: { alias: "targetA", playerNumber: 1, health: 10, maxHealth: 10, defenceFactor: 0, cellAlias: "targetCellA" } });
    setup.push({ kind: "createUnit", parameters: { alias: "targetB", playerNumber: 1, health: 10, maxHealth: 10, defenceFactor: 0, cellAlias: "targetCellB" } });
    setup.push({ kind: "createUnit", parameters: { alias: "safeTarget", playerNumber: 1, health: 10, maxHealth: 10, defenceFactor: 0, cellAlias: "safeCell" } });
  } else if (analysis.targetType === "self") {
    setup.push({ kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 6, maxHealth: 10 } });
  } else if (analysis.targetType === "ally") {
    setup.push({ kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } });
    setup.push({ kind: "createCell", parameters: { alias: "allyCell", x: 1, y: 0 } });
    setup.push({ kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, cellAlias: "casterCell" } });
    setup.push({ kind: "createUnit", parameters: { alias: "ally", playerNumber: 0, health: 6, maxHealth: 10, cellAlias: "allyCell" } });
  } else {
    setup.push({ kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } });
    setup.push({ kind: "createCell", parameters: { alias: "targetCell", x: 1, y: 0 } });
    setup.push({ kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10, cellAlias: "casterCell" } });
    setup.push({ kind: "createUnit", parameters: { alias: "target", playerNumber: 1, health: 10, maxHealth: 10, defenceFactor: 0, cellAlias: "targetCell" } });
  }

  setup.push({ kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } });

  return setup;
}

function buildActions(spec: SkillGraphSpecOutput, analysis: SpecAnalysis): ScenarioSpec["actions"] {
  const action: Record<string, unknown> = {
    graphAlias: "graph",
    casterAlias: "caster"
  };

  if (analysis.targetType === "single_enemy") {
    action.primaryTargetAlias = "target";
  } else if (analysis.targetType === "area") {
    action.targetPointAlias = "targetPointCell";
  }

  return [{ kind: "executeSkillGraph", parameters: action }];
}

function buildAssertions(spec: SkillGraphSpecOutput, analysis: SpecAnalysis): ScenarioSpec["assertions"] {
  const assertions: ScenarioSpec["assertions"] = [
    { kind: "executionStateEquals", expected: "Completed", parameters: {} }
  ];

  if (analysis.hasDamage) {
    if (analysis.targetType === "area") {
      assertions.push({ kind: "unitHealthEquals", target: "targetA", expected: 10 - analysis.baseDamage, parameters: {} });
      assertions.push({ kind: "unitHealthEquals", target: "targetB", expected: 10 - analysis.baseDamage, parameters: {} });
      assertions.push({ kind: "unitHealthEquals", target: "safeTarget", expected: 10, parameters: {} });
    } else if (analysis.targetType === "single_enemy") {
      assertions.push({ kind: "unitHealthEquals", target: "target", expected: 10 - analysis.baseDamage, parameters: {} });
    }
  }

  if (analysis.hasHeal) {
    if (analysis.targetType === "self") {
      assertions.push({ kind: "unitHealthEquals", target: "caster", expected: 10, parameters: {} });
    } else if (analysis.targetType === "ally") {
      assertions.push({ kind: "unitHealthEquals", target: "ally", expected: 10, parameters: {} });
    }
  }

  if (analysis.hasBuff) {
    if (analysis.targetType === "area") {
      assertions.push({ kind: "unitHasBuff", target: "targetA", expected: analysis.buffName, parameters: {} });
      assertions.push({ kind: "unitBuffDurationEquals", target: "targetA", expected: analysis.buffDuration, parameters: { buffName: analysis.buffName } });
    } else {
      const buffTarget = analysis.targetType === "self" ? "caster" : "target";
      assertions.push({ kind: "unitHasBuff", target: buffTarget, expected: analysis.buffName, parameters: {} });
      assertions.push({ kind: "unitBuffDurationEquals", target: buffTarget, expected: analysis.buffDuration, parameters: { buffName: analysis.buffName } });
    }
  }

  if (analysis.hasProjectile) {
    assertions.push({ kind: "projectileLaunched", target: "target", parameters: {} });
    assertions.push({ kind: "projectileHitTarget", target: "target", parameters: {} });
    assertions.push({ kind: "projectileCompleted", target: "target", parameters: {} });
  }

  return assertions;
}

function buildTags(analysis: SpecAnalysis): string[] {
  const tags = ["mvp", "skill"];
  if (analysis.hasDamage) tags.push("damage");
  if (analysis.targetType === "area") tags.push("aoe");
  if (analysis.hasHeal) tags.push("heal");
  if (analysis.hasBuff) tags.push("buff");
  if (analysis.hasDash) tags.push("movement");
  if (analysis.hasKnockback) tags.push("knockback");
  if (analysis.hasProjectile) tags.push("projectile");
  return tags;
}
