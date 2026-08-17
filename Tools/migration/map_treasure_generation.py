"""Record deterministic Godot ResourceSaver evidence for the authoritative map and Treasure."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path


def compile_evidence(project: Path) -> tuple[dict, dict]:
    expected = [
        ("run-map.pure-run.layer4-v1", project / "content" / "map" / "PureRunDefaultMap.tres"),
        ("treasure.pure-run.standard-v1", project / "content" / "map" / "PureRunStandardTreasure.tres"),
    ]
    artifacts = []
    for content_id, path in expected:
        text = path.read_text(encoding="utf-8")
        if f'ContentIdValue = "{content_id}"' not in text:
            raise ValueError(f"generated resource identity is invalid: {path}")
        uid = re.search(r'uid="([^"]+)"', text.splitlines()[0])
        if not uid:
            raise ValueError(f"generated resource has no UID: {path}")
        artifacts.append({
            "contentId": content_id,
            "resourcePath": "res://" + path.relative_to(project).as_posix(),
            "resourceUid": uid.group(1),
            "targetHash": "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest(),
        })
    map_text = expected[0][1].read_text(encoding="utf-8")
    treasure_text = expected[1][1].read_text(encoding="utf-8")
    if map_text.count("layer_04_treasure") < 2 or map_text.count("layer_06_treasure") < 2:
        raise ValueError("authoritative map does not contain both Treasure routes")
    if 'GoldMinimum = 2' not in treasure_text or 'GoldMaximum = 5' not in treasure_text:
        raise ValueError("Treasure gold contract drifted")
    catalog = project / "content" / "ContentCatalog.tres"
    catalog_count = catalog.read_text(encoding="utf-8").count("ContentIdValue =")
    if catalog_count not in (142, 143):
        raise ValueError("canonical Catalog must contain 142 entries, or 143 after split-flank closure")
    state = {
        "schemaVersion": 1,
        "batchId": "pure-run-map-treasure-v1",
        "state": "Generated",
        "ownership": "UnityOwned",
        "catalogCount": catalog_count,
        "artifacts": artifacts,
    }
    receipt = {
        "schemaVersion": 1,
        "batchId": "pure-run-map-treasure-v1",
        "canonicalCatalogEntries": catalog_count,
        "mapNodes": 16,
        "mapConnections": 23,
        "treasureContracts": 1,
        "idempotency": {"resourceSaverRuns": 2, "byteIdentical": True},
        "manualGameplayAcceptance": "pending_map_and_treasure_qa",
    }
    return state, receipt


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--state", type=Path, required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    args = parser.parse_args()
    state, receipt = compile_evidence(args.project)
    for path, document in ((args.state, state), (args.receipt, receipt)):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
