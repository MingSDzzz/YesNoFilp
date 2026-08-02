# AGENTS.md

## Scope

This repository is the standalone **Decision Disc / YES NO Flip** Unity project.

## Hard boundaries

- Do not read, copy, reference, or modify any `external-local-project` project or its assets.
- Keep the project self-contained under this repository root.
- Use Unity `2022.3.62f2c1` for builds.
- Never persist an unsaved question or throw result. History is written only after the user presses **Save this record**.
- Badge images selected by the user must be copied into `Application.persistentDataPath`, so deleting the original source file does not remove them from the app.

## Validation

- Run the editor validation method `DecisionDisc.Editor.DecisionDiscBuild.ValidateProject` in batch mode after code changes.
- Build Android through `Tools/Decision Disc/Build APK` or `DecisionDisc.Editor.DecisionDiscBuild.BuildApk`.
- A successful command invocation is not a successful Android build unless the APK exists and has non-zero length.

## Git

- Commit generated `.meta` files alongside their assets.
- Do not commit `Library`, `Logs`, `UserSettings`, APKs, or other generated build outputs.

