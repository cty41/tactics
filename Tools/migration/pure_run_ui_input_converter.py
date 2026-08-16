"""Compile frozen Unity UI/Input audit roots into the Phase 7A contract draft."""
from __future__ import annotations
import argparse, json
from pathlib import Path
from Tools.migration.export_document import export_semantic_hash, load_json, validate_export_document

BATCH_ID="pure-run-ui-input-v1"
ROOTS={"ui.home","ui.battle","ui.settlement","ui.summary","input.actions"}
SOURCE_BLOBS={
"Assets/Tactics/Scripts/UI/HomeUIController.cs":"28da6f4aec47aa16b71cdb541ce10631b2be5f13",
"Assets/Tactics/Scripts/UI/BattleUIController.cs":"5a953c158c51e861f3577a60bee60d37fed8ed7a",
"Assets/Tactics/Scripts/UI/BattleSettlementUIController.cs":"38d55d67e7ec10300cb4e32cf9783fa082691fd5",
"Assets/Tactics/Scripts/RoguelikeMap/UI/BossVictoryUIController.cs":"1eab56b026d0e459daa4ea66594c0f7d5cacb22a",
"Assets/Tactics/Scripts/Flow/HomeFlowCoordinator.cs":"88079e195b70ed586eb0c148000f5cfbd85a4226",
"Assets/Tactics/Scripts/Common/Battle/BattleSettlementFlow.cs":"4d4c89fcdc9f85d10d17f66945888446649771cd"}

def compile_draft(export:dict,spec:dict)->dict:
    warnings=validate_export_document(export,spec)
    if warnings: raise ValueError("UI/Input export drift: "+"; ".join(warnings))
    if export["batchId"]!=BATCH_ID or {a["sourceKey"] for a in export["assets"]}!=ROOTS: raise ValueError("UI/Input root identity drift")
    if any(asset.get("exportMode")!="audit-only-file" or asset.get("objects") for asset in export["assets"]):
        raise ValueError("UI/Input roots must use audit-only file export")
    return {"schemaVersion":1,"batchId":BATCH_ID,"classification":"audit_only_ui_input_contract","source":{"sourceTag":export["sourceTag"],"sourceCommit":export["sourceCommit"],"unityVersion":export["unityVersion"],"exporterVersion":export["exporterVersion"],"exportHash":export_semantic_hash(export),"sourceBlobs":SOURCE_BLOBS},"pages":["home","battle","settlement","summary"],"input":{"pointer":"left_confirm_right_cancel","keyboard":["escape_cancel","enter_end_turn"]},"flow":["Home","N1","Settlement","N2","Settlement","N3","Summary","Home"],"payloadBoundary":{"unityUiToolkitCopied":False,"formalVfxAudio":False,"manualUiInputAcceptance":"pending"}}

def main()->int:
    p=argparse.ArgumentParser();p.add_argument("--export",type=Path,required=True);p.add_argument("--specification",type=Path,required=True);p.add_argument("--output",type=Path,required=True);a=p.parse_args();draft=compile_draft(load_json(a.export),load_json(a.specification));a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(draft,ensure_ascii=False,sort_keys=True,indent=2)+"\n",encoding="utf-8",newline="\n");return 0
if __name__=="__main__":raise SystemExit(main())
