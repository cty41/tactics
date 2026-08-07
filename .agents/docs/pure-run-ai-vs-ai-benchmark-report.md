# Pure Run AI-vs-AI Benchmark Report

## Scope

Task 15 executed N1-N6 three times each on the fixed 10x10 production encounter path. The party seed was `20260804`; simulation seeds were `101`, `202`, and `303`. No manual operation, direct damage, forced turn end, or synthetic battle completion was used.

Player policy was explicitly recorded as a proxy:

- Mage: `AOEBrain`
- Necromancer: `SupportBrain`
- Amazon: `RangedBrain`

Enemy units retained their production encounter brains.

## Verified result

Unity PlayMode job `b9e1b713ddc340d2b35a1c6a36e887ce` executed one explicit benchmark fixture and reached its terminal result: 1 total, 0 passed, 1 failed. All 18 requested battle samples were written to a local baseline CSV before the fail-closed assertion; that generated CSV is intentionally not committed because it is failed diagnostic output.

Each sample now cancels and drains every `AIPlayer.Play()` continuation before destroying the battle world. The cancellation lifecycle contract is covered by RED job `64282a1fcd684ca895b2aa4af8d9d9a2`, GREEN job `31d14520665f4884a211016c12673010`, and the complete `GameTimeServiceSpeedTests` fixture job `1d5aba32366546dab8c01339e7c9fb15` (20/20). Fixture teardown restores global random/time/asset state in a `finally` block.

| Metric | Verified value |
|---|---:|
| Requested samples | 18 |
| Recorded samples | 18 |
| Natural completions | 0 |
| Timeouts | 18 |
| Manual-operation samples | 0 |
| Synthetic completions | 0 |
| Recorded round at timeout | 1 for all samples |
| Player casualties at timeout | 0 for all samples |

The 15-second per-sample timeout is a harness guard, not a claim that combat should complete within 15 seconds. It establishes that the current proxy cannot produce a practical natural-completion dataset.

## Threshold decision

**RED — unusable proxy.** The required 4-6 complete-round median cannot be calculated because completion rate is 0%. The recorded median of `current_round=1` is censored timeout data and must not be interpreted as a one-round battle result.

No HP, damage, range, mana, or enemy-count tuning was applied. Adjusting production balance from censored proxy data would be unsupported.

## Initial attackability observation

The production intent pipeline reported:

- N2 and N3: enemies had one distinct initially attackable player target.
- N1, N4, N5, and N6: neither side had an initially attackable target.
- The player proxy had zero initially attackable targets in all 18 samples.

These values describe position-zero intent legality only; they do not substitute for complete battle outcomes.

## Required follow-up

A valid automatic balance gate requires an authored player-class benchmark policy that uses Mage, Necromancer, and Amazon skills intentionally and can complete encounters without manual input. Until such a policy exists, N1-N6 balance remains a manual playtest gate; this AI dataset is diagnostic evidence only.
