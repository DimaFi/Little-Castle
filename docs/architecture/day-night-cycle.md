# Day / Night Cycle

Little Castle uses one continuous authoritative simulation clock. Night is not
a pause, sleep screen or global time skip: the settlement remains controllable
and simulation continues.

## Default pacing

| Period | Game time | Real time |
|---|---:|---:|
| Day | 06:00-20:00 | 360 s / 6 min |
| Night | 20:00-06:00 | 90 s / 1 min 30 s |
| Full cycle | 24 h | 450 s / 7 min 30 s |

Night therefore advances game time about 2.86x faster than day.

## Responsibilities

- `WorldClock` is pure authoritative simulation state.
- `WorldTimeSystem` advances the clock in Unity and publishes game-time delta.
- `DayNightLightingController` is presentation only and moves the sun / ambient light.
- `WorldTimeSettings` stores pacing and boundaries and is referenced by `WorldDefinition`.

The main profile is `Assets/_Game/Settings/World/MainWorldTimeSettings.asset`.

## Consumer contract

Gameplay systems must use authoritative game-time progress rather than
`Time.deltaTime` for in-world elapsed-time mechanics.

Use `WorldTimeSystem.GameHoursAdvancedLastTick` or subscribe to
`WorldTimeSystem.TimeAdvanced`.

This applies to crops, production, food, resident energy, sleep debt, work
schedules, taxes and timed events. It matters because one real second advances
a different number of game minutes during day and night.

## Resident night work

Night does not hard-disable work. Future resident AI should prefer rest when
appropriate, but player orders, emergencies, combat and transport may continue.

Repeated missed rest should accumulate sleep debt and feed into happiness and
eventually efficiency. A single emergency night should not cause a severe
permanent penalty. The time subsystem only exposes time; resident mood belongs
to the population subsystem.

## Presentation

- The sun physically goes below the horizon at night.
- Direct sun intensity reaches zero at night.
- Ambient night light remains intentionally readable for a management game.
- Dawn and dusk currently transition over roughly two game hours.
- Moon, stars, windows and lanterns are presentation layers and can be added
  without changing authoritative time.

## Save/load and multiplayer

Save `WorldTimeState.dayIndex` and `WorldTimeState.minuteOfDay`.
Do not save sun rotation as authoritative state.

Future multiplayer host/server owns the `WorldClock`. Clients disable local
automatic advancement, consume authoritative snapshots and interpolate visual
presentation locally.
