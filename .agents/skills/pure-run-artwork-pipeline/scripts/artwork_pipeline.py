#!/usr/bin/env python3
"""Deterministic contract state machine for Pure Run artwork.

Image generation deliberately remains outside this program.  This CLI records
the immutable inputs and enforces every transition from ingestion to promotion.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import shutil
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any, Iterable

from PIL import Image, ImageDraw


SCHEMA_VERSION = 3
SUPPORTED_SCHEMA_VERSIONS = {1, 2, 3}
PIPELINE_REL = Path("Tools/artworks/pipeline")
KINDS = {"ground_character", "flying_character", "action_pose", "death_pose", "projectile", "tile"}
STATES = {
    "ready", "ingested", "prepared", "annotated", "calibrated", "review_pending",
    "approved", "rejected", "promoted", "technical_failed",
}
SERIES_STATES = {"pending", "active", "review_pending", "approved", "promoted", "exhausted", "provisional"}
FEEDBACK_VERDICTS = {"selected", "backup", "retry", "technical_failed", "exhausted"}
FEEDBACK_CATEGORIES = {
    "identity", "core_geometry", "pose_axis", "gaze", "topology", "occlusion",
    "equipment_state", "equipment_scale", "chroma", "processing",
}
FORMAL_DIRS = {"approved", "calibrated"}
MASK_COLORS = {
    "core": (255, 0, 0, 255),
    "head_appendage": (255, 255, 0, 255),
    "near_hand": (0, 255, 0, 255),
    "far_hand": (0, 200, 0, 255),
    "near_foot": (0, 128, 255, 255),
    "far_foot": (0, 0, 255, 255),
    "equipment": (255, 0, 255, 255),
    "wings": (0, 255, 255, 255),
    "effect": (255, 128, 0, 255),
    # These labels are intentionally forbidden for capsule characters whose
    # four paws attach directly to the core.  They make an invented arm/leg
    # reviewable and mechanically rejectable rather than prompt-only.
    "near_arm": (128, 0, 255, 255),
    "far_arm": (96, 0, 192, 255),
    "near_leg": (128, 128, 0, 255),
    "far_leg": (96, 96, 0, 255),
}
IDENTITY_MASK_COLORS = {
    "forehead_blaze": (255, 255, 255, 255),
    "alternate_ear": (0, 255, 255, 255),
    "alternate_coat": (255, 128, 0, 255),
}
OCCLUSION_LABELS = {"near_hand", "far_hand", "equipment"}
WAIVABLE_GATE_ISSUES = {"core_size_out_of_tolerance"}
ASSET_ROLES = {"component", "assembled_sprite"}
COMPONENT_KINDS = {"body", "equipment", "paw_overlay", "foot_overlay", "death_expression_overlay"}
SOURCE_MODES = {"generated", "derived", "pre_v3_import"}
ASSEMBLY_CANONICAL_LAYER_ORDER = (
    "far_foot_overlay", "far_paw_overlay", "body", "equipment",
    "near_paw_overlay", "near_foot_overlay",
)
ASSEMBLY_LAYER_ROLES = set(ASSEMBLY_CANONICAL_LAYER_ORDER)


class PipelineError(RuntimeError):
    pass


def canonical_bytes(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def pretty_bytes(value: Any) -> bytes:
    # Record identity uses canonical_bytes(). Human-facing registries preserve
    # insertion order so appending one provenance item does not rewrite every
    # pre-existing object's field order.
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def pixel_data(image: Image.Image) -> list[tuple[int, int, int, int]]:
    getter = getattr(image, "get_flattened_data", image.getdata)
    return list(getter())


def stable_id(prefix: str, payload: Any, length: int = 16) -> str:
    return f"{prefix}-{hashlib.sha256(canonical_bytes(payload)).hexdigest()[:length]}"


def write_json_idempotent(path: Path, value: Any, immutable: bool = False) -> bool:
    data = pretty_bytes(value)
    if path.exists():
        if path.read_bytes() == data:
            return False
        if immutable:
            raise PipelineError(f"immutable record differs: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.tmp")
    temporary.write_bytes(data)
    os.replace(temporary, path)
    return True


def load_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise PipelineError(f"cannot read JSON {path}: {exc}") from exc
    if not isinstance(value, dict) or value.get("schemaVersion") not in SUPPORTED_SCHEMA_VERSIONS:
        raise PipelineError(f"unsupported or missing schemaVersion in {path}")
    return value


@dataclass(frozen=True)
class Store:
    root: Path

    @property
    def pipeline(self) -> Path:
        return self.root / PIPELINE_REL

    def relative(self, path: Path | str, *, must_exist: bool = False) -> str:
        candidate = Path(path)
        if not candidate.is_absolute():
            candidate = self.root / candidate
        candidate = candidate.resolve()
        try:
            rel = candidate.relative_to(self.root.resolve())
        except ValueError as exc:
            raise PipelineError(f"path escapes repository root: {path}") from exc
        if must_exist and not candidate.is_file():
            raise PipelineError(f"file does not exist: {rel.as_posix()}")
        return rel.as_posix()

    def absolute(self, rel: str, *, must_exist: bool = False) -> Path:
        self.relative(rel, must_exist=must_exist)
        return (self.root / rel).resolve()

    def record(self, group: str, record_id: str) -> Path:
        return self.pipeline / group / f"{record_id}.json"


def contract_id(payload: dict[str, Any]) -> str:
    return stable_id("contract", payload)


def registered_asset_state(store: Store, rel: str) -> str | None:
    legacy = store.pipeline / "legacy-assets.json"
    if legacy.is_file():
        for asset in load_json(legacy).get("assets", []):
            if asset.get("path") == rel:
                return asset.get("state")
    for path in (store.pipeline / "attempts").glob("*.json"):
        attempt = load_json(path)
        if attempt.get("state") != "promoted":
            continue
        if any(value.get("path") == rel for value in attempt.get("artifacts", {}).get("promoted", {}).values() if isinstance(value, dict)):
            return "promoted"
    return None


def approved_mask_pair(store: Store, candidate_hash: str, mask_hash: str) -> bool:
    for path in (store.pipeline / "approvals").glob("*.json"):
        receipt = load_json(path)
        if (receipt.get("decision") == "approved"
                and receipt.get("candidateSha256") == candidate_hash
                and receipt.get("maskSha256") == mask_hash):
            return True
    return False


def required_review_keys(attempt: dict[str, Any], contract: dict[str, Any]) -> set[str]:
    if attempt.get("sourceMode") == "reviewed_import":
        return {"sizeComparison"}
    required = {"overlay", "preview128", "tile64x32"}
    if contract.get("identitySpec"):
        required.add("anchorTileCompare")
    if contract.get("occlusion"):
        required.add("depthReview")
    if contract.get("assetRole") == "assembled_sprite":
        required.add("assemblyLayerReview")
    return required


def approval_review_hashes(store: Store, attempt: dict[str, Any], contract: dict[str, Any]) -> dict[str, str]:
    review = attempt.get("artifacts", {}).get("review")
    required = required_review_keys(attempt, contract)
    if not review or set(review) != required:
        raise PipelineError("approval requires deterministic review outputs")
    hashes: dict[str, str] = {}
    for key in sorted(review):
        artifact = review[key]
        review_path = store.absolute(artifact["path"], must_exist=True)
        if sha256_file(review_path) != artifact["sha256"]:
            raise PipelineError("review artifact hash mismatch")
        hashes[key] = artifact["sha256"]
    return hashes


def core_size_exception_evidence(store: Store, report: dict[str, Any], contract: dict[str, Any]) -> dict[str, Any]:
    core_box = report.get("geometry", {}).get("core", {}).get("bbox")
    anchor = contract.get("anchor") or {}
    if not core_box or not anchor.get("maskPath"):
        raise PipelineError("core size exception requires candidate and anchor core geometry")
    anchor_mask = Image.open(store.absolute(anchor["maskPath"], must_exist=True)).convert("RGBA")
    anchor_box = bbox_for(pixel_data(anchor_mask), anchor_mask.size, MASK_COLORS["core"])
    if not anchor_box:
        raise PipelineError("core size exception requires an anchor core mask")
    candidate_size = [core_box[2] - core_box[0] + 1, core_box[3] - core_box[1] + 1]
    anchor_size = [anchor_box[2] - anchor_box[0] + 1, anchor_box[3] - anchor_box[1] + 1]
    return {
        "code": "core_size_out_of_tolerance",
        "candidateCoreSize": candidate_size,
        "anchorCoreSize": anchor_size,
        "delta": [candidate_size[0] - anchor_size[0], candidate_size[1] - anchor_size[1]],
        "tolerancePx": contract["tolerances"]["sizePx"],
    }


def gate_exception_evidence(store: Store, issues: list[str], report: dict[str, Any], contract: dict[str, Any]) -> list[dict[str, Any]]:
    requested = set(issues)
    if len(requested) != len(issues):
        raise PipelineError("exception issues must be unique")
    if not requested:
        raise PipelineError("exception approval requires at least one issue")
    unsupported = requested - WAIVABLE_GATE_ISSUES
    if unsupported:
        raise PipelineError(f"issue is not waivable: {sorted(unsupported)[0]}")
    report_issues = set(report.get("issues", []))
    if requested != report_issues:
        raise PipelineError("exception issues must exactly match the validation report")
    evidence = []
    for issue in sorted(requested):
        if issue == "core_size_out_of_tolerance":
            evidence.append(core_size_exception_evidence(store, report, contract))
    return evidence


def validate_exception_receipt(store: Store, attempt: dict[str, Any], receipt: dict[str, Any]) -> None:
    if receipt.get("approvalMode") != "gate-exception":
        raise PipelineError("approval is not a gate exception receipt")
    if receipt.get("reviewer") != "cty41":
        raise PipelineError("gate exception reviewer must be cty41")
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract_path = store.record("contracts", job["contractId"])
    contract = load_json(contract_path)
    report_artifact = attempt.get("report") or {}
    report_path = store.absolute(report_artifact.get("path", ""), must_exist=True)
    if sha256_file(report_path) != report_artifact.get("sha256"):
        raise PipelineError("validation report hash mismatch")
    report = load_json(report_path)
    if report.get("passed"):
        raise PipelineError("passing reports must use standard approval")
    issue_codes = [item.get("code") for item in receipt.get("waivedIssues", [])]
    expected_evidence = gate_exception_evidence(store, issue_codes, report, contract)
    expected = {
        "candidateSha256": candidate_artifact(attempt).get("sha256"),
        "maskSha256": candidate_mask_artifact(attempt).get("sha256"),
        "reportSha256": report_artifact.get("sha256"),
        "contractId": job["contractId"],
        "contractSha256": sha256_file(contract_path),
        "reviewSha256": approval_review_hashes(store, attempt, contract),
        "waivedIssues": expected_evidence,
    }
    if contract.get("compositionSpec") and attempt.get("sourceMode") != "reviewed_import":
        annotation_id = attempt.get("annotationId")
        if not annotation_id:
            raise PipelineError("schema v2 gate exception requires annotations")
        expected["annotation"] = {"annotationId": annotation_id,
                                  "sha256": sha256_file(store.record("annotations", annotation_id))}
    for key, value in expected.items():
        if receipt.get(key) != value:
            raise PipelineError(f"gate exception receipt mismatch: {key}")


def approve_anchor(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    candidate_rel = store.relative(args.candidate, must_exist=True)
    mask_rel = store.relative(args.mask, must_exist=True)
    review_rel = store.relative(args.review, must_exist=True)
    if registered_asset_state(store, candidate_rel) not in {"legacy-approved", "promoted"}:
        raise PipelineError("bootstrap anchor candidate must be legacy-approved or promoted")
    try:
        decided_at = datetime.fromisoformat(args.decided_at)
    except ValueError as exc:
        raise PipelineError("--decided-at must be an ISO-8601 timestamp") from exc
    if decided_at.tzinfo is None:
        raise PipelineError("--decided-at must include a timezone offset")
    payload = {
        "attemptId": f"bootstrap:{candidate_rel}",
        "candidateSha256": sha256_file(store.absolute(candidate_rel)),
        "maskSha256": sha256_file(store.absolute(mask_rel)),
        "reviewSha256": sha256_file(store.absolute(review_rel)),
        "candidatePath": candidate_rel, "maskPath": mask_rel, "reviewPath": review_rel,
        "reviewer": args.reviewer, "decision": "approved", "reason": args.reason,
        "decidedAt": args.decided_at,
    }
    approval_id = stable_id("approval", payload)
    receipt = {"schemaVersion": 1, "approvalId": approval_id, **payload}
    write_json_idempotent(store.record("approvals", approval_id), receipt, immutable=True)
    return receipt


def series_pose(series: dict[str, Any], pose_id: str) -> dict[str, Any]:
    pose = next((item for item in series.get("poses", []) if item.get("poseId") == pose_id), None)
    if pose is None:
        raise PipelineError(f"series pose does not exist: {pose_id}")
    return pose


def save_series(store: Store, series: dict[str, Any]) -> None:
    write_json_idempotent(store.record("series", series["seriesId"]), series)


def create_series(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    if not args.pose:
        raise PipelineError("create-series requires at least one --pose")
    if len(set(args.pose)) != len(args.pose):
        raise PipelineError("series pose IDs must be unique")
    if args.max_unique_outputs is not None and args.max_unique_outputs < 1:
        raise PipelineError("maxUniqueOutputs must be a positive integer or unlimited")
    record = {
        "schemaVersion": 1, "seriesId": args.series_id, "assetId": args.asset_id,
        "maxUniqueOutputs": args.max_unique_outputs, "currentPoseId": args.pose[0],
        "provisionalAnchorAttemptId": None,
        "poses": [{"poseId": value, "state": "active" if index == 0 else "pending",
                   "jobIds": [], "attemptIds": [], "selectedAttemptId": None}
                  for index, value in enumerate(args.pose)],
    }
    write_json_idempotent(store.record("series", args.series_id), record, immutable=True)
    return record


def set_series_output_limit(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    series = load_json(store.record("series", args.series_id))
    try:
        decided_at = datetime.fromisoformat(args.decided_at)
    except ValueError as exc:
        raise PipelineError("--decided-at must be an ISO-8601 timestamp") from exc
    if decided_at.tzinfo is None:
        raise PipelineError("--decided-at must include a timezone offset")
    if args.unlimited:
        new_limit = None
    else:
        new_limit = args.max_unique_outputs
        if new_limit is None or new_limit < 1:
            raise PipelineError("provide --unlimited or a positive --max-unique-outputs")
    previous_limit = series.get("maxUniqueOutputs")
    payload = {
        "seriesId": series["seriesId"], "previousMaxUniqueOutputs": previous_limit,
        "maxUniqueOutputs": new_limit, "reviewer": args.reviewer,
        "reason": args.reason, "decidedAt": args.decided_at,
    }
    change_id = stable_id("series-limit-change", payload)
    record = {"schemaVersion": 1, "seriesLimitChangeId": change_id, **payload}
    write_json_idempotent(store.record("series-limit-changes", change_id), record, immutable=True)
    series["maxUniqueOutputs"] = new_limit
    series.setdefault("limitChangeIds", [])
    if change_id not in series["limitChangeIds"]:
        series["limitChangeIds"].append(change_id)
        save_series(store, series)
    return record


def create_contract(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    if args.kind not in KINDS:
        raise PipelineError(f"unsupported asset kind: {args.kind}")
    anchor = None
    if args.anchor:
        anchor_rel = store.relative(args.anchor, must_exist=True)
        if registered_asset_state(store, anchor_rel) not in {"legacy-approved", "promoted"}:
            raise PipelineError("core anchor must be legacy-approved or promoted")
        anchor = {"path": anchor_rel, "sha256": sha256_file(store.absolute(anchor_rel))}
        if args.anchor_mask:
            mask_rel = store.relative(args.anchor_mask, must_exist=True)
            mask_hash = sha256_file(store.absolute(mask_rel))
            if not approved_mask_pair(store, anchor["sha256"], mask_hash):
                raise PipelineError("core anchor mask requires an approved human receipt for the same candidate and mask hashes")
            anchor.update({"maskPath": mask_rel, "maskSha256": mask_hash})
    elif args.anchor_mask:
        raise PipelineError("--anchor-mask requires --anchor")
    layer_rules: dict[str, str] = {}
    for value in getattr(args, "layer_rule", []) or []:
        try:
            label, rule = value.split("=", 1)
        except ValueError as exc:
            raise PipelineError("--layer-rule must be LABEL=behind-core") from exc
        if label not in OCCLUSION_LABELS or rule != "behind-core":
            raise PipelineError(f"unsupported layer rule: {value}")
        if label in layer_rules:
            raise PipelineError(f"duplicate layer rule: {label}")
        layer_rules[label] = rule
    visibility_caps: dict[str, float] = {}
    for value in getattr(args, "visibility_cap", []) or []:
        try:
            label, raw_ratio = value.split("=", 1)
            ratio = float(raw_ratio)
        except (ValueError, TypeError) as exc:
            raise PipelineError("--visibility-cap must be LABEL=RATIO") from exc
        if label not in OCCLUSION_LABELS or not 0 < ratio <= 1:
            raise PipelineError(f"invalid visibility cap: {value}")
        if label in visibility_caps:
            raise PipelineError(f"duplicate visibility cap: {label}")
        visibility_caps[label] = ratio
    if set(visibility_caps) - set(layer_rules):
        raise PipelineError("visibility caps require a layer rule for the same label")
    occlusion = None
    if layer_rules:
        occlusion = {"layerRules": layer_rules, "visibilityCaps": visibility_caps}
    composition = None
    composition_id = getattr(args, "composition_id", None)
    if composition_id:
        composition_record = load_json(store.record("compositions", composition_id))
        composition = {
            "compositionId": composition_id,
            "sha256": sha256_file(store.record("compositions", composition_id)),
        }
    asset_role = getattr(args, "asset_role", None)
    component_kind = getattr(args, "component_kind", None)
    source_mode = getattr(args, "source_mode", None)
    if asset_role and asset_role not in ASSET_ROLES:
        raise PipelineError(f"unsupported asset role: {asset_role}")
    if asset_role == "component" and component_kind not in COMPONENT_KINDS:
        raise PipelineError("component contracts require --component-kind")
    if asset_role != "component" and component_kind:
        raise PipelineError("--component-kind is only valid for component contracts")
    if asset_role and source_mode not in SOURCE_MODES:
        raise PipelineError("schema v3 contracts require --source-mode")
    writes_v2 = hasattr(args, "composition_id")
    high_risk = (asset_role != "component"
                 and (args.kind in {"action_pose", "death_pose"} or bool(occlusion)
                      or bool(getattr(args, "pose_reference", False))))
    if writes_v2 and high_risk and not composition:
        raise PipelineError("high-risk schema v2 contract requires --composition-id")
    identity_spec = None
    identity_anchor_mask = getattr(args, "identity_anchor_mask", None)
    if identity_anchor_mask:
        if not anchor:
            raise PipelineError("identity anchor mask requires --anchor")
        identity_rel = store.relative(identity_anchor_mask, must_exist=True)
        identity_spec = {
            "anchorMaskPath": identity_rel,
            "anchorMaskSha256": sha256_file(store.absolute(identity_rel)),
            "foreheadBlazeMinIou": getattr(args, "forehead_blaze_min_iou", 0.45),
            "foreheadBlazeAreaRatio": [0.65, 1.45],
        }
    payload = {
        "assetId": args.asset_id,
        "approvedAssetId": args.approved_asset_id or args.asset_id,
        "kind": args.kind,
        "direction": args.direction,
        "pose": args.pose,
        "anchor": anchor,
        "maskRequired": bool(args.mask_required),
        "noArms": bool(args.no_arms),
        "handSides": {"near_hand": args.near_hand_side, "far_hand": args.far_hand_side},
        "occlusion": occlusion,
        "compositionSpec": composition,
        "identitySpec": identity_spec,
        "requiresInvocation": ((writes_v2 and high_risk and source_mode != "derived")
                               or (asset_role == "component" and source_mode == "generated")),
        "tolerances": {"sizePx": args.size_tolerance, "centerPx": args.center_tolerance},
        "outputs": {"master": store.relative(args.output_master), "preview": store.relative(args.output_preview)},
        "rights": {"rightsHolder": args.rights_holder, "license": args.license, "provenance": args.provenance},
    }
    if asset_role:
        payload.update({
            "assetRole": asset_role,
            "componentKind": component_kind,
            "sourceMode": source_mode,
            "runtimeEligible": asset_role == "assembled_sprite",
        })
    cid = contract_id(payload)
    version = 3 if asset_role else (2 if writes_v2 else 1)
    record = {"schemaVersion": version, "contractId": cid, **payload}
    write_json_idempotent(store.record("contracts", cid), record, immutable=True)
    return record


def create_job(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    contract = load_json(store.record("contracts", args.contract_id))
    pose_guide = None
    pose_guide_id = getattr(args, "pose_guide_id", None)
    if contract.get("requiresInvocation"):
        if not pose_guide_id:
            raise PipelineError("high-risk schema v2 job requires --pose-guide-id")
        guide_record = load_json(store.record("pose-guides", pose_guide_id))
        if guide_record.get("compositionId") != contract["compositionSpec"]["compositionId"]:
            raise PipelineError("pose guide does not match contract composition")
        pose_guide = {"poseGuideId": pose_guide_id,
                      "sha256": sha256_file(store.record("pose-guides", pose_guide_id))}
    series_binding = None
    series_id = getattr(args, "series_id", None)
    pose_id = getattr(args, "pose_id", None)
    if series_id or pose_id:
        if not series_id or not pose_id:
            raise PipelineError("--series-id and --pose-id must be provided together")
        series = load_json(store.record("series", series_id))
        pose = series_pose(series, pose_id)
        if pose["state"] not in {"active", "provisional"}:
            raise PipelineError("job can only be created for the active series pose")
        concept_only = bool(series.get("provisionalAnchorAttemptId") and pose_id != "idle-dr")
        if concept_only and any(part in FORMAL_DIRS for part in Path(contract["outputs"]["master"]).parts):
            raise PipelineError("provisional-anchor jobs must write to a non-formal concept path")
        series_binding = {"seriesId": series_id, "poseId": pose_id}
    else:
        concept_only = False
    inputs = []
    for role_path in args.input:
        try:
            role, raw_path = role_path.split("=", 1)
        except ValueError as exc:
            raise PipelineError("--input must be ROLE=PATH") from exc
        rel = store.relative(raw_path, must_exist=True)
        inputs.append({"role": role, "path": rel, "sha256": sha256_file(store.absolute(rel))})
    inputs.sort(key=lambda item: (item["role"], item["path"]))
    for item in inputs:
        if "anchor" in item["role"].lower() or "mother" in item["role"].lower():
            anchor = contract.get("anchor")
            if not anchor or item["path"] != anchor["path"]:
                raise PipelineError("mother/anchor input must equal the contract core anchor")
    prompt_rel = store.relative(args.prompt, must_exist=True)
    requirements = None
    if contract.get("occlusion"):
        requirements = {
            "occlusion": contract["occlusion"],
            "imageGenDirective": "Draw behind-core equipment and both hand paws first, then draw the capsule body over their inner portions; only outer arcs may remain visible.",
        }
    payload = {
        "contractId": contract["contractId"],
        "contractSha256": sha256_file(store.record("contracts", args.contract_id)),
        "prompt": {"path": prompt_rel, "sha256": sha256_file(store.absolute(prompt_rel))},
        "inputs": inputs,
        "target": {"direction": contract["direction"], "pose": contract["pose"]},
        "series": series_binding,
        "conceptOnly": concept_only,
        "contractRequirements": requirements,
        "requiresInvocation": bool(contract.get("requiresInvocation")),
        "poseGuide": pose_guide,
    }
    jid = stable_id("job", payload)
    record = {"schemaVersion": contract.get("schemaVersion", 1), "jobId": jid, "state": "ready", **payload}
    write_json_idempotent(store.record("jobs", jid), record, immutable=True)
    packet = store.pipeline / "packets" / f"{jid}.json"
    write_json_idempotent(packet, record, immutable=True)
    if series_binding and jid not in pose["jobIds"]:
        pose["jobIds"].append(jid)
        save_series(store, series)
    return record


def list_attempts(store: Store, job_id: str) -> list[dict[str, Any]]:
    result = []
    for path in sorted((store.pipeline / "attempts").glob(f"{job_id}-*.json")):
        result.append(load_json(path))
    return result


def retry(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    job = load_json(store.record("jobs", args.job_id))
    attempts = list_attempts(store, args.job_id)
    feedback = None
    if attempts and job.get("series"):
        latest = attempts[-1]
        feedback_id = getattr(args, "feedback_id", None)
        if not feedback_id:
            raise PipelineError("retry requires --feedback-id for the previous attempt")
        feedback = load_json(store.record("feedback", feedback_id))
        if feedback.get("attemptId") != latest["attemptId"] or latest.get("feedbackId") != feedback_id:
            raise PipelineError("retry feedback must belong to the latest attempt")
        allowed_verdicts = {"retry", "technical_failed"}
        if getattr(args, "technical_remediation", False) and latest.get("state") == "technical_failed":
            allowed_verdicts.add("selected")
        if feedback.get("verdict") == "exhausted":
            binding = job["series"]
            series = load_json(store.record("series", binding["seriesId"]))
            if series.get("maxUniqueOutputs") is None:
                # An immutable exhausted receipt records the historical decision.
                # If the series limit is later explicitly removed, it may seed the
                # next retry without rewriting that receipt.
                allowed_verdicts.add("exhausted")
        if feedback.get("verdict") not in allowed_verdicts:
            raise PipelineError("selected feedback requires --technical-remediation on a technical failure")
    ordinal = len(attempts) + 1
    parent = args.parent_attempt
    if parent:
        parent_record = load_json(store.record("attempts", parent))
        if parent_record["jobId"] != args.job_id:
            raise PipelineError("parent attempt belongs to another job")
    aid = f"{args.job_id}-a{ordinal:03d}"
    record = {"schemaVersion": job.get("schemaVersion", 1), "attemptId": aid, "jobId": args.job_id, "ordinal": ordinal,
              "parentAttemptId": parent, "retryFeedbackId": getattr(args, "feedback_id", None), "promptDelta": feedback.get("nextPromptDelta") if feedback else None,
              "technicalRemediation": bool(getattr(args, "technical_remediation", False)),
              "state": "ready", "artifacts": {}, "report": None, "approvalId": None, "feedbackId": None}
    write_json_idempotent(store.record("attempts", aid), record, immutable=True)
    binding = job.get("series")
    if binding:
        series = load_json(store.record("series", binding["seriesId"]))
        pose = series_pose(series, binding["poseId"])
        if aid not in pose["attemptIds"]:
            pose["attemptIds"].append(aid)
            if pose["state"] == "exhausted" and series.get("maxUniqueOutputs") is None:
                pose["state"] = "active"
            save_series(store, series)
        packet = {"schemaVersion": 1, "attemptId": aid, "jobId": args.job_id,
                  "promptDelta": record["promptDelta"], "inputs": job["inputs"], "target": job["target"],
                  "conceptOnly": bool(job.get("conceptOnly")),
                  "contractRequirements": job.get("contractRequirements")}
        write_json_idempotent(store.pipeline / "packets" / f"{aid}.json", packet, immutable=True)
    return record


def save_attempt(store: Store, attempt: dict[str, Any]) -> None:
    if attempt.get("state") not in STATES:
        raise PipelineError(f"invalid attempt state: {attempt.get('state')}")
    write_json_idempotent(store.record("attempts", attempt["attemptId"]), attempt)


def transition(attempt: dict[str, Any], expected: Iterable[str], target: str) -> None:
    if attempt["state"] == target:
        return
    if attempt["state"] not in set(expected):
        raise PipelineError(f"illegal transition {attempt['state']} -> {target}")
    attempt["state"] = target


def copy_bound(store: Store, source: str, destination: str) -> dict[str, str]:
    src_rel = store.relative(source, must_exist=True)
    dst_rel = store.relative(destination)
    src = store.absolute(src_rel)
    dst = store.absolute(dst_rel)
    digest = sha256_file(src)
    if dst.exists():
        if sha256_file(dst) != digest:
            raise PipelineError(f"destination exists with different bytes: {dst_rel}")
    else:
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, dst)
    return {"path": dst_rel, "sha256": digest}


def transaction_record(store: Store, operation: str, payload: dict[str, Any], state: str) -> tuple[str, dict[str, Any]]:
    transaction_id = stable_id("transaction", {"operation": operation, **payload})
    path = store.record("transactions", transaction_id)
    if path.is_file():
        record = load_json(path)
        if record.get("operation") != operation or record.get("payload") != payload:
            raise PipelineError("transaction identity collision")
    else:
        record = {"schemaVersion": 2, "transactionId": transaction_id,
                  "operation": operation, "payload": payload, "state": "started"}
    record["state"] = state
    write_json_idempotent(path, record)
    return transaction_id, record


def ingest(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    source_rel = store.relative(args.source, must_exist=True)
    digest = sha256_file(store.absolute(source_rel))
    transaction_payload = {"attemptId": attempt["attemptId"], "sourceSha256": digest,
                           "invocationId": getattr(args, "invocation_id", None)}
    transaction_id, _ = transaction_record(store, "ingest", transaction_payload, "started")
    existing = attempt["artifacts"].get("raw")
    if existing:
        if existing["sha256"] != digest:
            raise PipelineError("attempt already contains a different ImageGen output")
        transaction_record(store, "ingest", transaction_payload, "committed")
        return attempt
    if attempt["state"] != "ready":
        raise PipelineError("ingest requires ready attempt")
    job = load_json(store.record("jobs", attempt["jobId"]))
    invocation_id = getattr(args, "invocation_id", None)
    if job.get("requiresInvocation"):
        if not invocation_id:
            raise PipelineError("schema v2 ingest requires --invocation-id")
        invocation = load_json(store.record("generation-invocations", invocation_id))
        if invocation.get("attemptId") != attempt["attemptId"] or invocation.get("state") != "started":
            raise PipelineError("generation invocation does not match ready attempt")
    binding = job.get("series")
    if binding:
        series = load_json(store.record("series", binding["seriesId"]))
        pose = series_pose(series, binding["poseId"])
        unique_hashes = set()
        for attempt_id in pose["attemptIds"]:
            candidate = load_json(store.record("attempts", attempt_id))
            raw_hash = candidate.get("artifacts", {}).get("raw", {}).get("sha256")
            if raw_hash:
                unique_hashes.add(raw_hash)
        limit = series.get("maxUniqueOutputs")
        if limit is not None and digest not in unique_hashes and len(unique_hashes) >= limit:
            raise PipelineError(f"pose {pose['poseId']} already reached its unique ImageGen output limit")
    suffix = store.absolute(source_rel).suffix.lower() or ".png"
    dst = store.pipeline / "artifacts" / attempt["jobId"] / attempt["attemptId"] / f"raw{suffix}"
    source_artifact = {"path": source_rel, "sha256": digest}
    attempt["artifacts"]["source"] = source_artifact
    attempt["artifacts"]["raw"] = copy_bound(store, source_rel, store.relative(dst))
    if invocation_id:
        delivery_payload = {"invocationId": invocation_id, "attemptId": attempt["attemptId"], "rawSha256": digest}
        delivery_id = stable_id("generation-delivery", delivery_payload)
        delivery = {"schemaVersion": 2, "generationDeliveryId": delivery_id, **delivery_payload}
        write_json_idempotent(store.record("generation-deliveries", delivery_id), delivery, immutable=True)
        attempt["generationInvocationId"] = invocation_id
        attempt["generationDeliveryId"] = delivery_id
    register_public_artifacts(store, [source_artifact, attempt["artifacts"]["raw"]], "project-owned-gpt-generated")
    transition(attempt, {"ready"}, "ingested")
    save_attempt(store, attempt)
    transaction_record(store, "ingest", transaction_payload, "committed")
    return attempt


def prepare_image(source: Path, destination: Path, chroma: str | None, chroma_tolerance: int = 0) -> None:
    image = Image.open(source).convert("RGBA")
    pixels = pixel_data(image)
    key = None
    if chroma:
        value = chroma.lstrip("#")
        if len(value) != 6:
            raise PipelineError("--chroma must be RRGGBB")
        key = tuple(int(value[index:index + 2], 16) for index in (0, 2, 4))
    if not 0 <= chroma_tolerance <= 255:
        raise PipelineError("--chroma-tolerance must be between 0 and 255")
    cleaned = []
    for red, green, blue, alpha in pixels:
        distance_squared = sum((value - target) ** 2 for value, target in zip((red, green, blue), key)) if key else None
        if key and distance_squared is not None and distance_squared <= chroma_tolerance ** 2:
            cleaned.append((0, 0, 0, 0))
        elif alpha == 0:
            cleaned.append((0, 0, 0, 0))
        else:
            cleaned.append((red, green, blue, alpha))
    image.putdata(cleaned)
    destination.parent.mkdir(parents=True, exist_ok=True)
    image.save(destination, format="PNG", optimize=False, compress_level=9)


def clean_resampled_chroma(image: Image.Image, chroma: str | None, tolerance: int) -> Image.Image:
    if not chroma:
        return normalize_transparent_rgb(image)
    value = chroma.lstrip("#")
    key = tuple(int(value[index:index + 2], 16) for index in (0, 2, 4))
    cleaned = []
    for red, green, blue, alpha in pixel_data(image.convert("RGBA")):
        distance_squared = sum((channel - target) ** 2 for channel, target in zip((red, green, blue), key))
        if distance_squared <= tolerance ** 2 or (green > 180 and green > red + 80 and green > blue + 80):
            cleaned.append((0, 0, 0, 0))
        elif alpha == 0:
            cleaned.append((0, 0, 0, 0))
        else:
            cleaned.append((red, green, blue, alpha))
    result = Image.new("RGBA", image.size)
    result.putdata(cleaned)
    return result


def prepare(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    transaction_payload = {"attemptId": attempt["attemptId"], "chroma": args.chroma,
                           "chromaTolerance": getattr(args, "chroma_tolerance", 0)}
    transaction_record(store, "prepare", transaction_payload, "started")
    preparation = {"chroma": args.chroma.lower().lstrip("#") if args.chroma else None,
                   "chromaTolerance": getattr(args, "chroma_tolerance", 0)}
    existing = attempt["artifacts"].get("prepared")
    if existing:
        if sha256_file(store.absolute(existing["path"], must_exist=True)) != existing["sha256"]:
            transaction_record(store, "prepare", transaction_payload, "aborted")
            raise PipelineError("prepared artifact hash mismatch")
        recorded = attempt.get("preparation")
        if recorded is not None and recorded != preparation:
            # This is an intentional no-op rejection, not an interrupted
            # transaction.  Preserve its audit record without blocking strict
            # checks for the otherwise valid attempt.
            transaction_record(store, "prepare", transaction_payload, "aborted")
            raise PipelineError("attempt already uses different preparation parameters")
        transaction_record(store, "prepare", transaction_payload, "committed")
        return attempt
    if attempt["state"] != "ingested":
        raise PipelineError("prepare requires ingested attempt")
    raw = store.absolute(attempt["artifacts"]["raw"]["path"], must_exist=True)
    if sha256_file(raw) != attempt["artifacts"]["raw"]["sha256"]:
        raise PipelineError("raw artifact hash mismatch")
    dst = store.pipeline / "artifacts" / attempt["jobId"] / attempt["attemptId"] / "prepared.png"
    prepare_image(raw, dst, args.chroma, preparation["chromaTolerance"])
    attempt["artifacts"]["prepared"] = {"path": store.relative(dst), "sha256": sha256_file(dst)}
    attempt["preparation"] = preparation
    register_public_artifacts(store, [attempt["artifacts"]["prepared"]], "project-owned-derived-artwork")
    transition(attempt, {"ingested"}, "prepared")
    save_attempt(store, attempt)
    transaction_record(store, "prepare", transaction_payload, "committed")
    return attempt


def attach_mask(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    prepared = store.absolute(attempt["artifacts"].get("prepared", {}).get("path", ""), must_exist=True)
    mask_rel = store.relative(args.mask, must_exist=True)
    mask = store.absolute(mask_rel)
    with Image.open(prepared) as base, Image.open(mask) as overlay:
        if base.size != overlay.size:
            raise PipelineError("semantic mask must use the prepared image coordinate system")
    digest = sha256_file(mask)
    existing = attempt["artifacts"].get("mask")
    if existing and existing["sha256"] != digest:
        raise PipelineError("attempt already contains a different semantic mask")
    if not existing:
        if attempt["state"] != "prepared":
            raise PipelineError("attach-mask requires prepared attempt")
        dst = store.pipeline / "artifacts" / attempt["jobId"] / attempt["attemptId"] / "mask.png"
        attempt["artifacts"]["maskSource"] = {"path": mask_rel, "sha256": digest}
        attempt["artifacts"]["mask"] = copy_bound(store, mask_rel, store.relative(dst))
        register_public_artifacts(store, [attempt["artifacts"]["maskSource"], attempt["artifacts"]["mask"]], "project-owned-semantic-mask")
        transition(attempt, {"prepared"}, "annotated")
        save_attempt(store, attempt)
    elif not attempt["artifacts"].get("maskSource"):
        attempt["artifacts"]["maskSource"] = {"path": mask_rel, "sha256": digest}
        register_public_artifacts(store, [attempt["artifacts"]["maskSource"]], "project-owned-semantic-mask")
        save_attempt(store, attempt)
    return attempt


def attach_identity_mask(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    """Bind an identity-only mask after core calibration.

    Identity markings deliberately use a separate flat mask so that a forehead
    blaze never punches a hole through the core geometry mask.
    """
    attempt = load_json(store.record("attempts", args.attempt_id))
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    if not contract.get("identitySpec"):
        raise PipelineError("identity mask requires an identity-spec contract")
    candidate = store.absolute(candidate_artifact(attempt).get("path", ""), must_exist=True)
    mask_rel = store.relative(args.mask, must_exist=True)
    mask = store.absolute(mask_rel)
    with Image.open(candidate) as image, Image.open(mask) as identity:
        if image.size != identity.size:
            raise PipelineError("identity mask must use the calibrated candidate coordinate system")
    digest = sha256_file(mask)
    existing = attempt["artifacts"].get("identityMask")
    if existing and existing["sha256"] != digest:
        raise PipelineError("attempt already contains a different identity mask")
    if not existing:
        dst = store.pipeline / "artifacts" / attempt["jobId"] / attempt["attemptId"] / "identity-mask.png"
        attempt["artifacts"]["identityMaskSource"] = {"path": mask_rel, "sha256": digest}
        attempt["artifacts"]["identityMask"] = copy_bound(store, mask_rel, store.relative(dst))
        register_public_artifacts(store, [attempt["artifacts"]["identityMaskSource"], attempt["artifacts"]["identityMask"]],
                                  "project-owned-identity-mask")
        save_attempt(store, attempt)
    return attempt


def candidate_artifact(attempt: dict[str, Any]) -> dict[str, str]:
    return attempt.get("artifacts", {}).get("calibrated") or attempt.get("artifacts", {}).get("prepared", {})


def candidate_mask_artifact(attempt: dict[str, Any]) -> dict[str, str]:
    return attempt.get("artifacts", {}).get("calibratedMask") or attempt.get("artifacts", {}).get("mask", {})


def normalize_transparent_rgb(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    rgba.putdata([(0, 0, 0, 0) if alpha == 0 else (red, green, blue, alpha)
                  for red, green, blue, alpha in pixel_data(rgba)])
    return rgba


def calibrate_core(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    existing_image = attempt.get("artifacts", {}).get("calibrated")
    existing_mask = attempt.get("artifacts", {}).get("calibratedMask")
    if existing_image or existing_mask:
        if not existing_image or not existing_mask:
            raise PipelineError("calibration artifacts are incomplete")
        for artifact in (existing_image, existing_mask):
            if sha256_file(store.absolute(artifact["path"], must_exist=True)) != artifact["sha256"]:
                raise PipelineError("calibration artifact hash mismatch")
        return attempt
    if attempt["state"] != "annotated":
        raise PipelineError("calibrate-core requires annotated attempt")
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    anchor = contract.get("anchor") or {}
    if not anchor.get("maskPath"):
        raise PipelineError("calibrate-core requires an approved anchor mask")
    source_image = Image.open(store.absolute(attempt["artifacts"]["prepared"]["path"], must_exist=True)).convert("RGBA")
    source_mask = Image.open(store.absolute(attempt["artifacts"]["mask"]["path"], must_exist=True)).convert("RGBA")
    anchor_mask = Image.open(store.absolute(anchor["maskPath"], must_exist=True)).convert("RGBA")
    source_box = bbox_for(pixel_data(source_mask), source_mask.size, MASK_COLORS["core"])
    anchor_box = bbox_for(pixel_data(anchor_mask), anchor_mask.size, MASK_COLORS["core"])
    if not source_box or not anchor_box:
        raise PipelineError("source and anchor masks must both contain a core region")
    source_height = source_box[3] - source_box[1] + 1
    anchor_height = anchor_box[3] - anchor_box[1] + 1
    scale = anchor_height / source_height
    scaled_size = (max(1, round(source_image.width * scale)), max(1, round(source_image.height * scale)))
    scaled_image = source_image.resize(scaled_size, Image.Resampling.LANCZOS)
    scaled_mask = source_mask.resize(scaled_size, Image.Resampling.NEAREST)
    source_center = ((source_box[0] + source_box[2]) / 2, (source_box[1] + source_box[3]) / 2)
    anchor_center = ((anchor_box[0] + anchor_box[2]) / 2, (anchor_box[1] + anchor_box[3]) / 2)
    offset = (round(anchor_center[0] - source_center[0] * scale),
              round(anchor_center[1] - source_center[1] * scale))
    if contract["kind"] in {"ground_character", "action_pose"}:
        scaled_pixels = pixel_data(scaled_mask)
        foot_boxes = [bbox_for(scaled_pixels, scaled_mask.size, MASK_COLORS[label]) for label in ("near_foot", "far_foot")]
        foot_bottoms = [box[3] for box in foot_boxes if box]
        if foot_bottoms:
            offset = (offset[0], 236 - max(foot_bottoms))
    output_image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    output_mask = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    output_image.alpha_composite(scaled_image, offset)
    output_mask.alpha_composite(scaled_mask, offset)
    preparation = attempt.get("preparation", {})
    output_image = clean_resampled_chroma(output_image, preparation.get("chroma"), preparation.get("chromaTolerance", 0))
    out_dir = store.pipeline / "artifacts" / attempt["jobId"] / attempt["attemptId"]
    image_path = out_dir / "calibrated.png"
    mask_path = out_dir / "calibrated-mask.png"
    output_image.save(image_path, format="PNG", optimize=False, compress_level=9)
    output_mask.save(mask_path, format="PNG", optimize=False, compress_level=9)
    attempt["artifacts"]["calibrated"] = {"path": store.relative(image_path), "sha256": sha256_file(image_path)}
    attempt["artifacts"]["calibratedMask"] = {"path": store.relative(mask_path), "sha256": sha256_file(mask_path)}
    attempt["calibration"] = {"method": "uniform-core-height", "scale": scale, "offset": list(offset),
                              "sourceCoreBbox": list(source_box), "anchorCoreBbox": list(anchor_box)}
    register_public_artifacts(store, [attempt["artifacts"]["calibrated"], attempt["artifacts"]["calibratedMask"]],
                              "project-owned-deterministically-core-calibrated-artwork")
    transition(attempt, {"annotated"}, "calibrated")
    save_attempt(store, attempt)
    return attempt


def bbox_for(pixels: list[tuple[int, int, int, int]], size: tuple[int, int], color: tuple[int, int, int, int]) -> tuple[int, int, int, int] | None:
    width, _ = size
    points = [(index % width, index // width) for index, value in enumerate(pixels) if value == color]
    if not points:
        return None
    xs, ys = zip(*points)
    return min(xs), min(ys), max(xs), max(ys)


def inspect_technical(path: Path, kind: str, *, require_master_canvas: bool = True) -> tuple[dict[str, Any], list[str]]:
    image = Image.open(path).convert("RGBA")
    pixels = pixel_data(image)
    alpha = image.getchannel("A")
    bbox = alpha.getbbox()
    issues = []
    if require_master_canvas and image.size != (256, 256):
        issues.append("master_not_256x256")
    max_x, max_y = image.width - 1, image.height - 1
    if require_master_canvas and any(image.getpixel(point)[3] != 0 for point in ((0, 0), (max_x, 0), (0, max_y), (max_x, max_y))):
        issues.append("corner_not_transparent")
    if any(a == 0 and (r or g or b) for r, g, b, a in pixels):
        issues.append("transparent_rgb_nonzero")
    if any(a and ((r, g, b) == (0, 255, 0) or (r, g, b) == (255, 0, 255)) for r, g, b, a in pixels):
        issues.append("exact_chroma_residue")
    if any(a and (abs(r) <= 12 and abs(g - 255) <= 12 and abs(b) <= 12 or abs(r - 255) <= 12 and abs(g) <= 12 and abs(b - 255) <= 12)
           for r, g, b, a in pixels):
        issues.append("chroma_fringe")
    if bbox is None:
        issues.append("empty_alpha")
    return {"size": list(image.size), "alphaBbox": list(bbox) if bbox else None}, issues


def composition_gem_issues(weapon: dict[str, Any], regions: dict[str, Any]) -> list[str]:
    """Validate the manually annotated unique gem against a v2 composition spec."""
    gem_window = weapon.get("gemWindow")
    if not gem_window:
        return []
    gem = regions.get("gemRegion")
    if not gem:
        return ["gem_missing"]
    def overlap(a: list[int], b: list[int]) -> bool:
        return not (a[2] < b[0] or a[0] > b[2] or a[3] < b[1] or a[1] > b[3])
    issues = []
    gem_area = max(0, gem[2] - gem[0] + 1) * max(0, gem[3] - gem[1] + 1)
    if gem_area > weapon.get("maxGemAreaPx", gem_area):
        issues.append("gem_too_large")
    if not overlap(gem, gem_window):
        issues.append("gem_outside_guard_window")
    for forbidden in weapon.get("forbiddenGemRegions", []):
        if overlap(gem, forbidden):
            issues.append("gem_enters_forbidden_region")
    if regions.get("extraGemRegions"):
        issues.append("extra_gem_present")
    return issues


def composition_blade_issues(weapon: dict[str, Any], regions: dict[str, Any]) -> list[str]:
    """Validate a high-risk sword's annotated dimensions against its composition."""
    blade = regions.get("weaponBlade")
    if not blade:
        return ["weapon_blade_annotation_missing"] if weapon.get("bladeCenterline") else []
    width = blade[2] - blade[0] + 1
    height = blade[3] - blade[1] + 1
    issues = []
    if width < weapon.get("minBladeWidthPx", width):
        issues.append("weapon_blade_too_thin")
    if width > weapon.get("maxBladeWidthPx", width):
        issues.append("weapon_blade_too_wide")
    length_range = weapon.get("bladeLengthRangePx")
    if length_range:
        length = max(width, height)
        if length < length_range[0]:
            issues.append("weapon_blade_too_short")
        if length > length_range[1]:
            issues.append("weapon_blade_too_long")
    return issues


def composition_eye_occlusion_issues(spec: dict[str, Any], regions: dict[str, Any]) -> list[str]:
    eye_rule = spec.get("eyeOcclusion")
    if not eye_rule:
        return []
    blade = regions.get("weaponBlade")
    left_eye, right_eye = regions.get("leftEyeRegion"), regions.get("rightEyeRegion")
    if not blade or not left_eye or not right_eye:
        return ["eye_occlusion_annotations_missing"]
    def overlap(a: list[int], b: list[int]) -> bool:
        return not (a[2] < b[0] or a[0] > b[2] or a[3] < b[1] or a[1] > b[3])
    issues = []
    if eye_rule.get("bladeOverlapsBothInnerEyes") and (not overlap(blade, left_eye) or not overlap(blade, right_eye)):
        issues.append("blade_does_not_occlude_both_inner_eyes")
    left_center, right_center = (left_eye[0] + left_eye[2]) / 2, (right_eye[0] + right_eye[2]) / 2
    if right_center - left_center > eye_rule.get("maxEyeCenterGapPx", float("inf")):
        issues.append("eye_center_gap_too_wide")
    return issues


def identity_mask_issues(store: Store, contract: dict[str, Any], attempt: dict[str, Any]) -> list[str]:
    spec = contract.get("identitySpec")
    if not spec:
        return []
    artifact = attempt.get("artifacts", {}).get("identityMask")
    if not artifact:
        return ["identity_mask_missing"]
    anchor_path = store.absolute(spec["anchorMaskPath"], must_exist=True)
    candidate_path = store.absolute(artifact["path"], must_exist=True)
    if sha256_file(anchor_path) != spec["anchorMaskSha256"]:
        return ["identity_anchor_mask_hash_mismatch"]
    anchor = Image.open(anchor_path).convert("RGBA")
    candidate = Image.open(candidate_path).convert("RGBA")
    if anchor.size != candidate.size:
        return ["identity_mask_size_mismatch"]
    issues: list[str] = []
    for label, color in IDENTITY_MASK_COLORS.items():
        anchor_points = {index for index, value in enumerate(pixel_data(anchor)) if value == color}
        candidate_points = {index for index, value in enumerate(pixel_data(candidate)) if value == color}
        if not anchor_points:
            issues.append(f"identity_anchor_{label}_missing")
            continue
        if not candidate_points:
            issues.append(f"identity_{label}_missing")
            continue
        if label != "forehead_blaze":
            continue
        union = anchor_points | candidate_points
        iou = len(anchor_points & candidate_points) / max(1, len(union))
        ratio = len(candidate_points) / len(anchor_points)
        if iou < spec.get("foreheadBlazeMinIou", 0.45):
            issues.append("forehead_blaze_shape_mismatch")
        minimum, maximum = spec.get("foreheadBlazeAreaRatio", [0.65, 1.45])
        if not minimum <= ratio <= maximum:
            issues.append("forehead_blaze_area_out_of_range")
    return issues


def geometry_checks(store: Store, contract: dict[str, Any], attempt: dict[str, Any]) -> tuple[dict[str, Any], list[str]]:
    mask_artifact = candidate_mask_artifact(attempt)
    if contract["maskRequired"] and not mask_artifact:
        return {}, ["semantic_mask_missing"]
    if not mask_artifact:
        return {}, []
    mask = Image.open(store.absolute(mask_artifact["path"], must_exist=True)).convert("RGBA")
    pixels = pixel_data(mask)
    candidate = Image.open(store.absolute(candidate_artifact(attempt)["path"], must_exist=True)).convert("RGBA")
    if candidate.size != mask.size:
        return {}, ["semantic_mask_size_mismatch"]
    candidate_pixels = pixel_data(candidate)
    boxes = {label: bbox_for(pixels, mask.size, color) for label, color in MASK_COLORS.items()}
    issues = []
    allowed = set(MASK_COLORS.values()) | {(0, 0, 0, 0)}
    if any(value not in allowed for value in pixels):
        issues.append("semantic_mask_unknown_color")
    if any(a == 0 and (r or g or b) for r, g, b, a in pixels):
        issues.append("semantic_mask_transparent_rgb_nonzero")
    if any(mask_value[3] and not candidate_value[3] for mask_value, candidate_value in zip(pixels, candidate_pixels)):
        issues.append("semantic_mask_outside_subject")
    core = boxes["core"]
    if core is None:
        issues.append("core_missing")
    if contract.get("anchor") and not contract["anchor"].get("maskPath"):
        issues.append("anchor_mask_missing")
    if contract["noArms"]:
        for label in ("near_arm", "far_arm", "near_leg", "far_leg"):
            if boxes[label] is not None:
                issues.append(f"{label}_forbidden")
        for label in ("near_hand", "far_hand", "near_foot", "far_foot"):
            if boxes[label] is None:
                issues.append(f"{label}_missing")
        if core:
            width = mask.width
            core_points = {i for i, value in enumerate(pixels) if value == MASK_COLORS["core"]}
            for label in ("near_hand", "far_hand", "near_foot", "far_foot"):
                part_points = {i for i, value in enumerate(pixels) if value == MASK_COLORS[label]}
                contacts = 0
                for index in part_points:
                    x, y = index % width, index // width
                    if any((ny * width + nx) in core_points for nx, ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)) if 0 <= nx < width and 0 <= ny < mask.height):
                        contacts += 1
                if part_points and contacts < 3:
                    issues.append(f"{label}_contact_lt_3")
    if contract["kind"] in {"ground_character", "action_pose"}:
        foot_bottoms = [boxes[label][3] for label in ("near_foot", "far_foot") if boxes[label]]
        # The semantic foot mask is the deterministic virtual ground contact.
        # Full-sprite alpha can extend several rows lower because of antialiasing
        # or rear equipment, neither of which changes the character baseline.
        if foot_bottoms and max(foot_bottoms) not in {235, 236, 237}:
            issues.append("baseline_not_236")
    metrics: dict[str, Any] = {"boxes": {key: list(value) if value else None for key, value in boxes.items()}}
    if core:
        left, top, right, bottom = core
        foot_tops = [boxes[label][1] for label in ("near_foot", "far_foot") if boxes[label]]
        uninterrupted_bottom = min(foot_tops) - 1 if foot_tops else bottom
        row_widths = []
        core_rows: dict[int, list[int]] = {}
        for y in range(top, bottom + 1):
            xs = [x for x in range(mask.width) if pixels[y * mask.width + x] == MASK_COLORS["core"]]
            core_rows[y] = xs
            row_widths.append(max(xs) - min(xs) + 1 if xs else 0)
            # Feet legitimately occlude the lower capsule. Continuity is a
            # core-hull rule, so rows in the foot-overlap band are excluded.
            allowed_occluders = ("near_hand", "far_hand") if contract.get("assetRole") == "component" else ()
            if contract.get("assetRole") == "assembled_sprite":
                allowed_occluders = ("equipment", "near_hand", "far_hand")
            occluded_row = any(boxes[label] and boxes[label][1] <= y <= boxes[label][3] for label in allowed_occluders)
            if y <= uninterrupted_bottom and xs and len(xs) != max(xs) - min(xs) + 1 and not occluded_row:
                issues.append("core_row_disconnected")
        height = len(row_widths)
        bands = [row_widths[:max(1, height // 3)], row_widths[height // 3:max(height // 3 + 1, 2 * height // 3)], row_widths[2 * height // 3:]]
        widths = [max(band or [0]) for band in bands]
        metrics["core"] = {"bbox": [left, top, right, bottom], "center": [(left + right) / 2, (top + bottom) / 2], "bandMaxWidths": widths}
        if widths[2] > widths[1]:
            issues.append("core_lower_wider_than_middle")
        core_center_x = (left + right) / 2
        for label, side in contract.get("handSides", {}).items():
            box = boxes.get(label)
            if box and side and contract.get("assetRole") != "component":
                hand_center_x = (box[0] + box[2]) / 2
                if (side == "left" and hand_center_x >= core_center_x) or (side == "right" and hand_center_x <= core_center_x):
                    issues.append(f"{label}_wrong_side")
        anchor = contract.get("anchor")
        if anchor and anchor.get("maskPath"):
            anchor_mask = Image.open(store.absolute(anchor["maskPath"], must_exist=True)).convert("RGBA")
            anchor_pixels = pixel_data(anchor_mask)
            anchor_box = bbox_for(anchor_pixels, anchor_mask.size, MASK_COLORS["core"])
            if anchor_box:
                tol_size = contract["tolerances"]["sizePx"]
                tol_center = contract["tolerances"]["centerPx"]
                aw, ah = anchor_box[2] - anchor_box[0] + 1, anchor_box[3] - anchor_box[1] + 1
                cw, ch = right - left + 1, bottom - top + 1
                if abs(aw - cw) > tol_size or abs(ah - ch) > tol_size:
                    issues.append("core_size_out_of_tolerance")
                if abs((anchor_box[0] + anchor_box[2] - left - right) / 2) > tol_center or abs((anchor_box[1] + anchor_box[3] - top - bottom) / 2) > tol_center:
                    issues.append("core_center_out_of_tolerance")
                occlusion = contract.get("occlusion") or {}
                core_count = sum(1 for value in pixels if value == MASK_COLORS["core"])
                anchor_core_points = {index for index, value in enumerate(anchor_pixels) if value == MASK_COLORS["core"]}
                intrusion_counts: dict[str, int] = {}
                visible_ratios: dict[str, float] = {}
                for label, rule in occlusion.get("layerRules", {}).items():
                    label_points = {index for index, value in enumerate(pixels) if value == MASK_COLORS[label]}
                    if not label_points:
                        issues.append(f"{label}_missing_for_layer_rule")
                    if rule == "behind-core":
                        intrusion = 0
                        for index in label_points:
                            x, y = index % mask.width, index // mask.width
                            row = core_rows.get(y, [])
                            if row and min(row) < x < max(row):
                                intrusion += 1
                        intrusion_counts[label] = intrusion
                        if intrusion:
                            issues.append(f"{label}_intrudes_core")
                    ratio = len(label_points) / max(1, core_count)
                    visible_ratios[label] = ratio
                    cap = occlusion.get("visibilityCaps", {}).get(label)
                    if cap is not None and ratio > cap:
                        issues.append(f"{label}_visibility_cap_exceeded")
                metrics["occlusion"] = {"intrusionPixels": intrusion_counts, "visibleAreaRatios": visible_ratios}
        composition_ref = contract.get("compositionSpec")
        if composition_ref:
            composition = load_json(store.record("compositions", composition_ref["compositionId"]))
            spec = composition["spec"]
            annotation_id = attempt.get("annotationId")
            if not annotation_id and not spec.get("bodyLayer"):
                issues.append("annotations_missing")
            elif annotation_id:
                annotations = load_json(store.record("annotations", annotation_id))
                top_xs = core_rows.get(top, [])
                bottom_xs = core_rows.get(uninterrupted_bottom, [])
                if top_xs and bottom_xs:
                    dx = ((min(top_xs) + max(top_xs)) - (min(bottom_xs) + max(bottom_xs))) / 2
                    dy = max(1, uninterrupted_bottom - top)
                    import math
                    angle = math.degrees(math.atan2(dx, dy))
                    metrics.setdefault("composition", {})["coreTiltDegrees"] = angle
                    minimum, maximum = spec["coreAxis"]["tiltDegrees"]
                    if not minimum <= angle <= maximum:
                        issues.append("pose_axis_out_of_range")
                regions = annotations["regions"]
                def overlap(a: list[int], b: list[int]) -> bool:
                    return not (a[2] < b[0] or a[0] > b[2] or a[3] < b[1] or a[1] > b[3])
                if spec.get("bodyLayer"):
                    if any(regions.get(label) for label in ("weaponBlade", "guardRegion", "scabbard", "staticEffect")):
                        issues.append("body_layer_contains_weapon_or_effect_annotation")
                else:
                    exit_box = regions["weaponExit"]
                    if not overlap(exit_box, spec["weapon"]["exitWindow"]):
                        issues.append("weapon_exit_outside_window")
                    if not overlap(regions["weaponTip"], spec["weapon"]["tipRegion"]):
                        issues.append("weapon_tip_outside_region")
                blade_corridor = spec["weapon"].get("bladeCenterline")
                if blade_corridor and not spec.get("bodyLayer"):
                    blade = regions.get("weaponBlade")
                    if not blade:
                        issues.append("weapon_blade_annotation_missing")
                    else:
                        blade_width = blade[2] - blade[0] + 1
                        blade_height = blade[3] - blade[1] + 1
                        metrics.setdefault("composition", {})["bladeWidthPx"] = blade_width
                        metrics.setdefault("composition", {})["bladeLengthPx"] = max(blade_width, blade_height)
                        issues.extend(composition_blade_issues(spec["weapon"], regions))
                        if not overlap(blade, blade_corridor):
                            issues.append("weapon_blade_outside_centerline")
                issues.extend(composition_eye_occlusion_issues(spec, regions))
                guard_window = spec["weapon"].get("guardWindow")
                if guard_window and not spec.get("bodyLayer"):
                    guard = regions.get("guardRegion")
                    if not guard:
                        issues.append("guard_annotation_missing")
                    elif not overlap(guard, guard_window):
                        issues.append("guard_outside_window")
                equipment_box = boxes.get("equipment")
                if equipment_box:
                    for forbidden in spec["forbiddenRegions"]:
                        if overlap(list(equipment_box), forbidden["rect"]):
                            issues.append(f"equipment_enters_forbidden_{forbidden['name']}")
                if not spec.get("bodyLayer"):
                    issues.extend(composition_gem_issues(spec["weapon"], regions))
                    if spec["equipmentState"].get("scabbard") == "absent" and regions.get("scabbard"):
                        issues.append("scabbard_present")
                    if spec["equipmentState"].get("staticEffects") == "absent" and regions.get("staticEffect"):
                        issues.append("static_effect_present")
    issues.extend(identity_mask_issues(store, contract, attempt))
    return metrics, issues


def validate_attempt(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] in {"review_pending", "technical_failed"} and attempt.get("report"):
        report = load_json(store.absolute(attempt["report"]["path"], must_exist=True))
        if sha256_file(store.absolute(attempt["report"]["path"])) != attempt["report"]["sha256"]:
            raise PipelineError("validation report hash mismatch")
        return report
    if attempt["state"] not in {"prepared", "annotated", "calibrated"}:
        raise PipelineError("validate requires prepared, annotated, or calibrated attempt")
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    if contract.get("occlusion") and not attempt.get("artifacts", {}).get("calibrated"):
        raise PipelineError("occlusion contracts require calibrate-core before validation")
    candidate = candidate_artifact(attempt)
    prepared = store.absolute(candidate["path"], must_exist=True)
    technical, issues = inspect_technical(
        prepared, contract["kind"], require_master_canvas=contract.get("assetRole") != "component")
    if contract.get("assetRole") == "component" and contract.get("componentKind") != "body":
        mask_artifact = candidate_mask_artifact(attempt)
        geometry = {"componentKind": contract.get("componentKind")}
        geometry_issues = []
        if contract.get("maskRequired") and not mask_artifact:
            geometry_issues.append("component_mask_missing")
        elif mask_artifact:
            with Image.open(store.absolute(mask_artifact["path"], must_exist=True)) as mask_source, Image.open(prepared) as prepared_source:
                mask_image = mask_source.convert("RGBA")
                prepared_size = prepared_source.size
            if mask_image.size != prepared_size:
                geometry_issues.append("component_mask_size_mismatch")
            if not any(pixel[3] for pixel in pixel_data(mask_image)):
                geometry_issues.append("component_mask_empty")
    else:
        geometry, geometry_issues = geometry_checks(store, contract, attempt)
    issues.extend(geometry_issues)
    report = {"schemaVersion": 1, "attemptId": attempt["attemptId"], "inputSha256": sha256_file(prepared),
              "maskSha256": candidate_mask_artifact(attempt).get("sha256"), "technical": technical,
              "geometry": geometry, "issues": sorted(set(issues)), "passed": not issues}
    report_id = stable_id("report", report)
    report_path = store.pipeline / "reports" / f"{report_id}.json"
    write_json_idempotent(report_path, report, immutable=True)
    attempt["report"] = {"path": store.relative(report_path), "sha256": sha256_file(report_path)}
    attempt["state"] = "review_pending" if report["passed"] else "technical_failed"
    save_attempt(store, attempt)
    return report


def make_preview(master: Image.Image) -> Image.Image:
    preview = master.resize((128, 128), Image.Resampling.LANCZOS).convert("RGBA")
    preview.putdata([(0, 0, 0, 0) if alpha < 8 else (red, green, blue, alpha)
                     for red, green, blue, alpha in pixel_data(preview)])
    return preview


def render_anchor_tile_compare(prepared: Image.Image, anchor: Image.Image,
                               identity_spec: dict[str, Any] | None, store: Store) -> Image.Image:
    """Show the formal Idle and candidate on identical 64x32 isometric tiles."""
    image = Image.new("RGBA", (384, 160), (36, 36, 42, 255))
    draw = ImageDraw.Draw(image)
    for center_x in (96, 288):
        draw.polygon(((center_x - 32, 144), (center_x, 128), (center_x + 32, 144), (center_x, 159)),
                     fill=(94, 96, 104, 255), outline=(160, 162, 170, 255))
        draw.line(((center_x, 124), (center_x, 159)), fill=(80, 210, 255, 220), width=1)
    image.alpha_composite(make_preview(anchor), (32, 26))
    image.alpha_composite(make_preview(prepared), (224, 26))
    draw.text((42, 6), "Idle DR", fill=(230, 230, 235, 255))
    draw.text((226, 6), "Candidate", fill=(230, 230, 235, 255))
    if identity_spec:
        identity = Image.open(store.absolute(identity_spec["anchorMaskPath"], must_exist=True)).convert("RGBA")
        box = bbox_for(pixel_data(identity), identity.size, IDENTITY_MASK_COLORS["forehead_blaze"])
        if box:
            scaled = tuple(round(value / 2) for value in box)
            draw.rectangle((32 + scaled[0], 26 + scaled[1], 32 + scaled[2], 26 + scaled[3]), outline=(255, 255, 255, 220), width=1)
            draw.rectangle((224 + scaled[0], 26 + scaled[1], 224 + scaled[2], 26 + scaled[3]), outline=(255, 255, 255, 220), width=1)
    return image


def render_review(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] not in {"review_pending", "technical_failed", "approved", "rejected", "promoted"}:
        raise PipelineError("render-review requires a completed validation")
    prepared = Image.open(store.absolute(candidate_artifact(attempt)["path"], must_exist=True)).convert("RGBA")
    mask_artifact = candidate_mask_artifact(attempt)
    overlay = prepared.copy()
    if mask_artifact:
        mask = Image.open(store.absolute(mask_artifact["path"], must_exist=True)).convert("RGBA")
        tint = Image.new("RGBA", mask.size, (0, 0, 0, 0))
        tint.putdata([(r, g, b, 96 if a else 0) for r, g, b, a in pixel_data(mask)])
        overlay = Image.alpha_composite(overlay, tint)
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    anchor_art = contract.get("anchor")
    review = Image.new("RGBA", (768 if anchor_art else 512, 256), (36, 36, 42, 255))
    review.alpha_composite(prepared, (0, 0))
    review.alpha_composite(overlay, (256, 0))
    if anchor_art:
        anchor = Image.open(store.absolute(anchor_art["path"], must_exist=True)).convert("RGBA")
        review.alpha_composite(anchor, (512, 0))
    tile = Image.new("RGBA", (256, 160), (36, 36, 42, 255))
    tile_draw = ImageDraw.Draw(tile)
    tile_draw.polygon(((96, 144), (128, 128), (160, 144), (128, 159)), fill=(94, 96, 104, 255), outline=(160, 162, 170, 255))
    preview = make_preview(prepared)
    tile.alpha_composite(preview, (64, 26))  # preview anchor (64,118) -> tile center (128,144)
    out_dir = store.pipeline / "reviews" / attempt["attemptId"]
    out_dir.mkdir(parents=True, exist_ok=True)
    review_images = [("overlay", review), ("preview128", preview), ("tile64x32", tile)]
    if contract.get("identitySpec"):
        anchor = Image.open(store.absolute(contract["anchor"]["path"], must_exist=True)).convert("RGBA")
        review_images.append(("anchorTileCompare", render_anchor_tile_compare(prepared, anchor, contract["identitySpec"], store)))
    if contract.get("occlusion"):
        depth = Image.new("RGBA", (256, 256), (36, 36, 42, 255))
        depth.alpha_composite(prepared)
        anchor_mask = Image.open(store.absolute(contract["anchor"]["maskPath"], must_exist=True)).convert("RGBA")
        anchor_pixels = pixel_data(anchor_mask)
        mask_pixels = pixel_data(mask)
        depth_tint = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        colored = []
        behind_colors = {MASK_COLORS[label] for label in contract["occlusion"]["layerRules"]}
        for anchor_value, mask_value in zip(anchor_pixels, mask_pixels):
            if mask_value in behind_colors:
                colored.append((255, 48, 48, 180) if anchor_value == MASK_COLORS["core"] else (48, 220, 96, 150))
            elif anchor_value == MASK_COLORS["core"]:
                colored.append((64, 128, 255, 42))
            else:
                colored.append((0, 0, 0, 0))
        depth_tint.putdata(colored)
        depth = Image.alpha_composite(depth, depth_tint)
        review_images.append(("depthReview", depth))
    if contract.get("assetRole") == "assembled_sprite":
        assembly_id = attempt.get("assemblyId")
        if not assembly_id:
            raise PipelineError("assembled sprite review requires assembly binding")
        assembly = load_json(store.record("assemblies", assembly_id))
        layers = assembly["layers"]
        layer_review = Image.new("RGBA", (256 * len(layers), 512), (36, 36, 42, 255))
        cumulative = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        layer_draw = ImageDraw.Draw(layer_review)
        for index, layer in enumerate(layers):
            component = Image.open(store.absolute(layer["artifact"]["path"], must_exist=True)).convert("RGBA")
            rendered_component = apply_assembly_transform(component, layer["transform"])
            offset = tuple(layer["transform"]["translate"])
            layer_review.alpha_composite(rendered_component, (index * 256 + offset[0], offset[1]))
            cumulative.alpha_composite(rendered_component, offset)
            layer_review.alpha_composite(cumulative, (index * 256, 256))
            layer_draw.text((index * 256 + 6, 6), f"layer: {layer['role']}", fill=(235, 235, 240, 255))
            layer_draw.text((index * 256 + 6, 262), f"cumulative: {index + 1}/{len(layers)}", fill=(235, 235, 240, 255))
        review_images.append(("assemblyLayerReview", layer_review))
    outputs = {}
    for name, image in review_images:
        path = out_dir / f"{name}.png"
        if path.exists():
            before = sha256_file(path)
            tmp = out_dir / f".{name}.tmp.png"
            image.save(tmp, format="PNG", optimize=False, compress_level=9)
            if sha256_file(tmp) != before:
                tmp.unlink()
                raise PipelineError(f"review output is not deterministic: {path}")
            tmp.unlink()
        else:
            image.save(path, format="PNG", optimize=False, compress_level=9)
        outputs[name] = {"path": store.relative(path), "sha256": sha256_file(path)}
    attempt["artifacts"]["review"] = outputs
    register_public_artifacts(store, list(outputs.values()), "project-owned-artwork-review")
    save_attempt(store, attempt)
    return {"schemaVersion": 1, "attemptId": attempt["attemptId"], "outputs": outputs}


def decide(store: Store, args: argparse.Namespace, decision: str) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    binding = job.get("series")
    bound_series = bound_pose = None
    if binding:
        bound_series = load_json(store.record("series", binding["seriesId"]))
        bound_pose = series_pose(bound_series, binding["poseId"])
        if job.get("conceptOnly") or bound_pose["state"] == "provisional" or bound_series.get("provisionalAnchorAttemptId") == attempt["attemptId"]:
            raise PipelineError("provisional series artwork cannot be approved or rejected as formal art")
    target = "approved" if decision == "approved" else "rejected"
    if attempt["state"] == target and attempt.get("approvalId"):
        existing = load_json(store.record("approvals", attempt["approvalId"]))
        expected = {"reviewer": args.reviewer, "decision": decision, "reason": args.reason, "decidedAt": args.decided_at}
        if any(existing.get(key) != value for key, value in expected.items()):
            raise PipelineError("attempt already has a different approval receipt")
        return existing
    if attempt["state"] != "review_pending":
        raise PipelineError("approval decision requires review_pending attempt")
    if contract.get("schemaVersion") in {2, 3} and args.reviewer != "cty41":
        raise PipelineError("schema v2/v3 formal approval must be issued by cty41")
    annotation = None
    if contract.get("compositionSpec") and attempt.get("sourceMode") != "reviewed_import":
        annotation_id = attempt.get("annotationId")
        if not annotation_id:
            raise PipelineError("high-risk schema v2 approval requires annotations")
        annotation_path = store.record("annotations", annotation_id)
        annotation = {"annotationId": annotation_id, "sha256": sha256_file(annotation_path)}
    try:
        decided_at = datetime.fromisoformat(args.decided_at)
    except ValueError as exc:
        raise PipelineError("--decided-at must be an ISO-8601 timestamp") from exc
    if decided_at.tzinfo is None:
        raise PipelineError("--decided-at must include a timezone offset")
    report = load_json(store.absolute(attempt["report"]["path"], must_exist=True))
    if not report["passed"]:
        raise PipelineError("technical gate did not pass")
    review_hashes = approval_review_hashes(store, attempt, contract)
    receipt_payload = {
        "attemptId": attempt["attemptId"], "candidateSha256": candidate_artifact(attempt)["sha256"],
        "maskSha256": candidate_mask_artifact(attempt).get("sha256"), "reviewer": args.reviewer,
        "reviewSha256": review_hashes,
        "decision": decision, "reason": args.reason, "decidedAt": args.decided_at,
        "annotation": annotation, "reportSha256": attempt["report"]["sha256"],
    }
    approval_id = stable_id("approval", receipt_payload)
    receipt = {"schemaVersion": contract.get("schemaVersion", 1), "approvalId": approval_id, **receipt_payload}
    write_json_idempotent(store.record("approvals", approval_id), receipt, immutable=True)
    attempt["approvalId"] = approval_id
    transition(attempt, {"review_pending"}, target)
    save_attempt(store, attempt)
    if decision == "approved" and bound_series and bound_pose:
        bound_pose["state"] = "approved"
        save_series(store, bound_series)
    return receipt


def approve_exception(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract_path = store.record("contracts", job["contractId"])
    contract = load_json(contract_path)
    if args.reviewer != "cty41":
        raise PipelineError("gate exception reviewer must be cty41")
    if not args.reason.strip():
        raise PipelineError("gate exception reason must not be empty")
    try:
        decided_at = datetime.fromisoformat(args.decided_at)
    except ValueError as exc:
        raise PipelineError("--decided-at must be an ISO-8601 timestamp") from exc
    if decided_at.tzinfo is None:
        raise PipelineError("--decided-at must include a timezone offset")
    if not attempt.get("feedbackId"):
        raise PipelineError("gate exception approval requires recorded feedback")

    binding = job.get("series")
    bound_series = bound_pose = None
    if binding:
        bound_series = load_json(store.record("series", binding["seriesId"]))
        bound_pose = series_pose(bound_series, binding["poseId"])
        if job.get("conceptOnly") or bound_pose["state"] == "provisional" or bound_series.get("provisionalAnchorAttemptId") == attempt["attemptId"]:
            raise PipelineError("provisional series artwork cannot receive a gate exception approval")
        selected = bound_pose.get("selectedAttemptId")
        if selected and selected != attempt["attemptId"]:
            raise PipelineError("pose already has a different selected attempt")

    existing_id = attempt.get("approvalId")
    if attempt["state"] in {"approved", "promoted"} and existing_id:
        existing = load_json(store.record("approvals", existing_id))
        validate_exception_receipt(store, attempt, existing)
        expected = {
            "reviewer": args.reviewer, "reason": args.reason, "decidedAt": args.decided_at,
            "waivedIssues": gate_exception_evidence(
                store, args.issue,
                load_json(store.absolute(attempt["report"]["path"], must_exist=True)), contract),
        }
        if any(existing.get(key) != value for key, value in expected.items()):
            raise PipelineError("attempt already has a different gate exception approval receipt")
        return existing
    if attempt["state"] != "technical_failed":
        raise PipelineError("gate exception approval requires technical_failed attempt")

    report_path = store.absolute(attempt["report"]["path"], must_exist=True)
    if sha256_file(report_path) != attempt["report"]["sha256"]:
        raise PipelineError("validation report hash mismatch")
    report = load_json(report_path)
    if report.get("passed"):
        raise PipelineError("passing reports must use standard approval")
    waived_issues = gate_exception_evidence(store, args.issue, report, contract)
    review_hashes = approval_review_hashes(store, attempt, contract)
    annotation = None
    if contract.get("compositionSpec"):
        annotation_id = attempt.get("annotationId")
        if not annotation_id:
            raise PipelineError("schema v2 gate exception requires annotations")
        annotation = {"annotationId": annotation_id,
                      "sha256": sha256_file(store.record("annotations", annotation_id))}
    receipt_payload = {
        "attemptId": attempt["attemptId"],
        "candidateSha256": candidate_artifact(attempt)["sha256"],
        "maskSha256": candidate_mask_artifact(attempt).get("sha256"),
        "reportSha256": attempt["report"]["sha256"],
        "contractId": job["contractId"],
        "contractSha256": sha256_file(contract_path),
        "reviewSha256": review_hashes,
        "annotation": annotation,
        "reviewer": args.reviewer,
        "decision": "approved",
        "approvalMode": "gate-exception",
        "waivedIssues": waived_issues,
        "reason": args.reason,
        "decidedAt": args.decided_at,
    }
    approval_id = stable_id("approval", receipt_payload)
    receipt = {"schemaVersion": contract.get("schemaVersion", 1), "approvalId": approval_id, **receipt_payload}
    write_json_idempotent(store.record("approvals", approval_id), receipt, immutable=True)
    attempt["approvalId"] = approval_id
    transition(attempt, {"technical_failed"}, "approved")
    save_attempt(store, attempt)
    if bound_series and bound_pose:
        bound_pose["selectedAttemptId"] = attempt["attemptId"]
        bound_pose["state"] = "approved"
        save_series(store, bound_series)
    return receipt


def record_feedback(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] not in {"prepared", "annotated", "calibrated", "review_pending", "technical_failed"}:
        raise PipelineError("feedback requires a processed or validated attempt")
    if args.verdict not in FEEDBACK_VERDICTS:
        raise PipelineError(f"unsupported feedback verdict: {args.verdict}")
    payload = {
        "attemptId": attempt["attemptId"], "candidateSha256": candidate_artifact(attempt).get("sha256"),
        "reviewer": args.reviewer, "verdict": args.verdict, "strengths": args.strength,
        "defects": args.defect, "nextPromptDelta": args.next_prompt_delta or "", "recordedAt": args.recorded_at,
    }
    author_type = getattr(args, "author_type", None)
    categories = getattr(args, "category", []) or []
    frozen = getattr(args, "frozen", []) or []
    pending = getattr(args, "pending", []) or []
    if author_type:
        if author_type not in {"agent", "human"}:
            raise PipelineError("feedback author type must be agent or human")
        if set(categories) - FEEDBACK_CATEGORIES:
            raise PipelineError("feedback contains unsupported defect category")
        payload.update({"authorType": author_type, "categories": categories,
                        "disposition": args.verdict, "frozenInvariants": frozen,
                        "pendingFixes": pending or list(args.defect)})
    try:
        recorded_at = datetime.fromisoformat(args.recorded_at)
    except ValueError as exc:
        raise PipelineError("--recorded-at must be an ISO-8601 timestamp") from exc
    if recorded_at.tzinfo is None:
        raise PipelineError("--recorded-at must include a timezone offset")
    feedback_id = stable_id("feedback", payload)
    record = {"schemaVersion": 2 if author_type else 1, "feedbackId": feedback_id, **payload}
    existing_id = attempt.get("feedbackId")
    if existing_id and existing_id != feedback_id:
        raise PipelineError("attempt already has different immutable feedback")
    write_json_idempotent(store.record("feedback", feedback_id), record, immutable=True)
    if not existing_id:
        attempt["feedbackId"] = feedback_id
        save_attempt(store, attempt)
    return record


def record_feedback_addendum(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    feedback = load_json(store.record("feedback", args.feedback_id))
    attempt = load_json(store.record("attempts", feedback["attemptId"]))
    try:
        recorded_at = datetime.fromisoformat(args.recorded_at)
    except ValueError as exc:
        raise PipelineError("--recorded-at must be an ISO-8601 timestamp") from exc
    if recorded_at.tzinfo is None:
        raise PipelineError("--recorded-at must include a timezone offset")
    disposition = getattr(args, "disposition", None)
    if not args.defect and not disposition:
        raise PipelineError("feedback addendum requires a defect or disposition")
    if disposition and disposition not in FEEDBACK_VERDICTS:
        raise PipelineError("feedback addendum disposition is invalid")
    payload = {"attemptId": attempt["attemptId"], "parentFeedbackId": feedback["feedbackId"],
               "reviewer": args.reviewer, "defects": args.defect, "recordedAt": args.recorded_at,
               "authorType": getattr(args, "author_type", None), "disposition": disposition}
    addendum_id = stable_id("feedback-addendum", payload)
    record = {"schemaVersion": 2 if disposition or getattr(args, "author_type", None) else 1,
              "feedbackAddendumId": addendum_id, **payload}
    write_json_idempotent(store.record("feedback-addenda", addendum_id), record, immutable=True)
    attempt.setdefault("feedbackAddendumIds", [])
    if addendum_id not in attempt["feedbackAddendumIds"]:
        attempt["feedbackAddendumIds"].append(addendum_id)
        save_attempt(store, attempt)
    return record


def attempt_binding(store: Store, attempt: dict[str, Any]) -> tuple[dict[str, Any], dict[str, Any], dict[str, Any]]:
    job = load_json(store.record("jobs", attempt["jobId"]))
    binding = job.get("series")
    if not binding:
        raise PipelineError("attempt is not bound to a series")
    series = load_json(store.record("series", binding["seriesId"]))
    pose = series_pose(series, binding["poseId"])
    return job, series, pose


def unique_pose_hashes(store: Store, pose: dict[str, Any]) -> set[str]:
    hashes = set()
    for attempt_id in pose["attemptIds"]:
        attempt = load_json(store.record("attempts", attempt_id))
        digest = attempt.get("artifacts", {}).get("raw", {}).get("sha256")
        if digest:
            hashes.add(digest)
    return hashes


def select_attempt(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    _job, series, pose = attempt_binding(store, attempt)
    feedback_id = attempt.get("feedbackId")
    if not feedback_id:
        raise PipelineError("selection requires recorded feedback")
    feedback = load_json(store.record("feedback", feedback_id))
    if args.provisional:
        limit = series.get("maxUniqueOutputs")
        if limit is None or pose["poseId"] != "idle-dr" or len(unique_pose_hashes(store, pose)) != limit:
            raise PipelineError("provisional anchor is only allowed for exhausted idle-dr")
        pose["state"] = "provisional"
        series["provisionalAnchorAttemptId"] = attempt["attemptId"]
    else:
        if attempt["state"] != "review_pending" or feedback["verdict"] != "selected":
            raise PipelineError("normal selection requires review_pending and selected feedback")
        pose["state"] = "review_pending"
    existing = pose.get("selectedAttemptId")
    if existing and existing != attempt["attemptId"]:
        raise PipelineError("pose already has a different selected attempt")
    pose["selectedAttemptId"] = attempt["attemptId"]
    save_series(store, series)
    return series


def advance_series(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    series = load_json(store.record("series", args.series_id))
    current = series_pose(series, series["currentPoseId"])
    if current["state"] == "active":
        limit = series.get("maxUniqueOutputs")
        if limit is None:
            raise PipelineError("unlimited active pose must be selected, approved, promoted, or explicitly rejected before advancing")
        hashes = unique_pose_hashes(store, current)
        feedback_complete = all(load_json(store.record("attempts", aid)).get("feedbackId") for aid in current["attemptIds"] if load_json(store.record("attempts", aid)).get("artifacts", {}).get("raw"))
        if len(hashes) != limit or not feedback_complete:
            raise PipelineError("active pose can only exhaust after every unique output has feedback")
        current["state"] = "exhausted"
    if current["poseId"] == "idle-dr" and current["state"] not in {"promoted", "provisional"}:
        raise PipelineError("idle-dr must be promoted or explicitly provisional before advancing")
    if current["state"] not in {"review_pending", "approved", "promoted", "exhausted", "provisional"}:
        raise PipelineError("current pose is not ready to advance")
    index = series["poses"].index(current)
    if index + 1 >= len(series["poses"]):
        series["currentPoseId"] = None
        save_series(store, series)
        return series
    next_pose = series["poses"][index + 1]
    if next_pose["state"] == "pending":
        next_pose["state"] = "active"
    series["currentPoseId"] = next_pose["poseId"]
    save_series(store, series)
    return series


def update_provenance(store: Store, paths: list[dict[str, str]], contract: dict[str, Any]) -> None:
    manifest_path = store.root / "Tools/public-release/asset-provenance.json"
    manifest = load_json(manifest_path)
    by_path = {entry["path"]: entry for entry in manifest["entries"]}
    for artifact in paths:
        entry = {"path": artifact["path"], "sha256": artifact["sha256"], "status": "approved", **contract["rights"]}
        existing = by_path.get(artifact["path"])
        if existing and existing != entry:
            supporting_upgrade = (
                existing.get("sha256") == entry["sha256"]
                and existing.get("status") == "approved"
                and existing.get("provenance") == "project-owned-supporting-derived"
            )
            if not supporting_upgrade:
                raise PipelineError(f"conflicting provenance entry: {artifact['path']}")
            existing.clear()
            existing.update(entry)
        if not existing:
            manifest["entries"].append(entry)
            by_path[artifact["path"]] = entry
    write_json_idempotent(manifest_path, manifest)


def register_public_artifacts(store: Store, paths: list[dict[str, str]], provenance: str) -> None:
    manifest_path = store.root / "Tools/public-release/asset-provenance.json"
    if not manifest_path.is_file():
        return
    manifest = load_json(manifest_path)
    by_path = {entry["path"]: entry for entry in manifest["entries"]}
    for artifact in paths:
        existing = by_path.get(artifact["path"])
        if existing:
            expected = {"sha256": artifact["sha256"], "status": "approved", "rightsHolder": "cty41", "license": "CC-BY-4.0"}
            if any(existing.get(key) != value for key, value in expected.items()):
                raise PipelineError(f"conflicting provenance entry: {artifact['path']}")
            continue
        entry = {
            "path": artifact["path"], "sha256": artifact["sha256"], "status": "approved",
            "rightsHolder": "cty41", "license": "CC-BY-4.0", "provenance": provenance,
        }
        manifest["entries"].append(entry)
        by_path[artifact["path"]] = entry
    write_json_idempotent(manifest_path, manifest)


def _iso_timestamp(value: str, option: str) -> None:
    try:
        parsed = datetime.fromisoformat(value)
    except ValueError as exc:
        raise PipelineError(f"{option} must be an ISO-8601 timestamp") from exc
    if parsed.tzinfo is None:
        raise PipelineError(f"{option} must include a timezone offset")


def create_composition(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    spec_rel = store.relative(args.spec, must_exist=True)
    try:
        spec = json.loads(store.absolute(spec_rel).read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise PipelineError(f"cannot read composition spec: {exc}") from exc
    required = {"canvas", "coreAxis", "footCenter", "weapon", "forbiddenRegions", "equipmentState"}
    if not isinstance(spec, dict) or required - set(spec):
        raise PipelineError("composition spec is missing required v2 fields")
    if spec["equipmentState"].get("scabbard") not in {"present", "absent", "optional"}:
        raise PipelineError("composition equipmentState.scabbard is invalid")
    anchor_rel = store.relative(args.anchor, must_exist=True)
    payload = {
        "assetId": args.asset_id,
        "spec": spec,
        "source": {"path": spec_rel, "sha256": sha256_file(store.absolute(spec_rel))},
        "anchor": {"path": anchor_rel, "sha256": sha256_file(store.absolute(anchor_rel))},
    }
    composition_id = stable_id("composition", payload)
    record = {"schemaVersion": 2, "compositionId": composition_id, **payload}
    write_json_idempotent(store.record("compositions", composition_id), record, immutable=True)
    return record


def render_pose_guide(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    composition_path = store.record("compositions", args.composition_id)
    composition = load_json(composition_path)
    spec = composition["spec"]
    width, height = spec.get("canvas", [256, 256])
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    axis = spec["coreAxis"]
    draw.line([tuple(axis["bottom"]), tuple(axis["top"])], fill=(0, 220, 255, 255), width=3)
    foot = spec["footCenter"]
    draw.ellipse((foot[0] - 3, foot[1] - 3, foot[0] + 3, foot[1] + 3), fill=(255, 220, 0, 255))
    weapon = spec["weapon"]
    grip = weapon["hiddenGrip"]
    draw.ellipse((grip[0] - 3, grip[1] - 3, grip[0] + 3, grip[1] + 3), fill=(255, 0, 255, 255))
    for key, color in (("exitWindow", (0, 255, 0, 150)), ("tipRegion", (255, 128, 0, 150))):
        draw.rectangle(tuple(weapon[key]), outline=color, width=2)
    if weapon.get("guardWindow"):
        draw.rectangle(tuple(weapon["guardWindow"]), outline=(255, 255, 0, 180), width=2)
    if weapon.get("bladeCenterline"):
        draw.rectangle(tuple(weapon["bladeCenterline"]), outline=(120, 140, 255, 180), width=1)
    for region in spec["forbiddenRegions"]:
        draw.rectangle(tuple(region["rect"]), outline=(255, 0, 0, 220), fill=(255, 0, 0, 40), width=2)
    output_rel = store.relative(args.output)
    output = store.absolute(output_rel)
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(f".{output.name}.tmp")
    image.save(temporary, format="PNG", optimize=False, compress_level=9)
    os.replace(temporary, output)
    artifact = {"path": output_rel, "sha256": sha256_file(output)}
    payload = {
        "compositionId": composition["compositionId"],
        "compositionSha256": sha256_file(composition_path),
        "anchorSha256": composition["anchor"]["sha256"],
        "artifact": artifact,
        "role": "supporting-derived",
    }
    guide_id = stable_id("pose-guide", payload)
    record = {"schemaVersion": 2, "poseGuideId": guide_id, **payload}
    write_json_idempotent(store.record("pose-guides", guide_id), record, immutable=True)
    register_public_artifacts(store, [artifact], "project-owned-supporting-derived")
    return record


def compile_prompt(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    job = load_json(store.record("jobs", args.job_id))
    contract = load_json(store.record("contracts", job["contractId"]))
    if contract.get("schemaVersion") not in {2, 3}:
        raise PipelineError("compile-prompt requires schema v2 or v3 contract")
    composition_ref = contract.get("compositionSpec")
    if not composition_ref:
        raise PipelineError("schema v2 high-risk prompt requires composition spec")
    composition = load_json(store.record("compositions", composition_ref["compositionId"]))
    guide = load_json(store.record("pose-guides", args.pose_guide_id))
    if guide.get("compositionId") != composition["compositionId"]:
        raise PipelineError("pose guide does not match composition")
    unresolved = []
    for attempt in list_attempts(store, job["jobId"]):
        feedback_id = attempt.get("feedbackId")
        if feedback_id:
            feedback = load_json(store.record("feedback", feedback_id))
            unresolved.extend(feedback.get("pendingFixes", feedback.get("defects", [])))
    if contract.get("componentKind") == "death_expression_overlay":
        invariants = [
            "transparent expression overlay only", "exactly two compact crossed-eye marks",
            "no face, coat, ears, mouth, collar, paws, equipment, effects, text, or watermark",
        ]
    else:
        invariants = [
            "equal-width rigid capsule body", "exactly four paws directly attached to the body",
            "no arms and no legs between paws and body",
            "gray-white forehead blaze and heterochromic ear", "half-body alternate coat color",
        ]
    if composition["spec"]["equipmentState"].get("scabbard") == "absent":
        invariants.append("no scabbard anywhere")
    sections = [
        "# Deterministic ImageGen Task Packet",
        "## Frozen invariants\n" + "\n".join(f"- {item}" for item in invariants),
        "## Reference responsibilities\n" + "\n".join(f"- {item['role']}: {item['path']} @ {item['sha256']}" for item in job["inputs"]),
        "## Composition\n```json\n" + json.dumps(composition["spec"], ensure_ascii=False, sort_keys=True, indent=2) + "\n```",
        "## Unresolved fixes\n" + ("\n".join(f"- {item}" for item in unresolved) or "- none"),
        "## Base prompt\n" + store.absolute(job["prompt"]["path"]).read_text(encoding="utf-8"),
    ]
    data = ("\n\n".join(sections) + "\n").encode("utf-8")
    output_rel = store.relative(args.output)
    output = store.absolute(output_rel)
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(f".{output.name}.tmp")
    temporary.write_bytes(data)
    os.replace(temporary, output)
    artifact = {"path": output_rel, "sha256": sha256_file(output)}
    payload = {"jobId": job["jobId"], "poseGuideId": guide["poseGuideId"], "artifact": artifact}
    prompt_id = stable_id("compiled-prompt", payload)
    record = {"schemaVersion": 2, "compiledPromptId": prompt_id, **payload}
    write_json_idempotent(store.record("compiled-prompts", prompt_id), record, immutable=True)
    return record


def begin_generation(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] != "ready":
        raise PipelineError("generation can only begin for a ready attempt")
    compiled = load_json(store.record("compiled-prompts", args.compiled_prompt_id))
    if compiled["jobId"] != attempt["jobId"]:
        raise PipelineError("compiled prompt belongs to another job")
    _iso_timestamp(args.started_at, "--started-at")
    payload = {
        "attemptId": attempt["attemptId"], "compiledPromptId": compiled["compiledPromptId"],
        "compiledPromptSha256": sha256_file(store.record("compiled-prompts", compiled["compiledPromptId"])),
        "provider": args.provider, "startedAt": args.started_at,
    }
    invocation_id = stable_id("generation-invocation", payload)
    record = {"schemaVersion": 2, "invocationId": invocation_id, "state": "started", **payload}
    write_json_idempotent(store.record("generation-invocations", invocation_id), record, immutable=True)
    return record


def record_generation_failure(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    invocation = load_json(store.record("generation-invocations", args.invocation_id))
    _iso_timestamp(args.failed_at, "--failed-at")
    payload = {"invocationId": invocation["invocationId"], "attemptId": invocation["attemptId"],
               "reason": args.reason, "failedAt": args.failed_at}
    failure_id = stable_id("generation-failure", payload)
    record = {"schemaVersion": 2, "generationFailureId": failure_id, **payload}
    write_json_idempotent(store.record("generation-failures", failure_id), record, immutable=True)
    return record


def attach_annotations(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] not in {"prepared", "annotated", "calibrated"}:
        raise PipelineError("annotations require a prepared attempt")
    annotation_rel = store.relative(args.annotations, must_exist=True)
    annotations = load_json(store.absolute(annotation_rel))
    required = {"eyeRegion", "weaponExit", "weaponTip", "gemRegion"}
    if required - set(annotations.get("regions", {})):
        raise PipelineError("annotations are missing required high-risk regions")
    mask = candidate_mask_artifact(attempt)
    payload = {"attemptId": attempt["attemptId"], "candidateSha256": candidate_artifact(attempt)["sha256"],
               "maskSha256": mask["sha256"], "source": {"path": annotation_rel, "sha256": sha256_file(store.absolute(annotation_rel))},
               "regions": annotations["regions"]}
    annotation_id = stable_id("annotations", payload)
    record = {"schemaVersion": 2, "annotationId": annotation_id, **payload}
    write_json_idempotent(store.record("annotations", annotation_id), record, immutable=True)
    attempt["annotationId"] = annotation_id
    if attempt["state"] == "prepared":
        attempt["state"] = "annotated"
    save_attempt(store, attempt)
    return record


def record_advisory_review(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    _iso_timestamp(args.recorded_at, "--recorded-at")
    payload = {"attemptId": attempt["attemptId"], "candidateSha256": candidate_artifact(attempt)["sha256"],
               "reviewer": args.reviewer, "risks": args.risk, "recordedAt": args.recorded_at,
               "nonBinding": True}
    advisory_id = stable_id("advisory-review", payload)
    record = {"schemaVersion": 2, "advisoryReviewId": advisory_id, **payload}
    write_json_idempotent(store.record("advisory-reviews", advisory_id), record, immutable=True)
    return record


def register_supporting_artifact(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    rel = store.relative(args.path, must_exist=True)
    artifact = {"path": rel, "sha256": sha256_file(store.absolute(rel))}
    payload = {"artifact": artifact, "role": args.role, "note": args.note}
    record_id = stable_id("supporting-artifact", payload)
    record = {"schemaVersion": 2, "supportingArtifactId": record_id, **payload}
    write_json_idempotent(store.record("supporting-artifacts", record_id), record, immutable=True)
    if Path(rel).suffix.lower() == ".png":
        register_public_artifacts(store, [artifact], "project-owned-supporting-derived")
    return record


def _bound_artifact(store: Store, path: str) -> dict[str, str]:
    rel = store.relative(path, must_exist=True)
    return {"path": rel, "sha256": sha256_file(store.absolute(rel))}


def _visible_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    return image.getchannel("A").getbbox()


def _sprite_import_geometry(image: Image.Image, expected_size: tuple[int, int], label: str) -> dict[str, Any]:
    if image.mode != "RGBA" or image.size != expected_size:
        raise PipelineError(f"{label} must be {expected_size[0]}x{expected_size[1]} RGBA")
    corners = [image.getpixel(point)[3] for point in ((0, 0), (image.width - 1, 0), (0, image.height - 1), (image.width - 1, image.height - 1))]
    if any(corners):
        raise PipelineError(f"{label} corners must be transparent")
    if any(pixel[3] and pixel[:3] in {(0, 255, 0), (255, 0, 255)} for pixel in pixel_data(image)):
        raise PipelineError(f"{label} contains exact chroma residue")
    bbox = _visible_bbox(image)
    if not bbox:
        raise PipelineError(f"{label} is empty")
    center = [(bbox[0] + bbox[2] - 1) / 2, (bbox[1] + bbox[3] - 1) / 2]
    expected_center = [(image.width - 1) / 2, (image.height - 1) / 2]
    if any(abs(center[index] - expected_center[index]) > 0.5 for index in range(2)):
        raise PipelineError(f"{label} visible AABB must be centered")
    return {"bbox": list(bbox), "bboxSize": [bbox[2] - bbox[0], bbox[3] - bbox[1]], "center": center}


def render_size_comparison(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    inputs = [("Identity", args.identity), ("Previous", args.previous), ("Pose reference", args.reference), ("Candidate", args.candidate)]
    bound = []
    panels = []
    measurements = []
    for label, value in inputs:
        artifact = _bound_artifact(store, value)
        image = Image.open(store.absolute(artifact["path"])).convert("RGBA")
        preview = make_preview(image) if image.size == (256, 256) else image.resize((128, 128), Image.Resampling.MITCHELL)
        panel = Image.new("RGBA", (144, 176), (32, 32, 32, 255))
        panel.alpha_composite(preview, (8, 8))
        ImageDraw.Draw(panel).text((8, 144), label, fill=(255, 255, 255, 255))
        panels.append(panel)
        bbox = _visible_bbox(preview)
        measurements.append({"label": label, "bbox": list(bbox) if bbox else None,
                             "bboxSize": [bbox[2] - bbox[0], bbox[3] - bbox[1]] if bbox else None})
        bound.append({"label": label, "artifact": artifact})
    review = Image.new("RGBA", (144 * len(panels), 176), (32, 32, 32, 255))
    for index, panel in enumerate(panels):
        review.alpha_composite(panel, (144 * index, 0))
    output_rel = store.relative(args.output)
    output = store.absolute(output_rel); output.parent.mkdir(parents=True, exist_ok=True)
    review.save(output, format="PNG", optimize=False, compress_level=9)
    artifact = {"path": output_rel, "sha256": sha256_file(output)}
    payload = {"inputs": bound, "measurements": measurements, "artifact": artifact}
    comparison_id = stable_id("size-comparison", payload)
    record = {"schemaVersion": 1, "sizeComparisonId": comparison_id, **payload}
    write_json_idempotent(store.record("size-comparisons", comparison_id), record, immutable=True)
    register_public_artifacts(store, [artifact], "project-owned-artwork-review")
    return record


def adopt_reviewed_sprite(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    contract_path = store.record("contracts", args.contract_id)
    contract = load_json(contract_path)
    if contract.get("assetRole") == "component" or contract.get("runtimeEligible") is False:
        raise PipelineError("reviewed imports must be complete Sprite assets")
    if args.reviewer != "cty41":
        raise PipelineError("reviewed import reviewer must be cty41")
    try:
        accepted_at = datetime.fromisoformat(args.accepted_at)
    except ValueError as exc:
        raise PipelineError("--accepted-at must be an ISO-8601 timestamp") from exc
    if accepted_at.tzinfo is None:
        raise PipelineError("--accepted-at must include a timezone offset")
    source = _bound_artifact(store, args.source)
    candidate = _bound_artifact(store, args.candidate)
    preview = _bound_artifact(store, args.preview)
    comparison = _bound_artifact(store, args.size_comparison)
    comparison_records = [
        load_json(path) for path in (store.pipeline / "size-comparisons").glob("*.json")
        if load_json(path).get("artifact") == comparison
    ]
    if len(comparison_records) != 1:
        raise PipelineError("reviewed import requires one matching immutable size-comparison record")
    comparison_candidate = next(
        (item.get("artifact") for item in comparison_records[0].get("inputs", []) if item.get("label") == "Candidate"), None)
    if comparison_candidate != candidate:
        raise PipelineError("size comparison Candidate does not match the reviewed import candidate")
    with Image.open(store.absolute(candidate["path"])) as candidate_source:
        candidate_geometry = _sprite_import_geometry(candidate_source.copy(), (256, 256), "candidate")
    with Image.open(store.absolute(preview["path"])) as preview_source:
        preview_geometry = _sprite_import_geometry(preview_source.copy(), (128, 128), "preview")
    payload = {"contractId": args.contract_id, "source": source, "candidate": candidate, "preview": preview,
               "sizeComparison": comparison, "reviewer": args.reviewer, "reason": args.reason,
               "acceptedAt": args.accepted_at, "sourceMode": "reviewed_import"}
    adoption_id = stable_id("reviewed-import", payload)
    adoption = {"schemaVersion": 2, "reviewedImportId": adoption_id, **payload}
    write_json_idempotent(store.record("reviewed-imports", adoption_id), adoption, immutable=True)
    job_payload = {"contractId": args.contract_id, "reviewedImportId": adoption_id, "sourceMode": "reviewed_import"}
    job_id = stable_id("job", job_payload)
    job = {"schemaVersion": 2, "jobId": job_id, "state": "ready", "contractId": args.contract_id,
           "contractSha256": sha256_file(contract_path), "prompt": None,
           "inputs": [source, candidate, preview, comparison], "target": {"direction": contract["direction"], "pose": contract["pose"]},
           "series": None, "conceptOnly": False, "contractRequirements": None,
           "requiresInvocation": False, "sourceMode": "reviewed_import", "reviewedImportId": adoption_id}
    write_json_idempotent(store.record("jobs", job_id), job, immutable=True)
    report_payload = {"passed": True, "issues": [], "candidateGeometry": candidate_geometry,
                      "previewGeometry": preview_geometry, "reviewedImportId": adoption_id}
    report_id = stable_id("report", report_payload)
    report_path = store.record("reports", report_id)
    write_json_idempotent(report_path, {"schemaVersion": 2, "reportId": report_id, **report_payload}, immutable=True)
    attempt_id = f"{job_id}-a001"
    attempt = {"schemaVersion": 2, "attemptId": attempt_id, "jobId": job_id, "ordinal": 1,
               "parentAttemptId": None, "retryFeedbackId": None, "promptDelta": None,
               "technicalRemediation": False, "state": "review_pending", "sourceMode": "reviewed_import",
               "reviewedImportId": adoption_id,
               "artifacts": {"source": source, "prepared": candidate, "importedPreview": preview,
                             "review": {"sizeComparison": comparison}},
               "report": {"path": store.relative(report_path), "sha256": sha256_file(report_path)},
               "approvalId": None, "feedbackId": None}
    write_json_idempotent(store.record("attempts", attempt_id), attempt)
    return {"schemaVersion": 2, "adoption": adoption, "job": job, "attempt": attempt}


def _component_contract(store: Store, contract_id_value: str, kind: str | None = None) -> dict[str, Any]:
    contract = load_json(store.record("contracts", contract_id_value))
    if contract.get("schemaVersion") != 3 or contract.get("assetRole") != "component":
        raise PipelineError("operation requires a schema v3 component contract")
    if kind and contract.get("componentKind") != kind:
        raise PipelineError(f"component contract must have kind {kind}")
    return contract


def migrate_component(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    contract = _component_contract(store, args.contract_id)
    if contract.get("sourceMode") != "pre_v3_import":
        raise PipelineError("migrate-component requires sourceMode pre_v3_import")
    _iso_timestamp(args.accepted_at, "--accepted-at")
    source_rel = store.relative(args.source, must_exist=True)
    prepared_rel = store.relative(args.prepared, must_exist=True)
    payload = {
        "contractId": args.contract_id,
        "contractSha256": sha256_file(store.record("contracts", args.contract_id)),
        "source": {"path": source_rel, "sha256": sha256_file(store.absolute(source_rel))},
        "prepared": {"path": prepared_rel, "sha256": sha256_file(store.absolute(prepared_rel))},
        "processing": json.loads(store.absolute(store.relative(args.processing, must_exist=True)).read_text(encoding="utf-8")),
        "reviewer": args.reviewer,
        "reason": args.reason,
        "acceptedAt": args.accepted_at,
        "invocationStatus": "missing-pre-v3",
    }
    migration_id = stable_id("component-migration", payload)
    receipt = {"schemaVersion": 3, "componentMigrationId": migration_id, **payload}
    write_json_idempotent(store.record("component-migrations", migration_id), receipt, immutable=True)
    job_payload = {"contractId": args.contract_id, "migrationId": migration_id}
    job_id = stable_id("job", job_payload)
    job = {
        "schemaVersion": 3, "jobId": job_id, "state": "ready",
        "contractId": args.contract_id, "contractSha256": payload["contractSha256"],
        "prompt": None, "inputs": [], "target": {"direction": contract["direction"], "pose": contract["pose"]},
        "series": None, "conceptOnly": False, "contractRequirements": None,
        "requiresInvocation": False, "sourceMode": "pre_v3_import",
    }
    write_json_idempotent(store.record("jobs", job_id), job, immutable=True)
    attempt_id = f"{job_id}-a001"
    raw_dst = store.pipeline / "artifacts" / job_id / attempt_id / "raw.png"
    prepared_dst = store.pipeline / "artifacts" / job_id / attempt_id / "prepared.png"
    attempt = {
        "schemaVersion": 3, "attemptId": attempt_id, "jobId": job_id, "ordinal": 1,
        "parentAttemptId": None, "retryFeedbackId": None, "promptDelta": None,
        "technicalRemediation": False, "state": "prepared",
        "artifacts": {
            "source": payload["source"],
            "raw": copy_bound(store, source_rel, store.relative(raw_dst)),
            "prepared": copy_bound(store, prepared_rel, store.relative(prepared_dst)),
        },
        "report": None, "approvalId": None, "feedbackId": None,
        "componentMigrationId": migration_id, "sourceMode": "pre_v3_import",
    }
    write_json_idempotent(store.record("attempts", attempt_id), attempt)
    register_public_artifacts(store, list(attempt["artifacts"].values()), "project-owned-gpt-generated-or-derived")
    return {"schemaVersion": 3, "migration": receipt, "job": job, "attempt": attempt}


def derive_component(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    label_kind = {
        "near_hand": "paw_overlay", "far_hand": "paw_overlay",
        "near_foot": "foot_overlay", "far_foot": "foot_overlay",
        "body": "body", "equipment": "equipment",
    }
    if args.label not in label_kind:
        raise PipelineError("unsupported derived component semantic label")
    contract = _component_contract(store, args.contract_id, label_kind[args.label])
    if contract.get("sourceMode") != "derived":
        raise PipelineError("derive-component requires sourceMode derived")
    source_attempt = load_json(store.record("attempts", args.source_attempt_id))
    if source_attempt.get("state") not in {"approved", "promoted"}:
        raise PipelineError("derived components require an approved source pose")
    source_job = load_json(store.record("jobs", source_attempt["jobId"]))
    source_contract = load_json(store.record("contracts", source_job["contractId"]))
    if source_contract.get("assetRole") == "component":
        if source_contract.get("componentKind") != "body":
            raise PipelineError("derived overlays require a body component or complete pose source")
    elif source_contract.get("kind") not in {"ground_character", "action_pose"}:
        raise PipelineError("derived overlays require a body component or complete pose source")
    source_artifact = candidate_artifact(source_attempt)
    mask_artifact = candidate_mask_artifact(source_attempt)
    if not mask_artifact:
        raise PipelineError("derived overlay requires an approved source semantic mask")
    image = Image.open(store.absolute(source_artifact["path"], must_exist=True)).convert("RGBA")
    mask = Image.open(store.absolute(mask_artifact["path"], must_exist=True)).convert("RGBA")
    if image.size != mask.size:
        raise PipelineError("source component and semantic mask sizes differ")
    selected_labels = {"core", "head_appendage"} if args.label == "body" else {args.label}
    selected_colors = {MASK_COLORS[label] for label in selected_labels}
    derived = Image.new("RGBA", image.size, (0, 0, 0, 0))
    derived.putdata([pixel if mask_pixel in selected_colors else (0, 0, 0, 0)
                     for pixel, mask_pixel in zip(pixel_data(image), pixel_data(mask))])
    derived_mask = Image.new("RGBA", mask.size, (0, 0, 0, 0))
    derived_mask.putdata([mask_pixel if mask_pixel in selected_colors else (0, 0, 0, 0)
                          for mask_pixel in pixel_data(mask)])
    payload = {
        "contractId": args.contract_id, "sourceAttemptId": args.source_attempt_id,
        "sourceCandidateSha256": source_artifact["sha256"], "sourceMaskSha256": mask_artifact["sha256"],
        "label": args.label,
    }
    derivation_id = stable_id("component-derivation", payload)
    job_id = stable_id("job", payload)
    attempt_id = f"{job_id}-a001"
    output = store.pipeline / "artifacts" / job_id / attempt_id / "prepared.png"
    mask_output = store.pipeline / "artifacts" / job_id / attempt_id / "mask.png"
    output.parent.mkdir(parents=True, exist_ok=True)
    temp = output.with_name(".prepared.tmp.png")
    derived.save(temp, format="PNG", optimize=False, compress_level=9)
    if output.exists() and sha256_file(output) != sha256_file(temp):
        temp.unlink()
        raise PipelineError("derived component output is not deterministic")
    if output.exists():
        temp.unlink()
    else:
        os.replace(temp, output)
    artifact = {"path": store.relative(output), "sha256": sha256_file(output)}
    mask_temp = mask_output.with_name(".mask.tmp.png")
    derived_mask.save(mask_temp, format="PNG", optimize=False, compress_level=9)
    if mask_output.exists() and sha256_file(mask_output) != sha256_file(mask_temp):
        mask_temp.unlink()
        raise PipelineError("derived component mask is not deterministic")
    if mask_output.exists():
        mask_temp.unlink()
    else:
        os.replace(mask_temp, mask_output)
    mask_output_artifact = {"path": store.relative(mask_output), "sha256": sha256_file(mask_output)}
    receipt = {"schemaVersion": 3, "componentDerivationId": derivation_id, **payload,
               "artifact": artifact, "maskArtifact": mask_output_artifact}
    write_json_idempotent(store.record("component-derivations", derivation_id), receipt, immutable=True)
    job = {
        "schemaVersion": 3, "jobId": job_id, "state": "ready", "contractId": args.contract_id,
        "contractSha256": sha256_file(store.record("contracts", args.contract_id)), "prompt": None,
        "inputs": [source_artifact, mask_artifact], "target": {"direction": contract["direction"], "pose": contract["pose"]},
        "series": None, "conceptOnly": False, "contractRequirements": None,
        "requiresInvocation": False, "sourceMode": "derived",
    }
    write_json_idempotent(store.record("jobs", job_id), job, immutable=True)
    attempt = {
        "schemaVersion": 3, "attemptId": attempt_id, "jobId": job_id, "ordinal": 1,
        "parentAttemptId": None, "retryFeedbackId": None, "promptDelta": None,
        "technicalRemediation": False, "state": "annotated",
        "artifacts": {"prepared": artifact, "mask": mask_output_artifact},
        "report": None, "approvalId": None, "feedbackId": None,
        "componentDerivationId": derivation_id, "sourceMode": "derived",
    }
    write_json_idempotent(store.record("attempts", attempt_id), attempt)
    register_public_artifacts(store, [artifact, mask_output_artifact], "project-owned-supporting-derived")
    return attempt


def apply_assembly_transform(image: Image.Image, transform: dict[str, Any]) -> Image.Image:
    scale = transform["scalePercent"]
    width = max(1, round(image.width * scale / 100))
    height = max(1, round(image.height * scale / 100))
    result = image.resize((width, height), Image.Resampling.LANCZOS) if scale != 100 else image.copy()
    if transform["flipHorizontal"]:
        result = result.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    return normalize_transparent_rgb(result)


def apply_assembly_mask_transform(image: Image.Image, transform: dict[str, Any]) -> Image.Image:
    """Apply the assembly transform without inventing interpolated semantic labels."""
    scale = transform["scalePercent"]
    width = max(1, round(image.width * scale / 100))
    height = max(1, round(image.height * scale / 100))
    result = image.resize((width, height), Image.Resampling.NEAREST) if scale != 100 else image.copy()
    if transform["flipHorizontal"]:
        result = result.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    return normalize_transparent_rgb(result.convert("RGBA"))


def _mask_geometry(mask: Image.Image, color: tuple[int, int, int, int]) -> dict[str, float]:
    points = [(index % mask.width, index // mask.width)
              for index, pixel in enumerate(pixel_data(mask)) if pixel == color]
    if len(points) < 4:
        raise PipelineError("death geometry mask requires at least four core pixels")
    cx = sum(point[0] for point in points) / len(points)
    cy = sum(point[1] for point in points) / len(points)
    xx = sum((x - cx) ** 2 for x, _y in points) / len(points)
    yy = sum((y - cy) ** 2 for _x, y in points) / len(points)
    xy = sum((x - cx) * (y - cy) for x, y in points) / len(points)
    angle = 0.5 * math.atan2(2 * xy, xx - yy)
    ux, uy = math.cos(angle), math.sin(angle)
    vx, vy = -uy, ux
    major_values = [(x - cx) * ux + (y - cy) * uy for x, y in points]
    minor_values = [(x - cx) * vx + (y - cy) * vy for x, y in points]
    major = max(major_values) - min(major_values) + 1
    minor = max(minor_values) - min(minor_values) + 1
    if major < minor:
        major, minor = minor, major
        angle += math.pi / 2
    while angle >= math.pi / 2:
        angle -= math.pi
    while angle < -math.pi / 2:
        angle += math.pi
    return {"centerX": cx, "centerY": cy, "angleRadians": angle,
            "angleDegrees": math.degrees(angle), "major": major, "minor": minor,
            "ratio": major / minor}


def _affine_inverse_coefficients(matrix: tuple[float, float, float, float],
                                 source_center: tuple[float, float],
                                 target_center: tuple[float, float]) -> tuple[float, ...]:
    a, b, c, d = matrix
    determinant = a * d - b * c
    if abs(determinant) < 1e-8:
        raise PipelineError("death geometry transform is singular")
    ia, ib, ic, id_ = d / determinant, -b / determinant, -c / determinant, a / determinant
    sx, sy = source_center; tx, ty = target_center
    return ia, ib, sx - ia * tx - ib * ty, ic, id_, sy - ic * tx - id_ * ty


def _semantic_layer(image: Image.Image, mask: Image.Image, labels: set[str]) -> Image.Image:
    colors = {MASK_COLORS[label] for label in labels}
    result = Image.new("RGBA", image.size, (0, 0, 0, 0))
    result.putdata([pixel if mask_pixel in colors else (0, 0, 0, 0)
                    for pixel, mask_pixel in zip(pixel_data(image), pixel_data(mask))])
    return result


def _transform_semantic_layer(image: Image.Image, mask: Image.Image, labels: set[str],
                              matrix: tuple[float, float, float, float],
                              source_center: tuple[float, float], target_center: tuple[float, float]) -> tuple[Image.Image, Image.Image]:
    coefficients = _affine_inverse_coefficients(matrix, source_center, target_center)
    layer = _semantic_layer(image, mask, labels)
    label_layer = Image.new("RGBA", mask.size, (0, 0, 0, 0))
    allowed = {MASK_COLORS[label] for label in labels}
    label_layer.putdata([value if value in allowed else (0, 0, 0, 0) for value in pixel_data(mask)])
    transformed = layer.transform(image.size, Image.Transform.AFFINE, coefficients, Image.Resampling.BICUBIC)
    transformed_mask = label_layer.transform(mask.size, Image.Transform.AFFINE, coefficients, Image.Resampling.NEAREST)
    return normalize_transparent_rgb(transformed), normalize_transparent_rgb(transformed_mask)


def render_death_recipe(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    spec_rel = store.relative(args.spec, must_exist=True)
    spec = json.loads(store.absolute(spec_rel).read_text(encoding="utf-8"))
    required = {"schemaVersion", "assetId", "sourceImage", "sourceMask", "referenceMask",
                "output", "reviewOutput", "angleToleranceDegrees", "ratioTolerance"}
    optional = {"expressionOverlay", "eyeRegions", "axisPolicy"}
    if not isinstance(spec, dict) or set(spec) - optional != required or spec["schemaVersion"] != 1:
        raise PipelineError("death recipe requires schemaVersion 1 and the canonical geometry fields")
    source_rel = store.relative(spec["sourceImage"], must_exist=True)
    source_mask_rel = store.relative(spec["sourceMask"], must_exist=True)
    reference_mask_rel = store.relative(spec["referenceMask"], must_exist=True)
    source = Image.open(store.absolute(source_rel)).convert("RGBA")
    source_mask = Image.open(store.absolute(source_mask_rel)).convert("RGBA")
    reference_mask = Image.open(store.absolute(reference_mask_rel)).convert("RGBA")
    if source.size != (256, 256) or source_mask.size != source.size or reference_mask.size != source.size:
        raise PipelineError("death recipe inputs must be matching 256x256 images")
    source_geometry = _mask_geometry(source_mask, MASK_COLORS["core"])
    reference_geometry = _mask_geometry(reference_mask, MASK_COLORS["core"])
    source_angle = source_geometry["angleRadians"]; target_angle = reference_geometry["angleRadians"]
    target_ratio = reference_geometry["ratio"]
    if spec.get("axisPolicy", "legacy-major-only") == "legacy-major-only":
        major_scale = min(1.0, target_ratio / source_geometry["ratio"])
        minor_scale = 1.0
    elif target_ratio >= source_geometry["ratio"]:
        major_scale = 1.0
        minor_scale = source_geometry["ratio"] / target_ratio
    elif spec.get("axisPolicy") == "toward-reference-no-expand":
        major_scale = target_ratio / source_geometry["ratio"]
        minor_scale = 1.0
    else:
        raise PipelineError("death recipe axisPolicy must be toward-reference-no-expand")
    cos_s, sin_s = math.cos(source_angle), math.sin(source_angle)
    cos_t, sin_t = math.cos(target_angle), math.sin(target_angle)
    # R(target) * diag(major_scale, minor_scale) * R(-source).  Only the
    # axis that moves the ratio toward the approved reference may shrink;
    # death shaping never expands either local axis.
    matrix = (
        cos_t * major_scale * cos_s + sin_t * minor_scale * sin_s,
        cos_t * major_scale * sin_s - sin_t * minor_scale * cos_s,
        sin_t * major_scale * cos_s - cos_t * minor_scale * sin_s,
        sin_t * major_scale * sin_s + cos_t * minor_scale * cos_s,
    )
    source_center = (source_geometry["centerX"], source_geometry["centerY"])
    target_center = source_center
    core_image, core_mask = _transform_semantic_layer(source, source_mask, {"core"}, matrix, source_center, target_center)
    delta = target_angle - source_angle; rotation = (math.cos(delta), -math.sin(delta), math.sin(delta), math.cos(delta))
    attachments_image, attachments_mask = _transform_semantic_layer(
        source, source_mask, {"head_appendage", "near_hand", "far_hand", "near_foot", "far_foot"},
        rotation, source_center, target_center)
    equipment_image = _semantic_layer(source, source_mask, {"equipment"})
    equipment_mask = Image.new("RGBA", source.size, (0, 0, 0, 0))
    equipment_mask.putdata([value if value == MASK_COLORS["equipment"] else (0, 0, 0, 0)
                            for value in pixel_data(source_mask)])
    canvas = Image.new("RGBA", source.size, (0, 0, 0, 0)); mask_canvas = canvas.copy()
    for layer in (core_image, attachments_image, equipment_image): canvas.alpha_composite(layer)
    for layer in (core_mask, attachments_mask, equipment_mask): mask_canvas.alpha_composite(layer)
    expression_artifact = None
    expression = None
    if spec.get("expressionOverlay"):
        expression_rel = store.relative(spec["expressionOverlay"], must_exist=True)
        expression = Image.open(store.absolute(expression_rel)).convert("RGBA")
        if expression.size != source.size:
            raise PipelineError("death expression overlay must be 256x256")
        regions = spec.get("eyeRegions") or []
        if not regions or any(not isinstance(region, list) or len(region) != 4 for region in regions):
            raise PipelineError("death expression overlay requires eyeRegions rectangles")
        for index, pixel in enumerate(pixel_data(expression)):
            if not pixel[3]: continue
            x, y = index % expression.width, index // expression.width
            if not any(x0 <= x <= x1 and y0 <= y <= y1 for x0, y0, x1, y1 in regions):
                raise PipelineError("death expression overlay has opaque pixels outside eyeRegions")
        expression_artifact = {"path": expression_rel, "sha256": sha256_file(store.absolute(expression_rel))}
    alpha_box = canvas.getbbox()
    if not alpha_box:
        raise PipelineError("death recipe produced an empty image")
    cx = (alpha_box[0] + alpha_box[2]) // 2; cy = (alpha_box[1] + alpha_box[3]) // 2
    shift = (128 - cx, 128 - cy)
    # Recompose without wrap-around; alpha_composite clips at the canvas edge.
    composed = Image.alpha_composite(Image.alpha_composite(core_image, attachments_image), equipment_image)
    canvas = Image.new("RGBA", source.size, (0, 0, 0, 0)); canvas.alpha_composite(composed, shift)
    if expression is not None:
        # Expression overlays are authored against the final centered face and
        # therefore compose after geometry centering.
        canvas.alpha_composite(expression)
    mask_centered = Image.new("RGBA", source.size, (0, 0, 0, 0)); mask_centered.alpha_composite(mask_canvas, shift)
    output_rel = store.relative(spec["output"]); review_rel = store.relative(spec["reviewOutput"])
    output_path = store.absolute(output_rel); review_path = store.absolute(review_rel)
    output_path.parent.mkdir(parents=True, exist_ok=True); review_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path, format="PNG", optimize=False, compress_level=9)
    result_geometry = _mask_geometry(mask_centered, MASK_COLORS["core"])
    angle_error = abs(result_geometry["angleDegrees"] - reference_geometry["angleDegrees"])
    ratio_error = abs(result_geometry["ratio"] - reference_geometry["ratio"]) / reference_geometry["ratio"]
    report = {"angleErrorDegrees": angle_error, "ratioError": ratio_error,
              "passed": angle_error <= spec["angleToleranceDegrees"] and ratio_error <= spec["ratioTolerance"]}
    review = Image.new("RGBA", (768, 256), (0, 0, 0, 0))
    reference_visual = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); reference_visual.putalpha(reference_mask.getchannel("A"))
    review.alpha_composite(source, (0, 0)); review.alpha_composite(reference_visual, (256, 0)); review.alpha_composite(canvas, (512, 0))
    review.save(review_path, format="PNG", optimize=False, compress_level=9)
    artifact = lambda rel: {"path": rel, "sha256": sha256_file(store.absolute(rel))}
    payload = {"schemaVersion": 1, "assetId": spec["assetId"], "spec": artifact(spec_rel),
               "source": artifact(source_rel), "sourceMask": artifact(source_mask_rel),
               "referenceMask": artifact(reference_mask_rel), "expressionOverlay": expression_artifact,
               "sourceGeometry": source_geometry, "referenceGeometry": reference_geometry,
               "resultGeometry": result_geometry, "shift": list(shift), "report": report,
               "artifacts": {"prepared": artifact(output_rel), "review": artifact(review_rel)}}
    recipe_id = stable_id("death-recipe", payload)
    payload["deathRecipeId"] = recipe_id
    write_json_idempotent(store.record("death-recipes", recipe_id), payload, immutable=True)
    register_supporting_artifact(store, argparse.Namespace(
        path=output_rel, role="death-recipe-candidate",
        note=f"Deterministic death recipe output {recipe_id}; candidate only, not human-approved or promoted."))
    register_supporting_artifact(store, argparse.Namespace(
        path=review_rel, role="death-recipe-review",
        note=f"Deterministic source/reference/result review for {recipe_id}."))
    return payload


def _validated_assembly_spec(store: Store, spec_path: str) -> tuple[dict[str, Any], dict[str, Any]]:
    raw = json.loads(store.absolute(store.relative(spec_path, must_exist=True)).read_text(encoding="utf-8"))
    if not isinstance(raw, dict) or set(raw) != {"assetId", "contractId", "canvas", "layers"}:
        raise PipelineError("assembly spec requires exactly assetId, contractId, canvas, and layers")
    contract = load_json(store.record("contracts", raw["contractId"]))
    if contract.get("schemaVersion") != 3 or contract.get("assetRole") != "assembled_sprite":
        raise PipelineError("assembly requires a schema v3 assembled_sprite contract")
    if raw["assetId"] != contract["assetId"]:
        raise PipelineError("assembly assetId must match contract")
    if raw["canvas"] != [256, 256]:
        raise PipelineError("schema v3 sprite assemblies require a 256x256 canvas")
    roles = [layer.get("role") for layer in raw["layers"]]
    if len(roles) != len(ASSEMBLY_LAYER_ROLES) or set(roles) != ASSEMBLY_LAYER_ROLES:
        raise PipelineError("assembly requires each of the six component roles exactly once")
    normalized_layers = []
    for layer in raw["layers"]:
        if set(layer) != {"role", "attemptId", "transform"} or layer["role"] not in ASSEMBLY_LAYER_ROLES:
            raise PipelineError("invalid assembly layer")
        transform = layer["transform"]
        if set(transform) != {"scalePercent", "translate", "flipHorizontal"}:
            raise PipelineError("assembly transform only supports scalePercent, translate, and flipHorizontal")
        if not isinstance(transform["scalePercent"], int) or isinstance(transform["scalePercent"], bool) or not 1 <= transform["scalePercent"] <= 400:
            raise PipelineError("assembly scalePercent must be an integer from 1 to 400")
        if (not isinstance(transform["translate"], list) or len(transform["translate"]) != 2
                or any(not isinstance(value, int) or isinstance(value, bool) for value in transform["translate"])):
            raise PipelineError("assembly translate must contain two integers")
        if not isinstance(transform["flipHorizontal"], bool):
            raise PipelineError("assembly flipHorizontal must be boolean")
        attempt = load_json(store.record("attempts", layer["attemptId"]))
        job = load_json(store.record("jobs", attempt["jobId"]))
        component_contract = _component_contract(store, job["contractId"])
        if layer["role"].endswith("paw_overlay"):
            expected_kind = "paw_overlay"
        elif layer["role"].endswith("foot_overlay"):
            expected_kind = "foot_overlay"
        else:
            expected_kind = layer["role"]
        if component_contract.get("componentKind") != expected_kind:
            raise PipelineError("assembly layer role does not match component kind")
        artifact = candidate_artifact(attempt)
        mask_artifact = candidate_mask_artifact(attempt)
        if not mask_artifact:
            raise PipelineError("assembly components require semantic masks")
        with Image.open(store.absolute(artifact["path"], must_exist=True)) as image_source, Image.open(
                store.absolute(mask_artifact["path"], must_exist=True)) as mask_source:
            component_image = image_source.convert("RGBA")
            component_mask = mask_source.convert("RGBA")
        if component_image.size != component_mask.size:
            raise PipelineError("assembly component and semantic mask sizes differ")
        expected_labels = {
            "far_foot_overlay": {"far_foot"},
            "far_paw_overlay": {"far_hand"},
            "body": {"core", "head_appendage"},
            "equipment": {"equipment"},
            "near_paw_overlay": {"near_hand"},
            "near_foot_overlay": {"near_foot"},
        }[layer["role"]]
        allowed_colors = {MASK_COLORS[label] for label in expected_labels}
        for subject_pixel, mask_pixel in zip(pixel_data(component_image), pixel_data(component_mask)):
            if mask_pixel[3] and mask_pixel not in allowed_colors:
                raise PipelineError(f"assembly layer {layer['role']} contains a foreign semantic label")
            if subject_pixel[3] and mask_pixel not in allowed_colors:
                raise PipelineError(f"assembly layer {layer['role']} contains unlabelled subject pixels")
        source_mode = component_contract.get("sourceMode")
        if source_mode == "generated":
            if attempt.get("state") not in {"approved", "promoted"} or not attempt.get("approvalId"):
                raise PipelineError("generated assembly components require human approval")
            approval = load_json(store.record("approvals", attempt["approvalId"]))
            if (approval.get("decision") != "approved" or approval.get("reviewer") != "cty41"
                    or approval.get("attemptId") != attempt["attemptId"]
                    or approval.get("candidateSha256") != artifact.get("sha256")
                    or approval.get("maskSha256") != mask_artifact.get("sha256")):
                raise PipelineError("generated component approval receipt mismatch")
        elif source_mode in {"derived", "pre_v3_import"}:
            report_artifact = attempt.get("report") or {}
            report_path = store.absolute(report_artifact.get("path", ""), must_exist=True)
            if sha256_file(report_path) != report_artifact.get("sha256") or not load_json(report_path).get("passed"):
                raise PipelineError(f"{source_mode} assembly components require a passing validation report")
            if attempt.get("state") not in {"review_pending", "approved", "promoted"}:
                raise PipelineError(f"{source_mode} assembly component is not review-ready")
            receipt_group = "component-derivations" if source_mode == "derived" else "component-migrations"
            receipt_key = "componentDerivationId" if source_mode == "derived" else "componentMigrationId"
            receipt_id = attempt.get(receipt_key)
            if not receipt_id:
                raise PipelineError(f"{source_mode} assembly component is missing its source receipt")
            receipt = load_json(store.record(receipt_group, receipt_id))
            receipt_artifact = receipt.get("artifact") if source_mode == "derived" else receipt.get("prepared")
            if (receipt.get("contractId") != component_contract["contractId"]
                    or not receipt_artifact or receipt_artifact.get("sha256") != artifact.get("sha256")):
                raise PipelineError(f"{source_mode} component source receipt mismatch")
        else:
            raise PipelineError("assembly component has unsupported source mode")
        normalized_layers.append({**layer, "artifact": artifact, "componentContractId": component_contract["contractId"]})
    normalized = {"assetId": raw["assetId"], "contractId": raw["contractId"], "canvas": raw["canvas"], "layers": normalized_layers}
    return normalized, contract


def create_assembly(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    normalized, _contract = _validated_assembly_spec(store, args.spec)
    assembly_id = stable_id("assembly", normalized)
    record = {"schemaVersion": 3, "assemblyId": assembly_id, **normalized}
    write_json_idempotent(store.record("assemblies", assembly_id), record, immutable=True)
    return record


def render_assembly(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    assembly = load_json(store.record("assemblies", args.assembly_id))
    contract = load_json(store.record("contracts", assembly["contractId"]))
    job_payload = {"assemblyId": args.assembly_id, "contractId": assembly["contractId"], "rendererVersion": 4}
    job_id = stable_id("job", job_payload)
    attempt_id = f"{job_id}-a001"
    attempt_path = store.record("attempts", attempt_id)
    if attempt_path.is_file():
        return load_json(attempt_path)
    canvas = Image.new("RGBA", tuple(assembly["canvas"]), (0, 0, 0, 0))
    mask_canvas = Image.new("RGBA", tuple(assembly["canvas"]), (0, 0, 0, 0))
    for layer in assembly["layers"]:
        source_path = store.absolute(layer["artifact"]["path"], must_exist=True)
        if sha256_file(source_path) != layer["artifact"]["sha256"]:
            raise PipelineError("assembly component hash drift")
        rendered = apply_assembly_transform(Image.open(source_path).convert("RGBA"), layer["transform"])
        canvas.alpha_composite(rendered, tuple(layer["transform"]["translate"]))
        layer_attempt = load_json(store.record("attempts", layer["attemptId"]))
        mask_artifact = candidate_mask_artifact(layer_attempt)
        mask_path = store.absolute(mask_artifact["path"], must_exist=True)
        if sha256_file(mask_path) != mask_artifact["sha256"]:
            raise PipelineError("assembly component mask hash drift")
        rendered_mask = apply_assembly_mask_transform(Image.open(mask_path), layer["transform"])
        mask_canvas.alpha_composite(rendered_mask, tuple(layer["transform"]["translate"]))
    # Resampling a keyed component can reintroduce subpixel green on the edge.
    # Exact/fuzzy chroma is forbidden by the sprite contract, so normalize it
    # deterministically after the full layer stack has been composed.
    canvas = clean_resampled_chroma(canvas, "#00ff00", 12)
    mask_pixels = []
    for mask_pixel, subject_pixel in zip(pixel_data(mask_canvas), pixel_data(canvas)):
        subject_alpha = subject_pixel[3]
        mask_pixels.append(mask_pixel if subject_alpha else (0, 0, 0, 0))
    mask_canvas.putdata(mask_pixels)
    mask_canvas = normalize_transparent_rgb(mask_canvas)
    output = store.pipeline / "artifacts" / job_id / attempt_id / "prepared.png"
    mask_output = output.with_name("mask.png")
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, format="PNG", optimize=False, compress_level=9)
    mask_canvas.save(mask_output, format="PNG", optimize=False, compress_level=9)
    artifact = {"path": store.relative(output), "sha256": sha256_file(output)}
    mask_output_artifact = {"path": store.relative(mask_output), "sha256": sha256_file(mask_output)}
    receipt_payload = {
        "assemblyId": args.assembly_id,
        "rendererVersion": 4,
        "assemblySha256": sha256_file(store.record("assemblies", args.assembly_id)),
        "output": artifact,
        "maskOutput": mask_output_artifact,
    }
    receipt_id = stable_id("assembly-render", receipt_payload)
    receipt = {"schemaVersion": 3, "assemblyRenderId": receipt_id, **receipt_payload}
    write_json_idempotent(store.record("assembly-renders", receipt_id), receipt, immutable=True)
    job = {
        "schemaVersion": 3, "jobId": job_id, "state": "ready", "contractId": contract["contractId"],
        "contractSha256": sha256_file(store.record("contracts", contract["contractId"])), "prompt": None,
        "inputs": [layer["artifact"] for layer in assembly["layers"]],
        "target": {"direction": contract["direction"], "pose": contract["pose"]}, "series": None,
        "conceptOnly": False, "contractRequirements": None, "requiresInvocation": False, "sourceMode": "derived",
    }
    write_json_idempotent(store.record("jobs", job_id), job, immutable=True)
    attempt = {
        "schemaVersion": 3, "attemptId": attempt_id, "jobId": job_id, "ordinal": 1,
        "parentAttemptId": None, "retryFeedbackId": None, "promptDelta": None,
        "technicalRemediation": False, "state": "annotated",
        "artifacts": {"prepared": artifact, "mask": mask_output_artifact},
        "report": None, "approvalId": None, "feedbackId": None,
        "assemblyId": args.assembly_id, "assemblyRenderId": receipt_id, "sourceMode": "derived",
    }
    write_json_idempotent(attempt_path, attempt)
    register_public_artifacts(store, [artifact, mask_output_artifact], "project-owned-supporting-derived")
    return attempt


def update_approved_cases(store: Store, contract: dict[str, Any], master_path: str) -> None:
    cases_path = store.root / ".agents/skills/pure-run-artwork-pipeline/examples/cases.json"
    if not cases_path.is_file():
        return
    cases = json.loads(cases_path.read_text(encoding="utf-8"))
    direction_key = contract["direction"].replace("-", "_")
    if direction_key not in {"down_right", "up_left"}:
        return
    asset_id = contract.get("approvedAssetId", contract["assetId"])
    directions: dict[str, str] = {}
    for attempt_path in (store.pipeline / "attempts").glob("*.json"):
        attempt = load_json(attempt_path)
        if attempt.get("state") != "promoted":
            continue
        job = load_json(store.record("jobs", attempt["jobId"]))
        promoted_contract = load_json(store.record("contracts", job["contractId"]))
        if promoted_contract.get("approvedAssetId", promoted_contract["assetId"]) != asset_id:
            continue
        key = promoted_contract["direction"].replace("-", "_")
        promoted_master = attempt.get("artifacts", {}).get("promoted", {}).get("master", {}).get("path")
        if key in {"down_right", "up_left"} and promoted_master:
            directions[key] = promoted_master
    directions[direction_key] = master_path
    entry = next((item for item in cases.get("approved_assets", []) if item.get("id") == asset_id), None)
    if set(directions) != {"down_right", "up_left"}:
        if entry is not None:
            cases["approved_assets"].remove(entry)
        write_json_idempotent(cases_path, cases)
        return
    if entry is None:
        entry = {"id": asset_id}
        cases.setdefault("approved_assets", []).append(entry)
    for key, path in directions.items():
        existing = entry.get(key)
        if existing and existing != path:
            raise PipelineError(f"approved mother list already has a different {key} path for {asset_id}")
        entry[key] = path
    write_json_idempotent(cases_path, cases)


def promote(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] == "promoted":
        return attempt
    if attempt["state"] != "approved":
        raise PipelineError("promote requires approved attempt")
    approval = load_json(store.record("approvals", attempt["approvalId"]))
    candidate = candidate_artifact(attempt)
    if approval["candidateSha256"] != candidate["sha256"]:
        raise PipelineError("approval no longer matches candidate")
    if approval.get("approvalMode") == "gate-exception":
        validate_exception_receipt(store, attempt, approval)
    job = load_json(store.record("jobs", attempt["jobId"]))
    binding = job.get("series")
    bound_series = bound_pose = None
    if binding:
        bound_series = load_json(store.record("series", binding["seriesId"]))
        bound_pose = series_pose(bound_series, binding["poseId"])
        if job.get("conceptOnly") or bound_pose["state"] == "provisional" or bound_series.get("provisionalAnchorAttemptId") == attempt["attemptId"]:
            raise PipelineError("provisional series artwork cannot be promoted")
    contract = load_json(store.record("contracts", job["contractId"]))
    if contract.get("assetRole") == "component" or contract.get("runtimeEligible") is False:
        raise PipelineError("approved components cannot be promoted as runtime sprites")
    master = copy_bound(store, candidate["path"], contract["outputs"]["master"])
    preview_path = store.absolute(contract["outputs"]["preview"])
    preview_path.parent.mkdir(parents=True, exist_ok=True)
    if attempt.get("sourceMode") == "reviewed_import":
        imported_preview = attempt.get("artifacts", {}).get("importedPreview")
        if not imported_preview:
            raise PipelineError("reviewed import is missing its approved preview")
        preview_artifact = copy_bound(store, imported_preview["path"], contract["outputs"]["preview"])
    else:
        master_image = Image.open(store.absolute(master["path"])).convert("RGBA")
        preview = make_preview(master_image)
        if preview_path.exists():
            temp = preview_path.with_suffix(".candidate.png")
            preview.save(temp, format="PNG", optimize=False, compress_level=9)
            if sha256_file(temp) != sha256_file(preview_path):
                temp.unlink()
                raise PipelineError("promotion preview exists with different bytes")
            temp.unlink()
        else:
            preview.save(preview_path, format="PNG", optimize=False, compress_level=9)
        preview_artifact = {"path": store.relative(preview_path), "sha256": sha256_file(preview_path)}
    update_provenance(store, [master, preview_artifact], contract)
    update_approved_cases(store, contract, master["path"])
    attempt["artifacts"]["promoted"] = {"master": master, "preview": preview_artifact}
    transition(attempt, {"approved"}, "promoted")
    save_attempt(store, attempt)
    if bound_series and bound_pose:
        bound_pose["state"] = "promoted"
        save_series(store, bound_series)
    return attempt


def refresh_promoted_preview(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt.get("state") != "promoted":
        raise PipelineError("refresh-promoted-preview requires a promoted attempt")
    promoted = attempt.get("artifacts", {}).get("promoted", {})
    master_artifact = promoted.get("master")
    preview_artifact = promoted.get("preview")
    if not master_artifact or not preview_artifact:
        raise PipelineError("promoted attempt is missing master or preview")
    master_path = store.absolute(master_artifact["path"], must_exist=True)
    if sha256_file(master_path) != master_artifact["sha256"]:
        raise PipelineError("promoted master hash mismatch")
    preview_path = store.absolute(preview_artifact["path"], must_exist=True)
    with Image.open(master_path) as master_image:
        regenerated = make_preview(master_image.convert("RGBA"))
    temporary = preview_path.with_suffix(".refresh.png")
    regenerated.save(temporary, format="PNG", optimize=False, compress_level=9)
    if sha256_file(temporary) != sha256_file(preview_path):
        temporary.replace(preview_path)
    else:
        temporary.unlink()
    new_hash = sha256_file(preview_path)
    preview_artifact["sha256"] = new_hash
    manifest_path = store.root / "Tools/public-release/asset-provenance.json"
    manifest = load_json(manifest_path)
    entry = next((item for item in manifest.get("entries", []) if item.get("path") == preview_artifact["path"]), None)
    if not entry:
        raise PipelineError("promoted preview provenance entry is missing")
    entry["sha256"] = new_hash
    write_json_idempotent(manifest_path, manifest)
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    update_approved_cases(store, contract, master_artifact["path"])
    save_attempt(store, attempt)
    return attempt


def inventory_state(rel: str) -> str:
    parts = set(Path(rel).parts)
    if "rejected" in parts or "superseded" in parts:
        return "legacy-rejected"
    if parts & FORMAL_DIRS:
        return "legacy-approved"
    return "legacy-unresolved"


def migrate_legacy(store: Store, _args: argparse.Namespace) -> dict[str, Any]:
    provenance_path = store.root / "Tools/public-release/asset-provenance.json"
    provenance = load_json(provenance_path) if provenance_path.exists() else {"entries": []}
    approved_hashes = {entry["path"]: entry["sha256"] for entry in provenance.get("entries", []) if entry.get("status") == "approved"}
    entries = []
    for path in sorted((store.root / "Tools/artworks").rglob("*.png")):
        if store.pipeline in path.parents:
            continue
        rel = store.relative(path)
        state = inventory_state(rel)
        digest = sha256_file(path)
        if state == "legacy-approved" and approved_hashes.get(rel) != digest:
            state = "legacy-unresolved"
        entries.append({"path": rel, "sha256": digest, "state": state, "lineage": None,
                        "mayBeAnchor": False, "note": "directory is historical state evidence only"})
    record = {"schemaVersion": 1, "assets": entries}
    write_json_idempotent(store.pipeline / "legacy-assets.json", record)
    provenance_entries = provenance.get("entries", [])
    by_path = {entry["path"]: entry for entry in provenance_entries}
    for path in sorted((store.root / "Tools/artworks").rglob("*.png")):
        rel = store.relative(path)
        if rel in by_path:
            continue
        entry = {
            "path": rel, "sha256": sha256_file(path), "status": "approved",
            "rightsHolder": "cty41", "license": "CC-BY-4.0",
            "provenance": "project-owned-gpt-generated-or-derived",
        }
        by_path[rel] = entry
        provenance_entries.append(entry)
    provenance["entries"] = provenance_entries
    write_json_idempotent(provenance_path, provenance)
    return record


def strict_check(store: Store, strict: bool) -> dict[str, Any]:
    issues = []
    inventory_path = store.pipeline / "legacy-assets.json"
    inventory = load_json(inventory_path) if inventory_path.exists() else {"assets": []}
    inventory_by_path = {item["path"]: item for item in inventory.get("assets", [])}
    for rel, item in inventory_by_path.items():
        path = store.absolute(rel)
        if not path.exists():
            issues.append(f"inventory_missing:{rel}")
        elif sha256_file(path) != item["sha256"]:
            issues.append(f"inventory_hash:{rel}")
    current = {store.relative(path) for path in (store.root / "Tools/artworks").rglob("*.png") if store.pipeline not in path.parents}
    registered_paths = set()
    for attempt_path in (store.pipeline / "attempts").glob("*.json"):
        for artifact in load_json(attempt_path).get("artifacts", {}).values():
            values = artifact.values() if isinstance(artifact, dict) and "path" not in artifact else [artifact]
            registered_paths.update(value["path"] for value in values if isinstance(value, dict) and value.get("path"))
    for group in ("pose-guides", "supporting-artifacts"):
        for record_path in (store.pipeline / group).glob("*.json"):
            record = load_json(record_path)
            artifact = record.get("artifact", {})
            if artifact.get("path"):
                registered_paths.add(artifact["path"])
    missing = sorted(current - set(inventory_by_path) - registered_paths)
    issues.extend(f"asset_unregistered:{path}" for path in missing)
    for path in sorted((store.pipeline / "attempts").glob("*.json")):
        attempt = load_json(path)
        if attempt.get("state") not in STATES:
            issues.append(f"attempt_state:{path.name}")
        if bool(attempt.get("artifacts", {}).get("calibrated")) != bool(attempt.get("artifacts", {}).get("calibratedMask")):
            issues.append(f"attempt_calibration_pair:{attempt.get('attemptId')}")
        for artifact in attempt.get("artifacts", {}).values():
            values = artifact.values() if isinstance(artifact, dict) and "path" not in artifact else [artifact]
            for value in values:
                if isinstance(value, dict) and value.get("path"):
                    target = store.absolute(value["path"])
                    if not target.exists() or sha256_file(target) != value.get("sha256"):
                        issues.append(f"artifact_hash:{attempt['attemptId']}:{value.get('path')}")
        if not store.record("jobs", attempt.get("jobId", "")).is_file():
            issues.append(f"attempt_job_missing:{attempt.get('attemptId')}")
        else:
            job_record = load_json(store.record("jobs", attempt["jobId"]))
            contract_record = load_json(store.record("contracts", job_record["contractId"]))
            if contract_record.get("assetRole") == "component" and attempt.get("state") == "promoted":
                issues.append(f"component_promoted:{attempt.get('attemptId')}")
        if attempt.get("state") in {"approved", "rejected", "promoted"} and not attempt.get("approvalId"):
            issues.append(f"attempt_approval_missing:{attempt.get('attemptId')}")
        approval_id = attempt.get("approvalId")
        if approval_id:
            approval_path = store.record("approvals", approval_id)
            if not approval_path.is_file():
                issues.append(f"attempt_approval_record_missing:{attempt.get('attemptId')}")
            else:
                receipt = load_json(approval_path)
                expected_decision = "rejected" if attempt.get("state") == "rejected" else "approved"
                if receipt.get("decision") != expected_decision:
                    issues.append(f"attempt_approval_decision:{attempt.get('attemptId')}")
                approval_mode = receipt.get("approvalMode", "standard")
                if approval_mode not in {"standard", "gate-exception"}:
                    issues.append(f"attempt_approval_mode:{attempt.get('attemptId')}")
                if approval_mode == "gate-exception":
                    try:
                        validate_exception_receipt(store, attempt, receipt)
                    except PipelineError:
                        issues.append(f"attempt_gate_exception_invalid:{attempt.get('attemptId')}")
                elif attempt.get("report"):
                    report_path = store.absolute(attempt["report"]["path"], must_exist=True)
                    if not load_json(report_path).get("passed"):
                        issues.append(f"attempt_standard_approval_failed_report:{attempt.get('attemptId')}")
                prepared = candidate_artifact(attempt)
                mask = candidate_mask_artifact(attempt)
                if receipt.get("candidateSha256") != prepared.get("sha256") or receipt.get("maskSha256") != mask.get("sha256"):
                    issues.append(f"attempt_approval_hash:{attempt.get('attemptId')}")
                job = load_json(store.record("jobs", attempt["jobId"]))
                contract = load_json(store.record("contracts", job["contractId"]))
                required_reviews = required_review_keys(attempt, contract)
                if set(attempt.get("artifacts", {}).get("review", {})) != required_reviews:
                    issues.append(f"attempt_review_set:{attempt.get('attemptId')}")
                receipt_reviews = receipt.get("reviewSha256")
                current_reviews = {key: value.get("sha256") for key, value in sorted(attempt.get("artifacts", {}).get("review", {}).items())}
                if receipt_reviews is not None and receipt_reviews != current_reviews:
                    issues.append(f"attempt_review_receipt_hash:{attempt.get('attemptId')}")
    for path in sorted((store.pipeline / "jobs").glob("*.json")):
        job = load_json(path)
        contract_path = store.record("contracts", job.get("contractId", ""))
        if not contract_path.is_file():
            issues.append(f"job_contract_missing:{job.get('jobId')}")
        elif sha256_file(contract_path) != job.get("contractSha256"):
            issues.append(f"job_contract_hash:{job.get('jobId')}")
        else:
            contract = load_json(contract_path)
            expected_requirements = None if not contract.get("occlusion") else {
                "occlusion": contract["occlusion"],
                "imageGenDirective": "Draw behind-core equipment and both hand paws first, then draw the capsule body over their inner portions; only outer arcs may remain visible.",
            }
            if job.get("contractRequirements") != expected_requirements:
                issues.append(f"job_contract_requirements:{job.get('jobId')}")
        for bound in ([job["prompt"]] if isinstance(job.get("prompt"), dict) else []) + job.get("inputs", []):
            target = store.absolute(bound.get("path", ""))
            if not target.is_file() or sha256_file(target) != bound.get("sha256"):
                issues.append(f"job_input_hash:{job.get('jobId')}:{bound.get('path')}")
        if job.get("conceptOnly"):
            for attempt in list_attempts(store, job["jobId"]):
                if attempt.get("state") in {"approved", "promoted"}:
                    issues.append(f"concept_only_formal:{attempt.get('attemptId')}")
    for path in sorted((store.pipeline / "contracts").glob("*.json")):
        contract = load_json(path)
        if contract.get("schemaVersion") == 3:
            role = contract.get("assetRole")
            if role not in ASSET_ROLES:
                issues.append(f"contract_asset_role:{contract.get('contractId')}")
            if role == "component":
                if contract.get("componentKind") not in COMPONENT_KINDS or contract.get("runtimeEligible") is not False:
                    issues.append(f"contract_component_shape:{contract.get('contractId')}")
            elif contract.get("componentKind") is not None or contract.get("runtimeEligible") is not True:
                issues.append(f"contract_assembled_shape:{contract.get('contractId')}")
            if contract.get("sourceMode") not in SOURCE_MODES:
                issues.append(f"contract_source_mode:{contract.get('contractId')}")
        if contract.get("schemaVersion") == 2 and contract.get("requiresInvocation"):
            composition_ref = contract.get("compositionSpec")
            if not composition_ref:
                issues.append(f"contract_composition_missing:{contract.get('contractId')}")
            else:
                target = store.record("compositions", composition_ref.get("compositionId", ""))
                if not target.is_file() or sha256_file(target) != composition_ref.get("sha256"):
                    issues.append(f"contract_composition_hash:{contract.get('contractId')}")
        occlusion = contract.get("occlusion")
        if occlusion:
            if set(occlusion.get("layerRules", {})) - OCCLUSION_LABELS:
                issues.append(f"contract_occlusion_label:{contract.get('contractId')}")
            if any(rule != "behind-core" for rule in occlusion.get("layerRules", {}).values()):
                issues.append(f"contract_occlusion_rule:{contract.get('contractId')}")
            if set(occlusion.get("visibilityCaps", {})) - set(occlusion.get("layerRules", {})):
                issues.append(f"contract_visibility_cap_label:{contract.get('contractId')}")
        anchor = contract.get("anchor")
        if anchor:
            target = store.absolute(anchor.get("path", ""))
            if not target.is_file() or sha256_file(target) != anchor.get("sha256"):
                issues.append(f"contract_anchor_hash:{contract.get('contractId')}")
            mask_path = anchor.get("maskPath")
            if mask_path:
                mask = store.absolute(mask_path)
                if not mask.is_file() or sha256_file(mask) != anchor.get("maskSha256"):
                    issues.append(f"contract_anchor_mask_hash:{contract.get('contractId')}")
    for path in sorted((store.pipeline / "feedback").glob("*.json")):
        feedback = load_json(path)
        if feedback.get("schemaVersion") == 2:
            if feedback.get("authorType") not in {"agent", "human"}:
                issues.append(f"feedback_author_type:{feedback.get('feedbackId')}")
            if set(feedback.get("categories", [])) - FEEDBACK_CATEGORIES:
                issues.append(f"feedback_category:{feedback.get('feedbackId')}")
            if feedback.get("disposition") not in FEEDBACK_VERDICTS:
                issues.append(f"feedback_disposition:{feedback.get('feedbackId')}")
        attempt_path = store.record("attempts", feedback.get("attemptId", ""))
        if not attempt_path.is_file():
            issues.append(f"feedback_attempt_missing:{feedback.get('feedbackId')}")
            continue
        attempt = load_json(attempt_path)
        if attempt.get("feedbackId") != feedback.get("feedbackId"):
            issues.append(f"feedback_backlink:{feedback.get('feedbackId')}")
        if feedback.get("candidateSha256") != candidate_artifact(attempt).get("sha256"):
            remediated = any(
                child.get("technicalRemediation") and child.get("parentAttemptId") == attempt.get("attemptId")
                and child.get("artifacts", {}).get("raw", {}).get("sha256") == attempt.get("artifacts", {}).get("raw", {}).get("sha256")
                for child in (load_json(item) for item in (store.pipeline / "attempts").glob("*.json"))
            )
            if not remediated:
                issues.append(f"feedback_candidate_hash:{feedback.get('feedbackId')}")
    for path in sorted((store.pipeline / "feedback-addenda").glob("*.json")):
        addendum = load_json(path)
        feedback_path = store.record("feedback", addendum.get("parentFeedbackId", ""))
        attempt_path = store.record("attempts", addendum.get("attemptId", ""))
        if not feedback_path.is_file() or not attempt_path.is_file():
            issues.append(f"feedback_addendum_parent:{addendum.get('feedbackAddendumId')}")
            continue
        feedback = load_json(feedback_path)
        attempt = load_json(attempt_path)
        if feedback.get("attemptId") != attempt.get("attemptId"):
            issues.append(f"feedback_addendum_attempt:{addendum.get('feedbackAddendumId')}")
        if addendum.get("feedbackAddendumId") not in attempt.get("feedbackAddendumIds", []):
            issues.append(f"feedback_addendum_backlink:{addendum.get('feedbackAddendumId')}")
    for path in sorted((store.pipeline / "series").glob("*.json")):
        series = load_json(path)
        limit = series.get("maxUniqueOutputs")
        if limit is not None and (not isinstance(limit, int) or isinstance(limit, bool) or limit < 1):
            issues.append(f"series_limit:{series.get('seriesId')}")
        seen_attempts = set()
        for pose in series.get("poses", []):
            if pose.get("state") not in SERIES_STATES:
                issues.append(f"series_pose_state:{series.get('seriesId')}:{pose.get('poseId')}")
            if limit is not None and len(unique_pose_hashes(store, pose)) > limit:
                issues.append(f"series_pose_limit:{series.get('seriesId')}:{pose.get('poseId')}")
            for attempt_id in pose.get("attemptIds", []):
                if attempt_id in seen_attempts:
                    issues.append(f"series_attempt_duplicate:{series.get('seriesId')}:{attempt_id}")
                seen_attempts.add(attempt_id)
                attempt = load_json(store.record("attempts", attempt_id))
                if attempt.get("artifacts", {}).get("raw") and not attempt.get("feedbackId"):
                    issues.append(f"series_feedback_missing:{series.get('seriesId')}:{attempt_id}")
            selected = pose.get("selectedAttemptId")
            if selected and selected not in pose.get("attemptIds", []):
                issues.append(f"series_selection_foreign:{series.get('seriesId')}:{pose.get('poseId')}")
            if pose.get("state") == "provisional" and pose.get("poseId") != "idle-dr":
                issues.append(f"series_provisional_pose:{series.get('seriesId')}:{pose.get('poseId')}")
        if series.get("provisionalAnchorAttemptId"):
            for pose in series.get("poses", [])[1:]:
                if pose.get("state") in {"approved", "promoted"}:
                    issues.append(f"series_provisional_downstream_formal:{series.get('seriesId')}:{pose.get('poseId')}")
        for change_id in series.get("limitChangeIds", []):
            change_path = store.record("series-limit-changes", change_id)
            if not change_path.is_file():
                issues.append(f"series_limit_change_missing:{series.get('seriesId')}:{change_id}")
                continue
            change = load_json(change_path)
            if change.get("seriesId") != series.get("seriesId"):
                issues.append(f"series_limit_change_series:{series.get('seriesId')}:{change_id}")
        if series.get("limitChangeIds"):
            latest = load_json(store.record("series-limit-changes", series["limitChangeIds"][-1]))
            if latest.get("maxUniqueOutputs") != limit:
                issues.append(f"series_limit_change_backlink:{series.get('seriesId')}")
    manifest_path = store.root / "Tools/public-release/asset-provenance.json"
    if manifest_path.is_file():
        manifest = load_json(manifest_path)
        manifest_by_path = {item["path"]: item for item in manifest.get("entries", [])}
        for png in sorted(store.pipeline.rglob("*.png")):
            rel = store.relative(png)
            entry = manifest_by_path.get(rel)
            if not entry:
                issues.append(f"pipeline_png_provenance_missing:{rel}")
            elif entry.get("sha256") != sha256_file(png):
                issues.append(f"pipeline_png_provenance_hash:{rel}")
    for path in sorted((store.pipeline / "generation-deliveries").glob("*.json")):
        delivery = load_json(path)
        invocation = store.record("generation-invocations", delivery.get("invocationId", ""))
        attempt = store.record("attempts", delivery.get("attemptId", ""))
        if not invocation.is_file() or not attempt.is_file():
            issues.append(f"generation_delivery_parent:{delivery.get('generationDeliveryId')}")
            continue
        attempt_record = load_json(attempt)
        if attempt_record.get("artifacts", {}).get("raw", {}).get("sha256") != delivery.get("rawSha256"):
            issues.append(f"generation_delivery_hash:{delivery.get('generationDeliveryId')}")
    for path in sorted((store.pipeline / "transactions").glob("*.json")):
        transaction = load_json(path)
        if transaction.get("state") not in {"committed", "aborted"}:
            issues.append(f"transaction_incomplete:{transaction.get('transactionId')}")
    for path in sorted((store.pipeline / "assemblies").glob("*.json")):
        assembly = load_json(path)
        if assembly.get("schemaVersion") != 3:
            issues.append(f"assembly_schema:{assembly.get('assemblyId')}")
            continue
        expected = stable_id("assembly", {key: assembly[key] for key in ("assetId", "contractId", "canvas", "layers")})
        if expected != assembly.get("assemblyId"):
            issues.append(f"assembly_identity:{assembly.get('assemblyId')}")
        for layer in assembly.get("layers", []):
            artifact = layer.get("artifact", {})
            target = store.absolute(artifact.get("path", ""))
            if not target.is_file() or sha256_file(target) != artifact.get("sha256"):
                issues.append(f"assembly_layer_hash:{assembly.get('assemblyId')}:{layer.get('role')}")
    for path in sorted((store.pipeline / "approvals").glob("*.json")):
        approval = load_json(path)
        attempt_path = store.record("attempts", approval.get("attemptId", ""))
        if not attempt_path.is_file():
            continue
        attempt = load_json(attempt_path)
        job = load_json(store.record("jobs", attempt["jobId"]))
        contract = load_json(store.record("contracts", job["contractId"]))
        if contract.get("schemaVersion") == 2:
            if approval.get("reviewer") != "cty41":
                issues.append(f"approval_not_human:{approval.get('approvalId')}")
            if contract.get("compositionSpec") and attempt.get("sourceMode") != "reviewed_import" and not approval.get("annotation"):
                issues.append(f"approval_annotations_missing:{approval.get('approvalId')}")
            elif contract.get("compositionSpec") and attempt.get("sourceMode") != "reviewed_import":
                annotation = approval["annotation"]
                annotation_path = store.record("annotations", annotation.get("annotationId", ""))
                if not annotation_path.is_file() or sha256_file(annotation_path) != annotation.get("sha256"):
                    issues.append(f"approval_annotations_hash:{approval.get('approvalId')}")
    result = {"schemaVersion": 1, "strict": strict, "inventoryCount": len(inventory_by_path), "issues": sorted(issues), "ok": not issues}
    if strict and issues:
        raise PipelineError("strict check failed:\n" + "\n".join(issues))
    return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="repository root")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("migrate-legacy")
    anchor = commands.add_parser("approve-anchor")
    anchor.add_argument("--candidate", required=True); anchor.add_argument("--mask", required=True)
    anchor.add_argument("--review", required=True); anchor.add_argument("--reviewer", required=True)
    anchor.add_argument("--reason", required=True); anchor.add_argument("--decided-at", required=True)
    series = commands.add_parser("create-series")
    series.add_argument("--series-id", required=True); series.add_argument("--asset-id", required=True)
    series.add_argument("--pose", action="append", required=True)
    series.add_argument("--max-unique-outputs", type=int)
    limit = commands.add_parser("set-series-output-limit")
    limit.add_argument("--series-id", required=True)
    limit_group = limit.add_mutually_exclusive_group(required=True)
    limit_group.add_argument("--max-unique-outputs", type=int)
    limit_group.add_argument("--unlimited", action="store_true")
    limit.add_argument("--reviewer", required=True); limit.add_argument("--reason", required=True)
    limit.add_argument("--decided-at", required=True)
    create = commands.add_parser("create-contract")
    create.add_argument("--asset-id", required=True); create.add_argument("--approved-asset-id"); create.add_argument("--kind", required=True)
    create.add_argument("--direction", required=True); create.add_argument("--pose", required=True)
    create.add_argument("--anchor"); create.add_argument("--anchor-mask")
    create.add_argument("--mask-required", action=argparse.BooleanOptionalAction, default=True)
    create.add_argument("--no-arms", action="store_true"); create.add_argument("--size-tolerance", type=int, default=3)
    create.add_argument("--near-hand-side", choices=("left", "right")); create.add_argument("--far-hand-side", choices=("left", "right"))
    create.add_argument("--center-tolerance", type=int, default=2); create.add_argument("--output-master", required=True)
    create.add_argument("--layer-rule", action="append", default=[])
    create.add_argument("--visibility-cap", action="append", default=[])
    create.add_argument("--composition-id")
    create.add_argument("--identity-anchor-mask")
    create.add_argument("--forehead-blaze-min-iou", type=float, default=0.45)
    create.add_argument("--pose-reference", action="store_true")
    create.add_argument("--output-preview", required=True); create.add_argument("--rights-holder", default="cty41")
    create.add_argument("--license", default="CC-BY-4.0"); create.add_argument("--provenance", default="project-owned-gpt-generated")
    create.add_argument("--asset-role", choices=sorted(ASSET_ROLES))
    create.add_argument("--component-kind", choices=sorted(COMPONENT_KINDS))
    create.add_argument("--source-mode", choices=sorted(SOURCE_MODES))
    job = commands.add_parser("create-job"); job.add_argument("--contract-id", required=True)
    job.add_argument("--prompt", required=True); job.add_argument("--input", action="append", default=[])
    job.add_argument("--pose-guide-id")
    job.add_argument("--series-id"); job.add_argument("--pose-id")
    retry_p = commands.add_parser("retry"); retry_p.add_argument("--job-id", required=True); retry_p.add_argument("--parent-attempt")
    retry_p.add_argument("--feedback-id")
    retry_p.add_argument("--technical-remediation", action="store_true")
    ingest_p = commands.add_parser("ingest"); ingest_p.add_argument("--attempt-id", required=True); ingest_p.add_argument("--source", required=True)
    ingest_p.add_argument("--invocation-id")
    prepare_p = commands.add_parser("prepare"); prepare_p.add_argument("--attempt-id", required=True); prepare_p.add_argument("--chroma")
    prepare_p.add_argument("--chroma-tolerance", type=int, default=0)
    mask_p = commands.add_parser("attach-mask"); mask_p.add_argument("--attempt-id", required=True); mask_p.add_argument("--mask", required=True)
    identity_mask_p = commands.add_parser("attach-identity-mask"); identity_mask_p.add_argument("--attempt-id", required=True); identity_mask_p.add_argument("--mask", required=True)
    calibrate_p = commands.add_parser("calibrate-core"); calibrate_p.add_argument("--attempt-id", required=True)
    validate_p = commands.add_parser("validate"); validate_p.add_argument("--attempt-id", required=True)
    review_p = commands.add_parser("render-review"); review_p.add_argument("--attempt-id", required=True)
    feedback = commands.add_parser("record-feedback"); feedback.add_argument("--attempt-id", required=True)
    feedback.add_argument("--reviewer", required=True); feedback.add_argument("--verdict", choices=sorted(FEEDBACK_VERDICTS), required=True)
    feedback.add_argument("--strength", action="append", default=[]); feedback.add_argument("--defect", action="append", default=[])
    feedback.add_argument("--next-prompt-delta"); feedback.add_argument("--recorded-at", required=True)
    feedback.add_argument("--author-type", choices=("agent", "human")); feedback.add_argument("--category", action="append", default=[])
    feedback.add_argument("--frozen", action="append", default=[]); feedback.add_argument("--pending", action="append", default=[])
    addendum = commands.add_parser("record-feedback-addendum"); addendum.add_argument("--feedback-id", required=True)
    addendum.add_argument("--reviewer", required=True); addendum.add_argument("--defect", action="append", default=[])
    addendum.add_argument("--author-type", choices=("agent", "human")); addendum.add_argument("--disposition", choices=sorted(FEEDBACK_VERDICTS))
    addendum.add_argument("--recorded-at", required=True)
    select = commands.add_parser("select-attempt"); select.add_argument("--attempt-id", required=True)
    select.add_argument("--provisional", action="store_true")
    advance = commands.add_parser("advance-series"); advance.add_argument("--series-id", required=True)
    for name in ("approve", "reject"):
        decision = commands.add_parser(name); decision.add_argument("--attempt-id", required=True)
        decision.add_argument("--reviewer", required=True); decision.add_argument("--reason", required=True)
        decision.add_argument("--decided-at", required=True, help="explicit ISO-8601 timestamp")
    exception = commands.add_parser("approve-exception"); exception.add_argument("--attempt-id", required=True)
    exception.add_argument("--issue", action="append", required=True)
    exception.add_argument("--reviewer", required=True); exception.add_argument("--reason", required=True)
    exception.add_argument("--decided-at", required=True, help="explicit ISO-8601 timestamp")
    promote_p = commands.add_parser("promote"); promote_p.add_argument("--attempt-id", required=True)
    refresh_p = commands.add_parser("refresh-promoted-preview"); refresh_p.add_argument("--attempt-id", required=True)
    composition = commands.add_parser("create-composition"); composition.add_argument("--asset-id", required=True)
    composition.add_argument("--spec", required=True); composition.add_argument("--anchor", required=True)
    guide = commands.add_parser("render-pose-guide"); guide.add_argument("--composition-id", required=True); guide.add_argument("--output", required=True)
    compiled = commands.add_parser("compile-prompt"); compiled.add_argument("--job-id", required=True)
    compiled.add_argument("--pose-guide-id", required=True); compiled.add_argument("--output", required=True)
    begin = commands.add_parser("begin-generation"); begin.add_argument("--attempt-id", required=True)
    begin.add_argument("--compiled-prompt-id", required=True); begin.add_argument("--provider", default="openai-gpt-image")
    begin.add_argument("--started-at", required=True)
    failure = commands.add_parser("record-generation-failure"); failure.add_argument("--invocation-id", required=True)
    failure.add_argument("--reason", required=True); failure.add_argument("--failed-at", required=True)
    annotations = commands.add_parser("attach-annotations"); annotations.add_argument("--attempt-id", required=True)
    annotations.add_argument("--annotations", required=True)
    advisory = commands.add_parser("record-advisory-review"); advisory.add_argument("--attempt-id", required=True)
    advisory.add_argument("--reviewer", required=True); advisory.add_argument("--risk", action="append", required=True)
    advisory.add_argument("--recorded-at", required=True)
    supporting = commands.add_parser("register-supporting-artifact"); supporting.add_argument("--path", required=True)
    supporting.add_argument("--role", default="supporting-derived"); supporting.add_argument("--note", required=True)
    comparison_p = commands.add_parser("render-size-comparison")
    comparison_p.add_argument("--identity", required=True); comparison_p.add_argument("--previous", required=True)
    comparison_p.add_argument("--reference", required=True); comparison_p.add_argument("--candidate", required=True)
    comparison_p.add_argument("--output", required=True)
    adopt_p = commands.add_parser("adopt-reviewed-sprite")
    adopt_p.add_argument("--contract-id", required=True); adopt_p.add_argument("--source", required=True)
    adopt_p.add_argument("--candidate", required=True); adopt_p.add_argument("--preview", required=True)
    adopt_p.add_argument("--size-comparison", required=True); adopt_p.add_argument("--reviewer", required=True)
    adopt_p.add_argument("--reason", required=True); adopt_p.add_argument("--accepted-at", required=True)
    migrate_component_p = commands.add_parser("migrate-component")
    migrate_component_p.add_argument("--contract-id", required=True); migrate_component_p.add_argument("--source", required=True)
    migrate_component_p.add_argument("--prepared", required=True); migrate_component_p.add_argument("--processing", required=True)
    migrate_component_p.add_argument("--reviewer", required=True); migrate_component_p.add_argument("--reason", required=True)
    migrate_component_p.add_argument("--accepted-at", required=True)
    derive_component_p = commands.add_parser("derive-component")
    derive_component_p.add_argument("--contract-id", required=True); derive_component_p.add_argument("--source-attempt-id", required=True)
    derive_component_p.add_argument(
        "--label",
        choices=("near_hand", "far_hand", "near_foot", "far_foot", "body", "equipment"),
        required=True,
    )
    assembly_p = commands.add_parser("create-assembly"); assembly_p.add_argument("--spec", required=True)
    render_assembly_p = commands.add_parser("render-assembly"); render_assembly_p.add_argument("--assembly-id", required=True)
    check_p = commands.add_parser("check"); check_p.add_argument("--strict", action="store_true")
    return parser


def run(args: argparse.Namespace) -> dict[str, Any]:
    store = Store(Path(args.root).resolve())
    handlers = {
        "migrate-legacy": migrate_legacy, "approve-anchor": approve_anchor, "create-series": create_series,
        "set-series-output-limit": set_series_output_limit,
        "create-contract": create_contract, "create-job": create_job,
        "retry": retry, "ingest": ingest, "prepare": prepare, "attach-mask": attach_mask,
        "attach-identity-mask": attach_identity_mask,
        "calibrate-core": calibrate_core,
        "validate": validate_attempt, "render-review": render_review, "record-feedback": record_feedback,
        "record-feedback-addendum": record_feedback_addendum,
        "select-attempt": select_attempt, "advance-series": advance_series,
        "approve": lambda s, a: decide(s, a, "approved"),
        "reject": lambda s, a: decide(s, a, "rejected"),
        "approve-exception": approve_exception, "promote": promote,
        "refresh-promoted-preview": refresh_promoted_preview,
        "create-composition": create_composition, "render-pose-guide": render_pose_guide,
        "compile-prompt": compile_prompt, "begin-generation": begin_generation,
        "record-generation-failure": record_generation_failure,
        "attach-annotations": attach_annotations, "record-advisory-review": record_advisory_review,
        "register-supporting-artifact": register_supporting_artifact,
        "render-size-comparison": render_size_comparison,
        "adopt-reviewed-sprite": adopt_reviewed_sprite,
        "migrate-component": migrate_component, "derive-component": derive_component,
        "create-assembly": create_assembly, "render-assembly": render_assembly,
        "check": lambda s, a: strict_check(s, a.strict),
    }
    return handlers[args.command](store, args)


def main() -> int:
    try:
        result = run(build_parser().parse_args())
        print(json.dumps(result, ensure_ascii=False, sort_keys=True, indent=2))
        return 0
    except PipelineError as exc:
        print(f"artwork-pipeline: error: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
