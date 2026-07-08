from __future__ import annotations

from pathlib import Path

import dearpygui.dearpygui as dpg

from .models import FrameSource
from .sorting import natural_sort_key

SUPPORTED_IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".bmp", ".tga", ".webp"}


def collect_source_paths(source: Path) -> list[Path]:
    if source.is_file():
        if source.suffix.lower() not in SUPPORTED_IMAGE_EXTENSIONS:
            raise ValueError(f"Unsupported image format: {source.suffix}")
        return [source]

    if not source.is_dir():
        raise FileNotFoundError(f"Path does not exist: {source}")

    candidates = [
        path
        for path in source.iterdir()
        if path.is_file()
        and not path.name.startswith(".")
        and path.suffix.lower() in SUPPORTED_IMAGE_EXTENSIONS
    ]
    return sorted(candidates, key=natural_sort_key)


def load_frame_sources(source: Path) -> tuple[list[FrameSource], list[str]]:
    source_paths = collect_source_paths(source)
    warnings: list[str] = []
    frames: list[FrameSource] = []

    for path in source_paths:
        try:
            width, height, _channels, pixel_data = dpg.load_image(str(path))
        except Exception as exc:  # noqa: BLE001
            warnings.append(f"Skipped incompatible or corrupted image: {path.name} ({exc})")
            continue

        frames.append(FrameSource(path=path, width=width, height=height, pixel_data=pixel_data))

    return frames, warnings
