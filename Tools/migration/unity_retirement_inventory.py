from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import subprocess
from pathlib import Path


VALID_CLASSIFICATIONS = {
    "migrated_equivalent",
    "replaced_by_godot_design",
    "retired_legacy_prototype",
    "excluded_third_party",
    "deferred_audio_payload",
    "provenance_only",
    "unresolved",
}


def _git(root: Path, *args: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(root), *args], check=True, capture_output=True, text=True, encoding="utf-8"
    )
    return result.stdout


def compile_inventory(root: Path, rules_path: Path) -> dict:
    rules = json.loads(rules_path.read_text(encoding="utf-8"))
    tag = rules["sourceTag"]
    tag_object = _git(root, "rev-parse", tag).strip()
    commit = _git(root, "rev-parse", f"{tag}^{{commit}}").strip()
    if tag_object != rules["sourceTagObject"]:
        raise ValueError(f"source tag object drift: expected {rules['sourceTagObject']}, got {tag_object}")
    if commit != rules["sourceCommit"]:
        raise ValueError(f"source tag drift: expected {rules['sourceCommit']}, got {commit}")

    entries: list[dict] = []
    for line in _git(root, "ls-tree", "-r", "-l", tag, "--", *rules["roots"]).splitlines():
        metadata, path = line.split("\t", 1)
        mode, object_type, blob, size_text = metadata.split()
        classification = "unresolved"
        reason = "No retirement rule matched this tracked Unity file."
        matched_rule = ""
        for rule in rules["rules"]:
            if fnmatch.fnmatchcase(path, rule["pattern"]):
                classification = rule["classification"]
                reason = rule["reason"]
                matched_rule = rule["pattern"]
                break
        if classification not in VALID_CLASSIFICATIONS:
            raise ValueError(f"unknown classification {classification!r} for {path}")
        entries.append(
            {
                "path": path,
                "blobSha1": blob,
                "size": int(size_text) if size_text != "-" else 0,
                "classification": classification,
                "matchedRule": matched_rule,
                "reason": reason,
            }
        )

    counts = {name: 0 for name in sorted(VALID_CLASSIFICATIONS)}
    bytes_by_classification = {name: 0 for name in sorted(VALID_CLASSIFICATIONS)}
    for entry in entries:
        counts[entry["classification"]] += 1
        bytes_by_classification[entry["classification"]] += entry["size"]
    document = {
        "schemaVersion": 1,
        "inventoryId": "unity-retirement-inventory-v1",
        "sourceTag": tag,
        "sourceTagObject": tag_object,
        "sourceCommit": commit,
        "trackedFileCount": len(entries),
        "trackedByteCount": sum(entry["size"] for entry in entries),
        "counts": counts,
        "bytes": bytes_by_classification,
        "entries": entries,
    }
    canonical = json.dumps(document, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    document["semanticSha256"] = hashlib.sha256(canonical).hexdigest()
    return document


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument(
        "--rules",
        type=Path,
        default=Path(__file__).resolve().parent / "manifest/retirement/unity-retirement-rules-v1.json",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path(__file__).resolve().parent / "manifest/retirement/unity-retirement-inventory-v1.json",
    )
    args = parser.parse_args()
    document = compile_inventory(args.root.resolve(), args.rules.resolve())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if document["counts"]["unresolved"]:
        raise SystemExit(f"unresolved Unity retirement entries: {document['counts']['unresolved']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
