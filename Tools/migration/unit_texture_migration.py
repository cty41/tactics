"""Transactionally copy the frozen Pure Run Unit PNG allowlist into Godot."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from collections.abc import Mapping
from pathlib import Path
from typing import Any

from Tools.migration.export_document import load_json
from Tools.migration.staging import (
    MigrationSourceBinding,
    StagedArtifact,
    apply_staged_batch,
)

_SOURCE_PREFIX = "Assets/Tactics/Arts/PureRun/Textures/"
_TARGET_PREFIX = "res://assets/units/"
_SAFE_NAME = re.compile(r"^[a-z0-9][a-z0-9_]*\.png$")


def compile_unit_texture_artifacts(
    root: Path, draft: Mapping[str, Any]
) -> tuple[MigrationSourceBinding, list[StagedArtifact]]:
    """Bind every copied byte to the typed draft and its frozen PNG hash."""

    root = root.resolve()
    textures = draft.get("textureAssets")
    if not isinstance(textures, list) or len(textures) != 19:
        raise ValueError("Pure Run Unit texture migration requires exactly 19 PNGs")
    source = draft.get("source", {})
    binding = MigrationSourceBinding(
        source_tag=str(source["sourceTag"]),
        source_commit=str(source["sourceCommit"]),
        exporter_version=str(source["exporterVersion"]),
        export_hash=str(source["exportHash"]),
    )

    artifacts = []
    seen_targets: set[str] = set()
    for texture in textures:
        source_path = str(texture["sourcePath"])
        target_path = str(texture["targetPath"])
        if not source_path.startswith(_SOURCE_PREFIX):
            raise ValueError(f"Unit texture source is outside the project-owned allowlist: {source_path}")
        if not target_path.startswith(_TARGET_PREFIX):
            raise ValueError(f"Unit texture target is outside the canonical Godot folder: {target_path}")
        source_name = source_path.removeprefix(_SOURCE_PREFIX)
        target_name = target_path.removeprefix(_TARGET_PREFIX)
        if source_name != target_name or not _SAFE_NAME.fullmatch(source_name):
            raise ValueError(f"Unit texture filename mapping is not canonical: {source_path}")
        relative_path = "godot/" + target_path.removeprefix("res://")
        if relative_path in seen_targets:
            raise ValueError(f"duplicate Unit texture target: {relative_path}")

        source_file = root / source_path
        payload = source_file.read_bytes()
        actual_sha256 = hashlib.sha256(payload).hexdigest()
        if actual_sha256 != texture["sha256"]:
            raise ValueError(f"Unit texture bytes differ from the frozen SHA-256: {source_path}")
        content_slug = Path(source_name).stem.replace("_", "-")
        artifacts.append(
            StagedArtifact(
                content_id=f"texture.unit.{content_slug}",
                relative_path=relative_path,
                payload=payload,
                semantic_model={
                    "sourcePath": source_path,
                    "targetPath": target_path,
                    "gitBlobSha1": texture["gitBlobSha1"],
                    "sha256": texture["sha256"],
                    "width": texture["width"],
                    "height": texture["height"],
                    "kind": texture["kind"],
                    "importContract": texture["importContract"],
                },
            )
        )
        seen_targets.add(relative_path)
    return binding, artifacts


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--draft", type=Path, required=True)
    parser.add_argument("--dry-run", action="store_true")
    arguments = parser.parse_args()

    draft = load_json(arguments.draft)
    source, artifacts = compile_unit_texture_artifacts(arguments.root, draft)
    result = apply_staged_batch(
        arguments.root,
        "pure-run-unit-textures-v1",
        source,
        artifacts,
        ledger_relative_path="Tools/migration/manifest/state/pure-run-unit-textures-v1.json",
        dry_run=arguments.dry_run,
    )
    print(
        json.dumps(
            {
                "batchId": result.batch_id,
                "changedPaths": result.changed_paths,
                "unchangedPaths": result.unchanged_paths,
                "ledgerChanged": result.ledger_changed,
                "dryRun": result.dry_run,
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
