"""Compile frozen Buff AssetDatabase and Item JSON sources into a typed draft."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
from collections.abc import Mapping
from pathlib import Path
from typing import Any

from Tools.migration.export_document import export_semantic_hash, load_json, validate_export_document
from Tools.migration.manifest import normalize_content_id

_BUFF_TYPE = "Tactics.Common.Units.Buffs.BuffConfig"
_HEX_32 = re.compile(r"^[0-9a-f]{32}$")
_BUFF_FIELDS = (
    "_buffName",
    "_defaultDuration",
    "_canAct",
    "_polarity",
    "_effectType",
    "_triggerTiming",
    "_curseCategory",
    "_damagePerTurn",
    "_elementType",
    "_damageCategory",
    "_refreshStrategy",
    "_speedModifier",
    "_damageReductionPercent",
    "_meleeRetaliationDuration",
)
_POLARITIES = {"Beneficial", "Harmful"}
_EFFECT_TYPES = {
    "None",
    "Frozen",
    "Marked",
    "CurseDamageAmplifier",
    "DamageReduction",
    "Poison",
    "Burning",
    "Slow",
    "Stun",
    "Fear",
}
_TRIGGERS = {"None", "TurnStart", "DamageTaken", "BeforeAttacked"}
_REFRESH = {"AddDuration", "RefreshDuration", "AddStacks"}
_ELEMENTS = {"None", "Fire", "Ice", "Lightning", "Poison"}
_DAMAGE_CATEGORIES = {"Physical", "Magic", "True"}
_CONSUMABLE_RARITIES = {"Common", "Uncommon", "Rare"}
_CONSUMABLE_EFFECTS = {"RestoreHealth", "RestoreMana", "RemoveHarmfulBuffs"}
_CONSUMABLE_TARGETS = {"Self", "AllyIncludingSelf"}
_EQUIPMENT_RARITIES = {"Common", "Rare"}
_EQUIPMENT_SLOTS = {"Weapon", "Armor", "Helmet", "Boots", "Accessory", "Shield"}


def compile_buff_item_draft(
    export: Mapping[str, Any],
    specification: Mapping[str, Any],
    golden: Mapping[str, Any],
    consumables_document: Mapping[str, Any],
    equipment_document: list[Mapping[str, Any]],
    consumables_sha256: str,
    equipment_sha256: str,
) -> dict[str, Any]:
    """Validate all 29 identities and return their deterministic migration draft."""

    warnings = validate_export_document(export, specification)
    if warnings:
        raise ValueError("Buff export contains unsupported values: " + "; ".join(warnings))
    if golden.get("schemaVersion") != 1 or golden.get("batchId") != export.get("batchId"):
        raise ValueError("Buff/Item Golden identity does not match the export batch")
    source = golden.get("source", {})
    if source.get("unityTag") != export.get("sourceTag") or source.get(
        "unityCommit"
    ) != export.get("sourceCommit"):
        raise ValueError("Buff/Item Golden is not bound to the exported Unity snapshot")
    _validate_sha256(consumables_sha256, source["consumablesJson"]["sha256"])
    _validate_sha256(equipment_sha256, source["equipmentJson"]["sha256"])

    exported_assets = {str(item["sourceKey"]): item for item in export["assets"]}
    golden_buffs = golden.get("buffs", [])
    if len(exported_assets) != 14 or len(golden_buffs) != 14:
        raise ValueError("Buff/Item batch must contain exactly 14 Buff roots")
    compiled_buffs = [
        _compile_buff(expected, exported_assets.get(str(expected["contentId"])))
        for expected in golden_buffs
    ]

    compiled_consumables = _compile_consumables(consumables_document)
    compiled_equipment = _compile_equipment(equipment_document)
    _require_equal("Consumable Golden", compiled_consumables, golden.get("consumables"))
    _require_equal("Equipment Golden", compiled_equipment, golden.get("equipment"))

    content_ids = [item["contentId"] for item in compiled_buffs]
    content_ids.extend(item["contentId"] for item in compiled_consumables)
    content_ids.extend(item["contentId"] for item in compiled_equipment)
    for content_id in content_ids:
        normalize_content_id(content_id)
    if len(content_ids) != 29 or len(set(content_ids)) != 29:
        raise ValueError("Buff/Item draft must contain 29 unique ContentIds")
    if golden.get("externalContentDependencies") != ["buff.poison"]:
        raise ValueError("buff.poison must remain the sole external content dependency")

    return {
        "schemaVersion": 1,
        "batchId": golden["batchId"],
        "classification": "disposable_typed_buff_item_migration_draft",
        "source": {
            "sourceTag": export["sourceTag"],
            "sourceCommit": export["sourceCommit"],
            "unityVersion": export["unityVersion"],
            "exporterVersion": export["exporterVersion"],
            "exportHash": export_semantic_hash(export),
            "consumablesJson": {
                **source["consumablesJson"],
                "measuredSha256": consumables_sha256,
            },
            "equipmentJson": {
                **source["equipmentJson"],
                "measuredSha256": equipment_sha256,
            },
            "buffAssets": [
                {
                    "contentId": asset["sourceKey"],
                    "sourcePath": asset["sourcePath"],
                    "gitBlobSha1": asset["gitBlobSha1"],
                    "sourceGuid": asset["sourceGuid"],
                    "sourceLocalFileId": asset["sourceLocalFileId"],
                    "dependencyHash": asset["dependencyHash"],
                }
                for asset in export["assets"]
            ],
        },
        "buffs": sorted(compiled_buffs, key=lambda item: item["contentId"]),
        "consumables": sorted(compiled_consumables, key=lambda item: item["contentId"]),
        "consumablePools": _compile_pools(consumables_document),
        "equipment": sorted(compiled_equipment, key=lambda item: item["contentId"]),
        "externalContentDependencies": ["buff.poison"],
        "payloadBoundary": {
            "buffIcons": "audit_only_not_copied",
            "iconPayloadCopied": False,
            "thirdPartyPayloadCopied": False,
            "visualAcceptance": "not_applicable_no_visual_payload",
        },
    }


def canonical_json(document: Mapping[str, Any]) -> str:
    return json.dumps(document, ensure_ascii=False, sort_keys=True, indent=2) + "\n"


def _compile_buff(expected: Mapping[str, Any], asset: Mapping[str, Any] | None) -> dict[str, Any]:
    content_id = str(expected["contentId"])
    if asset is None:
        raise ValueError(f"Buff export is missing {content_id}")
    if asset["sourcePath"] != expected["sourcePath"]:
        raise ValueError(f"{content_id} source path differs from Golden")
    if asset["gitBlobSha1"] != expected["gitBlobSha1"]:
        raise ValueError(f"{content_id} frozen blob differs from Golden")
    if asset["sourceGuid"] != expected["sourceGuid"]:
        raise ValueError(f"{content_id} GUID differs from Golden")
    if asset["dependencyHash"] != expected["dependencyHash"]:
        raise ValueError(f"{content_id} dependency hash differs from Golden")
    objects = [item for item in asset["objects"] if item["objectType"] == _BUFF_TYPE]
    if len(objects) != 1:
        raise ValueError(f"{content_id} must contain exactly one {_BUFF_TYPE}")
    properties = {str(item["propertyPath"]): item for item in objects[0]["properties"]}
    for field in _BUFF_FIELDS:
        if field not in properties:
            raise ValueError(f"{content_id} is missing serialized field {field}")

    actual = {
        "contentId": content_id,
        "sourceId": _value(properties, "_buffName"),
        "sourcePath": asset["sourcePath"],
        "sourceGuid": asset["sourceGuid"],
        "sourceLocalFileId": int(asset["sourceLocalFileId"]),
        "defaultDuration": _integer(properties, "_defaultDuration"),
        "canAct": _boolean(properties, "_canAct"),
        "polarity": _enum(properties, "_polarity", _POLARITIES),
        "effectType": _enum(properties, "_effectType", _EFFECT_TYPES),
        "triggerTiming": _enum(properties, "_triggerTiming", _TRIGGERS),
        "curseCategory": _value(properties, "_curseCategory"),
        "damagePerTurn": _number(properties, "_damagePerTurn"),
        "elementType": _enum(properties, "_elementType", _ELEMENTS),
        "damageCategory": _enum(properties, "_damageCategory", _DAMAGE_CATEGORIES),
        "refreshStrategy": _enum(properties, "_refreshStrategy", _REFRESH),
        "speedModifier": _number(properties, "_speedModifier"),
        "damageReductionPercent": _number(properties, "_damageReductionPercent"),
        "meleeRetaliationDuration": _integer(properties, "_meleeRetaliationDuration"),
        "meleeRetaliationBuffContentId": _retaliation_content_id(properties),
        "iconAudit": _icon_audit(properties),
        "externalDependency": content_id == "buff.poison",
    }
    _require_equal(f"{content_id} Golden", actual, expected["definition"])
    return actual


def _compile_consumables(document: Mapping[str, Any]) -> list[dict[str, Any]]:
    if set(document) != {"Definitions", "Pools"}:
        raise ValueError("Consumables JSON contains unknown or missing root fields")
    definitions = document["Definitions"]
    if not isinstance(definitions, list) or len(definitions) != 3:
        raise ValueError("Consumables JSON must contain exactly 3 definitions")
    expected_fields = {
        "Id", "DisplayName", "Description", "Rarity", "Price", "MaxCharges",
        "EffectKind", "Magnitude", "MaxRange", "TargetMode",
    }
    result: list[dict[str, Any]] = []
    for item in definitions:
        if set(item) != expected_fields:
            raise ValueError(f"Consumable {item.get('Id')} has unknown or missing fields")
        source_id = _required_string(item, "Id")
        rarity = _required_enum(item, "Rarity", _CONSUMABLE_RARITIES)
        effect = _required_enum(item, "EffectKind", _CONSUMABLE_EFFECTS)
        target = _required_enum(item, "TargetMode", _CONSUMABLE_TARGETS)
        result.append({
            "contentId": "item.consumable." + source_id.replace("_", "-"),
            "sourceId": source_id,
            "displayName": _required_string(item, "DisplayName"),
            "description": _required_string(item, "Description"),
            "rarity": rarity,
            "price": _nonnegative_integer(item, "Price"),
            "maxCharges": _positive_integer(item, "MaxCharges"),
            "effectKind": effect,
            "magnitude": _finite_number(item, "Magnitude"),
            "maxRange": _nonnegative_integer(item, "MaxRange"),
            "targetMode": target,
        })
    return sorted(result, key=lambda item: item["contentId"])


def _compile_pools(document: Mapping[str, Any]) -> list[dict[str, Any]]:
    pools = document["Pools"]
    if not isinstance(pools, list) or len(pools) != 1:
        raise ValueError("Consumables JSON must contain exactly one pool")
    pool = pools[0]
    if set(pool) != {"Id", "Entries"} or pool["Id"] != "consumables":
        raise ValueError("Consumable pool identity differs from contract")
    entries = pool["Entries"]
    if not isinstance(entries, list) or len(entries) != 3:
        raise ValueError("Consumable pool must contain exactly three entries")
    compiled = []
    for item in entries:
        if set(item) != {"ConsumableId", "Weight"}:
            raise ValueError("Consumable pool entry has unknown or missing fields")
        source_id = _required_string(item, "ConsumableId")
        weight = _finite_number(item, "Weight")
        if weight <= 0:
            raise ValueError("Consumable pool weights must be positive")
        compiled.append({
            "consumableContentId": "item.consumable." + source_id.replace("_", "-"),
            "weight": weight,
        })
    return [{"sourceId": "consumables", "entries": compiled}]


def _compile_equipment(document: list[Mapping[str, Any]]) -> list[dict[str, Any]]:
    if not isinstance(document, list) or len(document) != 12:
        raise ValueError("Equipment JSON must contain exactly 12 definitions")
    expected_fields = {
        "Id", "DisplayName", "Slot", "Rarity", "Price", "StrengthBonus",
        "AgilityBonus", "ConstitutionBonus", "IntelligenceBonus", "CharismaBonus",
        "LuckBonus",
    }
    result: list[dict[str, Any]] = []
    for item in document:
        if set(item) != expected_fields:
            raise ValueError(f"Equipment {item.get('Id')} has unknown or missing fields")
        source_id = _required_string(item, "Id")
        result.append({
            "contentId": "item.equipment." + source_id.replace("_", "-"),
            "sourceId": source_id,
            "displayName": _required_string(item, "DisplayName"),
            "slot": _required_enum(item, "Slot", _EQUIPMENT_SLOTS),
            "rarity": _required_enum(item, "Rarity", _EQUIPMENT_RARITIES),
            "price": _nonnegative_integer(item, "Price"),
            "strengthBonus": _integer_value(item, "StrengthBonus"),
            "agilityBonus": _integer_value(item, "AgilityBonus"),
            "constitutionBonus": _integer_value(item, "ConstitutionBonus"),
            "intelligenceBonus": _integer_value(item, "IntelligenceBonus"),
            "charismaBonus": _integer_value(item, "CharismaBonus"),
            "luckBonus": _integer_value(item, "LuckBonus"),
        })
    return sorted(result, key=lambda item: item["contentId"])


def _icon_audit(properties: Mapping[str, Mapping[str, Any]]) -> dict[str, Any]:
    item = properties.get("_icon")
    if item is None:
        raise ValueError("Buff serialized icon field is missing")
    reference = item.get("reference")
    if reference is None:
        return {"sourcePath": "", "sourceGuid": "", "sourceLocalFileId": 0, "dependencyHash": "", "payloadCopied": False}
    source_path = str(reference.get("sourcePath", ""))
    source_guid = str(reference.get("sourceGuid", ""))
    dependency_hash = str(reference.get("dependencyHash", ""))
    if not source_path.startswith("Assets/Tactics/Arts/UI/Icons/Buffs/"):
        raise ValueError(f"Buff icon is outside the audit-only boundary: {source_path}")
    if not _HEX_32.fullmatch(source_guid) or not _HEX_32.fullmatch(dependency_hash):
        raise ValueError(f"Buff icon has invalid GUID or dependency hash: {source_path}")
    return {
        "sourcePath": source_path,
        "sourceGuid": source_guid,
        "sourceLocalFileId": int(reference.get("sourceLocalFileId", 0)),
        "dependencyHash": dependency_hash,
        "payloadCopied": False,
    }


def _retaliation_content_id(properties: Mapping[str, Mapping[str, Any]]) -> str:
    item = properties.get("_meleeRetaliationBuff")
    if item is None:
        raise ValueError("Buff serialized retaliation reference is missing")
    reference = item.get("reference")
    if reference is None:
        return ""
    if reference.get("sourcePath") != "Assets/Tactics/ScriptableObjects/Buffs/Slow.asset":
        raise ValueError("Only the frozen Slow retaliation reference is supported")
    if not _HEX_32.fullmatch(str(reference.get("dependencyHash", ""))):
        raise ValueError("Retaliation Buff dependency hash is invalid")
    return "buff.slow"


def _value(properties: Mapping[str, Mapping[str, Any]], path: str) -> str:
    item = properties.get(path)
    if item is None or item.get("value") is None:
        raise ValueError(f"serialized property is missing: {path}")
    return str(item["value"])


def _integer(properties: Mapping[str, Mapping[str, Any]], path: str) -> int:
    try:
        return int(_value(properties, path))
    except ValueError as error:
        raise ValueError(f"serialized property is not an integer: {path}") from error


def _boolean(properties: Mapping[str, Mapping[str, Any]], path: str) -> bool:
    value = _value(properties, path)
    if value not in {"true", "false"}:
        raise ValueError(f"serialized property is not a boolean: {path}")
    return value == "true"


def _number(properties: Mapping[str, Mapping[str, Any]], path: str) -> float:
    try:
        result = float(_value(properties, path))
    except ValueError as error:
        raise ValueError(f"serialized property is not numeric: {path}") from error
    if not math.isfinite(result):
        raise ValueError(f"serialized property is not finite: {path}")
    return result


def _enum(properties: Mapping[str, Mapping[str, Any]], path: str, allowed: set[str]) -> str:
    value = _value(properties, path)
    if value not in allowed:
        raise ValueError(f"serialized enum is unsupported: {path}={value}")
    return value


def _required_string(item: Mapping[str, Any], field: str) -> str:
    value = item.get(field)
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field} must be a non-empty string")
    return value


def _required_enum(item: Mapping[str, Any], field: str, allowed: set[str]) -> str:
    value = _required_string(item, field)
    if value not in allowed:
        raise ValueError(f"{field} has unsupported value {value}")
    return value


def _integer_value(item: Mapping[str, Any], field: str) -> int:
    value = item.get(field)
    if isinstance(value, bool) or not isinstance(value, int):
        raise ValueError(f"{field} must be an integer")
    return value


def _nonnegative_integer(item: Mapping[str, Any], field: str) -> int:
    value = _integer_value(item, field)
    if value < 0:
        raise ValueError(f"{field} must be nonnegative")
    return value


def _positive_integer(item: Mapping[str, Any], field: str) -> int:
    value = _integer_value(item, field)
    if value <= 0:
        raise ValueError(f"{field} must be positive")
    return value


def _finite_number(item: Mapping[str, Any], field: str) -> float:
    value = item.get(field)
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError(f"{field} must be finite")
    return float(value)


def _validate_sha256(actual: str, expected: str) -> None:
    if not re.fullmatch(r"[0-9a-f]{64}", actual) or actual != expected:
        raise ValueError("frozen JSON SHA-256 differs from Golden")


def _require_equal(label: str, actual: Any, expected: Any) -> None:
    if actual != expected:
        raise ValueError(f"{label} differs from frozen contract")


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _load_array(path: Path) -> list[Mapping[str, Any]]:
    document = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(document, list):
        raise ValueError(f"expected JSON array: {path}")
    return document


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export", type=Path, required=True)
    parser.add_argument("--specification", type=Path, required=True)
    parser.add_argument("--golden", type=Path, required=True)
    parser.add_argument("--consumables", type=Path, required=True)
    parser.add_argument("--equipment", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()
    draft = compile_buff_item_draft(
        load_json(arguments.export),
        load_json(arguments.specification),
        load_json(arguments.golden),
        load_json(arguments.consumables),
        _load_array(arguments.equipment),
        _sha256(arguments.consumables),
        _sha256(arguments.equipment),
    )
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(canonical_json(draft), encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
