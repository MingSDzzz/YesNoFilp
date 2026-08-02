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
- Every badge, including a newly created badge, starts with usable generated YES/NO text faces and a 50% base YES probability. User images are optional per-face replacements, not a prerequisite for selection.
- Creating a badge must first prompt for its name, then immediately add it visibly to the top of the list without forcing an image picker. The list must provide separate upload/update controls for both faces and a path into per-badge detail settings.
- Each badge has an editable base YES probability from 0% through 100%, defaulting to 50%. Fair mode ignores it and remains exactly 50/50; strength-influenced mode adjusts around that badge-specific base probability, while 0% must remain guaranteed NO and 100% guaranteed YES.
- The home page must show the active badge and provide a direct route to switch badges. Selecting a complete badge from the list must update the active badge immediately.
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
- Name generated APKs with the application version suffix, for example `YesNoFilp-v1.2.2.apk`.

## Git

- Commit generated `.meta` files alongside their assets.
- Do not commit `Library`, `Logs`, `UserSettings`, APKs, or other generated build outputs.
