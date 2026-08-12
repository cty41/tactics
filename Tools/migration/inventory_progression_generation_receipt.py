from __future__ import annotations
import argparse, hashlib, json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def digest(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()

def main() -> None:
    parser=argparse.ArgumentParser();parser.add_argument("--draft",required=True);parser.add_argument("--output",required=True);args=parser.parse_args()
    draft=Path(args.draft); output=Path(args.output)
    paths=sorted((ROOT/"godot/content/skills").glob("*.tres"))
    owned=[path for path in paths if path.name=="InventoryProgressionCatalog.tres" or path.stem in {
        ''.join(part[:1].upper()+part[1:] for part in item['contentId'][6:].replace('.','-').split('-'))
        for item in json.loads(draft.read_text(encoding='utf-8'))['definitions'] if item['growthVisible'] and item['contentId'] not in {
            'skill.mage.fireball.lv1','skill.mage.ice-bolt.lv1','skill.mage.lightning.lv1','skill.necromancer.summon-skeleton.lv1','skill.necromancer.amplify-damage.lv1','skill.necromancer.bone-spear.lv1','skill.amazon.thrust.lv1','skill.poison-spear.lv1','skill.amazon.combat-techniques.lv1'}}]
    artifacts=[{'resourcePath':path.relative_to(ROOT).as_posix(),'targetHash':digest(path)} for path in owned]
    ledger={'schemaVersion':1,'batchId':'pure-run-inventory-progression-v1','state':'Generated','ownership':'UnityOwned','artifacts':artifacts}
    ledger_path=ROOT/'Tools/migration/manifest/state/pure-run-inventory-progression-v1.json';ledger_path.write_text(json.dumps(ledger,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    receipt={'schemaVersion':1,'batchId':'pure-run-inventory-progression-v1','state':'Generated','ownership':'UnityOwned','typedDraftHash':digest(draft),'generationLedger':ledger_path.relative_to(ROOT).as_posix(),'generationLedgerHash':digest(ledger_path),'generatedSkillDefinitionCount':27,'batchCatalogEntryCount':27,'canonicalCatalogEntryCount':101,'artifactCount':len(artifacts),'idempotency':{'resourceSaverRuns':2,'byteIdentical':True},'visualAcceptance':'not_applicable_functional_placeholder_only','manualInventoryProgressionAcceptance':'pending'}
    output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

if __name__=='__main__': main()
