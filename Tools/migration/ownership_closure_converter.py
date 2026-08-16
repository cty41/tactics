"""Compile the final Unity-owned Lv3, Treasure, map and tooling contracts."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Mapping

from Tools.migration.export_document import export_semantic_hash, load_json, validate_export_document
from Tools.migration.manifest import normalize_content_id, validate_unique_content_ids

BATCH_ID = "pure-run-ownership-closure-v1"
CONFIG_TYPE = "Tactics.Common.Units.Abilities.SkillGraphAbilityConfig"

# These values are the normalized gameplay meaning of the frozen Unity graphs.  The
# converter still binds every row to the exported AbilityConfig and graph dependency
# hash; it never treats display text as executable gameplay data.
LV3_CONTRACTS: dict[str, dict[str, Any]] = {
    "skill.mage.fireball.lv3": dict(branchId="mage.fireball", role="Mage", executionKind="Fireball", damage=4, damageKind="Magical", statusContentId="buff.ignite", statusDuration=3, detonateStatusContentId="buff.ignite", areaRadius=1),
    "skill.mage.ice-bolt.lv3": dict(branchId="mage.ice-bolt", role="Mage", executionKind="IceBolt", damage=8, damageKind="Magical", statusContentId="buff.slow", statusDuration=2, bounceRange=3, bounceCount=1),
    "skill.mage.lightning.lv3": dict(branchId="mage.lightning", role="Mage", executionKind="Lightning", damage=11, damageKind="Magical", statusContentId="buff.stun", statusDuration=1, statusChancePercent=50, ignoreLineOfSight=True),
    "skill.necromancer.summon-skeleton.lv3": dict(branchId="necromancer.summon-skeleton", role="Necromancer", executionKind="SummonSkeleton", damage=0, damageKind="None", requiresCorpse=True, summonLimit=3, summonCategory="Skeleton", summonAttackContentId="skill.summon.skeleton-attack.lv3"),
    "skill.necromancer.amplify-damage.lv3": dict(branchId="necromancer.amplify-damage", role="Necromancer", executionKind="AmplifyDamage", damage=0, damageKind="None", statusContentId="buff.curse-damage-amplifier", statusDuration=5, areaRadius=1),
    "skill.necromancer.bone-spear.lv3": dict(branchId="necromancer.bone-spear", role="Necromancer", executionKind="BoneSpear", damage=7, damageKind="Magical", pierceAll=True, allowsEmptyTarget=True),
    "skill.amazon.thrust.lv3": dict(branchId="amazon.thrust", role="Amazon", executionKind="Thrust", damage=6, damageKind="Physical", movementDamagePerCell=1),
    "skill.amazon.poison-spear.lv3": dict(branchId="amazon.poison-spear", role="Amazon", executionKind="PoisonSpear", damage=10, damageKind="Physical", statusContentId="buff.poison", statusDuration=3, areaRadius=1, areaShape="square"),
}


def _properties(asset: Mapping[str, Any]) -> dict[str, Mapping[str, Any]]:
    objects = [item for item in asset["objects"] if item["objectType"] == CONFIG_TYPE]
    if len(objects) != 1:
        raise ValueError(f"{asset['sourceKey']} must contain exactly one {CONFIG_TYPE}")
    return {item["propertyPath"]: item for item in objects[0]["properties"]}


def _value(properties: Mapping[str, Mapping[str, Any]], key: str, default: Any = None) -> Any:
    return properties.get(key, {}).get("value", default)


def compile_ownership_closure_draft(
    export: Mapping[str, Any], specification: Mapping[str, Any]
) -> dict[str, Any]:
    warnings = validate_export_document(export, specification)
    if warnings:
        raise ValueError("ownership closure export contains unsupported values: " + "; ".join(warnings))
    if export["batchId"] != BATCH_ID:
        raise ValueError("ownership closure batch identity drift")

    assets = {item["sourceKey"]: item for item in export["assets"]}
    if set(LV3_CONTRACTS) - set(assets):
        raise ValueError("ownership closure export is missing an active Lv3 root")

    definitions: list[dict[str, Any]] = []
    for content_id, contract in sorted(LV3_CONTRACTS.items()):
        normalize_content_id(content_id)
        asset = assets[content_id]
        properties = _properties(asset)
        graph = properties.get("_skillGraph", {}).get("reference")
        if not graph or not str(graph.get("sourcePath", "")).endswith("_Graph.asset"):
            raise ValueError(f"{content_id} has no frozen SkillGraph dependency")
        definition = {
            "contentId": content_id,
            "level": 3,
            "kind": "Active",
            "manaCost": int(_value(properties, "_manaCost", -1)),
            "minRange": 0 if contract["executionKind"] == "SummonSkeleton" else 1,
            "maxRange": int(_value(properties, "_targetRange", -1)),
            "isBasicAbility": str(_value(properties, "_isBasicAbility", "false")).lower() == "true",
            "maxUsesPerTurn": int(_value(properties, "_maxUsesPerTurn", 0)),
            "displayName": str(_value(properties, "_displayName", "")),
            "description": str(_value(properties, "_description", "")),
            "requiredAttribute": "Intelligence" if contract["role"] == "Mage" else "Charisma" if contract["role"] == "Necromancer" else "Agility",
            "minimumAttribute": 5,
            "prerequisiteContentId": content_id[:-1] + "2",
            "growthVisible": True,
            "canCrit": True,
            "sourcePath": asset["sourcePath"],
            "sourceGuid": asset["sourceGuid"],
            "sourceLocalFileId": int(asset["sourceLocalFileId"]),
            "dependencyHash": asset["dependencyHash"],
            "graphPath": graph["sourcePath"],
            "graphDependencyHash": graph["dependencyHash"],
            **contract,
        }
        if definition["manaCost"] < 0 or definition["maxRange"] < definition["minRange"]:
            raise ValueError(f"{content_id} contains invalid numeric values")
        definitions.append(definition)

    passive_source = assets["skill.amazon.combat-techniques.lv3"]
    definitions.append({
        "contentId": "skill.amazon.combat-techniques.lv3", "branchId": "amazon.combat-techniques",
        "role": "Amazon", "kind": "Passive", "level": 3, "manaCost": 0, "minRange": 0,
        "maxRange": 0, "executionKind": "CombatTechniques", "damage": 0, "damageKind": "None",
        "requiredAttribute": "Luck", "minimumAttribute": 5,
        "prerequisiteContentId": "skill.amazon.combat-techniques.lv2", "growthVisible": True,
        "displayName": "战斗技巧 Lv3", "description": "通过战斗技巧闪避攻击并强化伤害。",
        "isBasicAbility": False, "maxUsesPerTurn": 0, "canCrit": True,
        "sourcePath": passive_source["sourcePath"], "sourceGuid": passive_source["sourceGuid"],
        "sourceLocalFileId": int(passive_source["sourceLocalFileId"]),
        "dependencyHash": passive_source["dependencyHash"], "graphPath": "", "graphDependencyHash": "",
    })

    internal_asset = assets["skill.summon.skeleton-attack.lv3"]
    internal_props = _properties(internal_asset)
    internal_graph = internal_props["_skillGraph"]["reference"]
    internal = {
        "contentId": "skill.summon.skeleton-attack.lv3", "branchId": "summon.skeleton-attack",
        "role": "Any", "kind": "Basic", "level": 3, "manaCost": 0, "minRange": 1,
        "maxRange": 1, "executionKind": "MeleeAttack", "damage": 4, "damageKind": "Physical",
        "displayName": str(_value(internal_props, "_displayName", "")),
        "description": str(_value(internal_props, "_description", "")), "isBasicAbility": True,
        "maxUsesPerTurn": int(_value(internal_props, "_maxUsesPerTurn", 0)), "canCrit": False,
        "growthVisible": False, "sourcePath": internal_asset["sourcePath"],
        "sourceGuid": internal_asset["sourceGuid"], "sourceLocalFileId": int(internal_asset["sourceLocalFileId"]),
        "dependencyHash": internal_asset["dependencyHash"], "graphPath": internal_graph["sourcePath"],
        "graphDependencyHash": internal_graph["dependencyHash"],
    }
    validate_unique_content_ids([*definitions, internal])

    audit_keys = [key for key in assets if key.startswith("runtime.") or key.startswith("tooling.")]
    audit = [{
        "sourceKey": key, "sourcePath": assets[key]["sourcePath"],
        "sourceFileSha256": assets[key]["sourceFileSha256"],
        "targetContentIds": assets[key]["targetContentIds"],
    } for key in sorted(audit_keys)]
    return {
        "schemaVersion": 1, "batchId": BATCH_ID,
        "classification": "disposable_typed_ownership_closure_migration_draft",
        "source": {"sourceTag": export["sourceTag"], "sourceCommit": export["sourceCommit"],
                   "unityVersion": export["unityVersion"], "exporterVersion": export["exporterVersion"],
                   "exportHash": export_semantic_hash(export)},
        "playerSkillDefinitions": sorted(definitions, key=lambda item: item["contentId"]),
        "internalSkillDefinitions": [internal],
        "treasureContract": {"contentId": "treasure.pure-run.standard-v1", "goldMinimum": 2,
                             "goldMaximum": 5, "weightedBuffCount": 1, "weightedEquipmentCount": 1,
                             "eventsCompleted": 1, "transactionPolicy": "resolve-once-persist-result"},
        "toolingContracts": audit,
        "payloadBoundary": {"unityUiPayloadCopied": False, "unityPresentationPayloadCopied": False,
                            "thirdPartyPayloadCopied": False, "audioPayloadCopied": False},
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export", type=Path, required=True)
    parser.add_argument("--specification", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    draft = compile_ownership_closure_draft(load_json(args.export), load_json(args.specification))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(draft, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
