# Concept river eligibility — 2026-10-09

Actual Unity6000.5.5f1 executeMethod `LittleCastle.Editor.ConceptRiverEligibilityAudit.Run`, exit0. New Editor-only diagnostic; no runtime algorithm, saved profile, seed or terrain changed. JSON companion contains eight real in-memory experiments. Read-only eligibility mirror follows RiverNetworkPlanner source-grid/filter order, then calls the real BuildRivers. It must be kept in sync if that prelude changes; task #22 should eventually integrate real planner counters instead.

1024m centered bounds +384m halo; saved spacing480/chance.65/class RollingHills|Highlands/minHeight8. First-rejection counters reflect short-circuit order, not independent causes.

| Seed | Experiment | Eligible / actual rivers | Observed exclusion |
|---|---|---:|---|
| 12345 | saved | 0 / 0 | All8 in-bounds sampled sources rejected by class |
| 12345 | chance1 | 0 / 0 | All16 rejected by class |
| 12345 | chance1, spacing320 | 0 / 0 | All31 rejected by class |
| 12345 | saved + allow Plains | 0 / 0 | All8 now fail height<8 instead |
| 54321 | saved | 0 / 0 | All10 sampled sources rejected by class |
| 54321 | chance1 | 1 / 1 | One eligible source creates a real traced river |
| 54321 | chance1, spacing320 | 0 / 0 | All31 rejected by class; changed grid misses the prior source |
| 54321 | saved + allow Plains | 0 / 0 | All10 now fail height<8 instead |

Thus the baseline absence is proven to occur **before tracing** for these two maps. A mere source-density increase or admitting Plains is not a reliable fix: the class and absolute-height gates both matter, and changing spacing changes deterministic source positions. No saved parameters were adjusted and minimum bridge/connectivity validation remains strict.

Next task #22: sample actual regional relief/eligible-source distribution, evaluate a justified concept-only source policy against the rounded low terrain; keep genuine downhill tracing, bounded failures and explicit generation-version changes. Then integrate #23 route recovery and re-run complete seeded connectivity, not only river count.

Exact final timings are in JSON. Trace timing uses a probe warmed by eligibility sampling: **not cold macro generation, FPS or a memory benchmark**. No bridges/road paths/player starts tested by this tool. Full-world acceptance remains FAIL from desktop report.
