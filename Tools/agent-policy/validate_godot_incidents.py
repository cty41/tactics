#!/usr/bin/env python3
"""Validate the project-local Godot Incident schema without external packages."""

from __future__ import annotations

import argparse
import re
from pathlib import Path


REQUIRED_FIELDS = {
    "id",
    "status",
    "signature",
    "godot_version",
    "dotnet_sdk",
    "os",
    "context",
    "language",
    "last_verified",
}
REQUIRED_SECTIONS = {
    "Observed",
    "Reproduction",
    "Cause and resolution",
    "Evidence",
    "Scope and invalidation",
}
ALLOWED_STATUS = {"observed", "reproduced", "verified", "superseded"}
ALLOWED_CONTEXT = {"editor", "runtime", "headless", "export", "build"}
ALLOWED_LANGUAGE = {"csharp", "gdscript", "gdextension", "mixed"}
ID_PATTERN = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
DATE_PATTERN = re.compile(r"^\d{4}-\d{2}-\d{2}$")


def parse_frontmatter(text: str) -> dict[str, str]:
    match = re.match(r"^---\s*\n(.*?)\n---\s*\n", text, re.DOTALL)
    if not match:
        raise ValueError("missing YAML frontmatter")

    fields: dict[str, str] = {}
    for line in match.group(1).splitlines():
        if not line.strip():
            continue
        key, separator, value = line.partition(":")
        if not separator:
            raise ValueError(f"invalid frontmatter line: {line}")
        fields[key.strip()] = value.strip().strip('"').strip("'")
    return fields


def validate_incident(path: Path) -> list[str]:
    errors: list[str] = []
    text = path.read_text(encoding="utf-8")
    try:
        fields = parse_frontmatter(text)
    except ValueError as error:
        return [f"{path}: {error}"]

    missing = sorted(REQUIRED_FIELDS - fields.keys())
    if missing:
        errors.append(f"{path}: missing fields: {', '.join(missing)}")
    extra = sorted(fields.keys() - REQUIRED_FIELDS)
    if extra:
        errors.append(f"{path}: unsupported fields: {', '.join(extra)}")

    incident_id = fields.get("id", "")
    if not ID_PATTERN.fullmatch(incident_id):
        errors.append(f"{path}: invalid id '{incident_id}'")
    if path.stem != incident_id:
        errors.append(f"{path}: id '{incident_id}' does not match filename")
    if fields.get("status") not in ALLOWED_STATUS:
        errors.append(f"{path}: invalid status '{fields.get('status', '')}'")
    if fields.get("context") not in ALLOWED_CONTEXT:
        errors.append(f"{path}: invalid context '{fields.get('context', '')}'")
    if fields.get("language") not in ALLOWED_LANGUAGE:
        errors.append(f"{path}: invalid language '{fields.get('language', '')}'")
    if not DATE_PATTERN.fullmatch(fields.get("last_verified", "")):
        errors.append(f"{path}: invalid last_verified date")

    sections = set(re.findall(r"^##\s+(.+?)\s*$", text, re.MULTILINE))
    for section in sorted(REQUIRED_SECTIONS - sections):
        errors.append(f"{path}: missing section '{section}'")
    if fields.get("status") == "verified" and "`verified_local`" not in text:
        errors.append(f"{path}: verified Incident requires verified_local evidence")
    return errors


def validate_root(root: Path) -> list[str]:
    errors: list[str] = []
    for path in sorted(root.glob("*.md")):
        if path.name in {"index.md", "schema.md"}:
            continue
        errors.extend(validate_incident(path))
    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(".agents/incidents/godot"),
        help="Godot Incident directory",
    )
    args = parser.parse_args()

    errors = validate_root(args.root)
    if errors:
        for error in errors:
            print(f"ERROR: {error}")
        return 1

    count = len([path for path in args.root.glob("*.md") if path.name not in {"index.md", "schema.md"}])
    print(f"Godot Incident validation passed: {count} incident(s) checked.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
