"""Compile the disposable Unity AssetDatabase export into a typed Poison Spear draft.

The resulting JSON is a migration boundary document, not a runtime asset format.  It is
consumed by the Godot ResourceSaver builder and may be deleted after the migration batch.
"""

from __future__ import annotations

import argparse
import json
import re
from collections.abc import Mapping
from pathlib import Path
from typing import Any

from Tools.migration.export_document import export_semantic_hash, load_json, validate_export_document

_INDEXED_RECORD = re.compile(r"^(?P<prefix>_.+?)\.Array\.data\[(?P<index>\d+)\](?:\.(?P<field>.+))?$")


def compile_poison_spear_draft(
    export: Mapping[str, Any],
    specification: Mapping[str, Any],
) -> dict[str, Any]:
    """Validate and compile the real Lv1 export into a concise deterministic draft."""

    warnings = validate_export_document(export, specification)
    if warnings:
        raise ValueError("Poison Spear export contains unsupported values: " + "; ".join(warnings))

    assets = {str(item["sourceKey"]): item for item in export["assets"]}
    ability = _main_properties(assets["skill.poison-spear.lv1/ability"])
    skill_graph = _main_properties(assets["skill.poison-spear.lv1/graph"])
    presentation_graph = _main_properties(assets["presentation.poison-spear.lv1/graph"])
    projectile = _main_properties(assets["projectile.poison-spear/profile"])
    poison = _main_properties(assets["buff.poison/definition"])

    skill_nodes = _records(skill_graph, "_nodes")
    skill_edges = _records(skill_graph, "_edges")
    presentation_nodes = _records(presentation_graph, "_nodes")
    presentation_edges = _records(presentation_graph, "_edges")
    preview_phases = _records(presentation_graph, "_previewPhases")

    amazon_node = _single_record(skill_nodes, "AmazonSkillNodeRecord")
    projectile_node = _single_record(skill_nodes, "ProjectileLaunchNodeRecord")
    selection_node = _single_record(skill_nodes, "SelectPrimaryTargetNodeRecord")
    presentation_projectile_node = _single_record(
        presentation_nodes, "PresentationProjectileNodeRecord"
    )

    range_value = _integer(ability, "_targetRange")
    if range_value != _record_integer(selection_node, "_maxRange"):
        raise ValueError("ability and SelectPrimaryTarget range disagree")
    if _record_string(amazon_node, "_skillKind") != "PoisonSpear":
        raise ValueError("real skill graph does not contain PoisonSpear semantics")
    if _record_integer(amazon_node, "_level") != 1:
        raise ValueError("real migration batch must contain Poison Spear Lv1")

    poison_duration = _integer(poison, "_defaultDuration")
    poison_tick_damage = _number(poison, "_damagePerTurn")
    projectile_speed = _record_number(projectile_node, "_speed")
    projectile_travel_time = _record_number(projectile_node, "_travelTime")

    typed_skill_graph = {
        "schemaVersion": _integer(skill_graph, "_version"),
        "displayName": _string(skill_graph, "_displayName"),
        "targeting": {
            "mode": _string(skill_graph, "_targeting.Mode"),
            "minimumSelections": _integer(skill_graph, "_targeting.MinimumSelections"),
            "maximumSelections": _integer(skill_graph, "_targeting.MaximumSelections"),
            "allowsEmptyCell": _boolean(skill_graph, "_targeting.AllowsEmptyCell"),
            "usesPathfinding": _boolean(skill_graph, "_targeting.UsesPathfinding"),
        },
        "nodes": _typed_records(skill_nodes),
        "edges": _typed_edges(skill_edges),
    }
    typed_presentation_graph = {
        "schemaVersion": _integer(presentation_graph, "_version"),
        "displayName": _string(presentation_graph, "_displayName"),
        "defaultPreviewEntry": _string(presentation_graph, "_defaultPreviewEntry"),
        "nodes": _typed_records(presentation_nodes),
        "edges": _typed_edges(presentation_edges),
        "previewPhases": [
            {
                "advanceKind": _record_string(record, "_advanceKind"),
                "continuationCue": _record_string(record, "_continuationCue"),
                "playTargetHitReaction": _record_boolean(record, "_playTargetHitReaction"),
            }
            for record in preview_phases
        ],
    }

    source = {
        "sourceTag": export["sourceTag"],
        "sourceCommit": export["sourceCommit"],
        "exporterVersion": export["exporterVersion"],
        "exportHash": export_semantic_hash(export),
        "assets": [
            {
                "sourceKey": asset["sourceKey"],
                "sourceGuid": asset["sourceGuid"],
                "sourceLocalFileId": asset["sourceLocalFileId"],
                "dependencyHash": asset["dependencyHash"],
                "gitBlobSha1": asset["gitBlobSha1"],
            }
            for asset in export["assets"]
        ],
    }

    contents = [
        _content(
            "buff.poison",
            "buff",
            [],
            {
                "name": _string(poison, "_buffName"),
                "duration": poison_duration,
                "tickDamage": poison_tick_damage,
                "damageCategory": _string(poison, "_damageCategory"),
                "effectType": _string(poison, "_effectType"),
                "polarity": _string(poison, "_polarity"),
                "refreshStrategy": _string(poison, "_refreshStrategy"),
                "triggerTiming": _string(poison, "_triggerTiming"),
            },
        ),
        _content(
            "encounter.poison-spear.10x10",
            "encounter",
            ["skill.poison-spear.lv1"],
            {
                "boardWidth": 10,
                "boardHeight": 10,
                "casterCell": [1, 1],
                "targetCell": [3, 2],
                "contract": "approved-10x10-probe-v1",
            },
        ),
        _content(
            "impact.poison-spear",
            "packed-scene",
            [],
            {
                "lifetime": _number(projectile, "_impactLifetime"),
                "scale": _number(projectile, "_impactScale"),
                "sourcePrefab": _reference(projectile, "_impactPrefab"),
            },
        ),
        _content(
            "presentation.poison-spear.lv1",
            "presentation",
            ["projectile.poison-spear", "impact.poison-spear"],
            {
                "graph": typed_presentation_graph,
                "projectileSpeed": _record_number(presentation_projectile_node, "_speed"),
                "fallbackTravelTime": _record_number(
                    presentation_projectile_node, "_fallbackTravelTime"
                ),
            },
        ),
        _content(
            "projectile.poison-spear",
            "packed-scene",
            [],
            {
                "arcHeight": _number(projectile, "_arcHeight"),
                "scale": _number(projectile, "_scale"),
                "tint": _string(projectile, "_tint"),
                "trajectoryStyle": _string(projectile, "_trajectoryStyle"),
                "visualKind": _string(projectile, "_visualKind"),
                "rotateAlongTangent": _boolean(projectile, "_rotateAlongTangent"),
                "sortingOrderOffset": _integer(projectile, "_sortingOrderOffset"),
                "sourcePrefab": _reference(projectile, "_flightPrefab"),
                "sourceSprite": _reference(projectile, "_sprite"),
            },
        ),
        _content(
            "skill.poison-spear.lv1",
            "skill",
            ["buff.poison", "presentation.poison-spear.lv1"],
            {
                "displayName": _string(ability, "_displayName"),
                "description": _string(ability, "_description"),
                "range": range_value,
                "manaCost": _integer(ability, "_manaCost"),
                "damage": 8,
                "poisonDuration": poison_duration,
                "poisonTickDamage": poison_tick_damage,
                "requiresLineOfSight": _record_boolean(
                    projectile_node, "_requiresLineOfSight"
                ),
                "projectileSpeed": projectile_speed,
                "projectileTravelTime": projectile_travel_time,
                "dropOnHit": _record_boolean(projectile_node, "_dropOnHit"),
                "authoredDropSearchRadius": _record_integer(projectile_node, "_dropSearchRadius"),
                "runtimeDropSearchRadius": 3,
                "dropsSpearOnCompletion": True,
                "skillGraph": typed_skill_graph,
            },
        ),
    ]

    return {
        "schemaVersion": 1,
        "batchId": "poison-spear-lv1-real",
        "sourceExportBatchId": export["batchId"],
        "classification": "disposable_typed_migration_draft",
        "source": source,
        "contents": sorted(contents, key=lambda item: item["contentId"]),
    }


def canonical_json(document: Mapping[str, Any]) -> str:
    return json.dumps(document, ensure_ascii=False, sort_keys=True, indent=2) + "\n"


def _content(
    content_id: str,
    resource_type_id: str,
    references: list[str],
    properties: Mapping[str, Any],
) -> dict[str, Any]:
    return {
        "contentId": content_id,
        "resourceTypeId": resource_type_id,
        "schemaVersion": 1,
        "references": sorted(references),
        "properties": dict(properties),
    }


def _main_properties(asset: Mapping[str, Any]) -> dict[str, Mapping[str, Any]]:
    main = next((item for item in asset["objects"] if item["objectPath"] == "main"), None)
    if main is None:
        raise ValueError(f"asset has no main object: {asset['sourceKey']}")
    return {str(item["propertyPath"]): item for item in main["properties"]}


def _records(
    properties: Mapping[str, Mapping[str, Any]], prefix: str
) -> list[dict[str, Any]]:
    records: dict[int, dict[str, Any]] = {}
    for path, value in properties.items():
        match = _INDEXED_RECORD.fullmatch(path)
        if match is None or match.group("prefix") != prefix:
            continue
        index = int(match.group("index"))
        field = match.group("field")
        record = records.setdefault(index, {})
        if field is None:
            managed_type = str(value.get("value") or "").strip()
            if managed_type:
                record["$type"] = managed_type.split()[-1].rsplit(".", 1)[-1]
        elif field == "_position":
            # The AssetDatabase export also contains explicit x/y children; use those
            # numeric components instead of the display-formatted aggregate value.
            continue
        elif field in {"_position.x", "_position.y"}:
            component = field.rsplit(".", 1)[-1]
            position = record.setdefault("_position", {})
            if not isinstance(position, dict):
                raise ValueError("record position has conflicting scalar and component values")
            position[component] = _property_value(value)
        elif ".Array" not in field and not field.endswith((".x", ".y", ".z", ".w")):
            record[field] = _property_value(value)
    if not records or sorted(records) != list(range(len(records))):
        raise ValueError(f"{prefix} records are missing or non-contiguous")
    return [records[index] for index in sorted(records)]


def _typed_records(records: list[dict[str, Any]]) -> list[dict[str, Any]]:
    typed: list[dict[str, Any]] = []
    for record in records:
        node_id = _record_string(record, "_nodeId")
        typed.append(
            {
                "id": node_id,
                "type": str(record["$type"]),
                "enabled": _record_boolean(record, "_enabled"),
                "position": _record_position(record),
                "fields": {
                    key.removeprefix("_"): value
                    for key, value in sorted(record.items())
                    if key not in {"$type", "_nodeId", "_enabled", "_position"}
                },
            }
        )
    return typed


def _record_position(record: Mapping[str, Any]) -> dict[str, float]:
    value = record.get("_position")
    if not isinstance(value, Mapping):
        raise ValueError("record field is not a two-component position: _position")
    components: dict[str, float] = {}
    for component in ("x", "y"):
        coordinate = value.get(component)
        if isinstance(coordinate, bool) or not isinstance(coordinate, (int, float)):
            raise ValueError(f"record position component is not numeric: {component}")
        components[component] = float(coordinate)
    return components


def _typed_edges(records: list[dict[str, Any]]) -> list[dict[str, Any]]:
    return [
        {
            "id": _record_string(record, "_edgeId"),
            "source": _record_string(record, "_sourceNodeId"),
            "target": _record_string(record, "_targetNodeId"),
            **(
                {"portType": _record_string(record, "_portType")}
                if "_portType" in record
                else {}
            ),
        }
        for record in records
    ]


def _single_record(records: list[dict[str, Any]], type_suffix: str) -> dict[str, Any]:
    matches = [record for record in records if str(record.get("$type", "")).endswith(type_suffix)]
    if len(matches) != 1:
        raise ValueError(f"expected one {type_suffix}, found {len(matches)}")
    return matches[0]


def _required(properties: Mapping[str, Mapping[str, Any]], path: str) -> Mapping[str, Any]:
    try:
        value = properties[path]
    except KeyError as error:
        raise ValueError(f"required Unity property is missing: {path}") from error
    if not value.get("supported", False):
        raise ValueError(f"required Unity property is unsupported: {path}")
    return value


def _property_value(property_value: Mapping[str, Any]) -> Any:
    if property_value.get("reference") is not None:
        reference = property_value["reference"]
        return {
            "sourcePath": reference["sourcePath"],
            "sourceGuid": reference["sourceGuid"],
            "sourceLocalFileId": reference["sourceLocalFileId"],
        }
    kind = str(property_value["propertyType"])
    value = property_value.get("value")
    if kind == "Boolean":
        return str(value).lower() == "true"
    if kind in {"Integer", "ArraySize"}:
        return int(str(value))
    if kind == "Float":
        return float(str(value))
    return value


def _string(properties: Mapping[str, Mapping[str, Any]], path: str) -> str:
    value = _property_value(_required(properties, path))
    if not isinstance(value, str):
        raise ValueError(f"Unity property is not a string: {path}")
    return value


def _integer(properties: Mapping[str, Mapping[str, Any]], path: str) -> int:
    value = _property_value(_required(properties, path))
    if isinstance(value, bool) or not isinstance(value, int):
        raise ValueError(f"Unity property is not an integer: {path}")
    return value


def _number(properties: Mapping[str, Mapping[str, Any]], path: str) -> float:
    value = _property_value(_required(properties, path))
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"Unity property is not numeric: {path}")
    return float(value)


def _boolean(properties: Mapping[str, Mapping[str, Any]], path: str) -> bool:
    value = _property_value(_required(properties, path))
    if not isinstance(value, bool):
        raise ValueError(f"Unity property is not a boolean: {path}")
    return value


def _reference(properties: Mapping[str, Mapping[str, Any]], path: str) -> dict[str, Any]:
    value = _property_value(_required(properties, path))
    if not isinstance(value, dict):
        raise ValueError(f"Unity property is not an object reference: {path}")
    return value


def _record_string(record: Mapping[str, Any], field: str) -> str:
    value = record.get(field)
    if not isinstance(value, str):
        raise ValueError(f"record field is not a string: {field}")
    return value


def _record_integer(record: Mapping[str, Any], field: str) -> int:
    value = record.get(field)
    if isinstance(value, bool) or not isinstance(value, int):
        raise ValueError(f"record field is not an integer: {field}")
    return value


def _record_number(record: Mapping[str, Any], field: str) -> float:
    value = record.get(field)
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"record field is not numeric: {field}")
    return float(value)


def _record_boolean(record: Mapping[str, Any], field: str) -> bool:
    value = record.get(field)
    if not isinstance(value, bool):
        raise ValueError(f"record field is not a boolean: {field}")
    return value


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export", type=Path, required=True)
    parser.add_argument("--specification", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()

    draft = compile_poison_spear_draft(
        load_json(arguments.export), load_json(arguments.specification)
    )
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(canonical_json(draft), encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
