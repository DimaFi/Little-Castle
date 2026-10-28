# Graphics/generation continuation — 2026-10-09

Additional **Sol 6 High** GitHub work items were created before local work:
[macro probe cost #21](https://github.com/DimaFi/Little-Castle/issues/21),
[river sources #22](https://github.com/DimaFi/Little-Castle/issues/22),
[realizable connectivity #23](https://github.com/DimaFi/Little-Castle/issues/23),
[streamed scene #24](https://github.com/DimaFi/Little-Castle/issues/24),
[terrain masks #25](https://github.com/DimaFi/Little-Castle/issues/25).
Exact file ownership, public contracts, dependencies, tests and report requirements:
`docs/handoffs/sol6-high-followups-2026-10-09.md`. Tasks21/22/23/25 may run in
parallel;24 follows their accepted integration. Manual model selection is required;
GitHub issues themselves do not launch agents. Queue is also linked from issue13.

One **gpt-5.6-sol / xhigh** agent performed only night-fixture palette refinement;
root reviewed and ran Unity. Real GPU close day/night captures and repeat-run
scene-hash idempotence passed. Focused relevant EditMode **31/31**, no skipped
tests. Day endpoint settings are unchanged; night bridge/road visibility improved.
See `concept-night-refinement-2026-10-09.md` and its new v2 evidence directory.
Existing evidence was preserved. Full concept visual approval remains FAIL.

Root added an Editor-only read-only river eligibility audit and ran eight
in-memory source experiments. For saved1024m seeds12345/54321, no source reaches
tracing: all sampled sources fail the class filter; admitting Plains reveals
height<8 rejection instead. Chance1 creates a real river only for54321 with480m
spacing;320m sampling loses that source. No runtime/profile/terrain/seed change
was made. Actual counters/settings/timings: `concept-river-eligibility-2026-10-09.json`.
Task22 was updated with these findings, so its executor can skip repeated diagnosis.

No complete-world/performance re-audit or fresh PlayMode claim this turn: only
the small changed scope was compiled/rendered/regression-tested. Main, shared
shaders, immutable bridge release and its dimensions are unchanged. Continuation
commits stay on existing draft PR20; umbrella issues remain open.
