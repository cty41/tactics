import type { GenerationResult, ScenarioSpec } from "./schema.js";
import { validateScenarioSpec } from "./validator.js";

function includesAny(text: string, words: string[]): boolean {
  return words.some(word => text.includes(word.toLowerCase()));
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
