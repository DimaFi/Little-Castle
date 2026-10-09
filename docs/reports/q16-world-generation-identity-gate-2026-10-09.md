# Q16 — World generation identity and save validation gate

Date: 2026-10-09. Issue #45:
https://github.com/DimaFi/Little-Castle/issues/45

Branch: gpt6/q16-generation-identity-gate-2026-10-09
Base: codex/concept-world-pass-2026-10-09 at e6a02df6f57461df9cd7cf853e8e9d62b776d71a.

**Source and tests authored only. Unity compilation, EditMode/PlayMode,
actual save/reload, multiplayer and Player performance have NOT been run.**

## Why this source-only PR

A deterministic world generated from a seed must not silently apply
runtime removals, depleted deposits, buildings or chunk revisions
from a different world configuration. The existing
WorldRuntimeDeltaState is authoritative for changes, while
WorldGenerationSettings has ProfileId/GenerationVersion and
WorldDefinition has WorldId.

The root owner has NOT YET approved the canonical serialization of
all gameplay-affecting settings. This PR therefore creates an
additive **fail-closed gate**, NOT a shipped save-format migration.

Owned diff:
- NEW Assets/_Game/Scripts/World/Core/WorldGenerationIdentity.cs + .meta
- NEW Assets/_Game/Scripts/World/Core/WorldGenerationIdentityValidator.cs + .meta
- NEW Assets/_Game/Tests/Editor/GenerationIdentityTests.cs + .meta
- This report

No changes to current save/delta schema, gameplay, WorldStreamer,
Main, scenes, resources, prefab assets, world settings, rendering,
or the user's parallel Q08 rock/scarp work.

## Compact identity contract

WorldGenerationIdentity includes:
- worldId: 1–128 ASCII-safe letters/digits/underscore/dash/dot;
- profileId: same rules;
- generationVersion: >= 1;
- worldSeed: signed int32;
- settingsSha256: exactly 64 lowercase hex characters.

Its invariant-culture compact format is:

    LCW1|worldId|profileId|generationVersion|worldSeed|settingsSha256

TryParse returns false on malformed, unexpected versions or overflow.
ToCompactString refuses invalid/uninitialized data.

WorldGenerationSettingsFingerprint.ComputeApprovedManifestSha256
takes NONEMPTY ROOT-APPROVED canonical manifest bytes, applies
UTF8 domain separation for manifest format v1 and computes SHA-256.
It does NOT obtain those bytes by automatically serializing
ScriptableObjects, Unity GUIDs, local files, visuals or changing
runtime state. The test manifest is fixture data, not approved content.

WorldGenerationIdentityValidator.Validate(saved,current) rejects:
1. missing current or malformed saved identity;
2. wrong world ID;
3. wrong generator profile ID;
4. different generation version;
5. different canonical settings hash;
6. different seed.

ValidateSavedCompact additionally rejects old saves lacking Q16
metadata with an explicit MissingSavedIdentity reason. It never
assumes that an old save belongs to the current generator. A match
means IDENTITY matches and only permits the subsequent, independent
schema/authority/delta validation. It does not prove visual parity,
security or gameplay compatibility by itself.

CreateCurrentFromApprovedManifest reads the existing
WorldDefinition.WorldId and GenerationSettings.ProfileId /
GenerationVersion plus passed seed and approved manifest bytes.

## REQUIRED root approval before production integration

1. Approve complete versioned canonical gameplay-settings manifest:
   generation-stage enabled/order/parameters, world cell/chunk
   geometry, procedural noise, river profiles and carving, roads,
   bridges, point feature rules/salts, resource/deposit/tree
   placement, player-start/fairness rules and finite map presets
   as applicable. Include AnimationCurve keys in stable order and
   specify locale-invariant float/enum encoding.
2. Exclude visual-only shader/materials, machine timestamps,
   environment-specific AssetDatabase paths/GUIDs, prefab instance
   IDs or string.GetHashCode(). The host and clients need
   identical manifest bytes when they share a deterministic world.
3. Decide how to migrate older saves lacking identity metadata.
   Never silently attach an old delta to current terrain.
4. Root connects identity validation **before** applying any
   WorldRuntimeDeltaState/WorldChunkStateSnapshot. Q16 does not
   alter serialization or any currently saved data.
5. Test same seed+version+manifest under actual world replay,
   negative-coordinate eviction/revisit, removed trees,
   depleted resources and player buildings. Verify a new version,
   different seed or manifest refuses to load old delta and
   preserves the original save unmodified.
6. Use Q15 preset regression evidence before testing large
   1024/3072/6144 and 2/8/16-player multiplayer matrices.
   A successful identity comparison is NOT a unit-navigation or
   multiplayer-balance guarantee.

## EditMode tests authored, NOT RUN

The source includes 14 NUnit test methods with 18 additional
parameterized case instances/attributes (inspect Test Runner XML
for actual executed case count). Coverage:
- deterministic full SHA-256 with domain separation;
- missing/empty manifest refused;
- invalid version, world/profile names, hashes refused;
- exact compact roundtrip across extreme positive/negative seeds;
- malformed or unknown legacy identity refused;
- typed world/profile/version/hash/seed mismatch reports;
- exact matching identity only permits next schema checks;
- real saved concept profile + fixture manifest adapter;
- snapshot apply after matching identity retains removed tree and
  runtime building record;
- changed seed refuses the old delta without mutating the state.

Unity 6000.5.5f1 (separate process, no -quit with -runTests):

    -batchmode -nographics -projectPath <root> -runTests -testPlatform EditMode -testFilter LittleCastle.Tests.GenerationIdentityTests -testResults <Logs/Q16-Identity.xml> -logFile <Logs/Q16-Identity.log>

Then run WorldRuntimeReplicationTests, world generation and finite
session tests, full EditMode, PlayMode and real save/reload acceptance.
Inspect NUnit XML and Unity compilation/import logs: a source
test without an executed XML PASS is not verification.

## Verification and risk

- Source, owned file scope and project existing APIs: reviewed.
- Core identity, validator and NUnit source: AUTHORED.
- Strict GitHub diff: to be inspected at PR creation.
- Unity compilation/New NUnit/PlayMode: NOT RUN HERE.
- Canonical manifest versioned and approved: NOT DONE.
- Real serialization/loading or legacy migration: NOT DONE.
- Persistent delta after real WorldStreamer eviction: NOT RUN.
- GPU/Player FPS and gameplay correctness: NOT RUN.

Prior desktop 175/175 EditMode and 5/5 PlayMode were obtained
on a different commit and do not validate Q16.

Keep issue OPEN and PR DRAFT until root/Codex has chosen the
canonical manifest and run real Unity acceptance.
