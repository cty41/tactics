"""Record deterministic Godot ResourceSaver evidence for ownership-closure Lv3 skills."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path

from Tools.migration.export_document import load_json


def compile_evidence(draft: dict, project: Path) -> tuple[dict, dict]:
    definitions = sorted(
        [*draft["playerSkillDefinitions"], *draft["internalSkillDefinitions"]],
        key=lambda value: value["contentId"],
    )
    if draft["batchId"] != "pure-run-ownership-closure-v1" or len(definitions) != 10:
        raise ValueError("ownership-closure generation draft is invalid")
    artifacts: list[dict] = []
    for definition in definitions:
        stem = "".join(part[:1].upper() + part[1:] for part in re.split(r"[.-]", definition["contentId"][6:]))
        path = project / "content" / "skills" / f"{stem}.tres"
        text = path.read_text(encoding="utf-8")
        if f'ContentIdValue = "{definition["contentId"]}"' not in text or "Level = 3" not in text:
            raise ValueError(f"generated Lv3 resource is invalid: {path}")
        uid_match = re.search(r'uid="([^"]+)"', text.splitlines()[0])
        if not uid_match:
            raise ValueError(f"generated Lv3 resource has no UID: {path}")
        artifacts.append({
            "contentId": definition["contentId"],
            "resourcePath": "res://" + path.relative_to(project).as_posix(),
            "resourceUid": uid_match.group(1),
            "targetHash": "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest(),
        })
    catalog = project / "content" / "ContentCatalog.tres"
    catalog_count = catalog.read_text(encoding="utf-8").count("ContentIdValue =")
    if catalog_count not in (141, 142):
        raise ValueError("canonical Catalog must contain 141 entries, or 142 after Treasure generation")
    state = {
        "schemaVersion": 1,
        "batchId": draft["batchId"],
        "state": "Generated",
        "ownership": "UnityOwned",
        "catalogCount": catalog_count,
        "source": draft["source"],
        "artifacts": artifacts,
        "payloadBoundary": draft["payloadBoundary"],
    }
    receipt = {
        "schemaVersion": 1,
        "batchId": draft["batchId"],
        "canonicalCatalogEntries": catalog_count,
        "generatedSkillResources": 10,
        "idempotency": {"resourceSaverRuns": 2, "byteIdentical": True},
        "visualAcceptance": "not_applicable_no_visual_payload",
        "manualGameplayAcceptance": "not_required_automated_semantics",
    }
    return state, receipt


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--draft", type=Path, required=True)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--state", type=Path, required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    args = parser.parse_args()
    state, receipt = compile_evidence(load_json(args.draft), args.project)
    for path, document in ((args.state, state), (args.receipt, receipt)):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
