"""
Regenerate Unity .meta files under TbsfFork with new GUIDs, mapping from original TBSFramework paths.
Run from repo root: python Tools/regenerate_tbsf_fork_meta.py
"""
from __future__ import annotations

import os
import re
import uuid
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]

PAIRS: list[tuple[Path, Path]] = [
    (REPO / "Assets/TBSFramework/External/tbsf-common", REPO / "Assets/Tactics/TbsfFork/External/tbsf-common"),
    (REPO / "Assets/TBSFramework/Scripts", REPO / "Assets/Tactics/TbsfFork/Scripts"),
    (REPO / "Assets/TBSFramework/Editor", REPO / "Assets/Tactics/TbsfFork/Editor"),
]

# dest basename -> source basename (asmdef renames)
ASMDEF_REVERSE = {
    "com.tactics.tbsf.common.asmdef": "com.crookedhead.tbsf.common.asmdef",
    "com.tactics.tbsf.unity.asmdef": "com.crookedhead.tbsf.unity.asmdef",
    "com.tactics.tbsf.editor.asmdef": "com.crookedhead.tbsf.unity.editor.asmdef",
}


def new_guid() -> str:
    return uuid.uuid4().hex


def source_for_dest(dst_file: Path, dst_root: Path, src_root: Path) -> Path | None:
    rel = dst_file.relative_to(dst_root)
    parts = list(rel.parts)
    if parts and parts[-1] in ASMDEF_REVERSE:
        parts[-1] = ASMDEF_REVERSE[parts[-1]]
        rel = Path(*parts)
    src = src_root / rel
    return src if src.is_file() else None


def main() -> dict[str, str]:
    guid_map: dict[str, str] = {}
    for src_root, dst_root in PAIRS:
        if not dst_root.is_dir():
            continue
        for dirpath, _, filenames in os.walk(dst_root):
            d = Path(dirpath)
            for name in filenames:
                if name.endswith(".meta"):
                    continue
                dst_file = d / name
                src_file = source_for_dest(dst_file, dst_root, src_root)
                if src_file is None or not src_file.is_file():
                    raise FileNotFoundError(f"No source for {dst_file}")
                src_meta = src_file.with_suffix(src_file.suffix + ".meta")
                if not src_meta.is_file():
                    raise FileNotFoundError(f"No source meta for {src_file}")
                text = src_meta.read_text(encoding="utf-8", errors="replace")
                m = re.search(r"^guid:\s*([a-f0-9]{32})\s*$", text, re.MULTILINE)
                if not m:
                    raise RuntimeError(f"Bad meta: {src_meta}")
                old = m.group(1)
                ng = new_guid()
                guid_map[old] = ng
                new_text = re.sub(
                    r"^guid:\s*[a-f0-9]{32}\s*$",
                    f"guid: {ng}",
                    text,
                    count=1,
                    flags=re.MULTILINE,
                )
                dst_meta = dst_file.with_suffix(dst_file.suffix + ".meta")
                dst_meta.write_text(new_text.rstrip() + "\n", encoding="utf-8", newline="\n")
    return guid_map


if __name__ == "__main__":
    m = main()
    out = REPO / "Tools/tbsf_guid_map.txt"
    lines = [f"{k}\t{v}" for k, v in sorted(m.items())]
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {len(m)} meta files; map saved to {out}")
