"""Validate and copy approved Pure Run player action poses into the canonical Godot project."""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
from pathlib import Path


def migrate(root: Path, manifest_path: Path, receipt_path: Path | None = None) -> list[Path]:
    document = json.loads(manifest_path.read_text(encoding="utf-8"))
    if document.get("schemaVersion") != 1 or document.get("contractId") != "pure-run-player-action-poses-v1":
        raise ValueError("action pose manifest identity is invalid")
    target_root = root / "godot/assets/units/actions"
    target_root.mkdir(parents=True, exist_ok=True)
    outputs: list[Path] = []
    names: set[str] = set()
    for asset in document.get("assets", []):
        source = root / asset["source"]
        if not source.is_file() or "Assets/Tactics/Arts/PureRun/Textures/Actions/" not in source.as_posix():
            raise ValueError(f"approved action pose source is missing: {source}")
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        if digest != asset["sha256"]:
            raise ValueError(f"action pose source hash drifted: {source}")
        if source.name in names:
            raise ValueError(f"duplicate action pose target name: {source.name}")
        names.add(source.name)
        target = target_root / source.name
        if not target.exists() or target.read_bytes() != source.read_bytes():
            shutil.copyfile(source, target)
        outputs.append(target)
    if len(outputs) != 14:
        raise ValueError("action pose contract must contain exactly 14 approved textures")
    if receipt_path is not None:
        receipt = {
            "schemaVersion": 1,
            "batchId": "pure-run-player-action-poses-v1",
            "classification": "Generated",
            "ownership": "UnityOwned",
            "manualVisualQa": "pending",
            "payloadBoundary": "project-owned-approved-action-poses-only",
            "artifacts": [
                {
                    "resourcePath": "res://assets/units/actions/" + output.name,
                    "sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
                }
                for output in sorted(outputs, key=lambda value: value.name)
            ],
        }
        receipt_path.parent.mkdir(parents=True, exist_ok=True)
        receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, sort_keys=True, indent=2) + "\n",
                                encoding="utf-8", newline="\n")
    return outputs


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--receipt", type=Path)
    arguments = parser.parse_args()
    for output in migrate(arguments.root.resolve(), arguments.manifest.resolve(),
                          arguments.receipt.resolve() if arguments.receipt else None):
        print(output.as_posix())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
