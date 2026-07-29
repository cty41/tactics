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

    if geometry_required:
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
    reports.extend(validate_tiles(root / "pure_run" / "tiles"))

    if args.include_candidates:
        candidate_dir = root / "pure_run" / "enemies" / "candidates"
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

    failures = [report for report in reports if report.get("issues")]
    summary = {
        "status": "fail" if failures else "ok",
        "root": root.as_posix(),
        "standard_height": args.standard_height,
        "baseline": args.baseline,
        "preview_size": args.preview_size,
        "include_candidates": args.include_candidates,
        "files_checked": len(reports),
        "failures": len(failures),
        "reports": reports,
    }
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 1 if args.strict and failures else 0


if __name__ == "__main__":
    sys.exit(main())
