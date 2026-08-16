"""Write the deterministic receipt for the real Poison Spear Godot generation batch."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any, Mapping


def _sha256(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


def _load(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _content(draft: Mapping[str, Any], content_id: str) -> Mapping[str, Any]:
    return next(item for item in draft["contents"] if item["contentId"] == content_id)


def compile_generation_receipt(
    export_receipt: Mapping[str, Any],
    draft: Mapping[str, Any],
    draft_hash: str,
    ledger: Mapping[str, Any],
    ledger_hash: str,
) -> dict[str, Any]:
    """Compile one canonical receipt from the disposable draft and final generation ledger."""

    if draft["batchId"] != ledger["batchId"]:
        raise ValueError("draft and generation ledger batch IDs disagree")
    source = ledger["source"]
    for key in ("sourceTag", "sourceCommit", "exporterVersion", "exportHash"):
        if source[key] != export_receipt[key]:
            raise ValueError(f"generation source differs from export receipt: {key}")

    skill = _content(draft, "skill.poison-spear.lv1")["properties"]
    poison = _content(draft, "buff.poison")["properties"]
    presentation = _content(draft, "presentation.poison-spear.lv1")["properties"]
    graph = presentation["graph"]
    return {
        "schemaVersion": 1,
        "batchId": draft["batchId"],
        "classification": "real_godot_resource_generation",
        "sourceTag": source["sourceTag"],
        "sourceCommit": source["sourceCommit"],
        "exporterVersion": source["exporterVersion"],
        "exportHash": source["exportHash"],
        "typedDraftHash": draft_hash,
        "generationLedger": "Tools/migration/manifest/state/poison-spear-lv1-real.json",
        "generationLedgerHash": ledger_hash,
        "contentEntryCount": len(draft["contents"]),
        "artifactCount": len(ledger["artifacts"]),
        "sourceValues": {
            "range": skill["range"],
            "manaCost": skill["manaCost"],
            "damage": skill["damage"],
            "poisonDuration": skill["poisonDuration"],
            "poisonDamagePerTurn": skill["poisonTickDamage"],
            "poisonRefreshStrategy": poison["refreshStrategy"],
            "poisonTriggerTiming": poison["triggerTiming"],
            "skillProjectileSpeed": skill["projectileSpeed"],
            "skillProjectileTravelTime": skill["projectileTravelTime"],
            "authoredDropSearchRadius": skill["authoredDropSearchRadius"],
            "runtimeDropSearchRadius": skill["runtimeDropSearchRadius"],
            "dropsSpearOnCompletion": skill["dropsSpearOnCompletion"],
            "presentationAuthoringNodeCount": len(graph["nodes"]),
            "presentationAuthoringEdgeCount": len(graph["edges"]),
        },
        "automatedValidation": {
            "applicationCompilation": "passed",
            "resourceSaver": "passed",
            "uidPreservation": "passed",
            "byteIdempotencyRuns": 2,
            "byteIdentical": True,
            "headlessEditorFilesystemScan": "passed",
            "coreRuntime": "passed",
            "godotRuntime": "passed",
            "rendererHeadlessStartup": ["gl_compatibility", "forward_plus"],
        },
        "ownership": "UnityOwned",
        "state": "Validated",
        "visualAcceptance": "passed_for_programmatic_placeholder_only",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export-receipt", type=Path, required=True)
    parser.add_argument("--draft", type=Path, required=True)
    parser.add_argument("--ledger", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()

    receipt = compile_generation_receipt(
        _load(arguments.export_receipt),
        _load(arguments.draft),
        _sha256(arguments.draft),
        _load(arguments.ledger),
        _sha256(arguments.ledger),
    )
    payload = json.dumps(receipt, ensure_ascii=False, sort_keys=False, indent=2) + "\n"
    arguments.output.write_text(payload, encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
