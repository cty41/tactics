"""Deterministic, dry-run friendly migration manifest converter.

The converter only produces audit/ledger data. Godot Resources are created by
the Godot adapter through ResourceSaver; this module never becomes a runtime
asset format.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import tempfile
from collections.abc import Iterable, Mapping
from pathlib import Path
from typing import Any

from .manifest import normalize_content_id, semantic_manifest_hash, validate_unique_content_ids


def content_id_from_unity(guid: str, local_file_id: int, prefix: str = "unity") -> str:
    normalized_guid = "".join(guid.strip().lower().split("-"))
    if len(normalized_guid) != 32 or any(character not in "0123456789abcdef" for character in normalized_guid):
        raise ValueError(f"invalid Unity GUID: {guid!r}")
    if local_file_id < 0:
        raise ValueError("localFileId must be non-negative")
    return normalize_content_id(f"{prefix}.{normalized_guid}.{local_file_id}")


def reference_diagnostics(entries: Iterable[Mapping[str, Any]]) -> list[dict[str, str]]:
    known = {str(entry["contentId"]) for entry in entries}
    diagnostics: list[dict[str, str]] = []
    for entry in entries:
        source_id = str(entry["contentId"])
        for reference in entry.get("references", []):
            if str(reference) not in known:
                diagnostics.append({"source": source_id, "missing": str(reference)})
    return diagnostics


def semantic_diff(before: Mapping[str, Any], after: Mapping[str, Any]) -> dict[str, dict[str, Any]]:
    volatile = {"convertedAt", "targetHash", "validation"}
    keys = (set(before) | set(after)) - volatile
    return {
        key: {"before": before.get(key), "after": after.get(key)}
        for key in sorted(keys)
        if before.get(key) != after.get(key)
    }


def _file_hash(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _atomic_write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            json.dump(payload, stream, ensure_ascii=False, indent=2, sort_keys=True)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_name, path)
    finally:
        if os.path.exists(temporary_name):
            os.unlink(temporary_name)


def convert_manifest(
    source: Path,
    target: Path,
    ledger: Path,
    *,
    dry_run: bool = False,
    force: bool = False,
    converter_version: str = "1",
) -> dict[str, Any]:
    entries = json.loads(source.read_text(encoding="utf-8"))
    if not isinstance(entries, list):
        raise ValueError("source manifest must be a JSON array")

    converted: list[dict[str, Any]] = []
    for raw in entries:
        item = dict(raw)
        item["contentId"] = normalize_content_id(
            str(item.get("contentId") or content_id_from_unity(str(item["guid"]), int(item["localFileId"])))
        )
        item["sourceHash"] = str(item.get("sourceHash") or _file_hash(Path(item["sourcePath"]))) if item.get("sourcePath") else str(item.get("sourceHash", ""))
        item["converterVersion"] = converter_version
        item["owner"] = item.get("owner", "UnityOwned")
        converted.append(item)

    validate_unique_content_ids(converted)
    diagnostics = reference_diagnostics(converted)
    previous = json.loads(target.read_text(encoding="utf-8")) if target.exists() else []
    if not isinstance(previous, list):
        raise ValueError("target manifest must be a JSON array")

    if target.exists() and semantic_manifest_hash(previous) != semantic_manifest_hash(converted) and not force:
        raise RuntimeError("target manifest differs from source; use --force only after reviewing semantic diff")

    payload = {
        "schemaVersion": 1,
        "source": str(source),
        "target": str(target),
        "converterVersion": converter_version,
        "contentIds": [entry["contentId"] for entry in sorted(converted, key=lambda value: value["contentId"])],
        "semanticHash": semantic_manifest_hash(converted),
        "referenceDiagnostics": diagnostics,
        "entries": converted,
    }
    if not dry_run:
        _atomic_write_json(target, converted)
        _atomic_write_json(ledger, payload)
    return payload


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("target", type=Path)
    parser.add_argument("ledger", type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--force", action="store_true")
    args = parser.parse_args()
    result = convert_manifest(args.source, args.target, args.ledger, dry_run=args.dry_run, force=args.force)
    print(json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
