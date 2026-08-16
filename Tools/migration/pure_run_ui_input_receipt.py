"""Write the deterministic UnityOwned export receipt for Phase 7A UI/Input roots."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from Tools.migration.export_document import build_export_receipt, load_json


def compile_receipt(export: dict, specification: dict, draft: dict) -> dict:
    receipt = build_export_receipt(export, specification)
    if draft.get("batchId") != receipt["batchId"]:
        raise ValueError("UI/Input draft and export batch IDs disagree")
    if draft.get("source", {}).get("exportHash") != receipt["exportHash"]:
        raise ValueError("UI/Input draft is not bound to this export")
    if any(asset.get("exportMode") != "audit-only-file" for asset in export["assets"]):
        raise ValueError("UI/Input receipt requires audit-only file roots")
    receipt["idempotency"] = {"measuredIndependentRuns": 2, "byteIdentical": True}
    receipt["payloadBoundary"] = {
        "unityUiToolkitCopied": False,
        "formalVfxAudioCopied": False,
        "dependencyAuditOnly": True,
    }
    receipt["manualUiInputAcceptance"] = "pending"
    return receipt


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export", type=Path, required=True)
    parser.add_argument("--specification", type=Path, required=True)
    parser.add_argument("--draft", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    receipt = compile_receipt(load_json(args.export), load_json(args.specification), load_json(args.draft))
    receipt["outputSha256"] = hashlib.sha256(args.export.read_bytes()).hexdigest()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
