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
VALIDATOR_SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "validate_sprite_assets.py"
VALIDATOR_SPEC = importlib.util.spec_from_file_location("validate_sprite_assets", VALIDATOR_SCRIPT)
validator = importlib.util.module_from_spec(VALIDATOR_SPEC)
assert VALIDATOR_SPEC.loader
VALIDATOR_SPEC.loader.exec_module(validator)


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

    def test_relicense_public_artifacts_records_cty41_decision_and_updates_manifest(self):
        asset = self.png("Tools/artworks/approved/relicensed.png")
        digest = pipeline.sha256_file(asset)
        manifest_path = self.root / "Tools/public-release/asset-provenance.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["entries"].append({
            "path": "Tools/artworks/approved/relicensed.png", "sha256": digest,
            "status": "approved", "rightsHolder": "cty41", "license": "project-owned",
            "provenance": "project-owned-gpt-generated",
        })
        manifest_path.write_text(json.dumps(manifest), encoding="utf-8")

        receipt = pipeline.relicense_public_artifacts(self.store, self.ns(
            path=[str(asset)], from_license="project-owned", to_license="CC-BY-4.0",
            reviewer="cty41", reason="publish approved project-owned output",
            decided_at="2026-08-21T22:00:00+08:00"))

        updated = json.loads(manifest_path.read_text(encoding="utf-8"))
        self.assertEqual("CC-BY-4.0", updated["entries"][0]["license"])
        self.assertEqual(digest, receipt["artifacts"][0]["sha256"])
        self.assertTrue(self.store.record("license-receipts", receipt["licenseReceiptId"]).is_file())
        with self.assertRaisesRegex(pipeline.PipelineError, "reviewer cty41"):
            pipeline.relicense_public_artifacts(self.store, self.ns(
                path=[str(asset)], from_license="project-owned", to_license="CC-BY-4.0",
                reviewer="codex", reason="not authorized", decided_at="2026-08-21T22:00:00+08:00"))

    def test_register_supporting_svg_records_public_provenance(self):
        guide = self.root / "Tools/artworks/doge/demonbound/pose-guide.svg"
        guide.parent.mkdir(parents=True)
        guide.write_text('<svg xmlns="http://www.w3.org/2000/svg"/>', encoding="utf-8")

        record = pipeline.register_supporting_artifact(self.store, self.ns(
            path=str(guide), role="pose-guide-source", note="offline artwork support only"))

        digest = pipeline.sha256_file(guide)
        self.assertEqual({"path": "Tools/artworks/doge/demonbound/pose-guide.svg", "sha256": digest},
                         record["artifact"])
        self.assertTrue(self.store.record("supporting-artifacts", record["supportingArtifactId"]).is_file())
        manifest = json.loads((self.root / "Tools/public-release/asset-provenance.json").read_text(encoding="utf-8"))
        self.assertEqual({
            "path": "Tools/artworks/doge/demonbound/pose-guide.svg", "sha256": digest,
            "status": "approved", "rightsHolder": "cty41", "license": "CC-BY-4.0",
            "provenance": "project-owned-supporting-derived",
        }, manifest["entries"][0])

    def png(self, rel: str, pear: bool = False, variant: int = 0) -> Path:
        path = self.root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        image = Image.new("RGBA", (256, 256), (9, 8, 7, 0))
        draw = ImageDraw.Draw(image)
        draw.rectangle((106, 116, 149, 236), fill=(90, 80, 70, 255))
        draw.rectangle((104, 150, 107, 153), fill=(90, 80, 70, 255))
        draw.rectangle((148, 150, 151, 153), fill=(90, 80, 70, 255))
        if pear:
            draw.rectangle((96, 200, 159, 236), fill=(90, 80, 70, 255))
        if variant:
            draw.point((110 + variant, 120), fill=(90 + variant, 80, 70, 255))
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
        draw.rectangle((108, 233, 115, 236), fill=pipeline.MASK_COLORS["near_foot"])
        draw.rectangle((140, 233, 147, 236), fill=pipeline.MASK_COLORS["far_foot"])
        image.save(path)
        return path

    def identity_mask(self, rel: str, diamond: bool = False) -> Path:
        path = self.root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        draw = ImageDraw.Draw(image)
        if diamond:
            draw.polygon(((128, 108), (136, 116), (128, 124), (120, 116)), fill=pipeline.IDENTITY_MASK_COLORS["forehead_blaze"])
        else:
            draw.rectangle((119, 108, 137, 116), fill=pipeline.IDENTITY_MASK_COLORS["forehead_blaze"])
        draw.rectangle((112, 82, 120, 102), fill=pipeline.IDENTITY_MASK_COLORS["alternate_ear"])
        draw.rectangle((128, 132, 147, 210), fill=pipeline.IDENTITY_MASK_COLORS["alternate_coat"])
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
        }, {
            "path": "Tools/artworks/masks/anchor.png", "sha256": pipeline.sha256_file(anchor_mask),
            "state": "legacy-unresolved", "lineage": None,
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

    def bind_series(self, job_args, poses=None, limit=5):
        poses = poses or ["idle-dr", "idle-ul"]
        series = pipeline.create_series(self.store, self.ns(
            series_id="demonbound-series", asset_id="demonbound", pose=poses, max_unique_outputs=limit))
        job_args.series_id = series["seriesId"]
        job_args.pose_id = poses[0]
        return series, pipeline.create_job(self.store, job_args)

    def occlusion_contract_and_job(self, equipment_cap: float = 0.20):
        _, _, job_args = self.contract_and_job()
        anchor = self.root / "Tools/artworks/approved/anchor.png"
        anchor_mask = self.root / "Tools/artworks/masks/anchor.png"
        contract = pipeline.create_contract(self.store, self.ns(
            asset_id="hero-ul", approved_asset_id="hero-ul", kind="ground_character", direction="up-left", pose="idle",
            anchor=str(anchor), anchor_mask=str(anchor_mask), mask_required=True, no_arms=True,
            near_hand_side="left", far_hand_side="right", size_tolerance=3, center_tolerance=2,
            layer_rule=["near_hand=behind-core", "far_hand=behind-core", "equipment=behind-core"],
            visibility_cap=["near_hand=0.05", "far_hand=0.05", f"equipment={equipment_cap}"],
            output_master="Tools/artworks/approved/hero-ul.png",
            output_preview="Tools/artworks/approved/hero-ul-128.png",
            rights_holder="cty41", license="CC-BY-4.0", provenance="project-owned-gpt-generated"))
        job_args.contract_id = contract["contractId"]
        job_args.series_id = None; job_args.pose_id = None
        return contract, pipeline.create_job(self.store, job_args)

    def occlusion_mask(self, rel: str, intrusion: bool = False) -> Path:
        path = self.mask(rel)
        image = Image.open(path).convert("RGBA")
        draw = ImageDraw.Draw(image)
        if intrusion:
            draw.rectangle((120, 160, 125, 190), fill=pipeline.MASK_COLORS["equipment"])
        else:
            draw.rectangle((100, 160, 107, 175), fill=pipeline.MASK_COLORS["equipment"])
        image.save(path)
        return path

    def process_series_attempt(self, job, variant=0, verdict="retry"):
        attempts = pipeline.list_attempts(self.store, job["jobId"])
        feedback_id = attempts[-1].get("feedbackId") if attempts else None
        attempt = pipeline.retry(self.store, self.ns(
            job_id=job["jobId"], parent_attempt=attempts[-1]["attemptId"] if attempts else None,
            feedback_id=feedback_id))
        raw = self.png(f"incoming/{attempt['attemptId']}.png", variant=variant)
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
        mask = self.mask(f"incoming/{attempt['attemptId']}-mask.png")
        pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(mask)))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertTrue(report["passed"], report["issues"])
        pipeline.render_review(self.store, self.ns(attempt_id=attempt["attemptId"]))
        feedback = pipeline.record_feedback(self.store, self.ns(
            attempt_id=attempt["attemptId"], reviewer="cty41", verdict=verdict,
            strength=["contract geometry retained"], defect=[] if verdict == "selected" else ["needs another visual option"],
            next_prompt_delta="preserve geometry and vary art details", recorded_at="2026-08-18T00:00:00+08:00"))
        return attempt, feedback

    def process_core_size_failure(self, job, *, add_chroma=False, render_review=True):
        attempts = pipeline.list_attempts(self.store, job["jobId"])
        feedback_id = attempts[-1].get("feedbackId") if attempts else None
        attempt = pipeline.retry(self.store, self.ns(
            job_id=job["jobId"], parent_attempt=attempts[-1]["attemptId"] if attempts else None,
            feedback_id=feedback_id))
        raw = self.png(f"incoming/{attempt['attemptId']}-wide.png")
        image = Image.open(raw).convert("RGBA")
        draw = ImageDraw.Draw(image)
        draw.rectangle((100, 116, 155, 236), fill=(90, 80, 70, 255))
        draw.rectangle((96, 150, 99, 153), fill=(90, 80, 70, 255))
        draw.rectangle((156, 150, 159, 153), fill=(90, 80, 70, 255))
        if add_chroma:
            draw.point((127, 140), fill=(0, 255, 0, 255))
        image.save(raw)
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
        mask_path = self.root / f"incoming/{attempt['attemptId']}-wide-mask.png"
        mask_path.parent.mkdir(parents=True, exist_ok=True)
        mask = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        mask_draw = ImageDraw.Draw(mask)
        mask_draw.rectangle((100, 116, 155, 236), fill=pipeline.MASK_COLORS["core"])
        mask_draw.rectangle((96, 150, 99, 153), fill=pipeline.MASK_COLORS["near_hand"])
        mask_draw.rectangle((156, 150, 159, 153), fill=pipeline.MASK_COLORS["far_hand"])
        mask_draw.rectangle((100, 233, 107, 236), fill=pipeline.MASK_COLORS["near_foot"])
        mask_draw.rectangle((148, 233, 155, 236), fill=pipeline.MASK_COLORS["far_foot"])
        mask.save(mask_path)
        pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(mask_path)))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
        if render_review:
            pipeline.render_review(self.store, self.ns(attempt_id=attempt["attemptId"]))
        feedback = pipeline.record_feedback(self.store, self.ns(
            attempt_id=attempt["attemptId"], reviewer="codex", verdict="technical_failed",
            strength=["visual candidate retained"], defect=["core width exceeds anchor"],
            next_prompt_delta="retain visual candidate", recorded_at="2026-08-18T00:00:00+08:00"))
        return attempt, report, feedback

    def v2_composition(self):
        anchor = self.png("Tools/artworks/approved/v2-anchor.png")
        spec_path = self.root / "Tools/artworks/specs/action.json"
        spec_path.parent.mkdir(parents=True, exist_ok=True)
        spec_path.write_text(json.dumps({
            "canvas": [256, 256],
            "coreAxis": {"bottom": [127, 236], "top": [127, 116], "tiltDegrees": [-3, 3]},
            "footCenter": [127, 236],
            "weapon": {"hiddenGrip": [127, 175], "exitWindow": [150, 140, 175, 190],
                       "tipRegion": [175, 90, 230, 160], "maxGemAreaPx": 36},
            "forbiddenRegions": [{"name": "eyes", "rect": [105, 125, 150, 155]}],
            "equipmentState": {"scabbard": "absent"}
        }), encoding="utf-8")
        return pipeline.create_composition(self.store, self.ns(
            asset_id="hero-action", spec=str(spec_path), anchor=str(anchor)))

    def test_v1_and_v2_records_load_without_rewriting_v1(self):
        v1_path = self.store.record("contracts", "legacy")
        pipeline.write_json_idempotent(v1_path, {"schemaVersion": 1, "contractId": "legacy"})
        before = v1_path.read_bytes()
        self.assertEqual(1, pipeline.load_json(v1_path)["schemaVersion"])
        composition = self.v2_composition()
        self.assertEqual(2, composition["schemaVersion"])
        self.assertEqual(before, v1_path.read_bytes())

    def test_pose_guide_is_deterministic_and_bound_to_composition(self):
        composition = self.v2_composition()
        args = self.ns(composition_id=composition["compositionId"], output="Tools/artworks/guides/action.png")
        first = pipeline.render_pose_guide(self.store, args)
        second = pipeline.render_pose_guide(self.store, args)
        self.assertEqual(first, second)
        self.assertEqual("supporting-derived", first["role"])

    def test_guard_gem_window_rejects_tip_pommel_missing_extra_and_oversized_gems(self):
        weapon = {
            "gemWindow": [122, 168, 132, 178],
            "forbiddenGemRegions": [[120, 76, 135, 101], [122, 184, 132, 198]],
            "maxGemAreaPx": 36,
        }
        self.assertEqual([], pipeline.composition_gem_issues(weapon, {"gemRegion": [125, 170, 128, 173]}))
        self.assertIn("gem_missing", pipeline.composition_gem_issues(weapon, {}))
        self.assertIn("gem_outside_guard_window", pipeline.composition_gem_issues(weapon, {"gemRegion": [125, 82, 128, 85]}))
        self.assertIn("gem_enters_forbidden_region", pipeline.composition_gem_issues(weapon, {"gemRegion": [125, 82, 128, 85]}))
        self.assertIn("gem_outside_guard_window", pipeline.composition_gem_issues(weapon, {"gemRegion": [125, 188, 128, 191]}))
        self.assertIn("extra_gem_present", pipeline.composition_gem_issues(weapon, {
            "gemRegion": [125, 170, 128, 173], "extraGemRegions": [[125, 82, 128, 85]],
        }))
        self.assertIn("gem_too_large", pipeline.composition_gem_issues(weapon, {"gemRegion": [122, 168, 128, 174]}))

    def test_blade_length_and_width_range_rejects_idle_ratio_drift(self):
        weapon = {"bladeCenterline": [123, 105, 132, 165], "minBladeWidthPx": 7, "maxBladeWidthPx": 9, "bladeLengthRangePx": [55, 65]}
        self.assertEqual([], pipeline.composition_blade_issues(weapon, {"weaponBlade": [124, 106, 131, 165]}))
        self.assertIn("weapon_blade_too_thin", pipeline.composition_blade_issues(weapon, {"weaponBlade": [124, 106, 129, 165]}))
        self.assertIn("weapon_blade_too_short", pipeline.composition_blade_issues(weapon, {"weaponBlade": [124, 120, 131, 160]}))
        self.assertIn("weapon_blade_too_long", pipeline.composition_blade_issues(weapon, {"weaponBlade": [124, 80, 131, 165]}))
        self.assertIn("weapon_blade_too_wide", pipeline.composition_blade_issues(weapon, {"weaponBlade": [120, 106, 131, 165]}))

    def test_eye_occlusion_requires_a_narrow_blade_to_overlap_both_inner_eyes(self):
        spec = {"eyeOcclusion": {"bladeOverlapsBothInnerEyes": True, "maxEyeCenterGapPx": 28}}
        regions = {"weaponBlade": [124, 125, 132, 150], "leftEyeRegion": [108, 128, 127, 146], "rightEyeRegion": [129, 128, 148, 146]}
        self.assertEqual([], pipeline.composition_eye_occlusion_issues(spec, regions))
        too_wide = dict(regions); too_wide["rightEyeRegion"] = [145, 128, 164, 146]
        self.assertIn("blade_does_not_occlude_both_inner_eyes", pipeline.composition_eye_occlusion_issues(spec, too_wide))
        self.assertIn("eye_center_gap_too_wide", pipeline.composition_eye_occlusion_issues(spec, too_wide))

    def test_generation_failure_does_not_create_raw_output(self):
        invocation = {"schemaVersion": 2, "invocationId": "generation-invocation-fixture",
                      "attemptId": "attempt-fixture", "state": "started"}
        pipeline.write_json_idempotent(self.store.record("generation-invocations", invocation["invocationId"]), invocation)
        failure = pipeline.record_generation_failure(self.store, self.ns(
            invocation_id=invocation["invocationId"], reason="delivery lost",
            failed_at="2026-08-18T00:00:00+08:00"))
        self.assertEqual("attempt-fixture", failure["attemptId"])
        self.assertFalse((self.store.pipeline / "attempts/attempt-fixture.json").exists())

    def test_feedback_v2_separates_author_and_backup_disposition(self):
        _, job, _ = self.contract_and_job()
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        raw = self.png("incoming/feedback-v2.png")
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
        feedback = pipeline.record_feedback(self.store, self.ns(
            attempt_id=attempt["attemptId"], reviewer="cty41", author_type="human",
            verdict="backup", category=["pose_axis"], strength=["viable fallback"],
            defect=["not selected"], frozen=["identity"], pending=["pose"],
            next_prompt_delta="", recorded_at="2026-08-18T00:00:00+08:00"))
        self.assertEqual("human", feedback["authorType"])
        self.assertEqual("backup", feedback["disposition"])

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

    def test_bootstrap_anchor_receipt_binds_candidate_mask_and_review(self):
        self.contract_and_job()
        review = self.png("Tools/artworks/reviews/anchor-review.png")
        receipt = pipeline.approve_anchor(self.store, self.ns(
            candidate="Tools/artworks/approved/anchor.png", mask="Tools/artworks/masks/anchor.png",
            review=str(review), reviewer="cty41", reason="fixture review",
            decided_at="2026-08-18T00:00:00+08:00"))
        self.assertEqual(pipeline.sha256_file(review), receipt["reviewSha256"])
        self.assertTrue(pipeline.approved_mask_pair(
            self.store, receipt["candidateSha256"], receipt["maskSha256"]))
        self.assertEqual(
            {"path": receipt["maskPath"], "sha256": receipt["maskSha256"]},
            pipeline.approved_anchor_mask(self.store, receipt["candidateSha256"]))
        evidence = pipeline.core_size_exception_evidence(
            self.store,
            {"geometry": {"core": {"bbox": [100, 100, 149, 220]}}},
            {
                "anchor": {"path": receipt["candidatePath"], "sha256": receipt["candidateSha256"]},
                "tolerances": {"sizePx": 3},
            },
        )
        self.assertEqual([40, 121], evidence["anchorCoreSize"])
        self.assertEqual([10, 0], evidence["delta"])

    def test_anchor_core_bbox_accepts_approved_binary_mask(self):
        mask = Image.new("RGBA", (32, 32), (0, 0, 0, 255))
        ImageDraw.Draw(mask).ellipse((8, 6, 23, 25), fill=(255, 255, 255, 255))
        self.assertEqual((8, 6, 23, 25), pipeline.anchor_core_bbox(mask))

    def test_compile_prompt_uses_bat_invariants_for_tomb_maw_bat(self):
        self.contract_and_job()
        anchor = self.root / "Tools/artworks/approved/anchor.png"
        spec_path = self.root / "Tools/artworks/specs/bat-action.json"
        spec_path.parent.mkdir(parents=True)
        spec_path.write_text(json.dumps({
            "canvas": [256, 256],
            "coreAxis": {"top": [128, 90], "bottom": [128, 180], "tiltDegrees": [-8, 8]},
            "footCenter": [128, 236],
            "weapon": {"hiddenGrip": [128, 145], "exitWindow": [100, 120, 156, 182], "tipRegion": [100, 130, 156, 190]},
            "forbiddenRegions": [],
            "equipmentState": {"scabbard": "absent", "staticEffects": "absent"},
        }), encoding="utf-8")
        composition = pipeline.create_composition(self.store, self.ns(
            asset_id="tomb-maw-bat-melee", spec=str(spec_path), anchor=str(anchor)))
        guide = pipeline.render_pose_guide(self.store, self.ns(
            composition_id=composition["compositionId"], output="Tools/artworks/reviews/bat-guide.png"))
        prompt = self.root / "Tools/artworks/prompts/bat.md"
        prompt.parent.mkdir(parents=True, exist_ok=True); prompt.write_text("bite", encoding="utf-8")
        contract = pipeline.create_contract(self.store, self.ns(
            asset_id="tomb-maw-bat-melee", approved_asset_id="tomb-maw-bat", kind="action_pose",
            direction="down-right", pose="melee", anchor=str(anchor), anchor_mask=None,
            mask_required=False, no_arms=False, near_hand_side=None, far_hand_side=None,
            size_tolerance=8, center_tolerance=2, layer_rule=[], visibility_cap=[],
            composition_id=composition["compositionId"], identity_anchor_mask=None,
            forehead_blaze_min_iou=0.45, pose_reference=True,
            output_master="Tools/artworks/approved/bat-melee.png",
            output_preview="Tools/artworks/approved/bat-melee_128.png", rights_holder="cty41",
            license="project-owned", provenance="project-owned-gpt-generated",
            asset_role=None, component_kind=None, source_mode=None))
        job = pipeline.create_job(self.store, self.ns(
            contract_id=contract["contractId"], prompt=str(prompt), input=[f"mother_anchor={anchor}"],
            pose_guide_id=guide["poseGuideId"], series_id=None, pose_id=None))
        compiled = pipeline.compile_prompt(self.store, self.ns(
            job_id=job["jobId"], pose_guide_id=guide["poseGuideId"], output="Tools/artworks/reviews/bat-prompt.md"))
        text = self.store.absolute(compiled["artifact"]["path"]).read_text(encoding="utf-8")
        self.assertIn("near-round spherical flying core", text)
        self.assertNotIn("exactly four paws", text)

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
        self.assertEqual([], cases["approved_assets"])  # a single direction is not a complete formal mother pair

    def test_action_identity_alias_does_not_replace_idle_casebook_mother(self):
        cases_path = self.root / ".agents/skills/pure-run-artwork-pipeline/examples/cases.json"
        cases_path.parent.mkdir(parents=True)
        cases_path.write_text(json.dumps({"version": 1, "approved_assets": [{
            "id": "tomb-maw-bat", "down_right": "idle-dr.png", "up_left": "idle-ul.png"
        }], "cases": []}), encoding="utf-8")
        pipeline.update_approved_cases(self.store, {
            "assetId": "tomb-maw-bat-melee-bite-dr-v01",
            "approvedAssetId": "tomb-maw-bat",
            "direction": "down-right",
        }, "bite-dr.png")
        cases = json.loads(cases_path.read_text(encoding="utf-8"))
        self.assertEqual("idle-dr.png", cases["approved_assets"][0]["down_right"])

    def test_different_ingest_and_failed_promotion_are_rejected(self):
        _, job, _ = self.contract_and_job()
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        first = self.png("incoming/first.png")
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(first)))
        second = self.png("incoming/second.png", pear=True)
        with self.assertRaises(pipeline.PipelineError):
            pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(second)))

    def test_technical_remediation_reuses_exact_parent_generation(self):
        _, job, _ = self.contract_and_job()
        job_record = pipeline.load_json(self.store.record("jobs", job["jobId"]))
        job_record["requiresInvocation"] = True
        pipeline.write_json_idempotent(self.store.record("jobs", job["jobId"]), job_record)
        parent = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        invocation = {
            "schemaVersion": 2, "invocationId": "generation-invocation-fixture",
            "attemptId": parent["attemptId"], "state": "started",
        }
        pipeline.write_json_idempotent(
            self.store.record("generation-invocations", invocation["invocationId"]), invocation, immutable=True)
        raw = self.png("incoming/remediation.png")
        pipeline.ingest(self.store, self.ns(
            attempt_id=parent["attemptId"], source=str(raw), invocation_id=invocation["invocationId"]))
        parent = pipeline.load_json(self.store.record("attempts", parent["attemptId"]))
        child = pipeline.retry(self.store, self.ns(
            job_id=job["jobId"], parent_attempt=parent["attemptId"], feedback_id=None,
            technical_remediation=True))
        remediated = pipeline.ingest(self.store, self.ns(
            attempt_id=child["attemptId"], source=str(raw), invocation_id=None))
        self.assertEqual(parent["generationInvocationId"], remediated["generationInvocationId"])
        self.assertEqual(parent["generationDeliveryId"], remediated["generationDeliveryId"])

    def test_no_arms_contract_rejects_forbidden_limb_labels(self):
        image = self.png("Tools/artworks/candidates/hero.png")
        mask = self.mask("Tools/artworks/masks/hero.png")
        with Image.open(mask) as source:
            edited = source.convert("RGBA")
        ImageDraw.Draw(edited).rectangle((100, 145, 103, 164), fill=pipeline.MASK_COLORS["near_arm"])
        edited.save(mask)
        attempt = {"artifacts": {"prepared": {"path": self.store.relative(image), "sha256": pipeline.sha256_file(image)},
                                 "mask": {"path": self.store.relative(mask), "sha256": pipeline.sha256_file(mask)}}}
        _, issues = pipeline.geometry_checks(self.store, {"maskRequired": True, "noArms": True, "kind": "action_pose",
                                                           "anchor": None, "handSides": {}, "compositionSpec": None}, attempt)
        self.assertIn("near_arm_forbidden", issues)

    def test_identity_mask_rejects_a_diamond_blaze(self):
        anchor = self.identity_mask("Tools/artworks/masks/idle-identity.png")
        candidate = self.identity_mask("Tools/artworks/masks/cast-identity.png", diamond=True)
        contract = {"identitySpec": {"anchorMaskPath": self.store.relative(anchor), "anchorMaskSha256": pipeline.sha256_file(anchor),
                                      "foreheadBlazeMinIou": 0.45, "foreheadBlazeAreaRatio": [0.65, 1.45]}}
        attempt = {"artifacts": {"identityMask": {"path": self.store.relative(candidate), "sha256": pipeline.sha256_file(candidate)}}}
        self.assertIn("forehead_blaze_shape_mismatch", pipeline.identity_mask_issues(self.store, contract, attempt))

    def test_identity_contract_requires_anchor_tile_compare_for_approval(self):
        with self.assertRaises(pipeline.PipelineError):
            pipeline.approval_review_hashes(self.store, {"artifacts": {"review": {}}}, {"identitySpec": {}})

    def test_prepare_parameter_mismatch_is_aborted_not_left_incomplete(self):
        _, job, _ = self.contract_and_job()
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None))
        raw = self.png("incoming/prepare-mismatch.png")
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None, chroma_tolerance=0))
        with self.assertRaises(pipeline.PipelineError):
            pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma="00ff00", chroma_tolerance=12))
        transaction = next((self.store.pipeline / "transactions").glob("*.json"))
        records = [json.loads(path.read_text(encoding="utf-8"))
                   for path in (self.store.pipeline / "transactions").glob("*.json")]
        self.assertIn("aborted", [record["state"] for record in records])
        self.assertNotEqual(transaction, None)
        with self.assertRaises(pipeline.PipelineError):
            pipeline.promote(self.store, self.ns(attempt_id=attempt["attemptId"]))

    def test_gate_exception_approval_is_idempotent_selects_series_and_promotes(self):
        _, _, job_args = self.contract_and_job()
        _, job = self.bind_series(job_args)
        cases_path = self.root / ".agents/skills/pure-run-artwork-pipeline/examples/cases.json"
        cases_path.parent.mkdir(parents=True)
        cases_path.write_text(json.dumps({"version": 1, "approved_assets": [], "cases": []}), encoding="utf-8")
        attempt, report, _ = self.process_core_size_failure(job)
        self.assertEqual(["core_size_out_of_tolerance"], report["issues"])
        args = self.ns(
            attempt_id=attempt["attemptId"], issue=["core_size_out_of_tolerance"], reviewer="cty41",
            reason="fixture accepts the known core width difference", decided_at="2026-08-18T01:00:00+08:00")
        receipt = pipeline.approve_exception(self.store, args)
        self.assertEqual(receipt, pipeline.approve_exception(self.store, args))
        self.assertEqual("gate-exception", receipt["approvalMode"])
        self.assertEqual([56, 121], receipt["waivedIssues"][0]["candidateCoreSize"])
        series = pipeline.load_json(self.store.record("series", "demonbound-series"))
        self.assertEqual(attempt["attemptId"], series["poses"][0]["selectedAttemptId"])
        self.assertEqual("approved", series["poses"][0]["state"])
        promoted = pipeline.promote(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertEqual("promoted", promoted["state"])
        self.assertEqual(receipt, pipeline.approve_exception(self.store, args))
        managed, state_error = validator.promoted_state_machine_paths(self.root)
        self.assertIsNone(state_error)
        master = self.root / "Tools/artworks/approved/hero.png"
        preview = self.root / "Tools/artworks/approved/hero_128.png"
        reports = validator.validate_pair(
            master, preview, standard_height=122, baseline=236, preview_size=128,
            geometry_required=master.resolve() not in managed)
        self.assertFalse([report for report in reports if report["issues"]], reports)

    def test_gate_exception_rejects_nonwaivable_or_incomplete_failures(self):
        _, job, _ = self.contract_and_job()
        attempt, report, _ = self.process_core_size_failure(job, add_chroma=True)
        self.assertIn("core_size_out_of_tolerance", report["issues"])
        self.assertIn("exact_chroma_residue", report["issues"])
        args = self.ns(
            attempt_id=attempt["attemptId"], issue=["core_size_out_of_tolerance"], reviewer="cty41",
            reason="must not bypass chroma", decided_at="2026-08-18T01:00:00+08:00")
        with self.assertRaisesRegex(pipeline.PipelineError, "exactly match"):
            pipeline.approve_exception(self.store, args)
        args.issue = ["exact_chroma_residue"]
        with self.assertRaisesRegex(pipeline.PipelineError, "not waivable"):
            pipeline.approve_exception(self.store, args)

    def test_gate_exception_requires_cty41_complete_review_and_immutable_hashes(self):
        _, job, _ = self.contract_and_job()
        missing_review, _, _ = self.process_core_size_failure(job, render_review=False)
        args = self.ns(
            attempt_id=missing_review["attemptId"], issue=["core_size_out_of_tolerance"], reviewer="cty41",
            reason="fixture exception", decided_at="2026-08-18T01:00:00+08:00")
        with self.assertRaisesRegex(pipeline.PipelineError, "review outputs"):
            pipeline.approve_exception(self.store, args)

        attempt, _, _ = self.process_core_size_failure(job)
        args.attempt_id = attempt["attemptId"]
        args.reviewer = "codex"
        with self.assertRaisesRegex(pipeline.PipelineError, "reviewer must be cty41"):
            pipeline.approve_exception(self.store, args)
        args.reviewer = "cty41"
        pipeline.approve_exception(self.store, args)
        record = pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))
        review_path = self.store.absolute(record["artifacts"]["review"]["preview128"]["path"])
        review_path.write_bytes(review_path.read_bytes() + b"tampered")
        with self.assertRaisesRegex(pipeline.PipelineError, "review artifact hash mismatch"):
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

    def test_technical_gate_accepts_contract_declared_large_master(self):
        path = self.root / "large-master.png"
        Image.new("RGBA", (384, 384), (0, 0, 0, 0)).save(path)
        technical, issues = pipeline.inspect_technical(
            path, "projectile", expected_master_size=(384, 384))
        self.assertEqual([384, 384], technical["size"])
        self.assertNotIn("master_size_mismatch", issues)

        _, default_issues = pipeline.inspect_technical(path, "projectile")
        self.assertIn("master_size_mismatch", default_issues)

    def test_tile_placement_contract_and_review_metrics(self):
        contract = pipeline.create_contract(self.store, self.ns(
            asset_id="altar", approved_asset_id=None, kind="projectile", direction="down-right", pose="display",
            anchor=None, anchor_mask=None, mask_required=False, no_arms=False,
            near_hand_side=None, far_hand_side=None, size_tolerance=3, center_tolerance=2,
            master_width=384, master_height=384,
            footprint_width=2, footprint_height=2, display_scale=0.5,
            ground_anchor_x=192, ground_anchor_y=342, anchor_mode="contact_shape_center",
            output_master="Tools/artworks/approved/altar.png",
            output_preview="Tools/artworks/approved/altar_128.png",
            rights_holder="cty41", license="CC-BY-4.0", provenance="project-owned-gpt-generated"))
        self.assertEqual([384, 384], contract["canvasSpec"]["masterSize"])
        self.assertEqual([2, 2], contract["tilePlacementSpec"]["footprintTiles"])

        image = Image.new("RGBA", (384, 384), (0, 0, 0, 0))
        review, metrics = pipeline.render_tile_placement_review(image, contract["tilePlacementSpec"])
        self.assertEqual([192, 192], metrics["displaySize"])
        self.assertEqual(metrics["logicalTileCenter"], metrics["anchorScreenPoint"])
        self.assertEqual(4, len(metrics["tileCenters"]))
        self.assertEqual([128, 224], metrics["logicalTileCenter"])
        self.assertEqual((256, 256), review.size)

    def test_tile_placement_contract_rejects_partial_or_out_of_canvas_values(self):
        common = dict(
            asset_id="bad", approved_asset_id=None, kind="projectile", direction="down-right", pose="display",
            anchor=None, anchor_mask=None, mask_required=False, no_arms=False,
            near_hand_side=None, far_hand_side=None, size_tolerance=3, center_tolerance=2,
            master_width=256, master_height=256, output_master="Tools/artworks/approved/bad.png",
            output_preview="Tools/artworks/approved/bad_128.png",
            rights_holder="cty41", license="CC-BY-4.0", provenance="project-owned-gpt-generated")
        with self.assertRaisesRegex(pipeline.PipelineError, "requires footprint"):
            pipeline.create_contract(self.store, self.ns(**common, footprint_width=1))
        with self.assertRaisesRegex(pipeline.PipelineError, "inside the master canvas"):
            pipeline.create_contract(self.store, self.ns(
                **common, footprint_width=1, footprint_height=1, display_scale=0.5,
                ground_anchor_x=128, ground_anchor_y=256, anchor_mode="contact_shape_center"))

    def test_target_orientation_places_asset_upper_right_facing_player(self):
        image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        spec = {
            "footprintTiles": [1, 1], "displayScale": 0.5,
            "groundAnchorPx": [128, 230], "anchorMode": "contact_shape_center",
            "boardRole": "target", "screenFacing": "down_left",
        }
        review, metrics = pipeline.render_tile_placement_review(image, spec)
        self.assertEqual("up_right", metrics["explorationDirection"])
        self.assertEqual("down_left", metrics["screenFacing"])
        self.assertGreater(metrics["logicalTileCenter"][0], metrics["playerReferenceTileCenter"][0])
        self.assertLess(metrics["logicalTileCenter"][1], metrics["playerReferenceTileCenter"][1])
        self.assertEqual(metrics["logicalTileCenter"], metrics["anchorScreenPoint"])
        self.assertEqual((320, 224), review.size)

    def test_finite_series_requires_feedback_and_rejects_sixth_unique_output(self):
        _, _, args = self.contract_and_job()
        _series, job = self.bind_series(args)
        first = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None, feedback_id=None))
        raw = self.png("incoming/series-first.png", variant=1)
        pipeline.ingest(self.store, self.ns(attempt_id=first["attemptId"], source=str(raw)))
        with self.assertRaisesRegex(pipeline.PipelineError, "feedback-id"):
            pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=first["attemptId"], feedback_id=None))
        pipeline.prepare(self.store, self.ns(attempt_id=first["attemptId"], chroma=None))
        pipeline.attach_mask(self.store, self.ns(attempt_id=first["attemptId"], mask=str(self.mask("incoming/series-first-mask.png"))))
        pipeline.validate_attempt(self.store, self.ns(attempt_id=first["attemptId"]))
        pipeline.render_review(self.store, self.ns(attempt_id=first["attemptId"]))
        previous = pipeline.record_feedback(self.store, self.ns(
            attempt_id=first["attemptId"], reviewer="cty41", verdict="retry", strength=[], defect=["v1"],
            next_prompt_delta="v2", recorded_at="2026-08-18T00:00:00+08:00"))
        for variant in range(2, 6):
            attempt = pipeline.retry(self.store, self.ns(
                job_id=job["jobId"], parent_attempt=None, feedback_id=previous["feedbackId"]))
            raw = self.png(f"incoming/series-{variant}.png", variant=variant)
            pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
            pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
            pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(self.mask(f"incoming/series-{variant}-mask.png"))))
            pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
            pipeline.render_review(self.store, self.ns(attempt_id=attempt["attemptId"]))
            previous = pipeline.record_feedback(self.store, self.ns(
                attempt_id=attempt["attemptId"], reviewer="cty41", verdict="retry", strength=[], defect=[f"v{variant}"],
                next_prompt_delta=f"v{variant + 1}", recorded_at="2026-08-18T00:00:00+08:00"))
        sixth = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None, feedback_id=previous["feedbackId"]))
        with self.assertRaisesRegex(pipeline.PipelineError, "output limit"):
            pipeline.ingest(self.store, self.ns(
                attempt_id=sixth["attemptId"], source=str(self.png("incoming/series-6.png", variant=6))))

    def test_unlimited_series_accepts_more_than_five_unique_outputs(self):
        _, _, args = self.contract_and_job()
        series, job = self.bind_series(args, limit=None)
        self.assertIsNone(series["maxUniqueOutputs"])
        for variant in range(1, 7):
            self.process_series_attempt(job, variant=variant, verdict="retry")
        stored = pipeline.load_json(self.store.record("series", series["seriesId"]))
        pose = pipeline.series_pose(stored, "idle-dr")
        self.assertEqual(6, len(pipeline.unique_pose_hashes(self.store, pose)))

    def test_series_limit_can_be_removed_with_audited_change(self):
        _, _, args = self.contract_and_job()
        series, job = self.bind_series(args, limit=1)
        _first, feedback = self.process_series_attempt(job, variant=1, verdict="retry")
        second = pipeline.retry(self.store, self.ns(
            job_id=job["jobId"], parent_attempt=None, feedback_id=feedback["feedbackId"]))
        raw = self.png("incoming/series-after-unlimited.png", variant=2)
        with self.assertRaisesRegex(pipeline.PipelineError, "output limit"):
            pipeline.ingest(self.store, self.ns(attempt_id=second["attemptId"], source=str(raw)))
        change = pipeline.set_series_output_limit(self.store, self.ns(
            series_id=series["seriesId"], max_unique_outputs=None, unlimited=True,
            reviewer="cty41", reason="continue reviewed iteration",
            decided_at="2026-08-18T00:00:00+08:00"))
        self.assertIsNone(change["maxUniqueOutputs"])
        pipeline.ingest(self.store, self.ns(attempt_id=second["attemptId"], source=str(raw)))
        stored = pipeline.load_json(self.store.record("series", series["seriesId"]))
        self.assertIsNone(stored["maxUniqueOutputs"])
        self.assertIn(change["seriesLimitChangeId"], stored["limitChangeIds"])

    def test_removed_limit_reopens_exhausted_pose_without_rewriting_feedback(self):
        _, _, args = self.contract_and_job()
        series, job = self.bind_series(args, limit=1)
        attempt, feedback = self.process_series_attempt(job, variant=1, verdict="exhausted")
        pipeline.set_series_output_limit(self.store, self.ns(
            series_id=series["seriesId"], max_unique_outputs=None, unlimited=True,
            reviewer="cty41", reason="continue reviewed iteration",
            decided_at="2026-08-18T00:00:00+08:00"))
        retried = pipeline.retry(self.store, self.ns(
            job_id=job["jobId"], parent_attempt=attempt["attemptId"], feedback_id=feedback["feedbackId"]))
        self.assertEqual(feedback["feedbackId"], retried["retryFeedbackId"])
        stored = pipeline.load_json(self.store.record("series", series["seriesId"]))
        self.assertEqual("active", pipeline.series_pose(stored, "idle-dr")["state"])

    def test_provisional_anchor_makes_downstream_jobs_concept_only(self):
        contract, _, args = self.contract_and_job()
        series, job = self.bind_series(args, limit=1)
        attempt, _ = self.process_series_attempt(job, variant=1, verdict="retry")
        pipeline.select_attempt(self.store, self.ns(attempt_id=attempt["attemptId"], provisional=True))
        pipeline.advance_series(self.store, self.ns(series_id=series["seriesId"]))
        next_contract = dict(contract)
        next_contract.pop("contractId")
        next_contract["outputs"] = {"master": "Tools/artworks/concepts/hero-ul.png", "preview": "Tools/artworks/concepts/hero-ul-128.png"}
        next_id = pipeline.contract_id(next_contract)
        pipeline.write_json_idempotent(self.store.record("contracts", next_id), {"schemaVersion": 1, "contractId": next_id, **next_contract}, immutable=True)
        next_args = self.ns(contract_id=next_id, prompt=args.prompt, input=args.input,
                            series_id=series["seriesId"], pose_id="idle-ul")
        downstream = pipeline.create_job(self.store, next_args)
        self.assertTrue(downstream["conceptOnly"])

    def test_nine_pose_series_end_to_end_without_imagegen(self):
        poses = ["idle-dr", "idle-ul", "melee-dr", "melee-ul", "cast-dr", "cast-ul", "hit-dr", "hit-ul", "death"]
        _contract, _, args = self.contract_and_job()
        cases_path = self.root / ".agents/skills/pure-run-artwork-pipeline/examples/cases.json"
        cases_path.parent.mkdir(parents=True)
        cases_path.write_text(json.dumps({"version": 1, "approved_assets": [], "cases": []}), encoding="utf-8")
        series, first_job = self.bind_series(args, poses=poses)
        first_attempt, _ = self.process_series_attempt(first_job, verdict="selected")
        pipeline.select_attempt(self.store, self.ns(attempt_id=first_attempt["attemptId"], provisional=False))
        approval_args = self.ns(attempt_id=first_attempt["attemptId"], reviewer="cty41", reason="fixture idle anchor",
                                decided_at="2026-08-18T00:00:00+08:00")
        pipeline.decide(self.store, approval_args, "approved")
        promoted_idle = pipeline.promote(self.store, self.ns(attempt_id=first_attempt["attemptId"]))
        idle_master = promoted_idle["artifacts"]["promoted"]["master"]["path"]
        idle_mask = promoted_idle["artifacts"]["mask"]["path"]
        pipeline.advance_series(self.store, self.ns(series_id=series["seriesId"]))

        selected = []
        for index, pose in enumerate(poses[1:], start=1):
            direction = "up-left" if pose.endswith("ul") else "down-right"
            contract = pipeline.create_contract(self.store, self.ns(
                asset_id=f"hero-{pose}", approved_asset_id=f"hero-{pose}", kind="ground_character",
                direction=direction, pose=pose, anchor=idle_master, anchor_mask=idle_mask,
                mask_required=True, no_arms=True, near_hand_side="left", far_hand_side="right",
                size_tolerance=3, center_tolerance=2,
                output_master=f"Tools/artworks/approved/hero-{pose}.png",
                output_preview=f"Tools/artworks/approved/hero-{pose}-128.png",
                rights_holder="cty41", license="CC-BY-4.0", provenance="project-owned-gpt-generated"))
            job = pipeline.create_job(self.store, self.ns(
                contract_id=contract["contractId"], prompt=args.prompt, input=[f"core_anchor={self.root / idle_master}"],
                series_id=series["seriesId"], pose_id=pose))
            attempt, _ = self.process_series_attempt(job, variant=index, verdict="selected")
            pipeline.select_attempt(self.store, self.ns(attempt_id=attempt["attemptId"], provisional=False))
            selected.append(attempt["attemptId"])
            pipeline.advance_series(self.store, self.ns(series_id=series["seriesId"]))

        for attempt_id in selected:
            decision = self.ns(attempt_id=attempt_id, reviewer="cty41", reason="fixture batch approval",
                               decided_at="2026-08-18T00:00:00+08:00")
            pipeline.decide(self.store, decision, "approved")
            pipeline.promote(self.store, self.ns(attempt_id=attempt_id))
        final_series = pipeline.load_json(self.store.record("series", series["seriesId"]))
        self.assertIsNone(final_series["currentPoseId"])
        self.assertTrue(all(pose["state"] == "promoted" for pose in final_series["poses"]))
        self.assertTrue(pipeline.strict_check(self.store, True)["ok"])

    def test_core_calibration_is_uniform_idempotent_and_256(self):
        _contract, job = self.occlusion_contract_and_job()
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None, feedback_id=None))
        source = self.root / "incoming/large.png"; source.parent.mkdir(parents=True, exist_ok=True)
        image = Image.new("RGBA", (1254, 1254), (0, 0, 0, 0)); draw = ImageDraw.Draw(image)
        draw.rectangle((450, 400, 749, 999), fill=(90, 80, 70, 255))
        draw.rectangle((430, 650, 449, 680), fill=(90, 80, 70, 255)); draw.rectangle((750, 650, 769, 680), fill=(90, 80, 70, 255))
        draw.rectangle((450, 980, 520, 1020), fill=(90, 80, 70, 255)); draw.rectangle((679, 980, 749, 1020), fill=(90, 80, 70, 255))
        draw.rectangle((420, 600, 449, 760), fill=(90, 80, 70, 255)); image.save(source)
        mask_path = self.root / "incoming/large-mask.png"
        mask = Image.new("RGBA", (1254, 1254), (0, 0, 0, 0)); md = ImageDraw.Draw(mask)
        md.rectangle((450, 400, 749, 999), fill=pipeline.MASK_COLORS["core"])
        md.rectangle((430, 650, 449, 680), fill=pipeline.MASK_COLORS["near_hand"])
        md.rectangle((750, 650, 769, 680), fill=pipeline.MASK_COLORS["far_hand"])
        md.rectangle((450, 980, 520, 1020), fill=pipeline.MASK_COLORS["near_foot"])
        md.rectangle((679, 980, 749, 1020), fill=pipeline.MASK_COLORS["far_foot"])
        md.rectangle((420, 600, 449, 760), fill=pipeline.MASK_COLORS["equipment"]); mask.save(mask_path)
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(source)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
        pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(mask_path)))
        first = pipeline.calibrate_core(self.store, self.ns(attempt_id=attempt["attemptId"]))
        second = pipeline.calibrate_core(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertEqual(first["artifacts"]["calibrated"], second["artifacts"]["calibrated"])
        with Image.open(self.store.absolute(first["artifacts"]["calibrated"]["path"])) as calibrated:
            self.assertEqual((256, 256), calibrated.size)
        self.assertAlmostEqual(121 / 600, first["calibration"]["scale"])
        with Image.open(self.store.absolute(first["artifacts"]["calibratedMask"]["path"])) as calibrated_mask:
            box = pipeline.bbox_for(pipeline.pixel_data(calibrated_mask.convert("RGBA")), calibrated_mask.size, pipeline.MASK_COLORS["core"])
        self.assertGreater(box[2] - box[0] + 1, 55)  # uniform scaling keeps the wide source wide instead of stretching to the 40px anchor

    def test_chroma_tolerance_removes_generated_green_variation(self):
        source = self.root / "green-source.png"
        image = Image.new("RGBA", (4, 4), (24, 240, 24, 255)); image.putpixel((2, 2), (90, 80, 70, 255)); image.save(source)
        output = self.root / "green-prepared.png"
        pipeline.prepare_image(source, output, "00ff00", 48)
        with Image.open(output) as prepared:
            prepared = prepared.convert("RGBA")
            self.assertEqual((0, 0, 0, 0), prepared.getpixel((0, 0)))
            self.assertEqual((90, 80, 70, 255), prepared.getpixel((2, 2)))

    def test_resampled_chroma_cleanup_removes_both_reserved_key_colors(self):
        image = Image.new("RGBA", (3, 1), (0, 0, 0, 0))
        image.putdata([(0, 255, 0, 1), (255, 0, 255, 1), (120, 70, 140, 255)])

        cleaned = pipeline.clean_resampled_chroma(image, "00ff00", 48)

        self.assertEqual((0, 0, 0, 0), cleaned.getpixel((0, 0)))
        self.assertEqual((0, 0, 0, 0), cleaned.getpixel((1, 0)))
        self.assertEqual((120, 70, 140, 255), cleaned.getpixel((2, 0)))

    def test_behind_core_intrusion_is_rejected_and_outer_arcs_pass(self):
        _contract, job = self.occlusion_contract_and_job()
        for intrusion in (False, True):
            attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None, feedback_id=None))
            raw = self.png(f"incoming/occlusion-{intrusion}.png")
            if not intrusion:
                image = Image.open(raw).convert("RGBA"); ImageDraw.Draw(image).rectangle((100, 160, 107, 175), fill=(90, 80, 70, 255)); image.save(raw)
            else:
                image = Image.open(raw).convert("RGBA"); ImageDraw.Draw(image).rectangle((120, 160, 125, 190), fill=(90, 80, 70, 255)); image.save(raw)
            pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
            pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
            pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(self.occlusion_mask(f"incoming/occlusion-{intrusion}-mask.png", intrusion))))
            pipeline.calibrate_core(self.store, self.ns(attempt_id=attempt["attemptId"]))
            report = pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
            if intrusion:
                self.assertFalse(report["passed"])
                self.assertIn("equipment_intrudes_core", report["issues"])
                self.assertIn("core_row_disconnected", report["issues"])
            else:
                self.assertTrue(report["passed"], report["issues"])
                review = pipeline.render_review(self.store, self.ns(attempt_id=attempt["attemptId"]))
                self.assertIn("depthReview", review["outputs"])
                record = pipeline.load_json(self.store.record("attempts", attempt["attemptId"]))
                record["artifacts"]["review"].pop("depthReview")
                pipeline.save_attempt(self.store, record)
                with self.assertRaisesRegex(pipeline.PipelineError, "review outputs"):
                    pipeline.decide(self.store, self.ns(
                        attempt_id=attempt["attemptId"], reviewer="cty41", reason="missing depth review",
                        decided_at="2026-08-18T00:00:00+08:00"), "approved")

    def test_visibility_cap_and_missing_far_hand_are_rejected(self):
        _contract, job = self.occlusion_contract_and_job(equipment_cap=0.01)
        attempt = pipeline.retry(self.store, self.ns(job_id=job["jobId"], parent_attempt=None, feedback_id=None))
        raw = self.png("incoming/visibility.png")
        image = Image.open(raw).convert("RGBA"); ImageDraw.Draw(image).rectangle((100, 160, 107, 175), fill=(90, 80, 70, 255)); image.save(raw)
        mask_path = self.occlusion_mask("incoming/visibility-mask.png")
        mask = Image.open(mask_path).convert("RGBA")
        pixels = mask.load()
        for y in range(mask.height):
            for x in range(mask.width):
                if pixels[x, y] == pipeline.MASK_COLORS["far_hand"]:
                    pixels[x, y] = (0, 0, 0, 0)
        mask.save(mask_path)
        pipeline.ingest(self.store, self.ns(attempt_id=attempt["attemptId"], source=str(raw)))
        pipeline.prepare(self.store, self.ns(attempt_id=attempt["attemptId"], chroma=None))
        pipeline.attach_mask(self.store, self.ns(attempt_id=attempt["attemptId"], mask=str(mask_path)))
        pipeline.calibrate_core(self.store, self.ns(attempt_id=attempt["attemptId"]))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=attempt["attemptId"]))
        self.assertIn("far_hand_missing", report["issues"])
        self.assertIn("far_hand_missing_for_layer_rule", report["issues"])
        self.assertIn("equipment_visibility_cap_exceeded", report["issues"])

    def v3_contract(self, asset_id: str, role: str, component_kind: str | None, source_mode: str,
                    no_arms: bool = False):
        return pipeline.create_contract(self.store, self.ns(
            asset_id=asset_id, approved_asset_id=asset_id, kind="tile", direction="down-right", pose="cast",
            anchor=None, anchor_mask=None, mask_required=True, no_arms=no_arms,
            near_hand_side=None, far_hand_side=None, size_tolerance=3, center_tolerance=2,
            layer_rule=[], visibility_cap=[], composition_id=None, identity_anchor_mask=None,
            forehead_blaze_min_iou=0.45, pose_reference=False,
            output_master=f"Tools/artworks/approved/{asset_id}.png",
            output_preview=f"Tools/artworks/approved/{asset_id}-128.png",
            rights_holder="cty41", license="CC-BY-4.0", provenance="project-owned-gpt-generated-or-derived",
            asset_role=role, component_kind=component_kind, source_mode=source_mode))

    def approved_component(self, asset_id: str, kind: str, image: Image.Image, source_mode: str = "generated"):
        contract = self.v3_contract(asset_id, "component", kind, source_mode)
        path = self.root / f"Tools/artworks/components/{asset_id}.png"
        path.parent.mkdir(parents=True, exist_ok=True); image.save(path)
        artifact = {"path": self.store.relative(path), "sha256": pipeline.sha256_file(path)}
        label = "core" if kind == "body" else "equipment"
        if kind == "paw_overlay":
            label = "far_hand" if "far" in asset_id else "near_hand"
        if kind == "foot_overlay":
            label = "far_foot" if "far" in asset_id else "near_foot"
        mask = Image.new("RGBA", image.size, (0, 0, 0, 0))
        mask.paste(pipeline.MASK_COLORS[label], mask=image.getchannel("A"))
        mask_path = path.with_name(f"{path.stem}-mask.png"); mask.save(mask_path)
        mask_artifact = {"path": self.store.relative(mask_path), "sha256": pipeline.sha256_file(mask_path)}
        job_id = f"job-{asset_id}"
        pipeline.write_json_idempotent(self.store.record("jobs", job_id), {
            "schemaVersion": 3, "jobId": job_id, "state": "ready", "contractId": contract["contractId"],
            "contractSha256": pipeline.sha256_file(self.store.record("contracts", contract["contractId"])),
            "prompt": None, "inputs": [], "target": {"direction": "down-right", "pose": "cast"},
            "series": None, "conceptOnly": False, "contractRequirements": None,
            "requiresInvocation": False, "sourceMode": source_mode})
        attempt_id = f"{job_id}-a001"
        approval_id = f"approval-{asset_id}"
        pipeline.write_json_idempotent(self.store.record("attempts", attempt_id), {
            "schemaVersion": 3, "attemptId": attempt_id, "jobId": job_id, "ordinal": 1,
            "parentAttemptId": None, "retryFeedbackId": None, "promptDelta": None,
            "technicalRemediation": False, "state": "approved",
            "artifacts": {"prepared": artifact, "mask": mask_artifact},
            "report": None, "approvalId": approval_id, "feedbackId": None})
        pipeline.write_json_idempotent(self.store.record("approvals", approval_id), {
            "schemaVersion": 3, "approvalId": approval_id, "attemptId": attempt_id,
            "candidateSha256": artifact["sha256"], "maskSha256": mask_artifact["sha256"],
            "reviewer": "cty41", "decision": "approved", "reason": "fixture",
            "decidedAt": "2026-08-19T00:00:00+08:00"})
        return attempt_id, contract, artifact

    def test_schema_v3_component_cannot_promote(self):
        image = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); ImageDraw.Draw(image).rectangle((100, 100, 140, 180), fill="red")
        attempt_id, _contract, artifact = self.approved_component("body-component", "body", image)
        with self.assertRaisesRegex(pipeline.PipelineError, "components cannot be promoted"):
            pipeline.promote(self.store, self.ns(attempt_id=attempt_id))

    def test_assembly_is_deterministic_and_rejects_unapproved_or_forbidden_transform(self):
        transparent = lambda: Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        body = transparent(); ImageDraw.Draw(body).rectangle((100, 80, 155, 230), fill=(90, 80, 70, 255))
        sword = transparent(); ImageDraw.Draw(sword).rectangle((126, 40, 130, 180), fill=(210, 210, 220, 255))
        far = transparent(); ImageDraw.Draw(far).ellipse((115, 160, 127, 172), fill=(255, 128, 0, 255))
        near = transparent(); ImageDraw.Draw(near).ellipse((130, 158, 142, 170), fill=(255, 128, 0, 255))
        far_foot = transparent(); ImageDraw.Draw(far_foot).ellipse((112, 220, 132, 240), fill=(200, 100, 0, 255))
        near_foot = transparent(); ImageDraw.Draw(near_foot).ellipse((130, 218, 152, 240), fill=(255, 128, 0, 255))
        components = [
            ("far_foot_overlay", *self.approved_component("assembly-far-foot-base", "foot_overlay", far_foot)),
            ("far_paw_overlay", *self.approved_component("assembly-far", "paw_overlay", far)),
            ("body", *self.approved_component("assembly-body", "body", body)),
            ("equipment", *self.approved_component("assembly-sword", "equipment", sword)),
            ("near_paw_overlay", *self.approved_component("assembly-near", "paw_overlay", near)),
            ("near_foot_overlay", *self.approved_component("assembly-near-foot-base", "foot_overlay", near_foot)),
        ]
        final_contract = self.v3_contract("assembled-cast", "assembled_sprite", None, "derived")
        spec = {
            "assetId": "assembled-cast", "contractId": final_contract["contractId"], "canvas": [256, 256],
            "layers": [{"role": role, "attemptId": attempt_id,
                        "transform": {"scalePercent": 100, "translate": [0, 0], "flipHorizontal": False}}
                       for role, attempt_id, _contract, _artifact in components],
        }
        spec_path = self.root / "assembly.json"; spec_path.write_text(json.dumps(spec), encoding="utf-8")
        assembly = pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        first = pipeline.render_assembly(self.store, self.ns(assembly_id=assembly["assemblyId"]))
        second = pipeline.render_assembly(self.store, self.ns(assembly_id=assembly["assemblyId"]))
        self.assertEqual(first["artifacts"]["prepared"], second["artifacts"]["prepared"])
        bad = json.loads(json.dumps(spec)); bad["layers"][1]["transform"]["rotation"] = 5
        spec_path.write_text(json.dumps(bad), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "only supports"):
            pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        unapproved = pipeline.load_json(self.store.record("attempts", components[1][1])); unapproved["state"] = "prepared"
        pipeline.write_json_idempotent(self.store.record("attempts", components[1][1]), unapproved)
        spec_path.write_text(json.dumps(spec), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "human approval"):
            pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))

    def test_derive_paw_overlay_uses_only_requested_semantic_label(self):
        body = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        draw = ImageDraw.Draw(body); draw.rectangle((20, 20, 29, 29), fill=(255, 128, 0, 255)); draw.rectangle((40, 20, 49, 29), fill=(255, 128, 0, 255))
        attempt_id, _contract, _artifact = self.approved_component("derive-body", "body", body)
        mask_path = self.root / "derive-mask.png"; mask = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); md = ImageDraw.Draw(mask)
        md.rectangle((20, 20, 29, 29), fill=pipeline.MASK_COLORS["near_hand"]); md.rectangle((40, 20, 49, 29), fill=pipeline.MASK_COLORS["far_hand"]); mask.save(mask_path)
        attempt = pipeline.load_json(self.store.record("attempts", attempt_id))
        attempt["artifacts"]["mask"] = {"path": self.store.relative(mask_path), "sha256": pipeline.sha256_file(mask_path)}
        pipeline.write_json_idempotent(self.store.record("attempts", attempt_id), attempt)
        overlay_contract = self.v3_contract("near-overlay", "component", "paw_overlay", "derived")
        derived = pipeline.derive_component(self.store, self.ns(contract_id=overlay_contract["contractId"], source_attempt_id=attempt_id, label="near_hand"))
        output = Image.open(self.store.absolute(derived["artifacts"]["prepared"]["path"])).convert("RGBA")
        self.assertGreater(output.getpixel((24, 24))[3], 0)
        self.assertEqual(0, output.getpixel((44, 24))[3])

    def test_derive_body_and_equipment_partition_complete_pose_semantics(self):
        source = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        draw = ImageDraw.Draw(source)
        draw.rectangle((20, 20, 29, 29), fill=(80, 70, 60, 255))
        draw.rectangle((30, 20, 39, 29), fill=(120, 90, 70, 255))
        draw.rectangle((40, 20, 49, 29), fill=(220, 220, 230, 255))
        draw.rectangle((50, 20, 59, 29), fill=(255, 128, 0, 255))
        attempt_id, _contract, _artifact = self.approved_component("derive-complete", "body", source)
        mask_path = self.root / "derive-complete-mask.png"
        mask = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); md = ImageDraw.Draw(mask)
        md.rectangle((20, 20, 29, 29), fill=pipeline.MASK_COLORS["core"])
        md.rectangle((30, 20, 39, 29), fill=pipeline.MASK_COLORS["head_appendage"])
        md.rectangle((40, 20, 49, 29), fill=pipeline.MASK_COLORS["equipment"])
        md.rectangle((50, 20, 59, 29), fill=pipeline.MASK_COLORS["near_hand"])
        mask.save(mask_path)
        attempt = pipeline.load_json(self.store.record("attempts", attempt_id))
        attempt["artifacts"]["mask"] = {"path": self.store.relative(mask_path), "sha256": pipeline.sha256_file(mask_path)}
        pipeline.write_json_idempotent(self.store.record("attempts", attempt_id), attempt)

        body_contract = self.v3_contract("derived-body", "component", "body", "derived")
        equipment_contract = self.v3_contract("derived-equipment", "component", "equipment", "derived")
        body = pipeline.derive_component(self.store, self.ns(
            contract_id=body_contract["contractId"], source_attempt_id=attempt_id, label="body"))
        equipment = pipeline.derive_component(self.store, self.ns(
            contract_id=equipment_contract["contractId"], source_attempt_id=attempt_id, label="equipment"))
        body_image = Image.open(self.store.absolute(body["artifacts"]["prepared"]["path"])).convert("RGBA")
        equipment_image = Image.open(self.store.absolute(equipment["artifacts"]["prepared"]["path"])).convert("RGBA")
        self.assertGreater(body_image.getpixel((24, 24))[3], 0)
        self.assertGreater(body_image.getpixel((34, 24))[3], 0)
        self.assertEqual(0, body_image.getpixel((44, 24))[3])
        self.assertGreater(equipment_image.getpixel((44, 24))[3], 0)
        self.assertEqual(0, equipment_image.getpixel((54, 24))[3])

    def test_derived_component_uses_passing_report_instead_of_human_approval(self):
        transparent = lambda: Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        source = transparent(); ImageDraw.Draw(source).ellipse((130, 158, 142, 170), fill=(255, 128, 0, 255))
        source_attempt_id, _contract, _artifact = self.approved_component("derive-near-source", "body", source)
        source_mask_path = self.root / "derive-near-source-mask.png"
        source_mask = transparent(); ImageDraw.Draw(source_mask).ellipse((130, 158, 142, 170), fill=pipeline.MASK_COLORS["near_hand"]); source_mask.save(source_mask_path)
        source_attempt = pipeline.load_json(self.store.record("attempts", source_attempt_id))
        source_attempt["artifacts"]["mask"] = {
            "path": self.store.relative(source_mask_path), "sha256": pipeline.sha256_file(source_mask_path)}
        pipeline.write_json_idempotent(self.store.record("attempts", source_attempt_id), source_attempt)
        derived_contract = self.v3_contract("derived-near-paw", "component", "paw_overlay", "derived")
        derived = pipeline.derive_component(self.store, self.ns(
            contract_id=derived_contract["contractId"], source_attempt_id=source_attempt_id, label="near_hand"))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=derived["attemptId"]))
        self.assertTrue(report["passed"])
        derived = pipeline.load_json(self.store.record("attempts", derived["attemptId"]))
        self.assertEqual("review_pending", derived["state"])
        self.assertIsNone(derived["approvalId"])

        body = transparent(); ImageDraw.Draw(body).rectangle((100, 80, 155, 230), fill=(90, 80, 70, 255))
        equipment = transparent(); ImageDraw.Draw(equipment).rectangle((126, 40, 130, 180), fill=(210, 210, 220, 255))
        far_hand = transparent(); ImageDraw.Draw(far_hand).ellipse((115, 160, 127, 172), fill=(220, 110, 0, 255))
        far_foot = transparent(); ImageDraw.Draw(far_foot).ellipse((112, 218, 132, 236), fill=(200, 100, 0, 255))
        near_foot = transparent(); ImageDraw.Draw(near_foot).ellipse((130, 218, 152, 236), fill=(255, 128, 0, 255))
        components = [
            ("far_foot_overlay", self.approved_component("derived-fixture-far-foot", "foot_overlay", far_foot)[0]),
            ("far_paw_overlay", self.approved_component("derived-fixture-far-paw", "paw_overlay", far_hand)[0]),
            ("body", self.approved_component("derived-fixture-body", "body", body)[0]),
            ("equipment", self.approved_component("derived-fixture-equipment", "equipment", equipment)[0]),
            ("near_paw_overlay", derived["attemptId"]),
            ("near_foot_overlay", self.approved_component("derived-fixture-near-foot", "foot_overlay", near_foot)[0]),
        ]
        final_contract = self.v3_contract("derived-assembly", "assembled_sprite", None, "derived")
        spec = {"assetId": "derived-assembly", "contractId": final_contract["contractId"], "canvas": [256, 256],
                "layers": [{"role": role, "attemptId": attempt_id,
                            "transform": {"scalePercent": 100, "translate": [0, 0], "flipHorizontal": False}}
                           for role, attempt_id in components]}
        spec_path = self.root / "derived-assembly.json"; spec_path.write_text(json.dumps(spec), encoding="utf-8")
        assembly = pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        self.assertEqual([role for role, _attempt_id in components], [layer["role"] for layer in assembly["layers"]])

    def test_foot_overlays_render_behind_body_in_canonical_order(self):
        transparent = lambda: Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        body = transparent(); ImageDraw.Draw(body).rectangle((100, 80, 155, 230), fill=(90, 80, 70, 255))
        far = transparent(); ImageDraw.Draw(far).ellipse((112, 218, 132, 236), fill=(200, 100, 0, 255))
        near = transparent(); ImageDraw.Draw(near).ellipse((130, 218, 152, 236), fill=(255, 128, 0, 255))
        sword = transparent(); ImageDraw.Draw(sword).rectangle((126, 40, 130, 180), fill=(210, 210, 220, 255))
        far_hand = transparent(); ImageDraw.Draw(far_hand).ellipse((115, 160, 127, 172), fill=(220, 110, 0, 255))
        near_hand = transparent(); ImageDraw.Draw(near_hand).ellipse((130, 158, 142, 170), fill=(255, 128, 0, 255))
        components = [
            ("far_foot_overlay", *self.approved_component("assembly-far-foot", "foot_overlay", far)),
            ("far_paw_overlay", *self.approved_component("assembly-foot-far-hand", "paw_overlay", far_hand)),
            ("body", *self.approved_component("assembly-foot-body", "body", body)),
            ("equipment", *self.approved_component("assembly-foot-sword", "equipment", sword)),
            ("near_paw_overlay", *self.approved_component("assembly-foot-near-hand", "paw_overlay", near_hand)),
            ("near_foot_overlay", *self.approved_component("assembly-near-foot", "foot_overlay", near)),
        ]
        final_contract = self.v3_contract("assembled-feet", "assembled_sprite", None, "derived", no_arms=True)
        spec = {"assetId": "assembled-feet", "contractId": final_contract["contractId"], "canvas": [256, 256],
                "layers": [{"role": role, "attemptId": attempt_id,
                            "transform": {"scalePercent": 100, "translate": [0, 0], "flipHorizontal": False}}
                           for role, attempt_id, _contract, _artifact in components]}
        spec_path = self.root / "feet-assembly.json"; spec_path.write_text(json.dumps(spec), encoding="utf-8")
        assembly = pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        rendered = pipeline.render_assembly(self.store, self.ns(assembly_id=assembly["assemblyId"]))
        output = Image.open(self.store.absolute(rendered["artifacts"]["prepared"]["path"])).convert("RGBA")
        self.assertEqual((90, 80, 70, 255), output.getpixel((120, 225)))
        report = pipeline.validate_attempt(self.store, self.ns(attempt_id=rendered["attemptId"]))
        self.assertFalse(any(issue.endswith("_contact_lt_3") for issue in report["issues"]))
        review = pipeline.render_review(self.store, self.ns(attempt_id=rendered["attemptId"]))
        layer_review_path = self.store.absolute(review["outputs"]["assemblyLayerReview"]["path"])
        with Image.open(layer_review_path) as layer_review:
            self.assertEqual((256 * 6, 512), layer_review.size)

        detached = json.loads(json.dumps(spec))
        detached["layers"][0]["transform"]["translate"] = [-80, 0]
        spec_path.write_text(json.dumps(detached), encoding="utf-8")
        detached_assembly = pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        detached_render = pipeline.render_assembly(self.store, self.ns(assembly_id=detached_assembly["assemblyId"]))
        detached_report = pipeline.validate_attempt(self.store, self.ns(attempt_id=detached_render["attemptId"]))
        self.assertIn("far_foot_contact_lt_3", detached_report["issues"])
        incomplete = json.loads(json.dumps(spec)); incomplete["layers"] = incomplete["layers"][:3]
        spec_path.write_text(json.dumps(incomplete), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "exactly once"):
            pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        pose_depth = json.loads(json.dumps(spec))
        pose_depth["layers"] = [pose_depth["layers"][index] for index in (0, 5, 1, 4, 3, 2)]
        spec_path.write_text(json.dumps(pose_depth), encoding="utf-8")
        depth_assembly = pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))
        self.assertEqual(["far_foot_overlay", "near_foot_overlay", "far_paw_overlay",
                          "near_paw_overlay", "equipment", "body"],
                         [layer["role"] for layer in depth_assembly["layers"]])
        bad = json.loads(json.dumps(spec)); bad["layers"][0]["role"] = bad["layers"][1]["role"]
        spec_path.write_text(json.dumps(bad), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "exactly once"):
            pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))

        far_attempt = pipeline.load_json(self.store.record("attempts", components[0][1]))
        far_mask_path = self.store.absolute(far_attempt["artifacts"]["mask"]["path"])
        contaminated = Image.open(far_mask_path).convert("RGBA")
        contaminated.putpixel((120, 225), pipeline.MASK_COLORS["near_foot"])
        contaminated.save(far_mask_path)
        spec_path.write_text(json.dumps(spec), encoding="utf-8")
        with self.assertRaisesRegex(pipeline.PipelineError, "foreign semantic label"):
            pipeline.create_assembly(self.store, self.ns(spec=str(spec_path)))

    def test_death_recipe_cli_is_retired_but_reviewed_import_promotes_exact_bytes(self):
        parser = pipeline.build_parser()
        self.assertNotIn("render-death-recipe", parser._subparsers._group_actions[0].choices)
        contract, _job, _job_args = self.contract_and_job()
        source = self.png("Tools/artworks/concepts/death-source.png")
        candidate_path = self.root / "Tools/artworks/candidates/death.png"; candidate_path.parent.mkdir(parents=True)
        candidate = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); ImageDraw.Draw(candidate).ellipse((73, 81, 182, 174), fill=(80, 70, 65, 255)); candidate.save(candidate_path)
        preview_path = self.root / "Tools/artworks/candidates/death_128.png"
        preview = Image.new("RGBA", (128, 128), (0, 0, 0, 0)); ImageDraw.Draw(preview).ellipse((36, 40, 91, 87), fill=(80, 70, 65, 255)); preview.save(preview_path)
        comparison = pipeline.render_size_comparison(self.store, self.ns(
            identity=str(source), previous=str(candidate_path), reference=str(candidate_path),
            candidate=str(candidate_path), output="Tools/artworks/reviews/death-size.png"))
        adopted = pipeline.adopt_reviewed_sprite(self.store, self.ns(
            contract_id=contract["contractId"], source=str(source), candidate=str(candidate_path), preview=str(preview_path),
            size_comparison=comparison["artifact"]["path"], reviewer="cty41", reason="visual size accepted",
            accepted_at="2026-08-21T00:00:00+08:00"))
        attempt_id = adopted["attempt"]["attemptId"]
        pipeline.decide(self.store, self.ns(attempt_id=attempt_id, reviewer="cty41", reason="visual size accepted",
                                             decided_at="2026-08-21T00:00:00+08:00"), "approved")
        master_output = self.store.absolute(contract["outputs"]["master"]); master_output.parent.mkdir(parents=True, exist_ok=True)
        preview_output = self.store.absolute(contract["outputs"]["preview"]); preview_output.parent.mkdir(parents=True, exist_ok=True)
        master_output.write_bytes(candidate_path.read_bytes()); preview_output.write_bytes(preview_path.read_bytes())
        pipeline.register_supporting_artifact(self.store, self.ns(path=str(master_output), role="pre-promotion-copy", note="upgrade regression"))
        pipeline.register_supporting_artifact(self.store, self.ns(path=str(preview_output), role="pre-promotion-copy", note="upgrade regression"))
        promoted = pipeline.promote(self.store, self.ns(attempt_id=attempt_id))
        self.assertEqual(pipeline.sha256_file(candidate_path), promoted["artifacts"]["promoted"]["master"]["sha256"])
        self.assertEqual(pipeline.sha256_file(preview_path), promoted["artifacts"]["promoted"]["preview"]["sha256"])
        self.assertTrue(pipeline.strict_check(self.store, True)["ok"])

    def test_reviewed_import_rejects_non_human_or_off_center_candidate(self):
        contract, _job, _job_args = self.contract_and_job()
        source = self.png("Tools/artworks/concepts/source.png")
        candidate_path = self.root / "Tools/artworks/candidates/off-center.png"; candidate_path.parent.mkdir(parents=True)
        candidate = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); ImageDraw.Draw(candidate).ellipse((10, 10, 80, 80), fill=(80, 70, 65, 255)); candidate.save(candidate_path)
        preview_path = self.root / "Tools/artworks/candidates/preview.png"
        Image.new("RGBA", (128, 128), (0, 0, 0, 0)).save(preview_path)
        review = self.root / "Tools/artworks/reviews/review.png"; review.parent.mkdir(parents=True); Image.new("RGBA", (576, 176), (32, 32, 32, 255)).save(review)
        args = self.ns(contract_id=contract["contractId"], source=str(source), candidate=str(candidate_path), preview=str(preview_path),
                       size_comparison=str(review), reviewer="agent", reason="invalid", accepted_at="2026-08-21T00:00:00+08:00")
        with self.assertRaisesRegex(pipeline.PipelineError, "reviewer must be cty41"):
            pipeline.adopt_reviewed_sprite(self.store, args)
        args.reviewer = "cty41"
        comparison = pipeline.render_size_comparison(self.store, self.ns(
            identity=str(source), previous=str(candidate_path), reference=str(candidate_path), candidate=str(candidate_path),
            output="Tools/artworks/reviews/review.png"))
        args.size_comparison = comparison["artifact"]["path"]
        with self.assertRaisesRegex(pipeline.PipelineError, "centered or use the artwork baseline"):
            pipeline.adopt_reviewed_sprite(self.store, args)

    def test_normalize_reviewed_sprite_preserves_visible_pixels_and_clears_transparent_rgb(self):
        source = self.root / "Tools/artworks/candidates/source.png"; source.parent.mkdir(parents=True)
        image = Image.new("RGBA", (256, 256), (12, 34, 56, 0)); ImageDraw.Draw(image).ellipse((73, 81, 182, 174), fill=(80, 70, 65, 255)); image.save(source)
        result = pipeline.normalize_reviewed_sprite(self.store, self.ns(
            source=str(source), output="Tools/artworks/candidates/normalized.png",
            preview="Tools/artworks/candidates/normalized_128.png"))
        normalized = Image.open(self.store.absolute(result["artifacts"]["candidate"]["path"])).convert("RGBA")
        self.assertEqual((0, 0, 0, 0), normalized.getpixel((0, 0)))
        self.assertEqual(image.getpixel((128, 128)), normalized.getpixel((128, 128)))
        with Image.open(self.store.absolute(result["artifacts"]["preview"]["path"])) as preview:
            self.assertEqual((128, 128), preview.size)

    def test_register_runtime_copy_requires_identical_approved_source(self):
        contract, _job, _job_args = self.contract_and_job()
        source = self.store.absolute(contract["anchor"]["path"])
        pipeline.register_public_artifacts(self.store, [contract["anchor"]], "project-owned-gpt-generated")
        target = self.root / "godot/assets/units/hero.png"; target.parent.mkdir(parents=True); target.write_bytes(source.read_bytes())
        result = pipeline.register_runtime_copy(self.store, self.ns(source=str(source), target=str(target)))
        self.assertEqual(result["source"]["sha256"], result["target"]["sha256"])
        Image.new("RGBA", (256, 256), (1, 2, 3, 255)).save(target)
        with self.assertRaisesRegex(pipeline.PipelineError, "byte-identical"):
            pipeline.register_runtime_copy(self.store, self.ns(source=str(source), target=str(target)))

    def test_reviewed_import_requires_native_rgba_and_matching_comparison_candidate(self):
        contract, _job, _job_args = self.contract_and_job()
        source = self.png("Tools/artworks/concepts/source.png")
        first = self.root / "Tools/artworks/candidates/first.png"; first.parent.mkdir(parents=True)
        second = self.root / "Tools/artworks/candidates/second.png"
        for path, color in ((first, (80, 70, 65, 255)), (second, (90, 80, 75, 255))):
            image = Image.new("RGBA", (256, 256), (0, 0, 0, 0)); ImageDraw.Draw(image).ellipse((73, 81, 182, 174), fill=color); image.save(path)
        preview = self.root / "Tools/artworks/candidates/preview.png"
        image = Image.new("RGBA", (128, 128), (0, 0, 0, 0)); ImageDraw.Draw(image).ellipse((36, 40, 91, 87), fill=(80, 70, 65, 255)); image.save(preview)
        comparison = pipeline.render_size_comparison(self.store, self.ns(
            identity=str(source), previous=str(first), reference=str(first), candidate=str(first),
            output="Tools/artworks/reviews/size.png"))
        args = self.ns(contract_id=contract["contractId"], source=str(source), candidate=str(second), preview=str(preview),
                       size_comparison=comparison["artifact"]["path"], reviewer="cty41", reason="test",
                       accepted_at="2026-08-21T00:00:00+08:00")
        with self.assertRaisesRegex(pipeline.PipelineError, "Candidate does not match"):
            pipeline.adopt_reviewed_sprite(self.store, args)
        args.candidate = str(first)
        Image.new("RGB", (128, 128), (0, 0, 0)).save(preview)
        with self.assertRaisesRegex(pipeline.PipelineError, "must be 128x128 RGBA"):
            pipeline.adopt_reviewed_sprite(self.store, args)


if __name__ == "__main__":
    unittest.main()
