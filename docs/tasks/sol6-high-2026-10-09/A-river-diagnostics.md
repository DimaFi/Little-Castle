# [Sol 6 High][P1] Diagnose missing rivers and validate concept-only source selection

Executor gpt-6-sol/high, manual selection. Base and constraints: docs/handoffs/sol6-high-followups-2026-10-09.md; draft PR #20 head66e9a41. Follow-up to #8, not a replacement of existing umbrella tasks.

Evidence: actual 1024m seeds12345/54321 have zero rivers, so min1 fixed bridge fails. -10101 has2 rivers; 3072m seed12345 has1. Source grid480m/chance.65, class RollingHills|Highlands, height>=8; trace minimum5 points. Exact failing filter is not yet measured.

## Exclusive files/classes
- Assets/_Game/Scripts/World/Macro/RiverNetworkPlanner.cs and RiverPlannerSettings.cs.
- New Macro/RiverGenerationDiagnostics.cs and Tests/Editor/RiverGenerationDiagnosticsTests.cs (+meta).
- Assets/_Game/Settings/World/ConceptWorld_v001/ConceptMacroWorldPlannerSettings.asset, only reviewed river parameters.
- New Editor/ConceptRiverSourceAudit.cs and docs/reports/concept-river-sources.md.

Contract: additive optional diagnostic overload preserving existing BuildRivers signature/results when diagnostics off; no MacroWorldPlan, probe API, bridge dimensions or validator edits. Counter collection cannot consume RNG or reorder candidates.

## Work and acceptance
Count source grid, chance/bounds/class/height/slope rejections, eligible sources, trace lengths/termination, accepted rivers. Include IDs/representative samples with bounded output. Determine cause before tuning. Test on in-memory concept clones first; no synthetic channel or reduced bridge minimum. Tune only justified concept parameters, preserve legacy/default generation outputs; document intentional seed-version changes.
Tests: diagnostics on/off identical rivers/IDs, same-seed repeat, negative bounds, empty/no-eligible/short-trace cases. Actual audit seeds12345/54321/-10101/777 at1024/3072m when Unity available; memory/time per run. Unusable seeds must return explicit failure, not invented river.

Parallel B/C/E with no shared writes. D depends on accepted A+B outputs. No foreign edits without reassignment. Report root cause, settings experiments, actual counts/tests/commit, remaining failures. If Unity unavailable, deliver source tests/audit and exact desktop commands, not fabricated PASS.
