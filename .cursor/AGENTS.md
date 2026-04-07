# Tactics Project - Agent Guide

## Overview

This is an agent-first Unity project where human engineers steer and agents execute. The codebase is maintained by AI agents with human oversight.

**Core Principle: No manual code contribution. Agents write all code.**

## Architecture

- **Game Engine**: Unity 6.2 (C# 12)
- **Pattern**: Traditional MonoBehaviour
- **Asset Pipeline**: AssetBundle-based with `GameAssetManager`

## Knowledge Base

```
.cursor/
├── AGENTS.md              # This file (map)
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

All rules in `.cursor/rules/` are **always applied** to all agent tasks:

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

For detailed information, navigate to:
- Architecture: See `ARCHITECTURE.md`
- Asset Pipeline: See `docs/references/GameAssetPipeline.md`
- Code Style: See `rules/unity-core.md`

## Agent Limitations

Agents cannot access:
- Google Docs or external wikis
- Slack/chat history
- Knowledge in human heads only

**If it's not in the codebase, it doesn't exist for agents.**

Keep documentation in `.cursor/docs/` so agents can find it.
