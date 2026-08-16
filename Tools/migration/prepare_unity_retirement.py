"""Build the exact, reviewable file manifest for retiring the archived Unity project."""

from __future__ import annotations

import hashlib
import json
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
GOVERNANCE = ROOT / "Tools/migration/manifest/retirement/unity-governance-retirement-v1.json"
OUTPUT = ROOT / "Tools/migration/manifest/retirement/unity-deletion-manifest-v1.json"
ARCHIVE_TAG = "unity-final-2026-08-08"

UNITY_ROOTS = ("Assets/", "Packages/", "ProjectSettings/", "UIElementsSchema/")
EXACT_TRANSITION_FILES = {
    "Tactics.Migration.slnx": "superseded_migration_solution",
    "Tools/migration/Verify-GodotMigration.ps1": "superseded_migration_verifier",
    "Tools/migration/Test-GodotOwnedWithoutUnity.ps1": "completed_transition_proof",
    "Tools/migration/tests/test_godot_mainline_verifier.py": "completed_transition_proof",
}
PRESERVED_PREFIXES = (
    "src/Tactics.Core/",
    "src/Tactics.Application/",
    "src/Tactics.FrozenOracle.Tests/",
    "godot/",
    "Tools/godot/",
    "Tools/gameplay-test-spec/",
    "Tests/gameplay-specs/godot/",
    "Tools/migration/manifest/retirement/",
    "Tools/migration/manifest/receipts/",
    "Tools/migration/manifest/golden/",
)
PRESERVED_EXACT = {"Tactics.Godot.slnx", "Tactics.Godot.runsettings"}


def git(*arguments: str) -> str:
    return subprocess.check_output(["git", *arguments], cwd=ROOT, text=True).strip()


def tracked_paths() -> list[str]:
    return [line for line in git("ls-files").splitlines() if line]


def tracked_blobs() -> dict[str, tuple[str, int]]:
    blob_ids: dict[str, str] = {}
    for line in git("ls-files", "-s").splitlines():
        metadata, path = line.split("\t", 1)
        blob_ids[path] = metadata.split()[1]
    process = subprocess.run(
        ["git", "cat-file", "--batch-check=%(objectname) %(objectsize)"],
        cwd=ROOT,
        input="\n".join(blob_ids.values()) + "\n",
        text=True,
        check=True,
        stdout=subprocess.PIPE,
    )
    sizes = {
        line.split()[0]: int(line.split()[1])
        for line in process.stdout.splitlines()
        if line
    }
    return {path: (blob, sizes[blob]) for path, blob in blob_ids.items()}


def classify(path: str, governance_paths: set[str]) -> str | None:
    if path.startswith(UNITY_ROOTS):
        return "unity_project_root"
    if path in governance_paths:
        return "unity_only_governance_tool_or_legacy_spec"
    return EXACT_TRANSITION_FILES.get(path)


def worktree_status() -> dict[str, str]:
    result: dict[str, str] = {}
    output = subprocess.check_output(
        ["git", "status", "--porcelain=v1", "--untracked-files=no"],
        cwd=ROOT,
        text=True,
    )
    for line in output.splitlines():
        if not line:
            continue
        result[line[3:].replace(" -> ", "\0").split("\0")[-1]] = line[:2]
    return result


def main() -> None:
    # The deletion manifest is immutable retirement evidence once every Unity
    # project root is physically absent.  Re-running this historical generator
    # after retirement must never replace that evidence with an empty manifest.
    unity_roots_exist = any(
        (ROOT / root.rstrip("/")).exists()
        for root in UNITY_ROOTS
    )
    if not unity_roots_exist:
        document = json.loads(OUTPUT.read_text(encoding="utf-8"))
        if document.get("manifestId") != "unity-deletion-manifest-v1":
            raise RuntimeError("Frozen Unity deletion manifest identity is invalid.")
        print(
            "UNITY_DELETION_MANIFEST_FROZEN "
            f"files={document['entryCount']} bytes={document['totalBytes']}"
        )
        return

    governance = json.loads(GOVERNANCE.read_text(encoding="utf-8"))
    governance_paths = {
        entry["path"]
        for entry in governance["entries"]
        if entry["retirementReason"] == "unity_only_governance_or_tooling"
    }
    dirty = worktree_status()
    blobs = tracked_blobs()
    entries = []
    for path in tracked_paths():
        reason = classify(path, governance_paths)
        if reason is None:
            continue
        blob, byte_count = blobs[path]
        worktree_sha256 = ""
        if path in dirty:
            worktree_sha256 = hashlib.sha256((ROOT / path).read_bytes()).hexdigest()
        entries.append(
            {
                "path": path,
                "retirementReason": reason,
                "gitBlobSha1": blob,
                "byteCount": byte_count,
                "worktreeStatus": dirty.get(path, ""),
                "worktreeSha256": worktree_sha256,
            }
        )

    paths = {entry["path"] for entry in entries}
    preserved_violations = sorted(
        path
        for path in paths
        if path in PRESERVED_EXACT or path.startswith(PRESERVED_PREFIXES)
    )
    if preserved_violations:
        raise SystemExit(f"Deletion manifest contains preserved paths: {preserved_violations}")
    missing_governance = sorted(governance_paths - paths)
    if missing_governance:
        raise SystemExit(f"Governance retirement paths are missing: {missing_governance}")

    counts: dict[str, int] = {}
    for entry in entries:
        reason = entry["retirementReason"]
        counts[reason] = counts.get(reason, 0) + 1
    document = {
        "schemaVersion": 1,
        "manifestId": "unity-deletion-manifest-v1",
        "baseline": {
            "branch": git("branch", "--show-current"),
            "commit": git("rev-parse", "HEAD"),
            "archiveTag": ARCHIVE_TAG,
            "archiveTagObject": git("rev-parse", ARCHIVE_TAG),
            "archiveCommit": git("rev-list", "-n", "1", ARCHIVE_TAG),
        },
        "entryCount": len(entries),
        "totalBytes": sum(entry["byteCount"] for entry in entries),
        "countsByReason": dict(sorted(counts.items())),
        "dirtyIncludedPaths": [entry["path"] for entry in entries if entry["worktreeStatus"]],
        "preservedContracts": {
            "exact": sorted(PRESERVED_EXACT),
            "prefixes": list(PRESERVED_PREFIXES),
        },
        "entries": entries,
    }
    canonical = json.dumps(document, indent=2) + "\n"
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(canonical, encoding="utf-8", newline="\n")
    print(
        "UNITY_DELETION_MANIFEST_OK "
        f"entries={document['entryCount']} bytes={document['totalBytes']} "
        f"dirty={len(document['dirtyIncludedPaths'])}"
    )


if __name__ == "__main__":
    main()
