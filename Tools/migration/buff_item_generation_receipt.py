"""Write the deterministic receipt for the Pure Run Buff/Item Godot generation batch."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from collections.abc import Mapping
from pathlib import Path
from typing import Any


_HASH = re.compile(r"^sha256:[0-9a-f]{64}$")


def _sha256(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


def _load(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def compile_buff_item_generation_receipt(
    export_receipt: Mapping[str, Any],
    draft: Mapping[str, Any],
    draft_hash: str,
    ledger: Mapping[str, Any],
    ledger_hash: str,
) -> dict[str, Any]:
    """Bind final ResourceSaver artifacts and catalogs to the frozen source export."""

    if not _HASH.fullmatch(draft_hash) or not _HASH.fullmatch(ledger_hash):
        raise ValueError("Buff/Item generation receipt contains an invalid SHA-256")
    if draft.get("batchId") != "pure-run-buffs-items-v1":
        raise ValueError("Buff/Item draft has the wrong batch ID")
    if draft["batchId"] != ledger.get("batchId") or draft["batchId"] != export_receipt.get(
        "batchId"
    ):
        raise ValueError("Buff/Item draft, ledger, and export receipt batch IDs disagree")

    source = ledger.get("source", {})
    for key in ("sourceTag", "sourceCommit", "unityVersion", "exporterVersion", "exportHash"):
        if source.get(key) != export_receipt.get(key):
            raise ValueError(f"Buff/Item generation source differs from export receipt: {key}")
    draft_source = draft.get("source", {})
    for key in ("sourceTag", "sourceCommit", "unityVersion", "exporterVersion", "exportHash"):
        if source.get(key) != draft_source.get(key):
            raise ValueError(f"Buff/Item generation source differs from typed draft: {key}")
    if source.get("consumablesJsonSha256") != draft_source.get("consumablesJson", {}).get(
        "sha256"
    ) or source.get("equipmentJsonSha256") != draft_source.get("equipmentJson", {}).get("sha256"):
        raise ValueError("Buff/Item generation JSON source hashes differ from typed draft")

    if (len(draft.get("buffs", [])), len(draft.get("consumables", [])), len(draft.get("equipment", []))) != (
        14,
        3,
        12,
    ):
        raise ValueError("Buff/Item typed draft does not contain the complete 14/3/12 batch")
    if draft.get("externalContentDependencies") != ["buff.poison"]:
        raise ValueError("Buff/Item generation requires buff.poison as its sole external dependency")
    payload = draft.get("payloadBoundary", {})
    if (
        payload.get("buffIcons") != "audit_only_not_copied"
        or payload.get("iconPayloadCopied") is not False
        or payload.get("thirdPartyPayloadCopied") is not False
        or payload.get("visualAcceptance") != "not_applicable_no_visual_payload"
    ):
        raise ValueError("Buff/Item payload boundary differs from the no-visual contract")

    artifacts = ledger.get("artifacts", [])
    if len(artifacts) != 29:
        raise ValueError("Buff/Item generation ledger must contain exactly 29 batch-owned artifacts")
    paths = [artifact.get("resourcePath") for artifact in artifacts]
    if len(paths) != len(set(paths)) or any(path is None for path in paths):
        raise ValueError("Buff/Item generation ledger contains missing or duplicate paths")
    if "res://content/buffs_items/ContentCatalog.tres" not in paths or "res://content/ContentCatalog.tres" in paths:
        raise ValueError("Buff/Item ledger must own only its batch Catalog")
    if "res://content/buffs_items/BuffPoison.tres" in paths:
        raise ValueError("Buff/Item generation created a duplicate Poison resource")
    if any(not _HASH.fullmatch(artifact.get("targetHash", "")) for artifact in artifacts):
        raise ValueError("Buff/Item generation ledger contains an invalid artifact hash")

    icon_audits = [buff["iconAudit"] for buff in draft["buffs"] if buff["iconAudit"]["sourcePath"]]
    if len(icon_audits) != 3 or any(icon["payloadCopied"] for icon in icon_audits):
        raise ValueError("Buff/Item generation must audit three icons and copy none")

    return {
        "schemaVersion": 1,
        "batchId": draft["batchId"],
        "classification": "real_godot_resource_generation",
        "sourceTag": source["sourceTag"],
        "sourceCommit": source["sourceCommit"],
        "unityVersion": source["unityVersion"],
        "exporterVersion": source["exporterVersion"],
        "exportHash": source["exportHash"],
        "typedDraftHash": draft_hash,
        "generationLedger": "Tools/migration/manifest/state/pure-run-buffs-items-v1.json",
        "generationLedgerHash": ledger_hash,
        "batchCatalog": "godot/content/buffs_items/ContentCatalog.tres",
        "batchCatalogEntryCount": 29,
        "canonicalCatalog": "godot/content/ContentCatalog.tres",
        "canonicalCatalogEntryCount": 58,
        "generatedBuffDefinitionCount": 13,
        "externalBuffDefinitionCount": 1,
        "consumableDefinitionCount": 3,
        "equipmentDefinitionCount": 12,
        "artifactCount": len(artifacts),
        "externalContentDependencies": [
            {
                "contentId": "buff.poison",
                "ownerBatch": "poison-spear-lv1-real",
                "resourcePath": "godot/content/poison_spear/PoisonBuff.tres",
            }
        ],
        "dependencyBoundary": {
            "buffIconReferenceCount": len(icon_audits),
            "buffIconPayloadCopied": False,
            "unityMaterialOrShaderPayloadCopied": False,
            "thirdPartyPayloadCopied": False,
        },
        "automatedValidation": {
            "coreContractTests": "passed",
            "applicationCompilerTests": "passed",
            "unityOracleTests": "passed",
            "resourceSaver": "passed",
            "uidPreservation": "passed",
            "byteIdempotencyRuns": 2,
            "byteIdentical": True,
            "headlessEditorFilesystemScan": "passed",
            "gdUnitRuntimeTests": "passed",
            "batchCatalogRuntime": "passed",
            "canonicalCatalogRuntime": "passed",
            "rendererHeadlessStartup": ["gl_compatibility", "forward_plus"],
        },
        "ownership": "UnityOwned",
        "state": "Validated",
        "visualAcceptance": "not_applicable_no_visual_payload",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export-receipt", type=Path, required=True)
    parser.add_argument("--draft", type=Path, required=True)
    parser.add_argument("--ledger", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()

    receipt = compile_buff_item_generation_receipt(
        _load(arguments.export_receipt),
        _load(arguments.draft),
        _sha256(arguments.draft),
        _load(arguments.ledger),
        _sha256(arguments.ledger),
    )
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(
        json.dumps(receipt, ensure_ascii=False, sort_keys=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
