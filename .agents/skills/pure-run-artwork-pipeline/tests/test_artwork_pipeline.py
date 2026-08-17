from __future__ import annotations

import argparse
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image, ImageDraw


SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "artwork_pipeline.py"
SPEC = importlib.util.spec_from_file_location("artwork_pipeline", SCRIPT)
pipeline = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
sys.modules[SPEC.name] = pipeline
SPEC.loader.exec_module(pipeline)


class ArtworkPipelineTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        (self.root / "Tools/public-release").mkdir(parents=True)
        (self.root / "Tools/public-release/asset-provenance.json").write_text(
            json.dumps({"schemaVersion": 1, "defaultAttribution": "cty41", "entries": []}), encoding="utf-8"
        )
        self.store = pipeline.Store(self.root)

    def tearDown(self):
        self.temp.cleanup()

    def png(self, rel: str, pear: bool = False) -> Path:
        path = self.root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        image = Image.new("RGBA", (256, 256), (9, 8, 7, 0))
        draw = ImageDraw.Draw(image)
        draw.rectangle((106, 116, 149, 236), fill=(90, 80, 70, 255))
        if pear:
            draw.rectangle((96, 200, 159, 236), fill=(90, 80, 70, 255))
        image.save(path)
        return path

    def mask(self, rel: str, pear: bool = False, contact: int = 4) -> Path:
        path = self.root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        draw = ImageDraw.Draw(image)
        draw.rectangle((108, 116, 147, 236), fill=pipeline.MASK_COLORS["core"])
        if pear:
            draw.rectangle((98, 198, 157, 236), fill=pipeline.MASK_COLORS["core"])
        draw.rectangle((104, 150, 107, 153), fill=pipeline.MASK_COLORS["near_hand"])
        draw.rectangle((148, 150, 151, 153), fill=pipeline.MASK_COLORS["far_hand"])
        if contact == 1:
            draw.rectangle((104, 150, 107, 153), fill=(0, 0, 0, 0))
            draw.point((107, 150), fill=pipeline.MASK_COLORS["near_hand"])
        draw.rectangle((108, 237, 115, 240), fill=pipeline.MASK_COLORS["near_foot"])
        draw.rectangle((140, 237, 147, 240), fill=pipeline.MASK_COLORS["far_foot"])
        image.save(path)
        return path

    def ns(self, **values):
        return argparse.Namespace(**values)

    def contract_and_job(self):
        anchor = self.png("Tools/artworks/approved/anchor.png")
        anchor_mask = self.mask("Tools/artworks/masks/anchor.png")
        inventory = {"schemaVersion": 1, "assets": [{
            "path": "Tools/artworks/approved/anchor.png", "sha256": pipeline.sha256_file(anchor),
            "state": "legacy-approved", "lineage": None,
        }]}
        pipeline.write_json_idempotent(self.store.pipeline / "legacy-assets.json", inventory)
        anchor_receipt = {
            "schemaVersion": 1, "approvalId": "approval-bootstrap", "attemptId": "bootstrap-anchor",
            "candidateSha256": pipeline.sha256_file(anchor), "maskSha256": pipeline.sha256_file(anchor_mask),
            "reviewer": "cty41", "decision": "approved", "reason": "fixture bootstrap",
            "decidedAt": "2026-08-18T00:00:00+08:00",
        }
        pipeline.write_json_idempotent(self.store.record("approvals", "approval-bootstrap"), anchor_receipt)
        prompt = self.root / "Tools/artworks/prompts/job.md"
        prompt.parent.mkdir(parents=True); prompt.write_text("fixed prompt", encoding="utf-8")
        contract = pipeline.create_contract(self.store, self.ns(
            asset_id="hero", approved_asset_id=None, kind="ground_character", direction="down-right", pose="idle",
            anchor=str(anchor), anchor_mask=str(anchor_mask), mask_required=True, no_arms=True,
            near_hand_side="left", far_hand_side="right",
            size_tolerance=3, center_tolerance=2,
            output_master="Tools/artworks/approved/hero.png", output_preview="Tools/artworks/approved/hero_128.png",
            rights_holder="cty41", license="CC-BY-4.0", provenance="project-owned-gpt-generated"))
        job_args = self.ns(contract_id=contract["contractId"], prompt=str(prompt), input=[f"core_anchor={anchor}"])
        return contract, pipeline.create_job(self.store, job_args), job_args

    def test_canonical_job_id_and_retry_numbering(self):
        _, job, args = self.contract_and_job()
        self.assertEqual(job, pipeline.create_job(self.store, args))
        first = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        second = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=first["attemptId"]))
        self.assertTrue(first["attemptId"].endswith("a001"))
        self.assertTrue(second["attemptId"].endswith("a002"))

    def test_path_escape_rejected(self):
        outside = self.root.parent / "outside.png"
        with self.assertRaises(pipeline.PipelineError):
            self.store.relative(outside)

    def test_end_to_end_is_idempotent_and_promotes(self):
        _, job, _ = self.contract_and_job()
        cases_path = self.root / ".agents/skills/pure-run-artwork-pipeline/examples/cases.json"
        cases_path.parent.mkdir(parents=True)
        cases_path.write_text(json.dumps({"version": 1, "approved_assets": [], "cases": []}), encoding="utf-8")
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        raw = self.png("incoming/raw.png")
        ing = self.ns(attempt_id=attempt["attemptId"], source=str(raw))
        pipeline.ingest(self.store, ing); pipeline.ingest(self.store, ing)
        prep = self.ns(attempt_id=attempt["attemptId"], chroma=None)
        pipeline.prepare(self.store, prep); pipeline.prepare(self.store, prep)
        mask = self.mask("incoming/mask.png")
        attach = self.ns(attempt_id=attempt["attemptId"], mask=str(mask))
        pipeline.attach_mask(self.store, attach); pipeline.attach_mask(self.store, attach)
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertTrue(report["passed"], report["issues"])
        pipeline.render_review(self.store, self.ns(attempt_id=attempt["attemptId"]))
        pipeline.render_review(self.store, self.ns(attempt_id=attempt["attemptId"]))
        approval = self.ns(attempt_id=attempt["attemptId"], reviewer="cty41", reason="fixture approved", decided_at="2026-08-18T00:00:00+08:00")
        pipeline.decide(self.store, approval, "approved"); pipeline.decide(self.store, approval, "approved")
        promoted = pipeline.promote(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertEqual("promoted", promoted["state"])
        pipeline.promote(self.store, self.ns(attempt_id=attempt["attemptId"]))
        cases = json.loads(cases_path.read_text(encoding="utf-8"))
        self.assertEqual("Tools/artworks/approved/hero.png", cases["approved_assets"][0]["down_right"])

    def test_different_ingest_and_failed_promotion_are_rejected(self):
        _, job, _ = self.contract_and_job()
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        first = self.png("incoming/first.png")
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(first)))
        second = self.png("incoming/second.png", pear=True)
        with self.assertRaises(pipeline.PipelineError):
            pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(second)))
        with self.assertRaises(pipeline.PipelineError):
            pipeline.promote(self.store, self.ns(attempt_id=attempt["attemptId"]))

    def test_raw_tampering_and_approval_without_review_are_rejected(self):
        _, job, _ = self.contract_and_job()
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        raw = self.png("incoming/raw-tamper.png")
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        attempt_record = pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))
        stored_raw = self.store.absolute(attempt_record["artifacts"]["raw"]["path"])
        stored_raw.write_bytes(stored_raw.read_bytes() + b"tampered")
        with self.assertRaisesRegex(pipeline.PipelineError, "raw artifact hash mismatch"):
            pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))

        second = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        clean = self.png("incoming/clean.png")
        pipeline.ingest(self.store, self.ns(attempt_id=second["attemptId"], source=str(clean)))
        pipeline.prepare(self.store, self.ns(attempt_id=second["attemptId"], chroma=None))
        mask = self.mask("incoming/clean-mask.png")
        pipeline.attach_mask(self.store, self.ns(attempt_id=second["attemptId"], mask=str(mask)))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=second["attemptId"]))
        self.assertTrue(report["passed"], report["issues"])
        approval = self.ns(attempt_id=second["attemptId"], reviewer="cty41", reason="bypass", decided_at="2026-08-18T00:00:00+08:00")
        with self.assertRaisesRegex(pipeline.PipelineError, "review outputs"):
            pipeline.decide(self.store, approval, "approved")

    def test_geometry_rejects_pear_and_missing_contact(self):
        _, job, _ = self.contract_and_job()
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        raw = self.png("incoming/pear.png", pear=True)
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
        mask = self.mask("incoming/pear-mask.png", pear=True, contact=1)
        pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(mask)))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertFalse(report["passed"])
        self.assertIn("core_lower_wider_than_middle", report["issues"])
        self.assertIn("near_hand_contact_lt_3", report["issues"])

    def test_technical_gate_rejects_chroma_and_transparent_rgb(self):
        path = self.png("bad.png")
        image = Image.open(path).convert("RGBA")
        image.putpixel((0, 0), (1, 2, 3, 0)); image.putpixel((10, 10), (0, 255, 0, 255)); image.save(path)
        _, issues = pipeline.inspect_technical(path, "ground_character")
        self.assertIn("transparent_rgb_nonzero", issues)
        self.assertIn("exact_chroma_residue", issues)


if __name__ == "__main__":
    unittest.main()
