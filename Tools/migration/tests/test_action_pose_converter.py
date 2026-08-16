import hashlib
import json
from pathlib import Path

import pytest

from Tools.migration.action_pose_converter import migrate


def test_migrate_rejects_hash_drift(tmp_path: Path) -> None:
    source = tmp_path / "Assets/Tactics/Arts/PureRun/Textures/Actions/Mage/pose.png"
    source.parent.mkdir(parents=True)
    source.write_bytes(b"pose")
    manifest = tmp_path / "manifest.json"
    manifest.write_text(json.dumps({
        "schemaVersion": 1,
        "contractId": "pure-run-player-action-poses-v1",
        "assets": [{"source": source.relative_to(tmp_path).as_posix(), "sha256": "0" * 64}],
    }), encoding="utf-8")

    with pytest.raises(ValueError, match="hash drifted"):
        migrate(tmp_path, manifest)


def test_manifest_has_fourteen_unique_byte_bound_sources() -> None:
    root = Path(__file__).resolve().parents[3]
    manifest = root / "Tools/migration/manifest/action-poses/pure-run-player-action-poses-v1.json"
    document = json.loads(manifest.read_text(encoding="utf-8"))
    assert len(document["assets"]) == 14
    assert len({item["source"] for item in document["assets"]}) == 14
    for item in document["assets"]:
        assert hashlib.sha256((root / item["source"]).read_bytes()).hexdigest() == item["sha256"]
