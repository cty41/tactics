"""Compile the frozen Pure Run starting-skill export into a disposable typed draft."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Mapping

from Tools.migration.export_document import export_semantic_hash, load_json, validate_export_document
from Tools.migration.manifest import normalize_content_id

CONFIG_TYPE = "Tactics.Common.Units.Abilities.SkillGraphAbilityConfig"
GRAPH_TYPE = "Tactics.Common.Skills.Graph.SkillGraphAsset"

CONTRACT: dict[str, dict[str, Any]] = {
    "skill.basic.magic": dict(sourceId="basic.magic", role="Any", kind="Basic", manaCost=0, minRange=1, maxRange=3, executionKind="MagicAttack", damage=0, damageKind="Magical", statusContentId="", statusDuration=0, hidden=False),
    "skill.basic.melee": dict(sourceId="basic.melee", role="Any", kind="Basic", manaCost=0, minRange=1, maxRange=1, executionKind="MeleeAttack", damage=2, damageKind="Physical", statusContentId="", statusDuration=0, hidden=False),
    "skill.mage.fireball.lv1": dict(sourceId="mage.fireball", role="Mage", kind="Active", manaCost=7, minRange=1, maxRange=4, executionKind="Fireball", damage=2, damageKind="Magical", statusContentId="buff.ignite", statusDuration=2, hidden=False),
    "skill.mage.ice-bolt.lv1": dict(sourceId="mage.ice_bolt", role="Mage", kind="Active", manaCost=6, minRange=1, maxRange=4, executionKind="IceBolt", damage=8, damageKind="Magical", statusContentId="buff.slow", statusDuration=1, hidden=False),
    "skill.mage.lightning.lv1": dict(sourceId="mage.lightning", role="Mage", kind="Active", manaCost=6, minRange=1, maxRange=4, executionKind="Lightning", damage=9, damageKind="Magical", statusContentId="", statusDuration=0, hidden=False),
    "skill.necromancer.summon-skeleton.lv1": dict(sourceId="necromancer.summon_skeleton", role="Necromancer", kind="Active", manaCost=3, minRange=0, maxRange=999, executionKind="SummonSkeleton", damage=0, damageKind="None", statusContentId="", statusDuration=0, hidden=False),
    "skill.necromancer.amplify-damage.lv1": dict(sourceId="necromancer.amplify_damage", role="Necromancer", kind="Active", manaCost=3, minRange=1, maxRange=4, executionKind="AmplifyDamage", damage=0, damageKind="None", statusContentId="buff.curse-damage-amplifier", statusDuration=5, hidden=False),
    "skill.necromancer.bone-spear.lv1": dict(sourceId="necromancer.bone_spear", role="Necromancer", kind="Active", manaCost=6, minRange=1, maxRange=4, executionKind="BoneSpear", damage=7, damageKind="Magical", statusContentId="", statusDuration=0, hidden=False),
    "skill.amazon.thrust.lv1": dict(sourceId="amazon.thrust", role="Amazon", kind="Active", manaCost=3, minRange=1, maxRange=2, executionKind="Thrust", damage=2, damageKind="Physical", statusContentId="", statusDuration=0, hidden=False),
    "skill.poison-spear.lv1": dict(sourceId="amazon.poison_spear", role="Amazon", kind="Active", manaCost=6, minRange=1, maxRange=5, executionKind="PoisonSpear", damage=8, damageKind="Physical", statusContentId="buff.poison", statusDuration=2, hidden=False),
    "skill.amazon.combat-techniques.lv1": dict(sourceId="amazon.combat_techniques", role="Amazon", kind="Passive", manaCost=0, minRange=0, maxRange=0, executionKind="CombatTechniques", damage=0, damageKind="None", statusContentId="", statusDuration=0, hidden=False),
    "skill.amazon.pickup-spear.lv1": dict(sourceId="amazon.pickup_spear", role="Amazon", kind="Utility", manaCost=0, minRange=0, maxRange=1, executionKind="PickupSpear", damage=0, damageKind="None", statusContentId="", statusDuration=0, hidden=True),
}
for _content_id, _contract in CONTRACT.items():
    _contract["isBasicAbility"] = _content_id in {"skill.basic.magic", "skill.basic.melee"}
    _contract["maxUsesPerTurn"] = 0
    if _content_id == "skill.poison-spear.lv1":
        _contract["branchId"] = "amazon.poison-spear"
    else:
        _contract["branchId"] = _content_id.removeprefix("skill.").removesuffix(".lv1")


def _properties(asset: Mapping[str, Any]) -> dict[str, Mapping[str, Any]]:
    objects = [item for item in asset["objects"] if item["objectType"] == CONFIG_TYPE]
    if len(objects) != 1:
        raise ValueError(f"{asset['sourceKey']} must contain one {CONFIG_TYPE}")
    return {item["propertyPath"]: item for item in objects[0]["properties"]}


def _integer(properties: Mapping[str, Mapping[str, Any]], path: str) -> int:
    try:
        return int(properties[path]["value"])
    except (KeyError, TypeError, ValueError) as error:
        raise ValueError(f"missing integer field {path}") from error


def _boolean(properties: Mapping[str, Mapping[str, Any]], path: str) -> bool:
    try:
        value = properties[path]["value"]
    except KeyError as error:
        raise ValueError(f"missing boolean field {path}") from error
    if value not in (True, False, "true", "false"):
        raise ValueError(f"invalid boolean field {path}")
    return value is True or value == "true"


def _reference(properties: Mapping[str, Mapping[str, Any]], path: str) -> Mapping[str, Any]:
    try:
        reference = properties[path]["reference"]
    except KeyError as error:
        raise ValueError(f"missing reference field {path}") from error
    if not reference:
        raise ValueError(f"empty reference field {path}")
    return reference


def compile_starting_skill_draft(export: Mapping[str, Any], specification: Mapping[str, Any]) -> dict[str, Any]:
    warnings = validate_export_document(export, specification)
    if warnings:
        raise ValueError("starting-skill export contains unsupported values: " + "; ".join(warnings))
    assets = {str(item["sourceKey"]): item for item in export["assets"]}
    configs = {key: value for key, value in assets.items() if key.startswith("skill.")}
    graphs = {"skill." + key[6:]: value for key, value in assets.items() if key.startswith("graph.")}
    if len(configs) != 11 or len(graphs) != 10:
        raise ValueError("starting-skill export must contain 11 configs and 10 graph roots")

    definitions: list[dict[str, Any]] = []
    for content_id, contract in sorted(CONTRACT.items()):
        normalize_content_id(content_id)
        if content_id == "skill.amazon.combat-techniques.lv1":
            definitions.append({
                "contentId": content_id,
                **contract,
                "level": 1,
                "displayName": "战斗技巧",
                "description": "通过战斗技巧闪避攻击并强化伤害。",
                "externalDependency": False,
                "sourceAudit": {"sourcePath": "Assets/Tactics/Scripts/Common/Battle/PureRunAbilityCatalog.cs", "payloadCopied": False},
            })
            continue
        asset = configs.get(content_id)
        if asset is None:
            raise ValueError(f"missing config {content_id}")
        properties = _properties(asset)
        if _boolean(properties, "_isBasicAbility") != contract["isBasicAbility"]:
            raise ValueError(f"{content_id} basic-ability flag differs from contract")
        if _integer(properties, "_maxUsesPerTurn") != contract["maxUsesPerTurn"]:
            raise ValueError(f"{content_id} max uses per turn differs from contract")
        if _integer(properties, "_manaCost") != contract["manaCost"]:
            raise ValueError(f"{content_id} mana cost differs from contract")
        if _integer(properties, "_targetRange") != contract["maxRange"] and content_id != "skill.amazon.pickup-spear.lv1":
            raise ValueError(f"{content_id} target range differs from contract")
        graph_reference = _reference(properties, "_skillGraph")
        graph = graphs.get(content_id)
        if content_id != "skill.poison-spear.lv1":
            if graph is None or graph["mainAssetType"] != GRAPH_TYPE:
                raise ValueError(f"missing graph root {content_id}")
            if graph_reference["sourcePath"] != graph["sourcePath"] or graph_reference["dependencyHash"] != graph["dependencyHash"]:
                raise ValueError(f"{content_id} graph reference differs from exported graph root")
        definitions.append({
            "contentId": content_id,
            **contract,
            "level": 1,
            "displayName": str(properties["_displayName"]["value"]),
            "description": str(properties["_description"]["value"]),
            "sourcePath": asset["sourcePath"],
            "sourceGuid": asset["sourceGuid"],
            "sourceLocalFileId": int(asset["sourceLocalFileId"]),
            "graphPath": graph_reference["sourcePath"],
            "graphDependencyHash": graph_reference["dependencyHash"],
            "externalDependency": content_id == "skill.poison-spear.lv1",
            "sourceAudit": {"presentationPayloadCopied": False, "thirdPartyPayloadCopied": False},
        })
    if len(definitions) != 12 or len({item["contentId"] for item in definitions}) != 12:
        raise ValueError("starting-skill draft must contain 12 unique ContentIds")
    return {
        "schemaVersion": 1,
        "batchId": "pure-run-starting-skills-v1",
        "classification": "disposable_typed_starting_skill_migration_draft",
        "source": {"sourceTag": export["sourceTag"], "sourceCommit": export["sourceCommit"], "unityVersion": export["unityVersion"], "exporterVersion": export["exporterVersion"], "exportHash": export_semantic_hash(export)},
        "definitions": definitions,
        "externalContentDependencies": ["skill.poison-spear.lv1"],
        "payloadBoundary": {"presentation": "audit_only_not_copied", "thirdPartyPayloadCopied": False, "visualAcceptance": "not_applicable_gameplay_only_no_visual_payload", "manualGameplayAcceptance": "pending"},
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export", type=Path, required=True)
    parser.add_argument("--specification", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    draft = compile_starting_skill_draft(load_json(args.export), load_json(args.specification))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(draft, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
