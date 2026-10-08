# Original verification evidence

Copied without editing from the local Unity 6000.5.5f1 batch runs after bridge
and landform implementation. This is a historical baseline, not a new green
result and not an in-game bridge visual approval.

- EditMode.xml: 78 total, 76 passed, 2 failed; all 16 new tests passed.
- PlayMode.xml: 1 total, 0 passed, 1 failed.
- Compilation/profile creation: passed after fixing NUnit Assert.Multiple
  compatibility in the new landform test.

See `../bridge-world-v002-2026-10-08.md` for the three failing test names.
Future runs must add dated evidence without overwriting these XML files.
Raw Unity/licensing/editor logs remain local and are not published.
