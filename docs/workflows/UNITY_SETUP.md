# Unity Project Setup

## Canonical project location

Open **the repository root itself** as the Unity project:

```text
Little-Castle/
├─ Assets/
├─ Packages/
├─ ProjectSettings/
├─ docs/
├─ README.md
└─ AGENTS.md
```

Do **not** create:

```text
Little-Castle/UnityProject/Assets
```

unless the repository is intentionally migrated.

## First-time setup

1. Clone `DimaFi/Little-Castle`.
2. In Unity Hub choose **Add project from disk**.
3. Select the repository root.
4. If Unity requires a valid project structure first, create a temporary Unity project with the chosen Unity version, then copy/merge only:
   - `Packages/`
   - `ProjectSettings/`
   - any required root Unity files
   into this repository.
5. Keep the existing `Assets/_Game/` folder.
6. Let Unity import the project.
7. Commit Unity-generated `*.meta` files.
8. Commit `Packages/manifest.json`, `Packages/packages-lock.json` and `ProjectSettings/`.
9. Never commit `Library/`, `Temp/`, `Logs/` or `UserSettings/`.

## Unity version

Once a Unity version is chosen for active development, treat it as project-wide.

Codex must not silently upgrade the Unity version.

Any Unity version migration should be deliberate and documented.

## Project-owned content

All Little Castle-specific content should normally live under:

```text
Assets/_Game/
```

This keeps project code and assets separate from third-party packages/imports.

## Moving assets

After Unity generates `.meta` files:
- move/rename assets inside the Unity Editor where practical;
- commit the corresponding `.meta` files;
- avoid deleting/regenerating GUIDs unnecessarily.

Broken GUIDs can disconnect:
- materials;
- prefabs;
- scenes;
- ScriptableObject references;
- animation/controller references.

## Codex rule

Before changing the Unity project structure, Codex must read:

- `AGENTS.md`
- `docs/PROJECT_STRUCTURE.md`
- this document

If a change affects the canonical layout, update the documentation in the same change.
