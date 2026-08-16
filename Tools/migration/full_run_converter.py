from __future__ import annotations
import argparse, hashlib, json
from pathlib import Path

BATCH='pure-run-full-seven-layer-v1'
EXPECTED={'map.generator','encounter.catalog','battle.rewards','battle.settlement','run.session','run.summary','node.transaction'}

def digest(path:Path)->str:return 'sha256:'+hashlib.sha256(path.read_bytes()).hexdigest()
def main()->None:
 p=argparse.ArgumentParser();p.add_argument('--export',required=True);p.add_argument('--specification',required=True);p.add_argument('--output',required=True);a=p.parse_args()
 export=json.loads(Path(a.export).read_text(encoding='utf-8'));spec=json.loads(Path(a.specification).read_text(encoding='utf-8'))
 if export['batchId']!=BATCH or spec['batchId']!=BATCH:raise ValueError('batch identity mismatch')
 assets={v['sourceKey']:v for v in export['assets']}
 if set(assets)!=EXPECTED:raise ValueError('source set drift')
 for expected in spec['assets']:
  actual=assets[expected['sourceKey']]
  if actual['sourcePath']!=expected['sourcePath'] or actual['gitBlobSha1']!=expected['gitBlobSha1']:raise ValueError('source binding drift')
 encounters=[
  {'contentId':'encounter.pure-run.n5','layout':'battle-layout.pure-run.center-blocker','units':['support','support','charger','aoe'],'health':1.0,'output':1.0},
  {'contentId':'encounter.pure-run.n6','layout':'battle-layout.pure-run.split-flank','units':['charger','charger','ranged','aoe'],'health':1.0,'output':1.0},
  {'contentId':'encounter.pure-run.e1','layout':'battle-layout.pure-run.center-blocker','units':['aoe','charger','charger','support'],'health':1.3,'output':1.15},
  {'contentId':'encounter.pure-run.e2','layout':'battle-layout.pure-run.split-flank','units':['ranged','ranged','aoe','charger'],'health':1.3,'output':1.15},
  {'contentId':'encounter.pure-run.special','layout':'battle-layout.pure-run.special-open','variants':[['elite-charger'],['elite-poison-caster']],'health':1.8,'output':1.25}]
 draft={'schemaVersion':1,'batchId':BATCH,'source':{'tag':export['sourceTag'],'commit':export['sourceCommit'],'unityVersion':export['unityVersion'],'exportHash':digest(Path(a.export))},'map':{'contentId':'run-map.pure-run.full-v1','layoutVersion':2,'layers':7,'layer5':'elite','layer6':['elite','rest','store','mystery'],'layer7':'special'},'encounters':encounters,'selection':{'elitePool':['E1','E2'],'bossPool':['elite-charger','elite-poison-caster'],'contract':'derive-seed-by-node'},'multipliers':{'elite':{'health':1.3,'output':1.15},'special':{'health':1.8,'output':1.25}},'excluded':['Lv3','Treasure','Presentation','VFX','Audio']}
 Path(a.output).write_text(json.dumps(draft,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
if __name__=='__main__':main()
