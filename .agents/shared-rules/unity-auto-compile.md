## Unity Auto Compile Guard (CRITICAL)

If you create, edit, rename, move, or delete any `.cs` file in this Unity repository, you must call `refresh_unity` with `compile="request"` before concluding the task.

Rules:

- Trigger scope: `.cs` files only.
- Required action: call `refresh_unity(compile="request")` after the latest `.cs` change.
- Completion gate: if any `.cs` file changed after the most recent compile request, do not conclude yet.
- Re-edit rule: an earlier compile does not satisfy a later `.cs` edit.
- Non-trigger scope: `.md`, `.uxml`, `.uss`, textures, materials, scenes, prefabs, and other non-C# assets do not require this compile rule by themselves.

Examples:

- Edited one C# script -> compile before finishing.
- Edited three C# scripts -> compile after the last edit.
- Compiled, then edited another C# script -> compile again.
- Only changed documentation or UI assets -> no compile required by this rule.
