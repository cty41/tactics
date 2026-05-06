/**
 * Auto-compile plugin for OpenCode
 * 
 * Ensures Agent calls refresh_unity after editing C# scripts.
 * Injects compilation rules into system prompt and adds reminders in tool output.
 */

export const AutoCompilePlugin = async () => {
  const EDIT_TOOLS = [
    'Edit',
    'Write',
    'unity-MCP_apply_text_edits',
    'unity-MCP_script_apply_edits',
    'unity-MCP_create_script',
    'unity-MCP_delete_script'
  ];

  const COMPILE_RULE = `
## Unity Compilation Rule (CRITICAL)

After editing, creating, or deleting C# scripts (.cs files), you MUST call refresh_unity with compile="request" to trigger Unity compilation.

Unity compilation is NOT automatic. Every C# file change requires an explicit refresh_unity call.
This applies to: Edit, Write, apply_text_edits, script_apply_edits, create_script, delete_script.
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
      if (EDIT_TOOLS.includes(input.tool)) {
        const path = input.args?.path || input.args?.uri || input.args?.filePath || '';
        if (path.endsWith('.cs')) {
          output.output = (output.output || '') 
            + '\n\n=== REMINDER: C# file modified, call refresh_unity to compile ===';
        }
      }
    }
  };
};
