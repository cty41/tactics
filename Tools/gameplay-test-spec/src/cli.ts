#!/usr/bin/env node
import { Command } from "commander";
import { readFile, writeFile, readdir, stat } from "node:fs/promises";
import { join, basename } from "node:path";
import { compileScenarioSpec, compileScenarioDraft } from "./compiler.js";
import { formatGameplayTestDocument, parseGameplayTestDocument } from "./frontmatter.js";
import { generateScenarioSpec, generateSkillGraphSpec, generateSkillGraphSpecFromAnswers, generateGameplayTestFromSpec, type SkillDesignAnswers } from "./generator.js";
import { validateScenarioSpec, validateScenarioDraft } from "./validator.js";
import { compileAuthoringSpec } from "./authoring/compiler.js";

const program = new Command();

program.command("validate-authoring-spec")
  .description("Validate an AuthoringAssetSpecV1 JSON document without writing resources")
  .requiredOption("-s, --spec <path>", "input authoring spec JSON")
  .action(async options => {
    const result = compileAuthoringSpec(JSON.parse(await readFile(options.spec, "utf8")));
    printJson({ valid: result.valid, diagnostics: result.diagnostics });
    if (!result.valid) process.exitCode = 1;
  });

program.command("compile-authoring-spec")
  .description("Compile AuthoringAssetSpecV1 JSON to MCP batch arguments")
  .requiredOption("-s, --spec <path>", "input authoring spec JSON")
  .requiredOption("-o, --out <path>", "output compiled authoring batch JSON")
  .action(async options => {
    const result = compileAuthoringSpec(JSON.parse(await readFile(options.spec, "utf8")));
    if (!result.valid || !result.batch) { printJson(result); process.exitCode = 1; return; }
    await writeFile(options.out, `${JSON.stringify(result.batch, null, 2)}\n`, "utf8");
    printJson({ ok: true, out: options.out, diagnostics: result.diagnostics });
  });

program
  .name("gameplay-test-spec")
  .description("Generate, validate, and compile gameplay test specs.")
  .version("0.1.0");

program
  .command("generate-spec")
  .description("Generate spec from natural language (legacy helper)")
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
  .description("Validate a ScenarioSpec from *.gameplay-test.md")
  .requiredOption("-s, --spec <path>", "input *.gameplay-test.md path")
  .action(async options => {
    const markdown = await readFile(options.spec, "utf8");
    const doc = parseGameplayTestDocument(markdown);
    const result = validateScenarioSpec(doc.frontmatter);
    printJson(result);
    process.exitCode = result.valid ? 0 : 1;
  });

program
  .command("validate-draft")
  .description("Validate a ScenarioDraft JSON file")
  .requiredOption("-d, --draft <path>", "input *.json draft path")
  .action(async options => {
    const content = await readFile(options.draft, "utf8");
    const draft = JSON.parse(content);
    const result = validateScenarioDraft(draft);
    printJson(result);
    process.exitCode = result.valid ? 0 : 1;
  });

program
  .command("compile-spec")
  .description("Compile ScenarioSpec to ExecutableScenarioPlan")
  .requiredOption("-s, --spec <path>", "input *.gameplay-test.md path")
  .requiredOption("-o, --out <path>", "output *.plan.json path")
  .option("--runtime <runtime>", "runtime target: unity or godot", "unity")
  .action(async options => {
    const markdown = await readFile(options.spec, "utf8");
    const doc = parseGameplayTestDocument(markdown);
    const runtime = parseRuntime(options.runtime);
    const result = compileScenarioSpec(doc.frontmatter, { runtime });
    if (!result.valid || !result.plan) {
      printJson(result);
      process.exitCode = 1;
      return;
    }

    await writeFile(options.out, `${JSON.stringify(result.plan, null, 2)}\n`, "utf8");
    printJson({ ok: true, out: options.out, diagnostics: result.diagnostics });
  });

program
  .command("compile-draft")
  .description("Compile ScenarioDraft JSON to ExecutableScenarioPlan")
  .requiredOption("-d, --draft <path>", "input *.json draft path")
  .requiredOption("-o, --out <path>", "output *.plan.json path")
  .option("--runtime <runtime>", "runtime target: unity or godot", "unity")
  .action(async options => {
    const content = await readFile(options.draft, "utf8");
    const draft = JSON.parse(content);
    const result = compileScenarioDraft(draft, { runtime: parseRuntime(options.runtime) });
    if (!result.valid || !result.plan) {
      printJson(result);
      process.exitCode = 1;
      return;
    }

    await writeFile(options.out, `${JSON.stringify(result.plan, null, 2)}\n`, "utf8");
    printJson({ ok: true, out: options.out, diagnostics: result.diagnostics });
  });

program
  .command("generate-skill-graph-spec")
  .description("Generate SkillGraphSpec JSON from natural language description")
  .requiredOption("-t, --text <text>", "natural language skill description")
  .option("-o, --out <path>", "output JSON path (default: stdout)")
  .action(async options => {
    const result = generateSkillGraphSpec(options.text);
    if (options.out && result.spec) {
      await writeFile(options.out, `${JSON.stringify(result.spec, null, 2)}\n`, "utf8");
      printJson({ ok: true, out: options.out, needsClarification: result.needsClarification, questionsToAsk: result.questionsToAsk });
    } else {
      printJson(result);
    }
    process.exitCode = result.needsClarification ? 1 : 0;
  });

program
  .command("generate-skill-graph-spec-answers")
  .description("Generate SkillGraphSpec JSON from structured answers JSON")
  .requiredOption("-a, --answers <path>", "input answers JSON path")
  .option("-o, --out <path>", "output JSON path (default: stdout)")
  .action(async options => {
    const content = await readFile(options.answers, "utf8");
    const answers: SkillDesignAnswers = JSON.parse(content);
    const spec = generateSkillGraphSpecFromAnswers(answers);
    if (options.out) {
      await writeFile(options.out, `${JSON.stringify(spec, null, 2)}\n`, "utf8");
      printJson({ ok: true, out: options.out });
    } else {
      printJson(spec);
    }
  });

program
  .command("generate-test-from-spec")
  .description("Generate gameplay-test.md from SkillGraphSpec JSON")
  .requiredOption("-s, --spec <path>", "input SkillGraphSpec JSON path")
  .requiredOption("-o, --out <path>", "output *.gameplay-test.md path")
  .action(async options => {
    const content = await readFile(options.spec, "utf8");
    const spec = JSON.parse(content);
    const scenarioSpec = generateGameplayTestFromSpec(spec);
    const validation = validateScenarioSpec(scenarioSpec);
    if (!validation.valid) {
      printJson({ ok: false, diagnostics: validation.diagnostics });
      process.exitCode = 1;
      return;
    }
    const markdown = formatGameplayTestDocument(scenarioSpec);
    await writeFile(options.out, markdown, "utf8");
    printJson({ ok: true, out: options.out, scenario: scenarioSpec.scenario, graphKind: scenarioSpec.setup.find((s: any) => s.kind === "createSkillGraph")?.parameters?.graphKind });
  });

// Batch commands
program
  .command("batch-compile")
  .description("Batch compile all *.gameplay-test.md files in a directory")
  .requiredOption("-d, --dir <path>", "input directory containing *.gameplay-test.md files")
  .option("-o, --out <path>", "output directory for *.plan.json files (default: same as input)")
  .option("--filter-adapter <adapter>", "filter by adapter (e.g., Battle, Skill)")
  .option("--filter-tag <tag>", "filter by tag")
  .option("--filter-feature <feature>", "filter by feature")
  .option("--filter-scenario <scenario>", "filter by scenario name")
  .option("--runtime <runtime>", "runtime target: unity or godot", "unity")
  .action(async options => {
    const inputDir = options.dir;
    const outDir = options.out || inputDir;
    const runtime = parseRuntime(options.runtime);
    const files = await findGameplayTestFiles(inputDir);
    
    const summary = {
      total: 0,
      compiled: 0,
      failed: 0,
      skipped: 0,
      failures: [] as Array<{ file: string; diagnostics: any[] }>
    };

    for (const file of files) {
      summary.total++;
      try {
        const markdown = await readFile(file, "utf8");
        const doc = parseGameplayTestDocument(markdown);
        const spec = doc.frontmatter as any;

        // Apply filters
        if (options.filterAdapter && !spec.requiredAdapters?.includes(options.filterAdapter)) {
          summary.skipped++;
          continue;
        }
        if (options.filterTag && !spec.tags?.includes(options.filterTag)) {
          summary.skipped++;
          continue;
        }
        if (options.filterFeature && spec.feature !== options.filterFeature) {
          summary.skipped++;
          continue;
        }
        if (options.filterScenario && spec.scenario !== options.filterScenario) {
          summary.skipped++;
          continue;
        }

        const result = compileScenarioSpec(spec, { runtime });
        if (result.valid && result.plan) {
          const outPath = join(outDir, basename(file, ".gameplay-test.md") + ".plan.json");
          await writeFile(outPath, `${JSON.stringify(result.plan, null, 2)}\n`, "utf8");
          summary.compiled++;
        } else {
          summary.failed++;
          summary.failures.push({
            file: basename(file),
            diagnostics: result.diagnostics
          });
        }
      } catch (error) {
        summary.failed++;
        summary.failures.push({
          file: basename(file),
          diagnostics: [{ code: "FileError", severity: "error", message: String(error) }]
        });
      }
    }

    printJson(summary);
    process.exitCode = summary.failed > 0 ? 1 : 0;
  });

program
  .command("batch-validate")
  .description("Batch validate all *.gameplay-test.md files in a directory")
  .requiredOption("-d, --dir <path>", "input directory containing *.gameplay-test.md files")
  .option("--filter-adapter <adapter>", "filter by adapter (e.g., Battle, Skill)")
  .option("--filter-tag <tag>", "filter by tag")
  .option("--filter-feature <feature>", "filter by feature")
  .option("--filter-scenario <scenario>", "filter by scenario name")
  .action(async options => {
    const inputDir = options.dir;
    const files = await findGameplayTestFiles(inputDir);
    
    const summary = {
      total: 0,
      valid: 0,
      invalid: 0,
      skipped: 0,
      failures: [] as Array<{ file: string; diagnostics: any[] }>
    };

    for (const file of files) {
      summary.total++;
      try {
        const markdown = await readFile(file, "utf8");
        const doc = parseGameplayTestDocument(markdown);
        const spec = doc.frontmatter as any;

        // Apply filters
        if (options.filterAdapter && !spec.requiredAdapters?.includes(options.filterAdapter)) {
          summary.skipped++;
          continue;
        }
        if (options.filterTag && !spec.tags?.includes(options.filterTag)) {
          summary.skipped++;
          continue;
        }
        if (options.filterFeature && spec.feature !== options.filterFeature) {
          summary.skipped++;
          continue;
        }
        if (options.filterScenario && spec.scenario !== options.filterScenario) {
          summary.skipped++;
          continue;
        }

        const result = validateScenarioSpec(spec);
        if (result.valid) {
          summary.valid++;
        } else {
          summary.invalid++;
          summary.failures.push({
            file: basename(file),
            diagnostics: result.diagnostics
          });
        }
      } catch (error) {
        summary.invalid++;
        summary.failures.push({
          file: basename(file),
          diagnostics: [{ code: "FileError", severity: "error", message: String(error) }]
        });
      }
    }

    printJson(summary);
    process.exitCode = summary.invalid > 0 ? 1 : 0;
  });

async function findGameplayTestFiles(dir: string): Promise<string[]> {
  const files: string[] = [];
  const entries = await readdir(dir, { withFileTypes: true });
  
  for (const entry of entries) {
    const fullPath = join(dir, entry.name);
    if (entry.isDirectory()) {
      const subFiles = await findGameplayTestFiles(fullPath);
      files.push(...subFiles);
    } else if (entry.name.endsWith(".gameplay-test.md")) {
      files.push(fullPath);
    }
  }
  
  return files;
}

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

function parseRuntime(value: string): "Unity" | "Godot" {
  const normalized = value.toLowerCase();
  if (normalized === "unity") return "Unity";
  if (normalized === "godot") return "Godot";
  throw new Error(`Unknown runtime '${value}'. Expected unity or godot.`);
}
