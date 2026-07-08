from __future__ import annotations

import argparse
from pathlib import Path
from typing import Sequence


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Sprite sequence preview tool")
    parser.add_argument("--source", type=Path, default=None, help="Sprite folder or single image path")
    parser.add_argument("--mode", choices=("fps", "duration"), default="duration", help="Playback mode")
    parser.add_argument("--fps", type=float, default=6.0, help="Playback fps when mode is fps")
    parser.add_argument("--duration", type=float, default=1.0, help="Playback duration in seconds when mode is duration")
    parser.add_argument("--loop", dest="loop", action="store_true", default=True, help="Enable looping")
    parser.add_argument("--no-loop", dest="loop", action="store_false", help="Disable looping")
    parser.add_argument("--autoplay", dest="autoplay", action="store_true", default=True, help="Auto play after loading")
    parser.add_argument("--no-autoplay", dest="autoplay", action="store_false", help="Do not auto play after loading")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)

    try:
        from sprite_preview.app import AppConfig, SpritePreviewApp
    except ModuleNotFoundError as exc:
        if exc.name == "dearpygui":
            print("Missing dependency: dearpygui. Please run: python -m pip install -r Tools/sprite_preview/requirements.txt")
            return 1
        raise

    config = AppConfig(
        default_mode=args.mode,
        default_fps=args.fps,
        default_duration_seconds=args.duration,
        default_loop=args.loop,
        default_autoplay=args.autoplay,
    )
    app = SpritePreviewApp(config)
    app.run(initial_source=args.source)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
