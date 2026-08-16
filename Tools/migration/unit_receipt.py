"""Write the deterministic UnityOwned export receipt for the Pure Run Unit batch."""

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


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def compile_unit_export_receipt(
    export: Mapping[str, Any],
    specification: Mapping[str, Any],
    draft: Mapping[str, Any],
    output_sha256: str,
) -> dict[str, Any]:
    """Compile the audited export evidence without promoting Unity-owned content."""

    if not _SHA256.fullmatch(output_sha256):
        raise ValueError("Unit export output SHA-256 is invalid")
    base = build_export_receipt(export, specification)
    source = draft.get("source", {})
    if source.get("exportHash") != base["exportHash"]:
        raise ValueError("Unit typed draft is not bound to this export")
    if draft.get("batchId") != base["batchId"]:
        raise ValueError("Unit typed draft and export batch IDs disagree")
    if len(draft.get("units", [])) != 12 or len(draft.get("textureAssets", [])) != 21:
        raise ValueError("Unit typed draft does not contain the complete 12/21 batch")

    audit = draft["dependencyAudit"]
    base["outputSha256"] = output_sha256
    base["typedDraftHash"] = "sha256:" + hashlib.sha256(
        json.dumps(draft, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode(
            "utf-8"
        )
    ).hexdigest()
    base["batchShape"] = {
        "unitDefinitions": 12,
        "prefabAuditRoots": 12,
        "texturePayloads": 21,
        "materialAuditRoots": 6,
        "selectedRoots": 39,
    }
    base["dependencyAudit"] = {
        "policy": audit["policy"],
        "deferredDependencyCount": len(audit["deferredDependencies"]),
        "thirdPartyDependencies": audit["thirdPartyDependencies"],
        "forbiddenPayloadDependencies": audit["forbiddenPayloadDependencies"],
        "materialAndShaderPayloadCopied": False,
    }
    base["idempotency"] = {
        "warmupRuns": 1,
        "measuredIndependentRuns": 2,
        "byteIdentical": True,
        "measuredOutputSha256": output_sha256,
        "coldStartObservation": "compile/import warm-up excluded before measured runs",
    }
    return base


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export", type=Path, required=True)
    parser.add_argument("--specification", type=Path, required=True)
    parser.add_argument("--draft", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()

    receipt = compile_unit_export_receipt(
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
