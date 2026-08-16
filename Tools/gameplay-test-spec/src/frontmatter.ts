import yaml from "js-yaml";
import type { ScenarioSpec } from "./schema.js";

export interface GameplayTestDocument {
  frontmatter: unknown;
  body: string;
}

export function parseGameplayTestDocument(markdown: string): GameplayTestDocument {
  const match = markdown.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n?([\s\S]*)$/);
  if (!match) {
    throw new Error("Missing YAML frontmatter block.");
  }

  return {
    frontmatter: yaml.load(match[1]),
    body: match[2] ?? ""
  };
}

export function formatGameplayTestDocument(spec: ScenarioSpec, body?: string): string {
  const yamlText = yaml.dump(spec, {
    lineWidth: 120,
    noRefs: true,
    sortKeys: false
  });

  const content = body?.trim()
    ? `${body.trim()}\n`
    : `# ${spec.feature} - ${spec.scenario}\n\nGenerated gameplay test spec.\n`;

  return `---\n${yamlText}---\n\n${content}`;
}
