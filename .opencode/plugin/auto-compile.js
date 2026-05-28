import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const pluginDir = dirname(fileURLToPath(import.meta.url));
const sharedRulePath = resolve(pluginDir, "../../.agents/shared-rules/unity-auto-compile.md");

async function loadCompileRule() {
  return readFile(sharedRulePath, "utf8");
}

export const AutoCompilePlugin = async () => {
  const compileRule = await loadCompileRule();

  return {
    'experimental.chat.system.transform': async (_input, output) => {
      if (!output.system) {
        output.system = [];
      }
      if (!output.system.some(s => s.includes('Unity Auto Compile Guard'))) {
        output.system.push(compileRule);
      }
    },

    'tool.execute.after': async (_input, _output) => {
      // No-op: OpenCode enforcement stays prompt-based; the shared rule source lives in .agents/shared-rules.
    }
  };
};
