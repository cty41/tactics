# Tactics Project - Agent Guide

## Overview

This is an agent-first Unity project where human engineers steer and agents execute. The codebase is maintained by AI agents with human oversight.

**Core Principle: No manual code contribution. Agents write all code.**

## Agent behavioral guidelines

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

### 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

### 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

### 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.


## Documentation map

Constraints and workflow are summarized in this file. **System architecture and design details** are in [`.agents/ARCHITECTURE.md`](.agents/ARCHITECTURE.md). Additional references (for example under [`.agents/docs/`](.agents/docs/)) supplement that document.

## Key Rules

### Using this repo with Cursor

This project includes a **Cursor project rule** so the Karpathy-inspired behavioral guidelines apply automatically when you work here.

#### In this repository

1. Open the folder in Cursor.
2. The rule [`.agents/rules/karpathy-guidelines.mdc`](.agents/rules/karpathy-guidelines.mdc) is committed with `alwaysApply: true`, so you do not need extra installation steps.
3. In Cursor, you can confirm it under **Settings → Rules** (or the project rules UI), where `karpathy-guidelines` should appear.

#### Use the same guidelines in another project

**Cursor (recommended):** Copy `.agents/rules/karpathy-guidelines.mdc` into that project’s `.cursor/rules/` directory (create the folders if needed). Adjust or merge with existing rules as you like.

**Other tools:** If a stack only supports a root instruction file, copy `.agents/rules/karpathy-guidelines.mdc` into that project's root instructions instead (or merge its contents into your existing instructions).


> **Note:** The following files under `.agents/rules/` constrain agent work on this Unity project. They are **not automatically injected** into the agent context; when working in the relevant area, the agent should actively read the corresponding file.

The following files under `.agents/rules/` constrain agent work on this Unity project (confirm names against the repo):

| Rule | Purpose |
|------|---------|
| `unity-core.md` | C# naming, MonoBehaviour lifecycle, serialization |
| `unity-asset-loading.md` | GameAssetManager API, Load/Release pairing |
| `unity-mcp-operations.md` | MCP tool usage for asset inspection and modification |
| `unity-input.md` | Unity Input System |
| `unity-performance.md` | Performance optimization |
| `unity-testing.md` | Unit testing |
| `code-organization.md` | Code organization conventions |

## Core Principles (summary)

Details live in [`.agents/ARCHITECTURE.md`](.agents/ARCHITECTURE.md) and the rule files above.

1. **Assets**: Use `GameAssetManager`, not `Resources.Load`; pair every Load with Release; prefer async loading.
2. **Paths**: Use project paths (`Assets/...`) instead of scene names where applicable.
3. **Inspectors**: Prefer Odin APIs for inspector-related code when appropriate.

## Agent Constraints

### Language

- **Plan, code, and debug output must be in Chinese (中文)**, including plan files, task descriptions, and communication during planning.
- Code comments and commit messages follow project conventions.
- Identifiers follow .NET naming (PascalCase, camelCase, etc.).

## Agent Limitations

**If it is not in the codebase, it does not exist for agents.**

Keep authoritative documentation under **`.agents/`**—especially [`.agents/ARCHITECTURE.md`](.agents/ARCHITECTURE.md)—so agents can find it.
