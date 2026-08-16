"""Index Unity-only project governance and legacy gameplay specifications for retirement."""

from __future__ import annotations

import hashlib
import json
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Tools/migration/manifest/retirement/unity-governance-retirement-v1.json"


PREFIXES = (
    ".agents/rules/unity-",
    ".agents/skills/unity-",
    ".agents/skills/game-asset-pipeline/",
    ".agents/skills/skill-graph-creation/",
    ".agents/skills/ui-development/",
    "Tools/unity-mcp/",
)
EXACT = {
    "Tools/apply_tbsf_guid_map.py",
    "Tools/regenerate_tbsf_fork_meta.py",
    "Tools/tbsf_guid_map.txt",
}
LEGACY_SPEC_TERMS = (
    "barbarian",
    "hunter",
    "charge-heal",
    "melee-heal",
    "battle-test-config",
)


def git_lines(*arguments: str) -> list[str]:
    output = subprocess.check_output(["git", *arguments], cwd=ROOT, text=True)
    return [line for line in output.splitlines() if line]


def classify(path: str) -> str | None:
    if path in EXACT or path.startswith(PREFIXES):
        return "unity_only_governance_or_tooling"
    lowered = path.lower()
    if path.startswith("Tests/gameplay-specs/") and any(term in lowered for term in LEGACY_SPEC_TERMS):
        return "retired_legacy_gameplay_spec"
    return None


def main() -> None:
    entries = []
    for path in git_lines("ls-files"):
        reason = classify(path)
        if reason is None:
            continue
        payload = (ROOT / path).read_bytes()
        entries.append(
            {
                "path": path,
                "retirementReason": reason,
                "gitBlobSha1": subprocess.check_output(
                    ["git", "hash-object", "--", path], cwd=ROOT, text=True
                ).strip(),
                "sha256": hashlib.sha256(payload).hexdigest(),
                "byteCount": len(payload),
            }
        )
    document = {
        "schemaVersion": 1,
        "manifestId": "unity-governance-retirement-v1",
        "entryCount": len(entries),
        "entries": entries,
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"UNITY_GOVERNANCE_RETIREMENT_OK entries={len(entries)}")


if __name__ == "__main__":
    main()
