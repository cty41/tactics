from __future__ import annotations

import re
from pathlib import Path

_number_pattern = re.compile(r"(\d+)")


def natural_sort_key(value: str | Path) -> tuple[object, ...]:
    text = value.name if isinstance(value, Path) else value
    parts = _number_pattern.split(text)
    key: list[object] = []
    for part in parts:
        if not part:
            continue
        if part.isdigit():
            key.append(int(part))
        else:
            key.append(part.casefold())
    return tuple(key)
