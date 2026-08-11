"""Compile the frozen Unity Pure Run persistence contract into a strict draft."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from Tools.migration.export_document import export_semantic_hash, load_json, validate_export_document
from Tools.migration.manifest import normalize_content_id

BATCH_ID = "pure-run-persistence-v1"
RUN_ID = "run.pure-run.three-encounter-v1"
ENCOUNTERS = ("encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3")
PARTY = (
    ("pure_run_mage", "unit.pure-run.mage", "skill.mage.fireball.lv1"),
    ("pure_run_necromancer", "unit.pure-run.necromancer", "skill.necromancer.summon-skeleton.lv1"),
    ("pure_run_amazon", "unit.pure-run.amazon", "skill.amazon.thrust.lv1"),
)
SOURCE_BLOBS = {
    "Assets/Tactics/Scripts/Roguelike/PureRunSessionStore.cs": "597614e4e4c201c99c1a79ef75fce3cd6c40e4e6",
    "Assets/Tactics/Scripts/Common/Roster/PlayerAdventureState.cs": "f4c7e392fbc1dc1abef0f10ded0f3002457215f4",
    "Assets/Tactics/Scripts/Common/Roster/PlayerAdventureStateStore.cs": "8a3a14a1392bb57562fb6ae286eb79508ce6fe75",
    "Assets/Tactics/Scripts/Roguelike/RoguelikeMapRuntimeState.cs": "1aff7a4f228fe8c8827152302c86d17d79be8645",
    "Assets/Tactics/Scripts/RoguelikeMap/RoguelikeNodeTransactionService.cs": "4435413dfe4b56b787a870f7c43786cce6777774",
    "Assets/Tactics/Scripts/RoguelikeMap/RunSummary.cs": "3086080b45a036108758905624b09598e9c1b512",
    "Assets/Tactics/Scripts/Roguelike/PureRunSummaryRecorder.cs": "7d532c2d7297f39335024e442bbe932bc23c8f76",
    "Assets/Tactics/Scripts/Roguelike/RoguelikeBattleReturnHandler.cs": "20da7982e99da5a81f34d6efa3d9613e749c43fc",
    "Assets/Tactics/Scripts/Common/Battle/BattleRewardSystem.cs": "f8e3eb4c9136f4935585f86e4d2010738ff9e207",
    "Assets/Tactics/Scripts/Common/Battle/BattleSettlementCoordinator.cs": "1d6e4997dfb8801329003f545e823ea5e3f01a49",
    "Assets/Tactics/Scripts/Common/RoguelikeMapGenerator.cs": "4f0feeb252d95f3d213fd96ede48a694b2cba9ed",
}


def compile_persistence_draft(export: dict, specification: dict) -> dict:
    warnings = validate_export_document(export, specification)
    if warnings:
        raise ValueError("Pure Run persistence export contains unsupported values: " + "; ".join(warnings))
    if export["batchId"] != BATCH_ID:
        raise ValueError("Unexpected persistence batch id")
    assets = {asset["sourceKey"]: asset for asset in export["assets"]}
    expected = {"pure-run.map-config", "pure-run.node.minor-enemy", "pure-run.node.start"}
    if set(assets) != expected:
        raise ValueError("Persistence export must contain exactly the map, start, and minor-enemy roots")
    for asset in assets.values():
        if asset["targetContentIds"] != [RUN_ID]:
            raise ValueError("Persistence asset ownership drift")
    normalize_content_id(RUN_ID)
    for encounter in ENCOUNTERS:
        normalize_content_id(encounter)
    for _, unit, skill in PARTY:
        normalize_content_id(unit)
        normalize_content_id(skill)
    return {
        "schemaVersion": 1,
        "batchId": BATCH_ID,
        "classification": "disposable_typed_pure_run_persistence_draft",
        "source": {
            "sourceTag": export["sourceTag"],
            "sourceCommit": export["sourceCommit"],
            "unityVersion": export["unityVersion"],
            "exporterVersion": export["exporterVersion"],
            "exportHash": export_semantic_hash(export),
            "sourceBlobs": SOURCE_BLOBS,
        },
        "definition": {
            "contentId": RUN_ID,
            "encounters": list(ENCOUNTERS),
            "party": [
                {"characterId": character, "unitContentId": unit, "startingSkillContentId": skill}
                for character, unit, skill in PARTY
            ],
            "saveFormatId": "tactics-pure-run-save",
            "saveSchemaVersion": 1,
            "unitySemanticSchemaVersion": 5,
            "battleResumePolicy": "restart_from_pre_battle_checkpoint",
            "progressionPolicy": "record_pending_without_consumption",
            "terminalVictory": "SliceCompleted",
        },
        "settlement": {
            "baseGold": 3,
            "roundBonuses": [{"maxRounds": 3, "gold": 5}, {"maxRounds": 5, "gold": 3}, {"maxRounds": 10, "gold": 1}],
            "goldCap": 50,
            "minorConsumableDropChance": 0.25,
            "livingHpRecoveryPerConstitution": 2,
            "livingMpRecoveryPerCharisma": 1,
            "deadUnitsRecover": False,
            "deadUnitsUnloadEquipment": True,
            "progressionTargetTieBreak": "active_party_order",
        },
        "excludedContent": ["N4", "N5", "N6", "E1", "E2", "Special", "Rest", "Store", "Mystery", "Boss"],
        "compatibility": {"unityPlayerPrefsImport": False, "unityV5SemanticOracleOnly": True},
        "payloadBoundary": {"visualPayload": "none", "manualGameplayAcceptance": "not_required_automated_observability"},
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--export", type=Path, required=True)
    parser.add_argument("--specification", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    draft = compile_persistence_draft(load_json(args.export), load_json(args.specification))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(draft, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
