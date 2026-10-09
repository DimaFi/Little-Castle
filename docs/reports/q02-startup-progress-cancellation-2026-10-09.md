# Q02 — Explicit startup progress and safe cancellation

Date: 2026-10-09. Source author: GPT-6 ChatGPT.
Tracking: [#31](https://github.com/DimaFi/Little-Castle/issues/31).
**STACKED DRAFT dependency**: Q01 PR #53, branch
`gpt6/q01-cooperative-macro-planner-2026-10-09`,
commit `6ab1a610366b6e4c92403db269d1c5b0ca717221`.
This Q02 branch is `gpt6/q02-startup-state-and-cancel-2026-10-09`,
based **on Q01 rather than independently on concept/main**.
Status: source/tests prepared, **NOT** tested in Unity.

## Scope / isolation

Owned changes relative to Q01:
- EDIT `Assets/_Game/Scripts/World/Streaming/WorldStartupWarmupController.cs`
- NEW `Assets/_Game/Scripts/World/Streaming/WorldStartupBootstrapAdapter.cs` + Unity `.meta`
- NEW `Assets/_Game/Tests/Editor/StartupCancellationTests.cs` + Unity `.meta`
- THIS report.

No changes to `WorldStreamer.cs`, `WorldDefinition`,
`Main*`, prefab/catalog, scene, multiplayer protocol, save/delta,
terrain, render materials, shaders or art. Existing local warmup
continues to work if no Q02 bootstrap adapter is attached.
This is **opt-in and not automatically active in the current game**.

## Problem and state machine

Existing startup warmup only checks local visible/near data ring,
and may release strategy-camera input after local preparation.
Future finite multiplayer startup also needs one complete, validated
strategic macro plan and authoritative fair starts before any player
can act.

New `WorldStartupBootstrapAdapter` serializes the stages:
1. **MacroPlanning** — Q01 `MacroPlanningSession.Step`, main thread,
   budgeted for at most `macroUnitsPerTick` units and soft
   `macroMillisecondsPerTick` time. Macro algorithm phases and
   processed grid-candidate count are factual counters; no fake percent.
2. **StartingData** — macro `Result` is available and the root-specific
   `IWorldStartupAreaPreparation.Begin(plan)` has returned.
   Wait for explicit ready boolean AND reported near data progress >= 0.9999.
   The current `WorldStreamer` wrapper may request a priority radius.
3. **VisibleReady** — wait for explicit visible ready boolean AND
   visible readiness >= 0.9999, **while still requiring starting data ready**.
   Data regression moves back to StartingData.
4. **Ready** — sole state setting `CanAcceptGameInput = true`.
5. **Cancelled** — no partial macro plan published, no camera release.
6. **Failed** — capture exception, attempt owned cleanup, never signal match ready.

Progress is always **phase-specific**:
- macro: planning subphase name + count of point candidate cells examined,
  no denominator/weighted global percent;
- starting data: `PreparedDataReadiness01`, only after beginning area;
- visible area: `VisibleAreaReadiness01`, only after beginning area;
- cancelled/failure: explicit terminal labels, no green ready status.

The Q02-aware `WorldStartupWarmupController` exposes
`AttachBootstrap(WorldStartupBootstrapAdapter)`,
`CancelBootstrap()` and `BootstrapPhase`. It calls the session
once per Update, shows phase-specific progress and does **not**
honor the F9 development shortcut while a Q02 adapter is attached.
Legacy F9 skip is now limited to Editor or `Debug.isDebugBuild`
when Q02 is absent, to prevent a release-build bootstrap bypass.
Attaching an adapter after legacy warmup has requested its own
priority radius, or already released the camera, is rejected.

Any early failure or cancellation leaves the camera input locked.
The controller also reasserts the lock for a **late-resolved camera**
while Q02 is active, and logs a failed bootstrap exactly once for
root diagnostics.
`OnDisable` cancels an in-flight attached adapter. The previous
no-adapter startup behavior stays available for old scenes.

## Ownership, cleanup, fail-closed semantics

`IWorldStartupAreaPreparation` defines `Begin(completedPlan)`,
separate prepared/visible readiness, `CompletePreparation()` (release
only temporary prewarm priority **on success** while keeping data/meshes),
and `CancelPreparation()` (release partial preparation on failure/abort).
Cancellation and failure are idempotent: release the current active
preparation once and invoke the optional owner-owned cleanup callback
once (even when `Begin` throws). Cleanup exceptions lead to
**Failed**, without unlocking input.

`WorldStreamerStartingAreaPreparation` is an optional lightweight
wrapper for existing read-only streamer readiness/priority methods.
Its `Begin(plan)` checks **reference equality**:
`ReferenceEquals(streamer.MacroPlan, completedPlan)`.
This prevents a streamer that synchronously generated another plan
from falsely satisfying Q02 readiness.

**Root integration is deliberately NOT performed by Q02:**
the current `WorldStreamer` still initializes its own macro plan
during start when configured. It does not yet provide an accepted
inject-completed-plan API owned by this PR. The root owner must
serialize Q01/Q02/world-start sequence, own streamer initialization
and pass the **same approved macro result**, or design an equivalent
`IWorldStartupAreaPreparation` that safely owns that synchronization.
Attaching Q02 to an unchanged eager-initializing streamer will
**fail closed**, not automatically eliminate the earlier sync spike.

The Q02 cancellation contract does not itself unload chunks,
delete session objects, reset the active game's network session,
or `Clear()` root-owned terrain probe caches. The root's optional
`releaseOwnedResources` callback must own those operations and
must never discard active world state from another match. Test
that no cache, priority radius, pending request or data reference
survives abort.

Do not call `Tick` concurrently, from `Task.Run`, or before
the Unity main-thread bootstrap controller is ready.
Q01's river/road/bridge planner phases remain atomic and can still
cause frame hitches larger than a soft budget. This PR is a
**safe stage/readiness contract**, not a measured no-hitch fix.

## Unit-test coverage (authored, not run)

`LittleCastle.Tests.StartupCancellationTests` contains:
- distinct macro, data and visible gates; only final Ready unlocks;
- dropping data readiness while waiting for visible reverts to data;
- cancellation during macro scanning frees partial session + root callback
  exactly once, then clean re-run belongs to a new session;
- cancellation after macro, before starting-area readiness;
- pre-cancelled macro cannot start area preparation;
- `Begin` failure / readiness provider exception fail closed and release;
- success finalization once; exception from that finalizer fails closed;
- cleanup exception becomes Failed, never Ready;
- NaN / infinity / out-of-range progress cannot manufacture readiness;
- invalid macro work/time budgets rejected at construction;
- real `WorldStreamerStartingAreaPreparation` rejects an unrelated macro
  plan until owner wires the exact completed one.

Tests use synthetic readiness providers; they are **not** real
streaming physics, GPU, network, or multiplayer acceptance.

## Codex desktop acceptance procedure

Checkout the **stacked Q01+Q02 PR tree**, Unity 6000.5.5f1:

```text
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.MacroPlanningSessionTests -testResults <Logs/Q01-Macro.xml> -logFile <Logs/Q01-Macro.log>
-batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.StartupCancellationTests -testResults <Logs/Q02-Startup.xml> -logFile <Logs/Q02-Startup.log>
```

Run these as separate Unity processes and **omit `-quit`**
when running Unity Test Runner. Inspect XML/log counts, not exit code
alone. Also run old world streaming/editor runtime suites,
full EditMode and PlayMode (including current warmup/camera behavior).
Root then creates a tiny **real root-owned integration sandbox**:
- disable/replace eager streamer macro initialization **with explicit
  root agreement**, inject exactly the validated Q01 result;
- construct adapter before legacy warmup releases input;
- verify no gameplay movement during macro, during incomplete
  starting-data prep, or when only visible meshes are ready;
- verify successful readiness releases temporary priority exactly once
  without deleting playable data; failure during this step fails closed;
- verify cancellation at each phase plus missing active chunk,
  negative coordinate start, streamer destroyed/unloaded, and failed
  strict route diagnostics; no camera unlock, stale priority radius,
  leaked data cache or double callback;
- test 2 / 8 / 16 players across two finite maps (separate measured
  budget), only required starting regions prewarmed and no entire
  map presentation; verify distant first visit and revisit state;
- capture duration / maximum stall separately by subphase, real
  Player profiler frame times and memory, not an invented global %.

**Keep both PR #53 and Q02 draft until Unity acceptance.**
Q02 is stacked on Q01: do not try to merge it directly to Main or
independently rebase over a different macro session API.

## Verification / authority

| Gate | Status |
|---|---|
| Q01 API and existing Warmup/Streamer reviewed | DONE |
| New adapter, warmup gate and NUnit test source authored | DONE |
| Scoped GitHub Q02 diff | Pending at report creation |
| Unity compile / Q01 and Q02 NUnit | **NOT RUN** |
| EditMode and PlayMode regressions | **NOT RUN** |
| Real root macro/streamer injection | **NOT ATTEMPTED** |
| Player movement, visible readiness, 2/8/16 session test | **NOT RUN** |
| CPU/GPU/FPS and no-hitch release criteria | **NOT RUN** |

Previously published desktop 175/175 EditMode and 5/5 PlayMode
are from a different revision. They must not be claimed as new Q02
acceptance results.
