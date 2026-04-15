# Tactics Project - Agent Guide

## Overview

This is an agent-first Unity project where human engineers steer and agents execute. The codebase is maintained by AI agents with human oversight.

**Core Principle: No manual code contribution. Agents write all code.**

## Knowledge Base

```
.kilo/
├── ARCHITECTURE.md        # System architecture
├── docs/
│   ├── references/        # Technical references
│   │   └── GameAssetPipeline.md
│   ├── design-docs/       # Design documents
│   ├── exec-plans/        # Execution plans
│   └── product-specs/     # Product specifications
└── rules/                 # Agent behavior constraints
```

## Key Rules

All rules in `.kilo/rules/` are **always applied** to all agent tasks:

| Rule | Purpose |
|------|---------|
| `unity-core.md` | C# naming, MonoBehaviour lifecycle, serialization |
| `unity-asset-loading.md` | GameAssetManager API, Load/Release pairing |
| `unity-mcp-operations.md` | MCP tool usage for asset inspection and modification |
| `unity-input.md` | Unity Input System |
| `unity-performance.md` | Performance optimization |
| `unity-testing.md` | Unit testing |

## Core Principles

1. **Asset Loading**: Always use `GameAssetManager`, never `Resources.Load`
2. **Load/Release Pairing**: Every Load must have corresponding Release
3. **Async First**: Prefer `LoadAsync` over sync loading
4. **Scene Paths**: Use project paths (`Assets/...`) not scene names

## Development Workflow

1. Human describes task and acceptance criteria
2. Agent implements, tests, and opens PR
3. Agent self-reviews via PR feedback loop
4. Human validates only when judgment required
5. Agent handles mundane merges autonomously

## Documentation
For detailed information, navigate to: `.kilo/ARCHITECTURE.md`


## Agent Constraints

### Language
- **Plan, code, debug mode output must be in Chinese** (中文), including: plan files, task descriptions, and communication during planning.
- Code comments and commit messages should follow the project's conventions.
- Exception: Code identifiers (variable names, class names, method names) always follow the project's .NET naming conventions (PascalCase, camelCase, etc.).

### Plan Mode Behavior
- **When using the plan tool, ONLY create/edit the plan file.** Do NOT implement code, create files, or make any changes outside of `.kilo/plans/`.
- Plan mode is READ-ONLY except for the plan file itself. Implementation begins ONLY after the user explicitly approves the plan.

### Plan File Naming
- **Plan files MUST use descriptive, content-based names in snake_case.**
- Examples: `combat-skills-expansion.md`, `inventory-system-refactor.md`, `ui-overhaul.md`
- **NEVER use auto-generated random names** (e.g., `cosmic-wolf.md`, `1775816560974-random-name.md`).

## Agent Limitations


**If it's not in the codebase, it doesn't exist for agents.**

Keep documentation in `.kilo/docs/` so agents can find it.
