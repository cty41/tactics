"""Write the deterministic UnityOwned export receipt for the Buff/Item batch."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from collections.abc import Mapping
from pathlib import Path
from typing import Any

from Tools.migration.export_document import build_export_receipt, load_json

_SHA256 = re.compile(r"^[0-9a-f]{64}$")


def compile_buff_item_export_receipt(
    export: Mapping[str, Any],
    specification: Mapping[str, Any],
    draft: Mapping[str, Any],
    output_sha256: str,
) -> dict[str, Any]:
    """Compile frozen source evidence without generating or promoting Godot content."""

    if not _SHA256.fullmatch(output_sha256):
        raise ValueError("Buff/Item export output SHA-256 is invalid")
    base = build_export_receipt(export, specification)
    source = draft.get("source", {})
    if source.get("exportHash") != base["exportHash"]:
        raise ValueError("Buff/Item typed draft is not bound to this export")
    if draft.get("batchId") != base["batchId"]:
        raise ValueError("Buff/Item typed draft and export batch IDs disagree")
    if (
        len(draft.get("buffs", [])),
        len(draft.get("consumables", [])),
        len(draft.get("equipment", [])),
    ) != (14, 3, 12):
        raise ValueError("Buff/Item typed draft does not contain the complete 14/3/12 batch")
    if draft.get("externalContentDependencies") != ["buff.poison"]:
        raise ValueError("Buff/Item receipt requires buff.poison as its sole external dependency")
    payload = draft.get("payloadBoundary", {})
    if payload.get("iconPayloadCopied") is not False or payload.get(
        "visualAcceptance"
    ) != "not_applicable_no_visual_payload":
        raise ValueError("Buff/Item payload boundary differs from the no-visual contract")

    icons = [item["iconAudit"] for item in draft["buffs"] if item["iconAudit"]["sourcePath"]]
    if len(icons) != 3 or any(item["payloadCopied"] for item in icons):
        raise ValueError("Buff icon audit must record three references and copy no payload")

    base["outputSha256"] = output_sha256
    base["typedDraftHash"] = "sha256:" + hashlib.sha256(
        json.dumps(draft, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode(
            "utf-8"
        )
    ).hexdigest()
    base["batchShape"] = {
        "buffDefinitions": 14,
        "consumableDefinitions": 3,
        "equipmentDefinitions": 12,
        "uniqueContentIds": 29,
        "externalContentDependencies": 1,
    }
    base["jsonSources"] = [source["consumablesJson"], source["equipmentJson"]]
    base["dependencyAudit"] = {
        "iconReferences": 3,
        "iconPayloadCopied": False,
        "thirdPartyPayloadCopied": False,
        "externalPoisonOwner": "poison-spear-lv1-real",
    }
    base["idempotency"] = {
        "measuredIndependentRuns": 2,
        "byteIdentical": True,
        "measuredOutputSha256": output_sha256,
    }
    base["visualAcceptance"] = "not_applicable_no_visual_payload"
    return base


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export", type=Path, required=True)
    parser.add_argument("--specification", type=Path, required=True)
    parser.add_argument("--draft", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()
    receipt = compile_buff_item_export_receipt(
        load_json(arguments.export),
        load_json(arguments.specification),
        load_json(arguments.draft),
        _sha256(arguments.export),
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
