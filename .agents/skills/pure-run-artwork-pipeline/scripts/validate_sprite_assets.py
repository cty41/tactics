#!/usr/bin/env python3
"""Read-only validation for the Pure Run artwork sprite contract."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional, Tuple

try:
    from PIL import Image
except ImportError:  # pragma: no cover - dependency failure is reported to the caller
    Image = None  # type: ignore[assignment]


GREEN = (0, 255, 0)
MAGENTA = (255, 0, 255)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default="Tools/artworks", type=Path)
    parser.add_argument("--standard-height", default=122, type=int)
    parser.add_argument("--baseline", default=236, type=int)
    parser.add_argument("--preview-size", default=128, type=int)
    parser.add_argument("--include-candidates", action="store_true")
    parser.add_argument(
        "--review-examples",
        action="store_true",
        help="校验 skill 案例清单、正式锚点和 128 正反快照",
    )
    parser.add_argument(
        "--review-manifest",
        type=Path,
        help="覆盖默认的 examples/cases.json 路径",
    )
    parser.add_argument("--strict", action="store_true")
    return parser.parse_args()


def add_issue(report: Dict[str, Any], message: str) -> None:
    report.setdefault("issues", []).append(message)


def alpha_bbox(image: Any) -> Optional[Tuple[int, int, int, int]]:
    alpha = image.getchannel("A")
    return alpha.getbbox()


def inspect_png(path: Path) -> Dict[str, Any]:
    report: Dict[str, Any] = {"path": path.as_posix(), "issues": []}
    try:
        with Image.open(path) as source:
            report["mode"] = source.mode
            report["size"] = list(source.size)
            if source.mode not in {"RGBA", "LA", "P"}:
                add_issue(report, f"alpha 通道无效（模式为 {source.mode}）")
            image = source.convert("RGBA")
            alpha = image.getchannel("A")
            alpha_bytes = alpha.tobytes()
            report["alpha_pixels"] = sum(1 for value in alpha_bytes if value)
            corners = [
                alpha.getpixel((0, 0)),
                alpha.getpixel((image.width - 1, 0)),
                alpha.getpixel((0, image.height - 1)),
                alpha.getpixel((image.width - 1, image.height - 1)),
            ]
            report["corner_alpha"] = corners
            if any(corners):
                add_issue(report, "四角不是透明")

            key_pixels = 0
            rgba_bytes = image.tobytes()
            for index in range(0, len(rgba_bytes), 4):
                coordinate = rgba_bytes[index : index + 4]
                if coordinate[3] and tuple(coordinate[:3]) in {GREEN, MAGENTA}:
                    key_pixels += 1
            report["exact_chroma_pixels"] = key_pixels
            if key_pixels:
                add_issue(report, "存在精确绿幕或洋红幕残留")

            bbox = alpha_bbox(image)
            if bbox is None:
                add_issue(report, "没有可见 alpha 轮廓")
            else:
                left, top, right, bottom = bbox
                report["bbox"] = [left, top, right - 1, bottom - 1]
                report["bbox_size"] = [right - left, bottom - top]
                report["bbox_baseline"] = bottom - 1
    except Exception as exc:  # Pillow reports malformed/truncated PNGs here.
        add_issue(report, f"无法读取 PNG：{exc}")
    return report


def expected_preview_height(standard_height: int, preview_size: int) -> int:
    # Inclusive pixel bounds make a 122 px master shrink to approximately 62 px.
    return round(standard_height * preview_size / 256) + 1


def validate_centered_alpha_bbox(report: Dict[str, Any]) -> None:
    """Require a visible alpha AABB to be centered on its sprite canvas."""
    size = report.get("size")
    bbox = report.get("bbox")
    if not isinstance(size, list) or len(size) != 2:
        return
    if not isinstance(bbox, list) or len(bbox) != 4:
        return

    expected_x = (size[0] - 1) * 0.5
    expected_y = (size[1] - 1) * 0.5
    actual_x = (bbox[0] + bbox[2]) * 0.5
    actual_y = (bbox[1] + bbox[3]) * 0.5
    report["bbox_center"] = [actual_x, actual_y]
    if abs(actual_x - expected_x) > 0.5 or abs(actual_y - expected_y) > 0.5:
        add_issue(report, "死亡图 Alpha AABB 中心未对齐画布中心（误差必须不超过 0.5 px）")


def validate_pair(
    master: Path,
    preview: Path,
    *,
    standard_height: int,
    baseline: int,
    preview_size: int,
    geometry_required: bool,
) -> List[Dict[str, Any]]:
    reports = [inspect_png(master), inspect_png(preview)]
    master_report, preview_report = reports
    if not preview.exists():
        add_issue(master_report, f"缺少配对预览：{preview.name}")
        return reports

    if master_report.get("size") != [256, 256]:
        add_issue(master_report, "母版必须为 256×256")
    if preview_report.get("size") != [preview_size, preview_size]:
        add_issue(preview_report, f"预览必须为 {preview_size}×{preview_size}")

    is_death_sprite = "_death_" in master.stem.lower()
    if is_death_sprite:
        validate_centered_alpha_bbox(master_report)
        validate_centered_alpha_bbox(preview_report)
    elif geometry_required:
        if master_report.get("bbox_size", [None, None])[1] != standard_height:
            add_issue(master_report, f"母版轮廓高度不是 {standard_height} px")
        if master_report.get("bbox_baseline") != baseline:
            add_issue(master_report, f"母版脚底基线不是 y={baseline}")
        preview_height = expected_preview_height(standard_height, preview_size)
        if abs(preview_report.get("bbox_size", [0, 0])[1] - preview_height) > 1:
            add_issue(preview_report, f"预览轮廓高度不接近 {preview_height} px")
        expected_baseline = round(baseline * preview_size / 256)
        if preview_report.get("bbox_baseline") != expected_baseline:
            add_issue(preview_report, f"预览脚底基线不是 y={expected_baseline}")
    return reports


def iter_masters(directory: Path) -> Iterable[Path]:
    if not directory.exists():
        return []
    return sorted(
        path
        for path in directory.glob("*.png")
        if not path.stem.endswith("_128")
    )


def validate_sprite_directory(
    directory: Path,
    *,
    standard_height: int,
    baseline: int,
    preview_size: int,
    geometry_required: bool,
    category: str,
) -> List[Dict[str, Any]]:
    reports: List[Dict[str, Any]] = []
    if not directory.exists():
        return [{"path": directory.as_posix(), "category": category, "issues": ["目录不存在"]}]
    masters = list(iter_masters(directory))
    expected_preview_names = set()
    for master in masters:
        preview = master.with_name(f"{master.stem}_128.png")
        expected_preview_names.add(preview.name)
        pair_reports = validate_pair(
            master,
            preview,
            standard_height=standard_height,
            baseline=baseline,
            preview_size=preview_size,
            geometry_required=geometry_required,
        )
        for report in pair_reports:
            report["category"] = category
        reports.extend(pair_reports)

    for preview in sorted(directory.glob("*_128.png")):
        if preview.name not in expected_preview_names:
            report = inspect_png(preview)
            report["category"] = category
            add_issue(report, "找不到对应的母版 PNG")
            reports.append(report)
    return reports


def validate_tiles(directory: Path) -> List[Dict[str, Any]]:
    reports: List[Dict[str, Any]] = []
    if not directory.exists():
        return [{"path": directory.as_posix(), "category": "tiles", "issues": ["目录不存在"]}]
    for path in sorted(directory.glob("*.png")):
        report = inspect_png(path)
        report["category"] = "tiles"
        if report.get("size") != [64, 32]:
            add_issue(report, "Tile 必须为 64×32")
        reports.append(report)
    return reports


def resolve_repo_path(repo_root: Path, value: str) -> Optional[Path]:
    candidate = (repo_root / value).resolve()
    try:
        candidate.relative_to(repo_root)
    except ValueError:
        return None
    return candidate


def validate_review_examples(
    repo_root: Path,
    manifest_path: Path,
    *,
    standard_height: int,
    baseline: int,
    preview_size: int,
) -> List[Dict[str, Any]]:
    reports: List[Dict[str, Any]] = []
    manifest_report: Dict[str, Any] = {
        "path": manifest_path.as_posix(),
        "category": "review-manifest",
        "issues": [],
    }
    reports.append(manifest_report)
    if not manifest_path.exists():
        add_issue(manifest_report, "案例清单不存在")
        return reports

    try:
        payload = json.loads(manifest_path.read_text(encoding="utf-8"))
    except Exception as exc:
        add_issue(manifest_report, f"无法读取案例清单：{exc}")
        return reports

    if payload.get("version") != 1:
        add_issue(manifest_report, "案例清单 version 必须为 1")

    approved_assets = payload.get("approved_assets")
    if not isinstance(approved_assets, list) or not approved_assets:
        add_issue(manifest_report, "approved_assets 必须是非空数组")
        approved_assets = []

    approved_paths: set[str] = set()
    for asset in approved_assets:
        if not isinstance(asset, dict):
            add_issue(manifest_report, "approved_assets 中存在非对象条目")
            continue
        asset_id = asset.get("id", "<missing>")
        for direction in ("down_right", "up_left"):
            value = asset.get(direction)
            if not isinstance(value, str):
                add_issue(manifest_report, f"{asset_id}.{direction} 缺少路径")
                continue
            normalized = Path(value).as_posix()
            if normalized in approved_paths:
                add_issue(manifest_report, f"正式资产重复引用：{normalized}")
            approved_paths.add(normalized)
            source = resolve_repo_path(repo_root, value)
            if source is None:
                add_issue(manifest_report, f"正式资产路径越界：{value}")
                continue
            parts = {part.lower() for part in source.parts}
            if "tmp" in parts or "rejected" in parts or "superseded" in parts:
                add_issue(manifest_report, f"正式资产引用了临时或反例目录：{value}")
                continue
            if not ({"calibrated", "approved"} & parts):
                add_issue(manifest_report, f"正式资产不在 calibrated/approved：{value}")
            preview = source.with_name(f"{source.stem}_128.png")
            geometry_required = "calibrated" in parts
            pair_reports = validate_pair(
                source,
                preview,
                standard_height=standard_height,
                baseline=baseline,
                preview_size=preview_size,
                geometry_required=geometry_required,
            )
            for report in pair_reports:
                report["category"] = "approved-anchor"
                report["asset_id"] = asset_id
                report["direction"] = direction
            reports.extend(pair_reports)

    cases = payload.get("cases")
    if not isinstance(cases, list) or not cases:
        add_issue(manifest_report, "cases 必须是非空数组")
        cases = []

    case_ids: set[str] = set()
    for case in cases:
        if not isinstance(case, dict):
            add_issue(manifest_report, "cases 中存在非对象条目")
            continue
        case_id = case.get("id", "<missing>")
        if case_id in case_ids:
            add_issue(manifest_report, f"案例 ID 重复：{case_id}")
        case_ids.add(case_id)
        approved_value = case.get("approved_source")
        rejected_value = case.get("rejected_source")
        if approved_value == rejected_value:
            add_issue(manifest_report, f"{case_id} 的正例和反例指向同一文件")
        if case.get("rejected_for_mother_image") is not True:
            add_issue(manifest_report, f"{case_id} 未禁止把反例作为母图")

        for field, expected_status in (
            ("approved_source", "approved"),
            ("rejected_source", "rejected"),
        ):
            value = case.get(field)
            if not isinstance(value, str):
                add_issue(manifest_report, f"{case_id}.{field} 缺少路径")
                continue
            source = resolve_repo_path(repo_root, value)
            if source is None:
                add_issue(manifest_report, f"{case_id}.{field} 路径越界")
                continue
            parts = {part.lower() for part in source.parts}
            if "tmp" in parts:
                add_issue(manifest_report, f"{case_id}.{field} 不能引用 tmp")
            if expected_status == "approved":
                if not ({"calibrated", "approved"} & parts):
                    add_issue(manifest_report, f"{case_id} 正例不在 calibrated/approved")
                if "rejected" in parts or "superseded" in parts:
                    add_issue(manifest_report, f"{case_id} 正例位于反例目录")
                if Path(value).as_posix() not in approved_paths:
                    add_issue(manifest_report, f"{case_id} 正例未登记在 approved_assets")
            elif "rejected" not in parts:
                add_issue(manifest_report, f"{case_id} 反例不在 rejected")
            source_report = inspect_png(source)
            source_report["category"] = f"review-{expected_status}-source"
            source_report["case_id"] = case_id
            reports.append(source_report)

        for field in ("approved_snapshot", "rejected_snapshot"):
            value = case.get(field)
            if not isinstance(value, str):
                add_issue(manifest_report, f"{case_id}.{field} 缺少路径")
                continue
            snapshot = resolve_repo_path(repo_root, value)
            if snapshot is None:
                add_issue(manifest_report, f"{case_id}.{field} 路径越界")
                continue
            snapshot_report = inspect_png(snapshot)
            snapshot_report["category"] = "review-snapshot"
            snapshot_report["case_id"] = case_id
            if snapshot_report.get("mode") != "RGBA":
                add_issue(snapshot_report, "案例快照必须为 RGBA")
            if snapshot_report.get("size") != [preview_size, preview_size]:
                add_issue(snapshot_report, f"案例快照必须为 {preview_size}×{preview_size}")
            source_field = (
                "approved_source"
                if field == "approved_snapshot"
                else "rejected_source"
            )
            source_value = case.get(source_field)
            if isinstance(source_value, str):
                source = resolve_repo_path(repo_root, source_value)
                if source is not None:
                    source_preview = source.with_name(f"{source.stem}_128.png")
                    if not source_preview.exists():
                        add_issue(
                            snapshot_report,
                            f"案例原图缺少配对预览：{source_preview.name}",
                        )
                    elif snapshot.exists() and (
                        snapshot.read_bytes() != source_preview.read_bytes()
                    ):
                        add_issue(snapshot_report, "案例快照与原图配对预览不一致")
            reports.append(snapshot_report)
    return reports


def main() -> int:
    args = parse_args()
    if Image is None:
        print(json.dumps({"status": "error", "issues": ["需要安装 Pillow 才能读取 PNG"]}, ensure_ascii=False))
        return 2

    root = args.root.resolve()
    reports: List[Dict[str, Any]] = []
    # The release set is deliberately explicit: concepts and uncalibrated enemies
    # are design records, not publication sprites.
    reports.extend(
        validate_sprite_directory(
            root / "doge" / "calibrated",
            standard_height=args.standard_height,
            baseline=args.baseline,
            preview_size=args.preview_size,
            geometry_required=True,
            category="release",
        )
    )
    reports.extend(
        validate_sprite_directory(
            root / "pure_run" / "enemies" / "approved",
            standard_height=args.standard_height,
            baseline=args.baseline,
            preview_size=args.preview_size,
            geometry_required=False,
            category="approved-enemy",
        )
    )
    reports.extend(validate_tiles(root / "pure_run" / "tiles"))

    if args.include_candidates:
        for candidate_dir in (root / "pure_run" / "enemies" / "candidates", root / "doge" / "candidates"):
            reports.extend(
                validate_sprite_directory(
                candidate_dir,
                standard_height=args.standard_height,
                baseline=args.baseline,
                preview_size=args.preview_size,
                geometry_required=False,
                category="candidate",
            )
            )

    if args.review_examples:
        repo_root = root.parent.parent
        default_manifest = (
            Path(__file__).resolve().parents[1] / "examples" / "cases.json"
        )
        manifest_path = (
            args.review_manifest.resolve()
            if args.review_manifest
            else default_manifest
        )
        reports.extend(
            validate_review_examples(
                repo_root,
                manifest_path,
                standard_height=args.standard_height,
                baseline=args.baseline,
                preview_size=args.preview_size,
            )
        )

    failures = [report for report in reports if report.get("issues")]
    summary = {
        "status": "fail" if failures else "ok",
        "root": root.as_posix(),
        "standard_height": args.standard_height,
        "baseline": args.baseline,
        "preview_size": args.preview_size,
        "include_candidates": args.include_candidates,
        "review_examples": args.review_examples,
        "files_checked": len(reports),
        "failures": len(failures),
        "reports": reports,
    }
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 1 if args.strict and failures else 0


if __name__ == "__main__":
    sys.exit(main())
