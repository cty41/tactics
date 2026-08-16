"""Transactional staging for converter outputs generated through supported engine APIs."""

from __future__ import annotations

import hashlib
import json
import os
import shutil
import uuid
from collections.abc import Callable, Mapping, Sequence
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

from Tools.migration.manifest import normalize_content_id


class MigrationConflictError(RuntimeError):
    """Raised when a generated artifact would overwrite unmanaged or manually edited data."""


@dataclass(frozen=True)
class MigrationSourceBinding:
    source_tag: str
    source_commit: str
    exporter_version: str
    export_hash: str


@dataclass(frozen=True)
class StagedArtifact:
    content_id: str
    relative_path: str
    payload: bytes
    semantic_model: Any
    resource_uid: str | None = None


@dataclass(frozen=True)
class StagingResult:
    batch_id: str
    changed_paths: tuple[str, ...]
    unchanged_paths: tuple[str, ...]
    ledger_changed: bool
    dry_run: bool

    @property
    def changed(self) -> bool:
        return bool(self.changed_paths) or self.ledger_changed


def sha256_bytes(payload: bytes) -> str:
    return "sha256:" + hashlib.sha256(payload).hexdigest()


def semantic_hash(model: Any) -> str:
    canonical = json.dumps(
        model,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return sha256_bytes(canonical)


def apply_staged_batch(
    root: Path,
    batch_id: str,
    source: MigrationSourceBinding,
    artifacts: Sequence[StagedArtifact],
    *,
    ledger_relative_path: str | None = None,
    dry_run: bool = False,
    failure_injector: Callable[[str], None] | None = None,
) -> StagingResult:
    """Validate, stage and replace a migration batch with conflict checks and rollback."""

    root = root.resolve()
    if not root.is_dir():
        raise ValueError(f"migration root does not exist: {root}")
    if not batch_id or any(character not in "abcdefghijklmnopqrstuvwxyz0123456789-." for character in batch_id):
        raise ValueError(f"invalid batch id: {batch_id!r}")
    _validate_source(source)
    if not artifacts:
        raise ValueError("migration batch contains no artifacts")

    ledger_relative_path = ledger_relative_path or f"Tools/migration/manifest/state/{batch_id}.json"
    ledger_path = _resolve_target(root, ledger_relative_path)
    previous_ledger = _read_ledger(ledger_path, batch_id)
    previous_entries = {
        str(entry["relativePath"]): entry for entry in previous_ledger.get("artifacts", [])
    } if previous_ledger else {}

    normalized = _normalize_artifacts(root, artifacts)
    changes: list[dict[str, Any]] = []
    unchanged_paths: list[str] = []
    ledger_entries: list[dict[str, Any]] = []

    for artifact, target_path, target_hash, model_hash in normalized:
        previous = previous_entries.get(artifact.relative_path)
        current_hash = sha256_bytes(target_path.read_bytes()) if target_path.is_file() else None
        previous_hash = str(previous["targetHash"]) if previous else None
        previous_semantic_hash = str(previous["semanticHash"]) if previous else None
        previous_uid = previous.get("resourceUid") if previous else None

        if previous and current_hash != previous_hash:
            raise MigrationConflictError(
                f"target was modified after the last migration: {artifact.relative_path}"
            )
        if not previous and current_hash is not None and current_hash != target_hash:
            raise MigrationConflictError(
                f"target exists without a matching migration ledger entry: {artifact.relative_path}"
            )
        if previous_uid and artifact.resource_uid and previous_uid != artifact.resource_uid:
            raise MigrationConflictError(
                f"resource UID changed for {artifact.relative_path}: {previous_uid} -> {artifact.resource_uid}"
            )

        effective_uid = artifact.resource_uid or previous_uid
        if previous and previous_semantic_hash == model_hash:
            effective_target_hash = current_hash
            unchanged_paths.append(artifact.relative_path)
        elif current_hash == target_hash:
            effective_target_hash = current_hash
            unchanged_paths.append(artifact.relative_path)
        else:
            effective_target_hash = target_hash
            changes.append(
                {
                    "artifact": artifact,
                    "targetPath": target_path,
                    "targetHash": target_hash,
                }
            )

        ledger_entries.append(
            {
                "contentId": artifact.content_id,
                "relativePath": artifact.relative_path,
                "resourceUid": effective_uid,
                "targetHash": effective_target_hash,
                "semanticHash": model_hash,
            }
        )

    ledger_document = {
        "schemaVersion": 1,
        "batchId": batch_id,
        "source": {
            "sourceTag": source.source_tag,
            "sourceCommit": source.source_commit,
            "exporterVersion": source.exporter_version,
            "exportHash": source.export_hash,
        },
        "artifacts": sorted(ledger_entries, key=lambda entry: entry["contentId"]),
    }
    ledger_payload = (
        json.dumps(ledger_document, ensure_ascii=False, sort_keys=True, indent=2) + "\n"
    ).encode("utf-8")
    previous_ledger_payload = ledger_path.read_bytes() if ledger_path.is_file() else None
    ledger_changed = previous_ledger_payload != ledger_payload

    result = StagingResult(
        batch_id=batch_id,
        changed_paths=tuple(sorted(change["artifact"].relative_path for change in changes)),
        unchanged_paths=tuple(sorted(unchanged_paths)),
        ledger_changed=ledger_changed,
        dry_run=dry_run,
    )
    if dry_run or (not changes and not ledger_changed):
        return result

    staging_root = root / ".migration-staging" / f"{batch_id}-{uuid.uuid4().hex}"
    backups: dict[Path, bytes | None] = {}
    created_directories: set[Path] = set()
    try:
        staging_root.mkdir(parents=True, exist_ok=False)
        staged_files: list[tuple[Path, Path]] = []
        for index, change in enumerate(changes):
            artifact = change["artifact"]
            staged_path = staging_root / f"artifact-{index}"
            staged_path.write_bytes(artifact.payload)
            staged_files.append((staged_path, change["targetPath"]))

        for index, (staged_path, target_path) in enumerate(staged_files, start=1):
            backups[target_path] = target_path.read_bytes() if target_path.is_file() else None
            _ensure_parent(target_path.parent, root, created_directories)
            os.replace(staged_path, target_path)
            if failure_injector:
                failure_injector(f"after_artifact:{index}")

        if ledger_changed:
            backups[ledger_path] = previous_ledger_payload
            _ensure_parent(ledger_path.parent, root, created_directories)
            ledger_temporary_path = ledger_path.with_name(f".{ledger_path.name}.{uuid.uuid4().hex}.tmp")
            ledger_temporary_path.write_bytes(ledger_payload)
            os.replace(ledger_temporary_path, ledger_path)
            if failure_injector:
                failure_injector("after_ledger")
    except Exception:
        _rollback(backups)
        _remove_empty_directories(created_directories, root)
        raise
    finally:
        shutil.rmtree(staging_root, ignore_errors=True)
        staging_parent = staging_root.parent
        if staging_parent.is_dir() and not any(staging_parent.iterdir()):
            staging_parent.rmdir()

    return result


def _normalize_artifacts(
    root: Path,
    artifacts: Sequence[StagedArtifact],
) -> list[tuple[StagedArtifact, Path, str, str]]:
    content_ids: set[str] = set()
    paths: set[str] = set()
    normalized: list[tuple[StagedArtifact, Path, str, str]] = []
    for artifact in artifacts:
        content_id = normalize_content_id(artifact.content_id)
        if content_id != artifact.content_id:
            raise ValueError(f"ContentId must already be normalized: {artifact.content_id!r}")
        if content_id in content_ids:
            raise ValueError(f"duplicate ContentId in staged batch: {content_id}")
        if artifact.relative_path in paths:
            raise ValueError(f"duplicate target path in staged batch: {artifact.relative_path}")
        if artifact.resource_uid is not None and not artifact.resource_uid.startswith("uid://"):
            raise ValueError(f"invalid resource UID: {artifact.resource_uid!r}")
        target_path = _resolve_target(root, artifact.relative_path)
        target_hash = sha256_bytes(artifact.payload)
        model_hash = semantic_hash(artifact.semantic_model)
        content_ids.add(content_id)
        paths.add(artifact.relative_path)
        normalized.append((artifact, target_path, target_hash, model_hash))
    return normalized


def _resolve_target(root: Path, relative_path: str) -> Path:
    path = Path(relative_path)
    if path.is_absolute() or not path.name or ".." in path.parts:
        raise ValueError(f"target path must stay inside migration root: {relative_path!r}")
    resolved = (root / path).resolve()
    if os.path.commonpath((str(root), str(resolved))) != str(root):
        raise ValueError(f"target path escapes migration root: {relative_path!r}")
    return resolved


def _validate_source(source: MigrationSourceBinding) -> None:
    for field_name, value in asdict(source).items():
        if not value or not str(value).strip():
            raise ValueError(f"source binding field is empty: {field_name}")


def _read_ledger(path: Path, batch_id: str) -> Mapping[str, Any] | None:
    if not path.is_file():
        return None
    document = json.loads(path.read_text(encoding="utf-8"))
    if document.get("schemaVersion") != 1 or document.get("batchId") != batch_id:
        raise MigrationConflictError(f"invalid or mismatched migration ledger: {path}")
    return document


def _ensure_parent(path: Path, root: Path, created_directories: set[Path]) -> None:
    missing: list[Path] = []
    current = path
    while current != root and not current.exists():
        missing.append(current)
        current = current.parent
    path.mkdir(parents=True, exist_ok=True)
    created_directories.update(missing)


def _rollback(backups: Mapping[Path, bytes | None]) -> None:
    for path, payload in reversed(tuple(backups.items())):
        if payload is None:
            if path.exists():
                path.unlink()
            continue
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary_path = path.with_name(f".{path.name}.{uuid.uuid4().hex}.rollback")
        temporary_path.write_bytes(payload)
        os.replace(temporary_path, path)


def _remove_empty_directories(directories: set[Path], root: Path) -> None:
    for directory in sorted(directories, key=lambda value: len(value.parts), reverse=True):
        if directory != root and directory.is_dir() and not any(directory.iterdir()):
            directory.rmdir()
