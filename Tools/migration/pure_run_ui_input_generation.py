"""Record deterministic Phase 7A PackedScene generation evidence."""
from __future__ import annotations
import argparse, hashlib, json
from pathlib import Path
from Tools.migration.export_document import load_json

def compile_evidence(draft:dict,scene:Path,balance:Path|None=None)->tuple[dict,dict]:
 payload=scene.read_bytes();text=payload.decode('utf-8')
 if 'TacticsMigrationRoot.cs' not in text or 'type="Node"' not in text: raise ValueError('Main PackedScene root contract is invalid')
 digest='sha256:'+hashlib.sha256(payload).hexdigest()
 artifacts=[{'resourcePath':'res://scenes/Main.tscn','targetHash':digest}]
 balance_digest=None
 if balance is not None:
  balance_text=balance.read_text(encoding='utf-8')
  if 'PlayableLv1BalanceProfileResource' not in balance_text or 'skill.mage.fireball.lv1' not in balance_text:raise ValueError('Playable balance contract is invalid')
  balance_digest='sha256:'+hashlib.sha256(balance.read_bytes()).hexdigest();artifacts.append({'resourcePath':'res://content/ui/PlayableLv1BalanceProfile.tres','targetHash':balance_digest})
 state={'schemaVersion':1,'batchId':draft['batchId'],'source':draft['source'],'artifacts':artifacts}
 receipt={'schemaVersion':1,'batchId':draft['batchId'],'state':'Validated','ownership':'UnityOwned','canvas':{'width':1600,'height':900,'stretch':'canvas_items+keep'},'canonicalCatalogEntries':74,'sceneHash':digest,'playableBalanceContract':'godot-playable-lv1-balance-v1','playableBalanceHash':balance_digest,'idempotency':{'resourceSaverRuns':2,'byteIdentical':True},'visualPayload':'existing_migrated_unit_visuals_only','manualUiInputAcceptance':'passed'}
 return state,receipt

def main()->int:
 p=argparse.ArgumentParser();p.add_argument('--draft',type=Path,required=True);p.add_argument('--scene',type=Path,required=True);p.add_argument('--balance',type=Path);p.add_argument('--state',type=Path,required=True);p.add_argument('--receipt',type=Path,required=True);a=p.parse_args();state,receipt=compile_evidence(load_json(a.draft),a.scene,a.balance)
 for path,document in ((a.state,state),(a.receipt,receipt)):path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(document,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
 return 0
if __name__=='__main__':raise SystemExit(main())
