"""Small deterministic helpers shared by migration converters and tests."""

from __future__ import annotations

import hashlib
import json
import re
from collections.abc import Iterable, Mapping

_CONTENT_ID = re.compile(r"^[a-z0-9]+(?:[.-][a-z0-9]+)*$")


def normalize_content_id(value: str) -> str:
    normalized = value.strip()
    if not _CONTENT_ID.fullmatch(normalized):
        raise ValueError(f"invalid ContentId: {value!r}")
    return normalized


def validate_unique_content_ids(entries: Iterable[Mapping[str, object]]) -> None:
    seen: set[str] = set()
    for entry in entries:
        content_id = normalize_content_id(str(entry["contentId"]))
        if content_id in seen:
            raise ValueError(f"duplicate ContentId: {content_id}")
        seen.add(content_id)


def semantic_manifest_hash(entries: Iterable[Mapping[str, object]]) -> str:
    canonical = json.dumps(
        sorted(entries, key=lambda entry: str(entry["contentId"])),
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(canonical).hexdigest()
