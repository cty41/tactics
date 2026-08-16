"""Validation and receipt helpers for disposable Unity AssetDatabase export DTOs."""

from __future__ import annotations

import hashlib
import json
import re
from collections.abc import Mapping
from pathlib import Path
from typing import Any

from Tools.migration.manifest import normalize_content_id

_HEX_32 = re.compile(r"^[0-9a-f]{32}$")
_HEX_40 = re.compile(r"^[0-9a-f]{40}$")
_HEX_64 = re.compile(r"^[0-9a-f]{64}$")


def load_json(path: Path) -> dict[str, Any]:
    document = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(document, dict):
        raise ValueError(f"expected JSON object: {path}")
    return document


def validate_export_document(
    document: Mapping[str, Any],
    specification: Mapping[str, Any],
) -> list[str]:
    """Validate identity, source binding, ordering and stable AssetDatabase identifiers."""

    for field in ("schemaVersion", "batchId", "exporterVersion", "sourceTag", "sourceCommit"):
        if document.get(field) != specification.get(field):
            raise ValueError(f"export field does not match specification: {field}")
    if not str(document.get("unityVersion", "")).strip():
        raise ValueError("export is missing Unity version")

    specification_assets = {
        str(asset["sourceKey"]): asset for asset in specification.get("assets", [])
    }
    assets = document.get("assets")
    if not isinstance(assets, list) or not assets:
        raise ValueError("export contains no assets")
    keys = [str(asset.get("sourceKey", "")) for asset in assets]
    if keys != sorted(keys) or len(keys) != len(set(keys)):
        raise ValueError("export source keys must be unique and ordinally sorted")
    if set(keys) != set(specification_assets):
        raise ValueError("export asset set does not match specification")

    warnings: list[str] = []
    for asset in assets:
        source_key = str(asset["sourceKey"])
        expected = specification_assets[source_key]
        for field in ("sourcePath", "gitBlobSha1"):
            if asset.get(field) != expected.get(field):
                raise ValueError(f"{source_key} does not match specification field {field}")
        if asset.get("targetContentIds") != sorted(expected.get("targetContentIds", [])):
            raise ValueError(f"{source_key} has mismatched target ContentIds")
        for content_id in asset["targetContentIds"]:
            normalize_content_id(str(content_id))
        if not _HEX_40.fullmatch(str(asset.get("gitBlobSha1", ""))):
            raise ValueError(f"{source_key} has invalid frozen git blob id")
        if not _HEX_32.fullmatch(str(asset.get("sourceGuid", ""))):
            raise ValueError(f"{source_key} has invalid Unity GUID")
        if int(asset.get("sourceLocalFileId", 0)) == 0:
            raise ValueError(f"{source_key} has empty Unity LocalFileId")
        if not _HEX_32.fullmatch(str(asset.get("dependencyHash", ""))):
            raise ValueError(f"{source_key} has invalid AssetDatabase dependency hash")
        expected_mode = str(expected.get("exportMode", "serialized-object"))
        export_mode = str(asset.get("exportMode", "serialized-object"))
        if export_mode != expected_mode:
            raise ValueError(f"{source_key} has mismatched export mode")
        objects = asset.get("objects")
        if not isinstance(objects, list):
            raise ValueError(f"{source_key} has invalid serialized objects")
        if export_mode == "audit-only-file":
            if objects:
                raise ValueError(f"{source_key} audit-only export contains serialized objects")
            if not _HEX_64.fullmatch(str(asset.get("sourceFileSha256", ""))):
                raise ValueError(f"{source_key} has invalid source file SHA-256")
            if int(asset.get("sourceByteLength", 0)) <= 0:
                raise ValueError(f"{source_key} has invalid source byte length")
        elif export_mode == "serialized-object":
            if not objects:
                raise ValueError(f"{source_key} contains no serialized objects")
        else:
            raise ValueError(f"{source_key} has unsupported export mode {export_mode}")
        object_paths = [str(item.get("objectPath", "")) for item in objects]
        if object_paths != sorted(object_paths):
            raise ValueError(f"{source_key} object paths are not sorted")
        for exported_object in objects:
            property_paths = [
                str(item.get("propertyPath", ""))
                for item in exported_object.get("properties", [])
            ]
            if property_paths != sorted(property_paths):
                raise ValueError(f"{source_key} property paths are not sorted")
        for property_kind in asset.get("unsupportedPropertyKinds", []):
            warnings.append(f"{source_key}: unsupported SerializedPropertyType {property_kind}")
    return warnings


def export_semantic_hash(document: Mapping[str, Any]) -> str:
    canonical = json.dumps(
        document,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return "sha256:" + hashlib.sha256(canonical).hexdigest()


def build_export_receipt(
    document: Mapping[str, Any],
    specification: Mapping[str, Any],
) -> dict[str, Any]:
    warnings = validate_export_document(document, specification)
    return {
        "schemaVersion": 1,
        "batchId": document["batchId"],
        "classification": "real_unity_assetdatabase_export",
        "sourceTag": document["sourceTag"],
        "sourceCommit": document["sourceCommit"],
        "unityVersion": document["unityVersion"],
        "exporterVersion": document["exporterVersion"],
        "exportHash": export_semantic_hash(document),
        "assets": [
            {
                "sourceKey": asset["sourceKey"],
                "sourcePath": asset["sourcePath"],
                "gitBlobSha1": asset["gitBlobSha1"],
                "sourceGuid": asset["sourceGuid"],
                "sourceLocalFileId": asset["sourceLocalFileId"],
                "dependencyHash": asset["dependencyHash"],
                **(
                    {
                        "exportMode": "audit-only-file",
                        "sourceFileSha256": asset["sourceFileSha256"],
                        "sourceByteLength": asset["sourceByteLength"],
                    }
                    if asset.get("exportMode") == "audit-only-file"
                    else {}
                ),
                "serializedObjectCount": len(asset["objects"]),
                "serializedPropertyCount": sum(
                    len(item.get("properties", [])) for item in asset["objects"]
                ),
                "targetContentIds": asset["targetContentIds"],
            }
            for asset in document["assets"]
        ],
        "warnings": warnings,
        "ownership": "UnityOwned",
        "nextState": "Exported",
    }
