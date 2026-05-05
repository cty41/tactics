/**
 * Auto-compile plugin for OpenCode
 * 
 * Automatically injects Unity compilation rules into system prompt.
 * Removes the need for manual refresh_unity calls after C# script edits.
 */

export const AutoCompilePlugin = async () => {
  const COMPILE_RULE = `
## Auto-Compile Rule

When you edit, create, or delete C# scripts (.cs files), Unity Editor scripts, shaders, or any file that triggers Unity recompilation:

1. **Do NOT manually call refresh_unity** - compilation is handled automatically by the auto-compile system.
2. If you need to verify compilation succeeded, check the Unity console for errors.
3. Continue with your task after editing scripts - the compilation will happen in the background.

This rule applies to all file operations including: Edit, Write, apply_text_edits, script_apply_edits, create_script, and delete_script.
`;

  return {
    'experimental.chat.system.transform': async (_input, output) => {
      // Inject compile rule into system prompt
      if (!output.system) {
        output.system = [];
      }
      
      // Check if rule already exists to avoid duplication
      const hasRule = output.system.some(s => s.includes('Auto-Compile Rule'));
      if (!hasRule) {
        output.system.push(COMPILE_RULE);
      }
    }
  };
};
