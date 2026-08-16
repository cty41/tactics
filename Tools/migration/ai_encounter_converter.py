"""Compile the frozen Pure Run AI/Encounter export into a strict disposable draft."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

from Tools.migration.export_document import export_semantic_hash, load_json, validate_export_document
from Tools.migration.manifest import normalize_content_id

AI_IDS = (
    "ai.pure-run.aoe", "ai.pure-run.charger", "ai.pure-run.elite-charger",
    "ai.pure-run.elite-poison-caster", "ai.pure-run.ranged", "ai.pure-run.support",
)
SKILLS = {
    "skill.enemy.area-blast.lv1": (0, 1, 3, "AreaBlast", 6, 2),
    "skill.enemy.charge-strike.lv1": (0, 1, 3, "ChargeStrike", 7, 0),
    "skill.enemy.heavy-shot.lv1": (8, 2, 4, "HeavyShot", 6, 0),
    "skill.enemy.ranged-attack.lv1": (0, 2, 4, "RangedAttack", 2, 0),
}
ALLOWED_SKILL_NODES = {
    "StartNodeRecord", "FinishNodeRecord", "SelectPrimaryTargetNodeRecord",
    "SelectTargetPointNodeRecord", "ProjectileLaunchNodeRecord", "OnHitNodeRecord",
    "ApplyDamageNodeRecord", "CollectTargetsInAreaNodeRecord", "ForEachTargetNodeRecord",
    "DashToTargetNodeRecord", "ApplyKnockbackNodeRecord",
}
ENCOUNTERS = [
    {"contentId":"encounter.pure-run.n1","layout":"battle-layout.pure-run.open","monsters":["unit.pure-run.goat-charger","unit.pure-run.goat-charger","unit.pure-run.goat-ranged"]},
    {"contentId":"encounter.pure-run.n2","layout":"battle-layout.pure-run.open","monsters":["unit.pure-run.goat-ranged","unit.pure-run.goat-ranged","unit.pure-run.goat-support"]},
    {"contentId":"encounter.pure-run.n3","layout":"battle-layout.pure-run.center-blocker","monsters":["unit.pure-run.goat-aoe","unit.pure-run.goat-charger","unit.pure-run.goat-charger","unit.pure-run.goat-support"]},
]

def _properties(asset):
    return {p["propertyPath"]: p for p in asset["objects"][0]["properties"]}

def _value(properties, path, default=None):
    item = properties.get(path)
    return default if item is None else item.get("value", default)

def _compile_decision_graph(asset):
    props = _properties(asset)
    nodes = []
    count = int(_value(props, "_nodes.Array.size", 0))
    for index in range(count):
        prefix = f"_nodes.Array.data[{index}]"
        node_id = str(_value(props, prefix + "._nodeId", ""))
        enabled = bool(_value(props, prefix + "._enabled", True))
        if prefix + "._intentType" in props:
            nodes.append({"nodeId":node_id,"kind":"intent","type":str(_value(props,prefix+"._intentType")),"basePriority":float(_value(props,prefix+"._basePriority",0)),"enabled":enabled})
        elif prefix + "._ruleType" in props:
            nodes.append({"nodeId":node_id,"kind":"rule","type":str(_value(props,prefix+"._ruleType")),"parameter":float(_value(props,prefix+"._parameter",0)),"enabled":enabled})
        elif prefix + "._scoreType" in props:
            keys=[]
            key_count=int(_value(props,prefix+"._responseCurve.m_Curve.Array.size",0))
            for key_index in range(key_count):
                key=f"{prefix}._responseCurve.m_Curve.Array.data[{key_index}]"
                keys.append({"time":float(_value(props,key+".time",0)),"value":float(_value(props,key+".value",0)),"inSlope":float(_value(props,key+".inSlope",0)),"outSlope":float(_value(props,key+".outSlope",0))})
            nodes.append({"nodeId":node_id,"kind":"score","type":str(_value(props,prefix+"._scoreType")),"weight":float(_value(props,prefix+"._weight",0)),"curve":keys,"enabled":enabled})
        else:
            raise ValueError(f"unknown AI graph node at {prefix}")
    edges=[]
    for index in range(int(_value(props,"_edges.Array.size",0))):
        prefix=f"_edges.Array.data[{index}]"
        edges.append({"sourceNodeId":str(_value(props,prefix+"._sourceNodeId","")),"targetNodeId":str(_value(props,prefix+"._targetNodeId",""))})
    if not nodes or not edges:
        raise ValueError("AI decision graph must contain nodes and edges")
    return {"nodes":nodes,"edges":edges,"sourcePath":asset["sourcePath"],"sourceGuid":asset["sourceGuid"],"dependencyHash":asset["dependencyHash"]}

def compile_ai_encounter_draft(export, specification):
    warnings = validate_export_document(export, specification)
    if warnings:
        raise ValueError("AI/Encounter export contains unsupported values: " + "; ".join(warnings))
    assets = {a["sourceKey"]: a for a in export["assets"]}
    if len(assets) != 21:
        raise ValueError("AI/Encounter export must contain exactly 21 roots")
    definitions = []
    graph = _compile_decision_graph(assets["ai.shared.basic-melee-graph"])
    for content_id in AI_IDS:
        stem = content_id.removeprefix("ai.pure-run.")
        brain, profile = assets[f"ai.{stem}.brain"], assets[f"ai.{stem}.profile"]
        bp = _properties(brain)
        refs = {bp[k]["reference"]["sourcePath"] for k in ("_decisionGraph", "_profile")}
        if profile["sourcePath"] not in refs:
            raise ValueError(f"{content_id} profile reference drift")
        pattern = [p["value"] for p in brain["objects"][0]["properties"] if p["propertyPath"].endswith("._abilityName")]
        definitions.append({"contentId":content_id,"kind":"ai","archetype":stem,"brainPath":brain["sourcePath"],"brainGuid":brain["sourceGuid"],"brainLocalFileId":brain["sourceLocalFileId"],"profilePath":profile["sourcePath"],"profileGuid":profile["sourceGuid"],"decisionGraphPath":bp["_decisionGraph"]["reference"]["sourcePath"],"decisionGraphHash":bp["_decisionGraph"]["reference"]["dependencyHash"],"pattern":pattern,"decisionGraph":graph,"maximumEngageCandidatesPerTarget":int(_value(bp,"_maxEngageCandidatesPerTarget",3)),"preferredMinimumRange":int(_value(bp,"_preferredMinimumRange",1)),"preferredMaximumRange":int(_value(bp,"_preferredMaximumRange",1)),"preferredRangeRepositionBonus":float(_value(bp,"_preferredRangeRepositionBonus",0))})
    for content_id, (mana, min_range, max_range, execution, damage, radius) in sorted(SKILLS.items()):
        config, graph = assets[content_id], assets["graph." + content_id.removeprefix("skill.")]
        cp, gp = _properties(config), _properties(graph)
        if int(cp["_manaCost"]["value"]) != mana or int(cp["_targetRange"]["value"]) != max_range:
            raise ValueError(f"{content_id} frozen cost/range drift")
        if cp["_skillGraph"]["reference"]["sourcePath"] != graph["sourcePath"]:
            raise ValueError(f"{content_id} graph reference drift")
        nodes = {p["value"].split(".")[-1] for p in graph["objects"][0]["properties"] if p["propertyType"] == "ManagedReference" and p.get("value")}
        unknown = nodes - ALLOWED_SKILL_NODES
        if unknown:
            raise ValueError(f"{content_id} unknown nodes: {sorted(unknown)}")
        definitions.append({"contentId":content_id,"kind":"skill","sourceId":content_id.removeprefix("skill."),"displayName":cp["_displayName"]["value"],"description":cp["_description"]["value"],"executionKind":execution,"manaCost":mana,"minRange":min_range,"maxRange":max_range,"damage":damage,"areaRadius":radius,"graphNodes":sorted(nodes),"sourcePath":config["sourcePath"],"sourceGuid":config["sourceGuid"],"sourceLocalFileId":config["sourceLocalFileId"],"graphPath":graph["sourcePath"],"graphDependencyHash":graph["dependencyHash"]})
    layouts = [
        {"contentId":"battle-layout.pure-run.open","enemySpawns":[[6,4],[7,3],[7,5],[8,4]],"blocked":[]},
        {"contentId":"battle-layout.pure-run.center-blocker","enemySpawns":[[6,3],[6,6],[7,4],[7,5]],"blocked":[[4,4],[4,5],[5,4],[5,5]]},
    ]
    for item in definitions + layouts + ENCOUNTERS:
        normalize_content_id(item["contentId"])
    return {"schemaVersion":1,"batchId":"pure-run-ai-encounter-v1","classification":"disposable_typed_ai_encounter_migration_draft","source":{"sourceTag":export["sourceTag"],"sourceCommit":export["sourceCommit"],"unityVersion":export["unityVersion"],"exporterVersion":export["exporterVersion"],"exportHash":export_semantic_hash(export),"encounterConfigBlob":"850f23e53869c04c8ff28adbd85c1d4f12da9bae"},"definitions":definitions,"layouts":layouts,"encounters":ENCOUNTERS,"excludedEncounterIds":["N4","N5","N6","E1","E2","Special"],"payloadBoundary":{"presentation":"audit_only_not_copied","thirdPartyPayloadCopied":False,"manualGameplayAcceptance":"pending"}}

def main():
    parser=argparse.ArgumentParser(); parser.add_argument("--export",type=Path,required=True); parser.add_argument("--specification",type=Path,required=True); parser.add_argument("--output",type=Path,required=True); args=parser.parse_args()
    draft=compile_ai_encounter_draft(load_json(args.export),load_json(args.specification)); args.output.parent.mkdir(parents=True,exist_ok=True); args.output.write_text(json.dumps(draft,ensure_ascii=False,sort_keys=True,indent=2)+"\n",encoding="utf-8",newline="\n")
    return 0

if __name__ == "__main__": raise SystemExit(main())
