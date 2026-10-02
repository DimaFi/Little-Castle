# ADR-0003: Authoritative variable-rate world clock

## Status
Accepted.

## Decision

Use a single authoritative `WorldClock` with separate real-time day/night
rates while preserving one continuous 24-hour game timeline.

Defaults:
- 06:00-20:00 in 360 real seconds;
- 20:00-06:00 in 90 real seconds;
- full cycle in 450 real seconds.

Night never pauses the match or forces a sleep skip. Gameplay systems consume
game-time delta from the clock. Lighting reads the clock but is not
authoritative. Save/load stores clock state. Future multiplayer host/server
owns the clock and clients consume snapshots.

## Consequences

This keeps crops, production and future NPC needs correct even when night runs
faster than day, and gives save/load and multiplayer one clear authority
boundary. The cost is that in-world elapsed-hour mechanics must not derive
their progression directly from wall-clock `Time.deltaTime`.
