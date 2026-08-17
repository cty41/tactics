#!/usr/bin/env python3
"""Deterministic contract state machine for Pure Run artwork.

Image generation deliberately remains outside this program.  This CLI records
the immutable inputs and enforces every transition from ingestion to promotion.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any, Iterable

from PIL import Image, ImageDraw


SCHEMA_VERSION = 1
PIPELINE_REL = Path("Tools/artworks/pipeline")
KINDS = {"ground_character", "flying_character", "action_pose", "death_pose", "projectile", "tile"}
STATES = {
    "ready", "ingested", "prepared", "annotated", "review_pending",
    "approved", "rejected", "promoted", "technical_failed",
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
}


class PipelineError(RuntimeError):
    pass


def canonical_bytes(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def pretty_bytes(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")


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
    path.write_bytes(data)
    return True


def load_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise PipelineError(f"cannot read JSON {path}: {exc}") from exc
    if not isinstance(value, dict) or value.get("schemaVersion") != SCHEMA_VERSION:
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
        "tolerances": {"sizePx": args.size_tolerance, "centerPx": args.center_tolerance},
        "outputs": {"master": store.relative(args.output_master), "preview": store.relative(args.output_preview)},
        "rights": {"rightsHolder": args.rights_holder, "license": args.license, "provenance": args.provenance},
    }
    cid = contract_id(payload)
    record = {"schemaVersion": 1, "contractId": cid, **payload}
    write_json_idempotent(store.record("contracts", cid), record, immutable=True)
    return record


def create_job(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    contract = load_json(store.record("contracts", args.contract_id))
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
    payload = {
        "contractId": contract["contractId"],
        "contractSha256": sha256_file(store.record("contracts", args.contract_id)),
        "prompt": {"path": prompt_rel, "sha256": sha256_file(store.absolute(prompt_rel))},
        "inputs": inputs,
        "target": {"direction": contract["direction"], "pose": contract["pose"]},
    }
    jid = stable_id("job", payload)
    record = {"schemaVersion": 1, "jobId": jid, "state": "ready", **payload}
    write_json_idempotent(store.record("jobs", jid), record, immutable=True)
    packet = store.pipeline / "packets" / f"{jid}.json"
    write_json_idempotent(packet, record, immutable=True)
    return record


def list_attempts(store: Store, job_id: str) -> list[dict[str, Any]]:
    result = []
    for path in sorted((store.pipeline / "attempts").glob(f"{job_id}-*.json")):
        result.append(load_json(path))
    return result


def retry(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    load_json(store.record("jobs", args.job_id))
    attempts = list_attempts(store, args.job_id)
    ordinal = len(attempts) + 1
    parent = args.parent_attempt
    if parent:
        parent_record = load_json(store.record("attempts", parent))
        if parent_record["jobId"] != args.job_id:
            raise PipelineError("parent attempt belongs to another job")
    aid = f"{args.job_id}-a{ordinal:03d}"
    record = {"schemaVersion": 1, "attemptId": aid, "jobId": args.job_id, "ordinal": ordinal,
              "parentAttemptId": parent, "state": "ready", "artifacts": {}, "report": None, "approvalId": None}
    write_json_idempotent(store.record("attempts", aid), record, immutable=True)
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


def ingest(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    source_rel = store.relative(args.source, must_exist=True)
    digest = sha256_file(store.absolute(source_rel))
    existing = attempt["artifacts"].get("raw")
    if existing:
        if existing["sha256"] != digest:
            raise PipelineError("attempt already contains a different ImageGen output")
        return attempt
    if attempt["state"] != "ready":
        raise PipelineError("ingest requires ready attempt")
    suffix = store.absolute(source_rel).suffix.lower() or ".png"
    dst = store.pipeline / "artifacts" / attempt["jobId"] / attempt["attemptId"] / f"raw{suffix}"
    attempt["artifacts"]["raw"] = copy_bound(store, source_rel, store.relative(dst))
    transition(attempt, {"ready"}, "ingested")
    save_attempt(store, attempt)
    return attempt


def prepare_image(source: Path, destination: Path, chroma: str | None) -> None:
    image = Image.open(source).convert("RGBA")
    pixels = pixel_data(image)
    key = None
    if chroma:
        value = chroma.lstrip("#")
        if len(value) != 6:
            raise PipelineError("--chroma must be RRGGBB")
        key = tuple(int(value[index:index + 2], 16) for index in (0, 2, 4))
    cleaned = []
    for red, green, blue, alpha in pixels:
        if key and (red, green, blue) == key:
            cleaned.append((0, 0, 0, 0))
        elif alpha == 0:
            cleaned.append((0, 0, 0, 0))
        else:
            cleaned.append((red, green, blue, alpha))
    image.putdata(cleaned)
    destination.parent.mkdir(parents=True, exist_ok=True)
    image.save(destination, format="PNG", optimize=False, compress_level=9)


def prepare(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    existing = attempt["artifacts"].get("prepared")
    if existing:
        if sha256_file(store.absolute(existing["path"], must_exist=True)) != existing["sha256"]:
            raise PipelineError("prepared artifact hash mismatch")
        return attempt
    if attempt["state"] != "ingested":
        raise PipelineError("prepare requires ingested attempt")
    raw = store.absolute(attempt["artifacts"]["raw"]["path"], must_exist=True)
    if sha256_file(raw) != attempt["artifacts"]["raw"]["sha256"]:
        raise PipelineError("raw artifact hash mismatch")
    dst = store.pipeline / "artifacts" / attempt["jobId"] / attempt["attemptId"] / "prepared.png"
    prepare_image(raw, dst, args.chroma)
    attempt["artifacts"]["prepared"] = {"path": store.relative(dst), "sha256": sha256_file(dst)}
    transition(attempt, {"ingested"}, "prepared")
    save_attempt(store, attempt)
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
        attempt["artifacts"]["mask"] = copy_bound(store, mask_rel, store.relative(dst))
        transition(attempt, {"prepared"}, "annotated")
        save_attempt(store, attempt)
    return attempt


def bbox_for(pixels: list[tuple[int, int, int, int]], size: tuple[int, int], color: tuple[int, int, int, int]) -> tuple[int, int, int, int] | None:
    width, _ = size
    points = [(index % width, index // width) for index, value in enumerate(pixels) if value == color]
    if not points:
        return None
    xs, ys = zip(*points)
    return min(xs), min(ys), max(xs), max(ys)


def inspect_technical(path: Path, kind: str) -> tuple[dict[str, Any], list[str]]:
    image = Image.open(path).convert("RGBA")
    pixels = pixel_data(image)
    alpha = image.getchannel("A")
    bbox = alpha.getbbox()
    issues = []
    if image.size != (256, 256):
        issues.append("master_not_256x256")
    if any(image.getpixel(point)[3] != 0 for point in ((0, 0), (255, 0), (0, 255), (255, 255))):
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
    elif kind in {"ground_character", "action_pose"} and bbox[3] - 1 != 236:
        issues.append("baseline_not_236")
    return {"size": list(image.size), "alphaBbox": list(bbox) if bbox else None}, issues


def geometry_checks(store: Store, contract: dict[str, Any], attempt: dict[str, Any]) -> tuple[dict[str, Any], list[str]]:
    mask_artifact = attempt["artifacts"].get("mask")
    if contract["maskRequired"] and not mask_artifact:
        return {}, ["semantic_mask_missing"]
    if not mask_artifact:
        return {}, []
    mask = Image.open(store.absolute(mask_artifact["path"], must_exist=True)).convert("RGBA")
    pixels = pixel_data(mask)
    boxes = {label: bbox_for(pixels, mask.size, color) for label, color in MASK_COLORS.items()}
    issues = []
    allowed = set(MASK_COLORS.values()) | {(0, 0, 0, 0)}
    if any(value not in allowed for value in pixels):
        issues.append("semantic_mask_unknown_color")
    if any(a == 0 and (r or g or b) for r, g, b, a in pixels):
        issues.append("semantic_mask_transparent_rgb_nonzero")
    core = boxes["core"]
    if core is None:
        issues.append("core_missing")
    if contract.get("anchor") and not contract["anchor"].get("maskPath"):
        issues.append("anchor_mask_missing")
    if contract["noArms"]:
        for label in ("near_hand", "far_hand", "near_foot", "far_foot"):
            if boxes[label] is None:
                issues.append(f"{label}_missing")
        if core:
            width = mask.width
            core_points = {i for i, value in enumerate(pixels) if value == MASK_COLORS["core"]}
            for label in ("near_hand", "far_hand"):
                hand_points = {i for i, value in enumerate(pixels) if value == MASK_COLORS[label]}
                contacts = 0
                for index in hand_points:
                    x, y = index % width, index // width
                    if any((ny * width + nx) in core_points for nx, ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)) if 0 <= nx < width and 0 <= ny < mask.height):
                        contacts += 1
                if hand_points and contacts < 3:
                    issues.append(f"{label}_contact_lt_3")
    metrics: dict[str, Any] = {"boxes": {key: list(value) if value else None for key, value in boxes.items()}}
    if core:
        left, top, right, bottom = core
        row_widths = []
        for y in range(top, bottom + 1):
            xs = [x for x in range(mask.width) if pixels[y * mask.width + x] == MASK_COLORS["core"]]
            row_widths.append(max(xs) - min(xs) + 1 if xs else 0)
        height = len(row_widths)
        bands = [row_widths[:max(1, height // 3)], row_widths[height // 3:max(height // 3 + 1, 2 * height // 3)], row_widths[2 * height // 3:]]
        widths = [max(band or [0]) for band in bands]
        metrics["core"] = {"bbox": [left, top, right, bottom], "center": [(left + right) / 2, (top + bottom) / 2], "bandMaxWidths": widths}
        if widths[2] > widths[1]:
            issues.append("core_lower_wider_than_middle")
        core_center_x = (left + right) / 2
        for label, side in contract.get("handSides", {}).items():
            box = boxes.get(label)
            if box and side:
                hand_center_x = (box[0] + box[2]) / 2
                if (side == "left" and hand_center_x >= core_center_x) or (side == "right" and hand_center_x <= core_center_x):
                    issues.append(f"{label}_wrong_side")
        anchor = contract.get("anchor")
        if anchor and anchor.get("maskPath"):
            anchor_mask = Image.open(store.absolute(anchor["maskPath"], must_exist=True)).convert("RGBA")
            anchor_box = bbox_for(pixel_data(anchor_mask), anchor_mask.size, MASK_COLORS["core"])
            if anchor_box:
                tol_size = contract["tolerances"]["sizePx"]
                tol_center = contract["tolerances"]["centerPx"]
                aw, ah = anchor_box[2] - anchor_box[0] + 1, anchor_box[3] - anchor_box[1] + 1
                cw, ch = right - left + 1, bottom - top + 1
                if abs(aw - cw) > tol_size or abs(ah - ch) > tol_size:
                    issues.append("core_size_out_of_tolerance")
                if abs((anchor_box[0] + anchor_box[2] - left - right) / 2) > tol_center or abs((anchor_box[1] + anchor_box[3] - top - bottom) / 2) > tol_center:
                    issues.append("core_center_out_of_tolerance")
    return metrics, issues


def validate_attempt(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] in {"review_pending", "technical_failed"} and attempt.get("report"):
        report = load_json(store.absolute(attempt["report"]["path"], must_exist=True))
        if sha256_file(store.absolute(attempt["report"]["path"])) != attempt["report"]["sha256"]:
            raise PipelineError("validation report hash mismatch")
        return report
    if attempt["state"] not in {"prepared", "annotated"}:
        raise PipelineError("validate requires prepared or annotated attempt")
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    prepared = store.absolute(attempt["artifacts"]["prepared"]["path"], must_exist=True)
    technical, issues = inspect_technical(prepared, contract["kind"])
    geometry, geometry_issues = geometry_checks(store, contract, attempt)
    issues.extend(geometry_issues)
    report = {"schemaVersion": 1, "attemptId": attempt["attemptId"], "inputSha256": sha256_file(prepared),
              "maskSha256": attempt["artifacts"].get("mask", {}).get("sha256"), "technical": technical,
              "geometry": geometry, "issues": sorted(set(issues)), "passed": not issues}
    report_id = stable_id("report", report)
    report_path = store.pipeline / "reports" / f"{report_id}.json"
    write_json_idempotent(report_path, report, immutable=True)
    attempt["report"] = {"path": store.relative(report_path), "sha256": sha256_file(report_path)}
    attempt["state"] = "review_pending" if report["passed"] else "technical_failed"
    save_attempt(store, attempt)
    return report


def make_preview(master: Image.Image) -> Image.Image:
    return master.resize((128, 128), Image.Resampling.LANCZOS)


def render_review(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] not in {"review_pending", "approved", "rejected", "promoted"}:
        raise PipelineError("render-review requires a passed validation")
    prepared = Image.open(store.absolute(attempt["artifacts"]["prepared"]["path"], must_exist=True)).convert("RGBA")
    mask_artifact = attempt["artifacts"].get("mask")
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
    outputs = {}
    for name, image in (("overlay", review), ("preview128", preview), ("tile64x32", tile)):
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
    save_attempt(store, attempt)
    return {"schemaVersion": 1, "attemptId": attempt["attemptId"], "outputs": outputs}


def decide(store: Store, args: argparse.Namespace, decision: str) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    target = "approved" if decision == "approved" else "rejected"
    if attempt["state"] == target and attempt.get("approvalId"):
        existing = load_json(store.record("approvals", attempt["approvalId"]))
        expected = {"reviewer": args.reviewer, "decision": decision, "reason": args.reason, "decidedAt": args.decided_at}
        if any(existing.get(key) != value for key, value in expected.items()):
            raise PipelineError("attempt already has a different approval receipt")
        return existing
    if attempt["state"] != "review_pending":
        raise PipelineError("approval decision requires review_pending attempt")
    try:
        decided_at = datetime.fromisoformat(args.decided_at)
    except ValueError as exc:
        raise PipelineError("--decided-at must be an ISO-8601 timestamp") from exc
    if decided_at.tzinfo is None:
        raise PipelineError("--decided-at must include a timezone offset")
    report = load_json(store.absolute(attempt["report"]["path"], must_exist=True))
    if not report["passed"]:
        raise PipelineError("technical gate did not pass")
    review = attempt.get("artifacts", {}).get("review")
    if not review or set(review) != {"overlay", "preview128", "tile64x32"}:
        raise PipelineError("approval requires deterministic review outputs")
    for artifact in review.values():
        review_path = store.absolute(artifact["path"], must_exist=True)
        if sha256_file(review_path) != artifact["sha256"]:
            raise PipelineError("review artifact hash mismatch")
    receipt_payload = {
        "attemptId": attempt["attemptId"], "candidateSha256": attempt["artifacts"]["prepared"]["sha256"],
        "maskSha256": attempt["artifacts"].get("mask", {}).get("sha256"), "reviewer": args.reviewer,
        "decision": decision, "reason": args.reason, "decidedAt": args.decided_at,
    }
    approval_id = stable_id("approval", receipt_payload)
    receipt = {"schemaVersion": 1, "approvalId": approval_id, **receipt_payload}
    write_json_idempotent(store.record("approvals", approval_id), receipt, immutable=True)
    attempt["approvalId"] = approval_id
    transition(attempt, {"review_pending"}, target)
    save_attempt(store, attempt)
    return receipt


def update_provenance(store: Store, paths: list[dict[str, str]], contract: dict[str, Any]) -> None:
    manifest_path = store.root / "Tools/public-release/asset-provenance.json"
    manifest = load_json(manifest_path)
    by_path = {entry["path"]: entry for entry in manifest["entries"]}
    for artifact in paths:
        entry = {"path": artifact["path"], "sha256": artifact["sha256"], "status": "approved", **contract["rights"]}
        existing = by_path.get(artifact["path"])
        if existing and existing != entry:
            raise PipelineError(f"conflicting provenance entry: {artifact['path']}")
        by_path[artifact["path"]] = entry
    manifest["entries"] = [by_path[key] for key in sorted(by_path)]
    write_json_idempotent(manifest_path, manifest)


def update_approved_cases(store: Store, contract: dict[str, Any], master_path: str) -> None:
    cases_path = store.root / ".agents/skills/pure-run-artwork-pipeline/examples/cases.json"
    if not cases_path.is_file():
        return
    cases = json.loads(cases_path.read_text(encoding="utf-8"))
    direction_key = contract["direction"].replace("-", "_")
    if direction_key not in {"down_right", "up_left"}:
        return
    asset_id = contract.get("approvedAssetId", contract["assetId"])
    entry = next((item for item in cases.get("approved_assets", []) if item.get("id") == asset_id), None)
    if entry is None:
        entry = {"id": asset_id}
        cases.setdefault("approved_assets", []).append(entry)
    existing = entry.get(direction_key)
    if existing and existing != master_path:
        raise PipelineError(f"approved mother list already has a different {direction_key} path for {asset_id}")
    entry[direction_key] = master_path
    write_json_idempotent(cases_path, cases)


def promote(store: Store, args: argparse.Namespace) -> dict[str, Any]:
    attempt = load_json(store.record("attempts", args.attempt_id))
    if attempt["state"] == "promoted":
        return attempt
    if attempt["state"] != "approved":
        raise PipelineError("promote requires approved attempt")
    approval = load_json(store.record("approvals", attempt["approvalId"]))
    if approval["candidateSha256"] != attempt["artifacts"]["prepared"]["sha256"]:
        raise PipelineError("approval no longer matches candidate")
    job = load_json(store.record("jobs", attempt["jobId"]))
    contract = load_json(store.record("contracts", job["contractId"]))
    master = copy_bound(store, attempt["artifacts"]["prepared"]["path"], contract["outputs"]["master"])
    master_image = Image.open(store.absolute(master["path"])).convert("RGBA")
    preview_path = store.absolute(contract["outputs"]["preview"])
    preview_path.parent.mkdir(parents=True, exist_ok=True)
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
    missing = sorted(current - set(inventory_by_path))
    issues.extend(f"asset_unregistered:{path}" for path in missing)
    for path in sorted((store.pipeline / "attempts").glob("*.json")):
        attempt = load_json(path)
        if attempt.get("state") not in STATES:
            issues.append(f"attempt_state:{path.name}")
        for artifact in attempt.get("artifacts", {}).values():
            values = artifact.values() if isinstance(artifact, dict) and "path" not in artifact else [artifact]
            for value in values:
                if isinstance(value, dict) and value.get("path"):
                    target = store.absolute(value["path"])
                    if not target.exists() or sha256_file(target) != value.get("sha256"):
                        issues.append(f"artifact_hash:{attempt['attemptId']}:{value.get('path')}")
        if not store.record("jobs", attempt.get("jobId", "")).is_file():
            issues.append(f"attempt_job_missing:{attempt.get('attemptId')}")
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
                prepared = attempt.get("artifacts", {}).get("prepared", {})
                mask = attempt.get("artifacts", {}).get("mask", {})
                if receipt.get("candidateSha256") != prepared.get("sha256") or receipt.get("maskSha256") != mask.get("sha256"):
                    issues.append(f"attempt_approval_hash:{attempt.get('attemptId')}")
    for path in sorted((store.pipeline / "jobs").glob("*.json")):
        job = load_json(path)
        contract_path = store.record("contracts", job.get("contractId", ""))
        if not contract_path.is_file():
            issues.append(f"job_contract_missing:{job.get('jobId')}")
        elif sha256_file(contract_path) != job.get("contractSha256"):
            issues.append(f"job_contract_hash:{job.get('jobId')}")
        for bound in [job.get("prompt", {})] + job.get("inputs", []):
            target = store.absolute(bound.get("path", ""))
            if not target.is_file() or sha256_file(target) != bound.get("sha256"):
                issues.append(f"job_input_hash:{job.get('jobId')}:{bound.get('path')}")
    for path in sorted((store.pipeline / "contracts").glob("*.json")):
        contract = load_json(path)
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
    result = {"schemaVersion": 1, "strict": strict, "inventoryCount": len(inventory_by_path), "issues": sorted(issues), "ok": not issues}
    if strict and issues:
        raise PipelineError("strict check failed:\n" + "\n".join(issues))
    return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="repository root")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("migrate-legacy")
    create = commands.add_parser("create-contract")
    create.add_argument("--asset-id", required=True); create.add_argument("--approved-asset-id"); create.add_argument("--kind", required=True)
    create.add_argument("--direction", required=True); create.add_argument("--pose", required=True)
    create.add_argument("--anchor"); create.add_argument("--anchor-mask")
    create.add_argument("--mask-required", action=argparse.BooleanOptionalAction, default=True)
    create.add_argument("--no-arms", action="store_true"); create.add_argument("--size-tolerance", type=int, default=3)
    create.add_argument("--near-hand-side", choices=("left", "right")); create.add_argument("--far-hand-side", choices=("left", "right"))
    create.add_argument("--center-tolerance", type=int, default=2); create.add_argument("--output-master", required=True)
    create.add_argument("--output-preview", required=True); create.add_argument("--rights-holder", default="cty41")
    create.add_argument("--license", default="CC-BY-4.0"); create.add_argument("--provenance", default="project-owned-gpt-generated")
    job = commands.add_parser("create-job"); job.add_argument("--contract-id", required=True)
    job.add_argument("--prompt", required=True); job.add_argument("--input", action="append", default=[])
    retry_p = commands.add_parser("retry"); retry_p.add_argument("--job-id", required=True); retry_p.add_argument("--parent-attempt")
    ingest_p = commands.add_parser("ingest"); ingest_p.add_argument("--attempt-id", required=True); ingest_p.add_argument("--source", required=True)
    prepare_p = commands.add_parser("prepare"); prepare_p.add_argument("--attempt-id", required=True); prepare_p.add_argument("--chroma")
    mask_p = commands.add_parser("attach-mask"); mask_p.add_argument("--attempt-id", required=True); mask_p.add_argument("--mask", required=True)
    validate_p = commands.add_parser("validate"); validate_p.add_argument("--attempt-id", required=True)
    review_p = commands.add_parser("render-review"); review_p.add_argument("--attempt-id", required=True)
    for name in ("approve", "reject"):
        decision = commands.add_parser(name); decision.add_argument("--attempt-id", required=True)
        decision.add_argument("--reviewer", required=True); decision.add_argument("--reason", required=True)
        decision.add_argument("--decided-at", required=True, help="explicit ISO-8601 timestamp")
    promote_p = commands.add_parser("promote"); promote_p.add_argument("--attempt-id", required=True)
    check_p = commands.add_parser("check"); check_p.add_argument("--strict", action="store_true")
    return parser


def run(args: argparse.Namespace) -> dict[str, Any]:
    store = Store(Path(args.root).resolve())
    handlers = {
        "migrate-legacy": migrate_legacy, "create-contract": create_contract, "create-job": create_job,
        "retry": retry, "ingest": ingest, "prepare": prepare, "attach-mask": attach_mask,
        "validate": validate_attempt, "render-review": render_review, "approve": lambda s, a: decide(s, a, "approved"),
        "reject": lambda s, a: decide(s, a, "rejected"), "promote": promote,
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
