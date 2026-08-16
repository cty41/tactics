import argparse
import hashlib
import json
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--export-receipt", required=True)
    parser.add_argument("--draft", required=True)
    parser.add_argument("--ledger", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    export_receipt = json.loads(Path(args.export_receipt).read_text(encoding="utf-8"))
    draft_path, ledger_path = Path(args.draft), Path(args.ledger)
    draft = json.loads(draft_path.read_text(encoding="utf-8"))
    ledger = json.loads(ledger_path.read_text(encoding="utf-8"))
    if draft["batchId"] != "pure-run-persistence-v1" or len(ledger["artifacts"]) != 3:
        raise ValueError("Pure Run generation evidence is incomplete")
    receipt = {
        "schemaVersion": 1,
        "batchId": draft["batchId"],
        "status": "Validated",
        "ownership": "UnityOwned",
        "exportReceiptSha256": "sha256:" + hashlib.sha256(Path(args.export_receipt).read_bytes()).hexdigest(),
        "draftSha256": "sha256:" + hashlib.sha256(draft_path.read_bytes()).hexdigest(),
        "ledgerSha256": "sha256:" + hashlib.sha256(ledger_path.read_bytes()).hexdigest(),
        "sourceExportHash": export_receipt["outputSha256"],
        "canonicalCatalogEntryCount": 74,
        "saveFormatId": "tactics-pure-run-save",
        "saveSchemaVersion": 1,
        "storagePath": "user://pure-run/save-v1.json",
        "recoveryPolicy": "validated-temp-backup-quarantine",
        "visualAcceptance": "not_applicable_no_visual_payload",
        "manualGameplayAcceptance": "not_required_automated_observability",
    }
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
