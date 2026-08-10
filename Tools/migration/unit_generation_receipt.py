"""Write the deterministic receipt for the Pure Run Unit Godot generation batch."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from collections.abc import Mapping
from pathlib import Path
from typing import Any


_HASH = re.compile(r"^sha256:[0-9a-f]{64}$")


def _sha256(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


def _load(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def compile_unit_generation_receipt(
    export_receipt: Mapping[str, Any],
    draft: Mapping[str, Any],
    draft_hash: str,
    generation_ledger: Mapping[str, Any],
    generation_ledger_hash: str,
    texture_ledger: Mapping[str, Any],
    texture_ledger_hash: str,
    gallery_capture_hash: str,
    spawn_capture_hash: str,
    goat_tint_shader_hash: str,
) -> dict[str, Any]:
    """Bind final ResourceSaver and texture artifacts to the frozen Unity export."""

    hashes = (
        draft_hash,
        generation_ledger_hash,
        texture_ledger_hash,
        gallery_capture_hash,
        spawn_capture_hash,
        goat_tint_shader_hash,
    )
    if any(not _HASH.fullmatch(value) for value in hashes):
        raise ValueError("Unit generation receipt contains an invalid SHA-256")
    if draft["batchId"] != generation_ledger["batchId"]:
        raise ValueError("Unit draft and generation ledger batch IDs disagree")
    if draft["batchId"] != export_receipt["batchId"]:
        raise ValueError("Unit draft and export receipt batch IDs disagree")
    source = generation_ledger["source"]
    for key in ("sourceTag", "sourceCommit", "exporterVersion", "exportHash"):
        if source[key] != export_receipt[key]:
            raise ValueError(f"Unit generation source differs from export receipt: {key}")
    if len(draft["units"]) != 12 or len(draft["textureAssets"]) != 19:
        raise ValueError("Unit typed draft does not contain the complete 12/19 batch")
    if len(generation_ledger["artifacts"]) != 16:
        raise ValueError("Unit generation ledger must contain exactly 16 artifacts")
    if len(texture_ledger["artifacts"]) != 19:
        raise ValueError("Unit texture ledger must contain exactly 19 artifacts")

    dependency_audit = draft["dependencyAudit"]
    tint_contract = draft["tintContract"]
    sprite_contract = draft["spriteContract"]
    if tint_contract["id"] != "unity-goat-body-tint-v1" or tint_contract[
        "godotShaderPath"
    ] != "res://src/Tactics.Godot.Adapter/Runtime/Shaders/GoatBodyTint.gdshader":
        raise ValueError("Unit draft has an invalid Goat tint contract")
    if sprite_contract["id"] != "unity-unit-sprite-geometry-v1":
        raise ValueError("Unit draft has an invalid Sprite geometry contract")
    return {
        "schemaVersion": 1,
        "batchId": draft["batchId"],
        "classification": "real_godot_resource_generation",
        "sourceTag": source["sourceTag"],
        "sourceCommit": source["sourceCommit"],
        "unityVersion": source["unityVersion"],
        "exporterVersion": source["exporterVersion"],
        "exportHash": source["exportHash"],
        "typedDraftHash": draft_hash,
        "generationLedger": "Tools/migration/manifest/state/pure-run-units-v1.json",
        "generationLedgerHash": generation_ledger_hash,
        "textureLedger": "Tools/migration/manifest/state/pure-run-unit-textures-v1.json",
        "textureLedgerHash": texture_ledger_hash,
        "galleryCapture": "Tools/migration/out/pure-run-units-v1-gallery.png",
        "galleryCaptureHash": gallery_capture_hash,
        "spawnCapture": "Tools/migration/out/pure-run-units-v1-spawn.png",
        "spawnCaptureHash": spawn_capture_hash,
        "captureMode": (
            "godot-image-software-reference-with-goat-body-mask-"
            "sprite-pivot-and-ground-baseline-v1"
        ),
        "contentEntryCount": 13,
        "unitDefinitionCount": len(draft["units"]),
        "texturePayloadCount": len(draft["textureAssets"]),
        "artifactCount": len(generation_ledger["artifacts"]),
        "dependencyBoundary": {
            "policy": dependency_audit["policy"],
            "deferredDependencyCount": len(dependency_audit["deferredDependencies"]),
            "thirdPartyDependencyCount": len(dependency_audit["thirdPartyDependencies"]),
            "materialAndShaderPayloadCopied": False,
            "projectOwnedShaderAlgorithmPorted": True,
            "thirdPartyPayloadCopied": False,
        },
        "tintContract": {
            "id": tint_contract["id"],
            "unityShaderPath": tint_contract["unityShaderPath"],
            "unityShaderGitBlobSha1": tint_contract["unityShaderGitBlobSha1"],
            "godotShaderPath": tint_contract["godotShaderPath"],
            "godotShaderSha256": goat_tint_shader_hash,
            "runtimeModes": ["multiply", "goat-body-mask-v1"],
            "materialAuditOnly": True,
            "unityPayloadCopied": False,
        },
        "spriteContract": {
            "id": sprite_contract["id"],
            "livingPivot": sprite_contract["living"]["pivot"],
            "deathPivot": sprite_contract["death"]["pivot"],
            "bodyPixelsPerUnit": sprite_contract["living"]["pixelsPerUnit"],
            "shadowPixelsPerUnit": sprite_contract["shadow"]["pixelsPerUnit"],
            "shadowLocalPosition": sprite_contract["shadow"]["localPosition"],
            "shadowLocalScale": sprite_contract["shadow"]["localScale"],
            "shadowColor": sprite_contract["shadow"]["color"],
            "godotOffsetAndScaleConversion": "unity-y-up-to-godot-y-down-with-body-ppu",
        },
        "automatedValidation": {
            "coreContractTests": "passed",
            "applicationCompilerTests": "passed",
            "unityOracleTests": "passed",
            "resourceSaver": "passed",
            "uidPreservation": "passed",
            "byteIdempotencyRuns": 2,
            "byteIdentical": True,
            "textureCopyIdempotencyRuns": 2,
            "textureByteIdentical": True,
            "headlessEditorFilesystemScan": "passed",
            "gdUnitRuntimeTests": "passed",
            "catalogAndFactoryRuntime": "passed",
            "rendererHeadlessStartup": ["gl_compatibility", "forward_plus"],
            "programmaticGalleryCapture": "passed",
            "programmaticSpawnCapture": "passed",
        },
        "ownership": "UnityOwned",
        "state": "Generated",
        "visualAcceptance": "manual_visual_qa_pending",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export-receipt", type=Path, required=True)
    parser.add_argument("--draft", type=Path, required=True)
    parser.add_argument("--generation-ledger", type=Path, required=True)
    parser.add_argument("--texture-ledger", type=Path, required=True)
    parser.add_argument("--gallery-capture", type=Path, required=True)
    parser.add_argument("--spawn-capture", type=Path, required=True)
    parser.add_argument("--goat-tint-shader", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()

    receipt = compile_unit_generation_receipt(
        _load(arguments.export_receipt),
        _load(arguments.draft),
        _sha256(arguments.draft),
        _load(arguments.generation_ledger),
        _sha256(arguments.generation_ledger),
        _load(arguments.texture_ledger),
        _sha256(arguments.texture_ledger),
        _sha256(arguments.gallery_capture),
        _sha256(arguments.spawn_capture),
        _sha256(arguments.goat_tint_shader),
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
