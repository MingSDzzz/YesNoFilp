# AGENTS.md

## Scope

This repository is the standalone **Decision Disc / YES NO Flip** Unity project.

## Hard boundaries

- Do not read, copy, reference, or modify any `external-local-project` project or its assets.
- Keep the project self-contained under this repository root.
- Use Unity `2022.3.62f2c1` for builds.
- Never persist an unsaved question or throw result. History is written only after the user presses **Save this record**.
- Badge images selected by the user must be copied into `Application.persistentDataPath`, so deleting the original source file does not remove them from the app.
- User-facing copy should be Simplified Chinese unless a specific feature explicitly requires another language.

## Product and UI

- Target a modern Android mobile visual language; avoid default, retro, or desktop-like uGUI styling.
- A custom badge is incomplete until both its YES face and NO face have app-owned image copies. Incomplete badges must remain editable but cannot be selected for throwing.
- Badge management must expose both face previews and allow renaming, replacing either face, selecting, and deleting each custom badge.
- During a throw, alternate the current badge's real YES and NO faces as the disc rotates.
- The history page must distinguish session-only in-memory throws from explicitly saved persistent records.
- Diagnostic operation logs stay in memory by default and are written only after the user explicitly requests an export.

## Android signing

- Release APKs must use the project-specific local keystore configured under `.signing`; never commit the keystore or its password file.
- Preserve and back up the same signing key for future upgrades. Do not silently replace an existing key.
- Before reporting a successful Android release, verify the non-empty APK, version code/name, launchable activity, declared permissions, and signing certificate with Android build tools.

## Validation

- Run the editor validation method `DecisionDisc.Editor.DecisionDiscBuild.ValidateProject` in batch mode after code changes.
- Build Android through `Tools/Decision Disc/Build APK` or `DecisionDisc.Editor.DecisionDiscBuild.BuildApk`.
- A successful command invocation is not a successful Android build unless the APK exists and has non-zero length.

## Git

- Commit generated `.meta` files alongside their assets.
- Do not commit `Library`, `Logs`, `UserSettings`, APKs, or other generated build outputs.
