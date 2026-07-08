from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Literal, Sequence

PlaybackMode = Literal["fps", "duration"]


@dataclass(slots=True)
class FrameSource:
    path: Path
    width: int
    height: int
    pixel_data: Sequence[float]


@dataclass(slots=True)
class FrameAsset:
    path: Path
    width: int
    height: int
    texture_tag: str


@dataclass(slots=True)
class SpriteSequence:
    source: Path
    frames: list[FrameAsset]
    warnings: list[str] = field(default_factory=list)
