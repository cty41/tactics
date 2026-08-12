"""Compile frozen Pure Run Inventory/Progression and Lv1/Lv2 skill contracts."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Mapping

from Tools.migration.export_document import export_semantic_hash, load_json, validate_export_document
from Tools.migration.manifest import normalize_content_id

BATCH_ID = "pure-run-inventory-progression-v1"
CONFIG_TYPE = "Tactics.Common.Units.Abilities.SkillGraphAbilityConfig"

BRANCHES = {
    "mage": ("fireball", "ice-bolt", "lightning", "summon-fire-demon", "ice-armor", "teleport"),
    "necromancer": ("summon-skeleton", "amplify-damage", "bone-spear", "skeleton-mage", "fear-curse", "bone-shield"),
    "amazon": ("thrust", "poison-spear", "combat-techniques", "multi-stab", "recover-spear", "decoy"),
}

PREREQUISITES = {
    "mage.summon-fire-demon": "mage.fireball", "mage.ice-armor": "mage.ice-bolt", "mage.teleport": "mage.lightning",
    "necromancer.skeleton-mage": "necromancer.summon-skeleton", "necromancer.fear-curse": "necromancer.amplify-damage", "necromancer.bone-shield": "necromancer.bone-spear",
    "amazon.multi-stab": "amazon.thrust", "amazon.recover-spear": "amazon.poison-spear", "amazon.decoy": "amazon.combat-techniques",
}

ATTRIBUTE = {
    "mage": "Intelligence", "necromancer.summon-skeleton": "Charisma", "necromancer.amplify-damage": "Charisma",
    "necromancer.bone-spear": "Intelligence", "necromancer.skeleton-mage": "Charisma", "necromancer.fear-curse": "Charisma",
    "necromancer.bone-shield": "Charisma", "amazon.thrust": "Agility", "amazon.poison-spear": "Agility",
    "amazon.combat-techniques": "Luck", "amazon.multi-stab": "Agility", "amazon.recover-spear": "Agility", "amazon.decoy": "Luck",
}

EXECUTION = {
    "mage.fireball": ("Fireball", "Magical", 2), "mage.ice-bolt": ("IceBolt", "Magical", 8),
    "mage.lightning": ("Lightning", "Magical", 9), "mage.summon-fire-demon": ("SummonFireDemon", "None", 0),
    "mage.ice-armor": ("IceArmor", "None", 0), "mage.teleport": ("Teleport", "None", 0),
    "necromancer.summon-skeleton": ("SummonSkeleton", "None", 0), "necromancer.amplify-damage": ("AmplifyDamage", "None", 0),
    "necromancer.bone-spear": ("BoneSpear", "Magical", 7), "necromancer.skeleton-mage": ("SummonSkeletonMage", "None", 0),
    "necromancer.fear-curse": ("FearCurse", "None", 0), "necromancer.bone-shield": ("BoneShield", "None", 0),
    "amazon.thrust": ("Thrust", "Physical", 6), "amazon.poison-spear": ("PoisonSpear", "Physical", 8),
    "amazon.multi-stab": ("MultiStab", "Physical", 4), "amazon.recover-spear": ("RecoverSpear", "None", 0),
    "amazon.decoy": ("Decoy", "None", 0),
}

def _execution_contract(branch_id: str, level: int) -> dict[str, Any]:
    execution, damage_kind, damage = EXECUTION[branch_id]
    if branch_id == "mage.fireball" and level == 2: damage = 4
    if branch_id == "amazon.poison-spear" and level == 2: damage = 10
    result: dict[str, Any] = {"executionKind": execution, "damageKind": damage_kind, "damage": damage}
    if branch_id in ("mage.fireball", "necromancer.amplify-damage", "necromancer.fear-curse", "amazon.poison-spear") and level == 2: result["areaRadius"] = 1
    if branch_id == "amazon.multi-stab": result["orderedTargetCount"] = 4 if level == 2 else 3
    if branch_id in ("necromancer.summon-skeleton", "necromancer.skeleton-mage"):
        result.update({"requiresCorpse": True, "summonLimit": min(2, level), "summonCategory": "Skeleton" if branch_id.endswith("summon-skeleton") else "SkeletonMage"})
    if branch_id == "mage.summon-fire-demon": result.update({"summonCount": level, "summonLimit": level, "summonCategory": "FireDemon"})
    if branch_id == "mage.teleport" and level == 2: result["ignoreLineOfSight"] = True
    if branch_id == "mage.ice-armor": result.update({"statusContentId": "buff.ice-armor", "statusDuration": 2})
    if branch_id == "necromancer.amplify-damage": result.update({"statusContentId": "buff.curse-damage-amplifier", "statusDuration": 5})
    if branch_id == "necromancer.fear-curse": result.update({"statusContentId": "buff.fear", "statusDuration": 1})
    if branch_id == "necromancer.bone-shield": result.update({"shieldMultiplier": 2, "shieldAbsorbsAllDamage": level == 2, "statusDuration": 99})
    if branch_id == "amazon.recover-spear" and level == 2: result["secondaryDamage"] = 6
    if branch_id == "amazon.decoy" and level == 2: result["cleanseHarmful"] = True
    return result

def _props(asset: Mapping[str, Any]) -> dict[str, Mapping[str, Any]]:
    objects = [value for value in asset["objects"] if value["objectType"] == CONFIG_TYPE]
    if len(objects) != 1:
        raise ValueError(f"{asset['sourceKey']} must contain one {CONFIG_TYPE}")
    return {value["propertyPath"]: value for value in objects[0]["properties"]}

def _value(props: Mapping[str, Mapping[str, Any]], key: str, default: Any = None) -> Any:
    return props.get(key, {}).get("value", default)

def compile_inventory_progression_draft(export: Mapping[str, Any], specification: Mapping[str, Any]) -> dict[str, Any]:
    warnings = validate_export_document(export, specification)
    if warnings:
        raise ValueError("inventory/progression export contains unsupported values: " + "; ".join(warnings))
    if export["batchId"] != BATCH_ID:
        raise ValueError("inventory/progression batch identity drift")
    assets = {value["sourceKey"]: value for value in export["assets"]}
    graph_assets = {key[6:]: value for key, value in assets.items() if key.startswith("graph.")}
    if len(graph_assets) != 34:
        raise ValueError(f"inventory/progression export must contain 34 graph roots, got {len(graph_assets)}")
    definitions: list[dict[str, Any]] = []
    for role, branch_names in BRANCHES.items():
        for branch in branch_names:
            branch_id = f"{role}.{branch}"
            for level in (1, 2):
                content_id = "skill.poison-spear.lv1" if branch_id == "amazon.poison-spear" and level == 1 else f"skill.{branch_id}.lv{level}"
                normalize_content_id(content_id)
                if branch_id == "amazon.combat-techniques":
                    definitions.append({"contentId": content_id, "branchId": branch_id, "role": "Amazon", "level": level, "kind": "Passive", "requiredAttribute": "Luck", "minimumAttribute": 5, "prerequisiteBranchId": "", "sourceKind": "linked-source", "growthVisible": True})
                    continue
                asset = assets.get(content_id)
                if asset is None:
                    raise ValueError(f"missing skill root {content_id}")
                props = _props(asset)
                graph = graph_assets.get(content_id[6:])
                graph_reference = props.get("_skillGraph", {}).get("reference")
                if graph is None or not graph_reference or graph_reference.get("sourcePath") != graph["sourcePath"] or graph_reference.get("dependencyHash") != graph["dependencyHash"]:
                    raise ValueError(f"{content_id} graph root differs from its AbilityConfig reference")
                minimum = 7 if branch_id in PREREQUISITES else 5
                required = ATTRIBUTE.get(branch_id, ATTRIBUTE.get(role))
                definition = {
                    "contentId": content_id, "branchId": branch_id, "role": role.title(), "level": level, "kind": "Active",
                    "requiredAttribute": required, "minimumAttribute": minimum, "prerequisiteBranchId": PREREQUISITES.get(branch_id, ""),
                    "manaCost": int(_value(props, "_manaCost", 0)), "targetRange": int(_value(props, "_targetRange", 0)),
                    "isBasicAbility": str(_value(props, "_isBasicAbility", "false")).lower() == "true", "maxUsesPerTurn": int(_value(props, "_maxUsesPerTurn", 0)),
                    "displayName": str(_value(props, "_displayName", content_id)), "description": str(_value(props, "_description", "")),
                    "sourcePath": asset["sourcePath"], "sourceGuid": asset["sourceGuid"], "sourceLocalFileId": int(asset["sourceLocalFileId"]),
                    "dependencyHash": asset["dependencyHash"], "graphPath": graph["sourcePath"], "graphDependencyHash": graph["dependencyHash"],
                    "graphObjectCount": len(graph["objects"]), "growthVisible": True,
                }
                definition.update(_execution_contract(branch_id, level))
                definitions.append(definition)
    if len(definitions) != 36 or len({value["contentId"] for value in definitions}) != 36:
        raise ValueError("inventory/progression draft must contain 36 unique player skill levels")
    audit = {key: {"sourcePath": assets[key]["sourcePath"], "sourceFileSha256": assets[key]["sourceFileSha256"]} for key in ("contract.ability-catalog", "contract.loadout", "contract.level-up")}
    dependencies = [{"sourceKey": key, "sourcePath": value["sourcePath"], "dependencyHash": value["dependencyHash"]} for key, value in sorted(assets.items()) if key.startswith("dependency.")]
    return {
        "schemaVersion": 1, "batchId": BATCH_ID, "classification": "disposable_typed_inventory_progression_migration_draft",
        "source": {"sourceTag": export["sourceTag"], "sourceCommit": export["sourceCommit"], "unityVersion": export["unityVersion"], "exporterVersion": export["exporterVersion"], "exportHash": export_semantic_hash(export)},
        "branches": [{"branchId": f"{role}.{branch}", "role": role.title(), "maxLevel": 2} for role, values in BRANCHES.items() for branch in values],
        "definitions": definitions, "internalSkillDependencies": dependencies, "sourceContracts": audit,
        "inventoryContract": {"capacity": 20, "equipmentPolicy": "unique-slot-replace", "carriedConsumableSlotsPerCharacter": 1, "deathUnequips": True},
        "progressionContract": {"target": "lowest-level-living-active-party-stable-order", "attributeBeforeSkill": True, "skillRequiredWhenCandidatesExist": True, "maximumSkillLevel": 2},
        "payloadBoundary": {"unityUiPayloadCopied": False, "formalVfxAudioCopied": False, "thirdPartyPayloadCopied": False, "manualInventoryProgressionAcceptance": "pending"},
    }

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export", type=Path, required=True)
    parser.add_argument("--specification", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    draft = compile_inventory_progression_draft(load_json(args.export), load_json(args.specification))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(draft, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8", newline="\n")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
