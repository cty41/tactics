"""Compile the frozen Pure Run Unit AssetDatabase export into a typed migration draft."""

from __future__ import annotations

import argparse
import json
import math
import re
from collections.abc import Mapping
from pathlib import Path
from typing import Any

from Tools.migration.export_document import export_semantic_hash, load_json, validate_export_document

_MATERIAL_COLOR_NAME = re.compile(
    r"^m_SavedProperties\.m_Colors\.Array\.data\[(?P<index>\d+)\]\.first$"
)
_MATERIAL_FLOAT_NAME = re.compile(
    r"^m_SavedProperties\.m_Floats\.Array\.data\[(?P<index>\d+)\]\.first$"
)
_FORBIDDEN_PAYLOAD_MARKERS = ("pilotoadapted", "helisprite", "floatingunitshader")
_UNIT_COMPONENT_TYPE = "Tactics.Units.TilemapUnit"
_VISUAL_COMPONENT_TYPE = "Tactics.Common.Units.FourDirectionSpriteVisual"


def compile_unit_draft(
    export: Mapping[str, Any],
    specification: Mapping[str, Any],
    golden: Mapping[str, Any],
) -> dict[str, Any]:
    """Validate the real Unity export and compile the complete 12-Unit draft."""

    warnings = validate_export_document(export, specification)
    if warnings:
        raise ValueError("Unit export contains unsupported values: " + "; ".join(warnings))
    if golden.get("schemaVersion") != 1 or golden.get("batchId") != export.get("batchId"):
        raise ValueError("Unit Golden identity does not match the export batch")

    source = golden.get("source", {})
    if source.get("unityTag") != export.get("sourceTag") or source.get(
        "unityCommit"
    ) != export.get("sourceCommit"):
        raise ValueError("Unit Golden is not bound to the exported Unity snapshot")

    assets = {str(item["sourceKey"]): item for item in export["assets"]}
    tint_contract = golden.get("tintContract", {})
    if tint_contract.get("id") != "unity-goat-body-tint-v1":
        raise ValueError("Unit Golden has no canonical Goat body tint contract")
    sprite_contract = golden.get("spriteContract", {})
    if sprite_contract.get("id") != "unity-unit-sprite-geometry-v1":
        raise ValueError("Unit Golden has no canonical Sprite geometry contract")
    selected_paths = {str(item["sourcePath"]) for item in specification["assets"]}
    _validate_selected_payload_boundary(selected_paths)

    prefab_assets = [item for key, item in assets.items() if key.endswith("/prefab")]
    texture_assets = [item for key, item in assets.items() if "/texture-" in key]
    material_assets = [item for key, item in assets.items() if key.endswith("/material-audit")]
    if (len(prefab_assets), len(texture_assets), len(material_assets)) != (12, 19, 6):
        raise ValueError("Unit export must contain 12 prefabs, 19 textures, and 6 material audits")

    golden_units = golden.get("units")
    if not isinstance(golden_units, list) or len(golden_units) != 12:
        raise ValueError("Unit Golden must contain exactly 12 definitions")
    content_ids = [str(item.get("contentId", "")) for item in golden_units]
    if len(content_ids) != len(set(content_ids)):
        raise ValueError("Unit Golden contains duplicate ContentIds")

    compiled_units = []
    for unit in golden_units:
        content_id = str(unit["contentId"])
        prefab = _required_asset(assets, f"{content_id}/prefab")
        if prefab["sourcePath"] != unit["sourcePrefabPath"]:
            raise ValueError(f"{content_id} prefab path differs from the Unit Golden")
        if prefab["gitBlobSha1"] != unit["sourcePrefabGitBlobSha1"]:
            raise ValueError(f"{content_id} prefab blob differs from the Unit Golden")

        unit_component = _single_object(prefab, _UNIT_COMPONENT_TYPE)
        visual_component = _single_object(prefab, _VISUAL_COMPONENT_TYPE)
        _validate_visual_contract(unit, prefab, visual_component, sprite_contract)
        if unit["category"] != "player":
            _validate_prefab_definition_values(unit, unit_component)
        expected_tint_mode = "goat-body-mask-v1" if unit["category"] == "goat" else "multiply"
        if unit["visual"].get("tintMode") != expected_tint_mode:
            raise ValueError(f"{content_id} body tint mode differs from its Unit family")
        if unit["category"] == "goat":
            material = _required_asset(assets, f"{content_id}/material-audit")
            _validate_body_tint(content_id, unit["visual"], material, tint_contract)

        compiled_units.append(json.loads(json.dumps(unit)))

    compiled_textures = _validate_texture_assets(golden, assets, sprite_contract)
    dependency_audit = _compile_dependency_audit(prefab_assets, selected_paths)
    return {
        "schemaVersion": 1,
        "batchId": golden["batchId"],
        "sourceExportBatchId": export["batchId"],
        "classification": "disposable_typed_unit_migration_draft",
        "source": {
            "sourceTag": export["sourceTag"],
            "sourceCommit": export["sourceCommit"],
            "unityVersion": export["unityVersion"],
            "exporterVersion": export["exporterVersion"],
            "exportHash": export_semantic_hash(export),
            "derivedContract": source["derivedContract"],
            "assets": [
                {
                    "sourceKey": asset["sourceKey"],
                    "sourcePath": asset["sourcePath"],
                    "gitBlobSha1": asset["gitBlobSha1"],
                    "sourceGuid": asset["sourceGuid"],
                    "sourceLocalFileId": asset["sourceLocalFileId"],
                    "dependencyHash": asset["dependencyHash"],
                }
                for asset in export["assets"]
            ],
        },
        "actorContentId": source["actorContentId"],
        "tintContract": tint_contract,
        "spriteContract": sprite_contract,
        "formulaCases": golden["formulaCases"],
        "units": sorted(compiled_units, key=lambda item: item["contentId"]),
        "textureAssets": sorted(compiled_textures, key=lambda item: item["targetPath"]),
        "dependencyAudit": dependency_audit,
    }


def canonical_json(document: Mapping[str, Any]) -> str:
    return json.dumps(document, ensure_ascii=False, sort_keys=True, indent=2) + "\n"


def _validate_selected_payload_boundary(selected_paths: set[str]) -> None:
    for source_path in selected_paths:
        lowered = source_path.lower()
        if source_path.startswith("Assets/ThirdParty/") or any(
            marker in lowered for marker in _FORBIDDEN_PAYLOAD_MARKERS
        ):
            raise ValueError(f"forbidden Unit payload selected for migration: {source_path}")


def _required_asset(assets: Mapping[str, Any], source_key: str) -> Mapping[str, Any]:
    try:
        return assets[source_key]
    except KeyError as error:
        raise ValueError(f"Unit export is missing required asset: {source_key}") from error


def _single_object(asset: Mapping[str, Any], object_type: str) -> Mapping[str, Any]:
    matches = [item for item in asset["objects"] if item["objectType"] == object_type]
    if len(matches) != 1:
        raise ValueError(f"{asset['sourceKey']} must contain exactly one {object_type}")
    return matches[0]


def _properties(exported_object: Mapping[str, Any]) -> dict[str, Mapping[str, Any]]:
    return {str(item["propertyPath"]): item for item in exported_object["properties"]}


def _property_value(properties: Mapping[str, Mapping[str, Any]], path: str) -> str:
    item = properties.get(path)
    if item is None or item.get("value") is None:
        raise ValueError(f"serialized property is missing: {path}")
    return str(item["value"])


def _reference_path(properties: Mapping[str, Mapping[str, Any]], path: str) -> str:
    item = properties.get(path)
    if item is None:
        raise ValueError(f"serialized reference is missing: {path}")
    reference = item.get("reference")
    return "" if reference is None else str(reference.get("sourcePath", ""))


def _float_vector(value: str, expected_length: int, label: str) -> list[float]:
    try:
        vector = [float(item) for item in value.split(",")]
    except ValueError as error:
        raise ValueError(f"{label} is not a numeric vector") from error
    if len(vector) != expected_length or any(not math.isfinite(item) for item in vector):
        raise ValueError(f"{label} has an invalid vector length or value")
    return vector


def _assert_vector_close(actual: list[float], expected: list[float], label: str) -> None:
    if len(actual) != len(expected) or any(
        not math.isclose(left, right, rel_tol=0.0, abs_tol=1e-6)
        for left, right in zip(actual, expected, strict=True)
    ):
        raise ValueError(f"{label} drifted")


def _unity_texture_path(godot_path: str | None) -> str:
    if not godot_path:
        return ""
    prefix = "res://assets/units/"
    if not godot_path.startswith(prefix):
        raise ValueError(f"Unit texture is outside the canonical Godot folder: {godot_path}")
    return "Assets/Tactics/Arts/PureRun/Textures/" + godot_path.removeprefix(prefix)


def _validate_visual_contract(
    unit: Mapping[str, Any],
    prefab: Mapping[str, Any],
    visual_component: Mapping[str, Any],
    sprite_contract: Mapping[str, Any],
) -> None:
    content_id = str(unit["contentId"])
    visual = unit["visual"]
    properties = _properties(visual_component)
    expected = {
        "_downRightSprite": _unity_texture_path(visual["downRightTexture"]),
        "_upLeftSprite": _unity_texture_path(visual["upLeftTexture"]),
        "_deathSprite": _unity_texture_path(visual.get("deathTexture")),
    }
    for property_path, source_path in expected.items():
        if _reference_path(properties, property_path) != source_path:
            raise ValueError(f"{content_id} visual reference drifted: {property_path}")

    has_death_texture = bool(expected["_deathSprite"])
    if bool(unit["canProduceCorpse"]) != has_death_texture:
        raise ValueError(f"{content_id} corpse contract disagrees with its death texture")
    shadow_path = _unity_texture_path(visual["shadowTexture"])
    referenced_paths = {
        str(reference["sourcePath"])
        for exported_object in prefab["objects"]
        for prop in exported_object["properties"]
        if isinstance((reference := prop.get("reference")), Mapping)
        and reference.get("sourcePath")
    }
    if shadow_path not in referenced_paths:
        raise ValueError(f"{content_id} prefab does not reference the frozen Unit shadow")
    shadow_objects = [
        item
        for item in prefab["objects"]
        if re.search(r"/Shadow\[\d+\]/", str(item["objectPath"]))
    ]
    shadow_transform = next(
        (item for item in shadow_objects if item["objectType"] == "UnityEngine.Transform"),
        None,
    )
    shadow_renderer = next(
        (item for item in shadow_objects if item["objectType"] == "UnityEngine.SpriteRenderer"),
        None,
    )
    if shadow_transform is None or shadow_renderer is None:
        raise ValueError(f"{content_id} prefab has no auditable Shadow Transform/Renderer")
    transform_properties = _properties(shadow_transform)
    renderer_properties = _properties(shadow_renderer)
    shadow_contract = sprite_contract["shadow"]
    _assert_vector_close(
        _float_vector(_property_value(transform_properties, "m_LocalPosition"), 3, "Shadow position"),
        [float(item) for item in shadow_contract["localPosition"]],
        f"{content_id} Shadow local position",
    )
    _assert_vector_close(
        _float_vector(_property_value(transform_properties, "m_LocalScale"), 3, "Shadow scale"),
        [float(item) for item in shadow_contract["localScale"]],
        f"{content_id} Shadow local scale",
    )
    _assert_vector_close(
        _float_vector(_property_value(renderer_properties, "m_Color"), 4, "Shadow color"),
        [float(item) for item in shadow_contract["color"]],
        f"{content_id} Shadow color",
    )
    _assert_vector_close(
        [float(item) for item in visual["shadowOffset"]] + [0.0],
        [float(item) for item in shadow_contract["localPosition"]],
        f"{content_id} Golden Shadow local position",
    )


def _validate_prefab_definition_values(
    unit: Mapping[str, Any], unit_component: Mapping[str, Any]
) -> None:
    content_id = str(unit["contentId"])
    properties = _properties(unit_component)
    expected_integers = {
        "_strength": unit["attributes"]["strength"],
        "_agility": unit["attributes"]["agility"],
        "_constitution": unit["attributes"]["constitution"],
        "_intelligence": unit["attributes"]["intelligence"],
        "_charisma": unit["attributes"]["charisma"],
        "_luck": unit["attributes"]["luck"],
        "_attackRange": unit["combat"]["attackRange"],
        "_attackFactor": unit["combat"]["attackFactor"],
        "_defenceFactor": unit["combat"]["defenceFactor"],
    }
    for property_path, expected in expected_integers.items():
        if int(_property_value(properties, property_path)) != int(expected):
            raise ValueError(f"{content_id} prefab value drifted: {property_path}")
    speed = float(_property_value(properties, "_speed"))
    if not math.isclose(speed, float(unit["speed"]), rel_tol=0.0, abs_tol=1e-6):
        raise ValueError(f"{content_id} prefab value drifted: _speed")


def _validate_body_tint(
    content_id: str,
    visual: Mapping[str, Any],
    material: Mapping[str, Any],
    tint_contract: Mapping[str, Any],
) -> None:
    main = next((item for item in material["objects"] if item["objectPath"] == "main"), None)
    if main is None:
        raise ValueError(f"{content_id} material audit has no main object")
    properties = _properties(main)
    shader_reference = properties.get("m_Shader", {}).get("reference")
    if not isinstance(shader_reference, Mapping) or shader_reference.get(
        "sourcePath"
    ) != tint_contract.get("unityShaderPath"):
        raise ValueError(f"{content_id} material shader path differs from the Unit Golden")
    if shader_reference.get("name") != tint_contract.get("materialShaderName"):
        raise ValueError(f"{content_id} material shader name differs from the Unit Golden")

    _validate_material_vector(
        content_id,
        properties,
        "_BodyTint",
        visual["bodyTint"],
        "body tint",
    )
    _validate_material_vector(
        content_id,
        properties,
        "_BaseBodyColor",
        visual["baseBodyColor"],
        "base body color",
    )
    _validate_material_vector(
        content_id,
        properties,
        "_Color",
        tint_contract["materialColor"],
        "material color",
    )
    threshold = _material_saved_value(properties, _MATERIAL_FLOAT_NAME, "_BodyThreshold")
    if not math.isclose(
        float(threshold),
        float(tint_contract["materialThresholdAudit"]),
        rel_tol=0.0,
        abs_tol=1e-6,
    ):
        raise ValueError(f"{content_id} body threshold differs from the Unit Golden")


def _validate_material_vector(
    content_id: str,
    properties: Mapping[str, Mapping[str, Any]],
    property_name: str,
    expected: list[Any],
    diagnostic_name: str,
) -> None:
    serialized = _material_saved_value(properties, _MATERIAL_COLOR_NAME, property_name)
    actual = [float(value) for value in serialized.split(",")]
    if len(actual) != 4 or any(
        not math.isclose(value, float(expected[index]), rel_tol=0.0, abs_tol=1e-6)
        for index, value in enumerate(actual)
    ):
        raise ValueError(f"{content_id} {diagnostic_name} differs from the Unit Golden")


def _material_saved_value(
    properties: Mapping[str, Mapping[str, Any]],
    name_pattern: re.Pattern[str],
    property_name: str,
) -> str:
    value_path = ""
    for path, item in properties.items():
        match = name_pattern.fullmatch(path)
        if match and item.get("value") == property_name:
            prefix = path.removesuffix(".first")
            value_path = prefix + ".second"
            break
    if not value_path:
        raise ValueError(f"material audit has no {property_name}")
    return _property_value(properties, value_path)


def _validate_texture_assets(
    golden: Mapping[str, Any],
    assets: Mapping[str, Any],
    sprite_contract: Mapping[str, Any],
) -> list[dict[str, Any]]:
    by_source_path = {str(item["sourcePath"]): item for item in assets.values()}
    result = []
    for texture in golden["textureAssets"]:
        source_path = str(texture["sourcePath"])
        asset = by_source_path.get(source_path)
        if asset is None or "/texture-" not in str(asset["sourceKey"]):
            raise ValueError(f"Unit export is missing frozen texture root: {source_path}")
        if asset["gitBlobSha1"] != texture["gitBlobSha1"]:
            raise ValueError(f"Unit texture blob differs from the Golden: {source_path}")

        main = next(item for item in asset["objects"] if item["objectPath"] == "main")
        importer = next(
            (item for item in asset["objects"] if item["objectPath"] == "importer"), None
        )
        if importer is None or importer["objectType"] != "UnityEditor.TextureImporter":
            raise ValueError(f"Unit texture has no AssetDatabase importer contract: {source_path}")
        main_properties = _properties(main)
        importer_properties = _properties(importer)
        if int(_property_value(main_properties, "m_Width")) != int(texture["width"]) or int(
            _property_value(main_properties, "m_Height")
        ) != int(texture["height"]):
            raise ValueError(f"Unit texture dimensions differ from the Golden: {source_path}")

        kind = str(texture["kind"])
        import_contract = sprite_contract[
            "shadow" if kind == "shadow" else "death" if kind == "death" else "living"
        ]
        expected_importer = {
            "m_TextureType": "8",
            "m_TextureShape": "1",
            "m_sRGBTexture": "1",
            "m_AlphaIsTransparency": "1",
            "m_SpriteMode": "1",
            "m_EnableMipMap": "0",
            "m_IsReadable": "0",
            "m_TextureSettings.m_FilterMode": "1",
            "m_Alignment": str(import_contract["alignment"]),
            "m_SpritePixelsToUnits": str(import_contract["pixelsPerUnit"]),
        }
        for property_path, expected in expected_importer.items():
            if _property_value(importer_properties, property_path) != expected:
                raise ValueError(f"Unit texture importer drifted: {source_path} {property_path}")
        _assert_vector_close(
            _float_vector(_property_value(importer_properties, "m_SpritePivot"), 2, "Sprite pivot"),
            [float(item) for item in import_contract["pivot"]],
            f"Unit texture importer drifted: {source_path} m_SpritePivot",
        )
        compiled = dict(texture)
        compiled["importContract"] = {
            "textureType": "Sprite",
            "shape": "2D",
            "sRgb": True,
            "alphaIsTransparency": True,
            "spriteMode": "Single",
            "mipmaps": False,
            "readable": False,
            "filter": "Bilinear",
            "alignment": int(import_contract["alignment"]),
            "pivot": [float(item) for item in import_contract["pivot"]],
            "pixelsPerUnit": int(import_contract["pixelsPerUnit"]),
        }
        result.append(compiled)
    return result


def _compile_dependency_audit(
    prefab_assets: list[Mapping[str, Any]], selected_paths: set[str]
) -> dict[str, Any]:
    dependencies = sorted(
        {
            str(dependency["sourcePath"])
            for asset in prefab_assets
            for dependency in asset["dependencies"]
            if str(dependency["sourcePath"]).startswith("Assets/")
        }
    )
    deferred = [path for path in dependencies if path not in selected_paths]
    third_party = [path for path in deferred if path.startswith("Assets/ThirdParty/")]
    forbidden = [
        path
        for path in deferred
        if any(marker in path.lower() for marker in _FORBIDDEN_PAYLOAD_MARKERS)
    ]
    return {
        "policy": "audit_only_not_selected_or_copied",
        "deferredDependencies": deferred,
        "thirdPartyDependencies": third_party,
        "forbiddenPayloadDependencies": forbidden,
        "selectedPayloadCount": len(selected_paths),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export", type=Path, required=True)
    parser.add_argument("--specification", type=Path, required=True)
    parser.add_argument("--golden", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()

    draft = compile_unit_draft(
        load_json(arguments.export),
        load_json(arguments.specification),
        load_json(arguments.golden),
    )
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(canonical_json(draft), encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
