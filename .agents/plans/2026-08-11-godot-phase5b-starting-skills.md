# Godot Phase 5B: Starting Skills

This active plan migrates the three Pure Run starting Lv1 branches, two base attacks, and Amazon spear pickup into a deterministic engine-neutral skill runtime. Poison Spear remains externally owned by its validated batch. Skill visuals are audit-only; summon AI, Run/Persistence, upgrade UI, Input, and formal Presentation remain later-phase work.

The implementation checkpoints are frozen Unity contracts, generic Core/Application runtime, and ResourceSaver-generated Godot content plus a native 1600x900 gameplay fixture. The batch remains `Generated/UnityOwned + manual_gameplay_qa_pending` until the user accepts the fixture and assembly-reload smoke.
