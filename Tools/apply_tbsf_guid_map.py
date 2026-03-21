"""Apply Tools/tbsf_guid_map.txt to Unity YAML under Assets/Tactics (excluding TbsfFork)."""
from __future__ import annotations

import re
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
MAP_FILE = REPO / "Tools/tbsf_guid_map.txt"
ROOT = REPO / "Assets/Tactics"
SKIP_PART = "TbsfFork"

EXTS = {".unity", ".prefab", ".asset", ".asmdef", ".controller", ".overrideController", ".mask", ".anim"}


def load_map() -> list[tuple[str, str]]:
    pairs: list[tuple[str, str]] = []
    for line in MAP_FILE.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or "\t" not in line:
            continue
        old, new = line.split("\t", 1)
        if len(old) == 32 and len(new) == 32:
            pairs.append((old, new))
    # Longest-first avoids partial overlap issues (none expected for guids)
    pairs.sort(key=lambda x: len(x[0]), reverse=True)
    return pairs


def main() -> None:
    mapping = load_map()
    if not mapping:
        raise SystemExit("empty map")
    changed = 0
    files = 0
    for path in ROOT.rglob("*"):
        if not path.is_file():
            continue
        rel = path.relative_to(ROOT)
        if rel.parts and rel.parts[0] == SKIP_PART:
            continue
        if path.suffix.lower() not in EXTS:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        orig = text
        for old, new in mapping:
            if old in text:
                text = text.replace(old, new)
        if text != orig:
            path.write_text(text, encoding="utf-8", newline="\n")
            changed += 1
        files += 1
    print(f"Scanned {files} files under {ROOT.relative_to(REPO)} (skip {SKIP_PART}); updated {changed}")


if __name__ == "__main__":
    main()
