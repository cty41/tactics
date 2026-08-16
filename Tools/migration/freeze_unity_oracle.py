"""Materialize immutable Unity Oracle source evidence from the final Git tag.

The generated files are historical test inputs. They are deliberately stored outside
the Unity project so the Godot mainline can validate the same contracts after Assets/
is retired.
"""

from __future__ import annotations

import hashlib
import json
import re
import subprocess
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SOURCE_TEST = REPOSITORY_ROOT / "src/Tactics.FrozenOracle.Tests/FrozenUnitySourceTests.cs"
OUTPUT_ROOT = REPOSITORY_ROOT / "src/Tactics.FrozenOracle.Tests/FrozenSources"
MANIFEST_PATH = REPOSITORY_ROOT / "src/Tactics.FrozenOracle.Tests/frozen-source-manifest.json"
TAG_NAME = "unity-final-2026-08-08"
TAG_OBJECT = "b881177a7a34eff2d4ef8bc3ca6e47c12f5a468d"
PEELED_COMMIT = "168d19345d7e0f7f22ce2516351eda9cef2e1cb1"


def git_bytes(*arguments: str) -> bytes:
    return subprocess.check_output(["git", *arguments], cwd=REPOSITORY_ROOT)


def git_text(*arguments: str) -> str:
    return git_bytes(*arguments).decode("utf-8").strip()


def discover_sources() -> dict[str, str | None]:
    source = SOURCE_TEST.read_text(encoding="utf-8")
    bindings = {
        path: blob
        for path, blob in re.findall(
            r'\["(Assets/[^"\r\n]+)"\]\s*=\s*"([0-9a-f]{40})"', source
        )
    }
    bindings["Assets/Tactics/Arts/PureRun/Shaders/GoatBodyTint.shader"] = (
        "d4da8e21404ac1b5d134b0f1455f36839900e7c2"
    )
    bindings.setdefault("Assets/Tactics/GameData/Consumables.json", None)
    bindings.setdefault("Assets/Tactics/GameData/Equipment.json", None)
    return dict(sorted(bindings.items()))


def main() -> None:
    actual_tag_object = git_text("rev-parse", TAG_NAME)
    actual_commit = git_text("rev-parse", f"{TAG_NAME}^{{commit}}")
    if actual_tag_object != TAG_OBJECT or actual_commit != PEELED_COMMIT:
        raise RuntimeError(
            "Unity final tag drifted: "
            f"tag={actual_tag_object}, commit={actual_commit}"
        )

    entries: list[dict[str, object]] = []
    for source_path, expected_blob in discover_sources().items():
        if expected_blob is None:
            blob = git_text("rev-parse", f"{PEELED_COMMIT}:{source_path}")
            source_kind = "final_tag_path"
        else:
            blob = expected_blob
            source_kind = "oracle_blob_binding"
        content = git_bytes("cat-file", "blob", blob)
        frozen_relative = Path("FrozenSources") / Path(source_path)
        destination = MANIFEST_PATH.parent / frozen_relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(content)
        entries.append(
            {
                "sourcePath": source_path,
                "frozenPath": frozen_relative.as_posix(),
                "gitBlobSha1": blob,
                "sourceKind": source_kind,
                "sha256": hashlib.sha256(content).hexdigest(),
                "byteCount": len(content),
            }
        )

    manifest = {
        "schemaVersion": 1,
        "tagName": TAG_NAME,
        "tagObject": TAG_OBJECT,
        "peeledCommit": PEELED_COMMIT,
        "entryCount": len(entries),
        "entries": entries,
    }
    MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST_PATH.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"FROZEN_UNITY_ORACLE_OK entries={len(entries)}")


if __name__ == "__main__":
    main()
