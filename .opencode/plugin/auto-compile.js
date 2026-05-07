/**
 * Auto-compile plugin for OpenCode
 * 
 * Injects compilation rules into system prompt to ensure Agent calls
 * refresh_unity after editing C# scripts.
 */

export const AutoCompilePlugin = async () => {
  const COMPILE_RULE = `
## Unity Compilation Rule (CRITICAL)

After editing, creating, or deleting C# scripts (.cs files), you MUST call refresh_unity with compile="request" to trigger Unity compilation.

Unity compilation is NOT automatic. Every C# file change requires an explicit refresh_unity call.
This applies to: Edit, Write, apply_text_edits, script_apply_edits, create_script, delete_script.

### Build Mode End Check
Before concluding a build mode session, if ANY .cs file was modified during this session, 
you MUST call refresh_unity as the final step. Do not end the session without compiling.
`;

  return {
    'experimental.chat.system.transform': async (_input, output) => {
      if (!output.system) {
        output.system = [];
      }
      if (!output.system.some(s => s.includes('Unity Compilation Rule'))) {
        output.system.push(COMPILE_RULE);
      }
    },

    'tool.execute.after': async (input, output) => {
      // No-op: rules are enforced via system prompt injection above
    }
  };
};
