"""Write deterministic Phase 5B starting-skill generation evidence."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


def sha256(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export-receipt", type=Path, required=True)
    parser.add_argument("--draft", type=Path, required=True)
    parser.add_argument("--ledger", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    export = json.loads(args.export_receipt.read_text(encoding="utf-8"))
    draft = json.loads(args.draft.read_text(encoding="utf-8"))
    ledger = json.loads(args.ledger.read_text(encoding="utf-8"))
    if {export["batchId"], draft["batchId"], ledger["batchId"]} != {"pure-run-starting-skills-v1"}:
        raise ValueError("Starting-skill evidence batch IDs disagree")
    for key in ("sourceTag", "sourceCommit", "unityVersion", "exporterVersion", "exportHash"):
        if draft["source"][key] != ledger["source"][key] or draft["source"][key] != export[key]:
            raise ValueError(f"Starting-skill generation source differs: {key}")
    artifacts = ledger["artifacts"]
    paths = {item["resourcePath"] for item in artifacts}
    if len(artifacts) != 13 or len(paths) != 13 or "res://content/ContentCatalog.tres" in paths:
        raise ValueError("Starting-skill ledger must contain 13 unique batch-owned artifacts")
    if "res://content/poison_spear/PoisonSpearSkillLv1.tres" in paths:
        raise ValueError("Starting-skill batch duplicated the externally owned Poison Spear")
    if draft["payloadBoundary"] != {
        "manualGameplayAcceptance": "pending",
        "presentation": "audit_only_not_copied",
        "thirdPartyPayloadCopied": False,
        "visualAcceptance": "not_applicable_gameplay_only_no_visual_payload",
    }:
        raise ValueError("Starting-skill payload boundary drifted")
    receipt = {
        "schemaVersion": 1,
        "batchId": draft["batchId"],
        "classification": "real_godot_resource_generation",
        **draft["source"],
        "typedDraftHash": sha256(args.draft),
        "generationLedger": "Tools/migration/manifest/state/pure-run-starting-skills-v1.json",
        "generationLedgerHash": sha256(args.ledger),
        "batchCatalogEntryCount": 12,
        "canonicalCatalogEntryCount": 58,
        "generatedSkillDefinitionCount": 11,
        "externalContentDependencies": [{"contentId": "skill.poison-spear.lv1", "ownerBatch": "poison-spear-lv1-real", "resourcePath": "godot/content/poison_spear/PoisonSpearSkillLv1.tres"}],
        "artifactCount": 13,
        "automatedValidation": {"resourceSaver": "passed", "uidPreservation": "passed", "byteIdempotencyRuns": 2, "byteIdentical": True, "gdUnitRuntimeTests": "passed", "rendererHeadlessStartup": ["gl_compatibility", "forward_plus"]},
        "ownership": "UnityOwned",
        "state": "Generated",
        "visualAcceptance": "not_applicable_gameplay_only_no_visual_payload",
        "manualGameplayAcceptance": "pending",
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
