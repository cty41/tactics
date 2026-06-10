#!/usr/bin/env node
import { Command } from "commander";
import { readFile, writeFile } from "node:fs/promises";
import { compileScenarioSpec } from "./compiler.js";
import { formatGameplayTestDocument, parseGameplayTestDocument } from "./frontmatter.js";
import { generateScenarioSpec } from "./generator.js";
import { validateScenarioSpec } from "./validator.js";

const program = new Command();

program
  .name("gameplay-test-spec")
  .description("Generate, validate, and compile gameplay test specs.")
  .version("0.1.0");

program
  .command("generate-spec")
  .requiredOption("-t, --text <text>", "natural language expectation text")
  .requiredOption("-o, --out <path>", "output *.gameplay-test.md path")
  .action(async options => {
    const result = generateScenarioSpec(options.text);
    if (!result.spec || result.needsClarification) {
      printJson(result);
      process.exitCode = 1;
      return;
    }

    await writeFile(options.out, formatGameplayTestDocument(result.spec), "utf8");
    printJson({ ok: true, out: options.out, diagnostics: result.diagnostics });
  });

program
  .command("validate-spec")
  .requiredOption("-s, --spec <path>", "input *.gameplay-test.md path")
  .action(async options => {
    const markdown = await readFile(options.spec, "utf8");
    const doc = parseGameplayTestDocument(markdown);
    const result = validateScenarioSpec(doc.frontmatter);
    printJson(result);
    process.exitCode = result.valid ? 0 : 1;
  });

program
  .command("compile-spec")
  .requiredOption("-s, --spec <path>", "input *.gameplay-test.md path")
  .requiredOption("-o, --out <path>", "output *.plan.json path")
  .action(async options => {
    const markdown = await readFile(options.spec, "utf8");
    const doc = parseGameplayTestDocument(markdown);
    const result = compileScenarioSpec(doc.frontmatter);
    if (!result.valid || !result.plan) {
      printJson(result);
      process.exitCode = 1;
      return;
    }

    await writeFile(options.out, `${JSON.stringify(result.plan, null, 2)}\n`, "utf8");
    printJson({ ok: true, out: options.out, diagnostics: result.diagnostics });
  });

program.parseAsync().catch(error => {
  printJson({
    ok: false,
    diagnostics: [{
      code: "UnhandledCliError",
      severity: "error",
      message: error instanceof Error ? error.message : String(error)
    }]
  });
  process.exitCode = 1;
});

function printJson(value: unknown): void {
  console.log(JSON.stringify(value, null, 2));
}
