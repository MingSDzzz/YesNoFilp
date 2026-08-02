# Decision Disc (YES / NO Flip)

A private-first Android decision helper built from scratch with Unity `2022.3.62f2c1`.

## Features

- Four portrait pages: throw, badge manager, saved history, and settings.
- Enter a question, hold to charge, then release to throw an animated two-sided disc.
- Uses real Android touch pressure when available. Otherwise it combines hold duration, touch radius/area, and release movement speed into a simulated strength.
- **Fair 50/50** mode: strength is mixed into the random entropy but does not bias either face.
- **Strength affects probability** mode: YES probability ranges from 25% to 75% according to strength.
- The current question and throw result live in memory only. They are persisted only when **Save this record** is explicitly pressed.
- Saved history includes question, YES/NO, strength, strength source, mode, timestamp, optional note, and badge.
- Delete records, export versioned JSON, import through Android's system file picker, preview it, then merge or replace.
- Custom badges copy their YES/NO images into `Application.persistentDataPath`; the app remains independent of the original selected images.
- Safe-area-aware UI for portrait phones and display cutouts.

## Data locations

Runtime data is stored below `Application.persistentDataPath`:

- `history-v1.json` — only explicitly saved records.
- `badges-v1.json` — badge metadata.
- `Badges/<badge-id>/yes.*` and `no.*` — private copies of user-selected images.

No current question or unsaved result is automatically written.

## Editor menus

- `Tools/Decision Disc/Setup Android` configures portrait orientation, package identifier, minimum API, IL2CPP/ARM64, and creates the runtime scene.
- `Tools/Decision Disc/Build APK` first validates Android Build Support, SDK, NDK, and OpenJDK, then builds `Builds/DecisionDisc.apk`.

The build command throws a clear error naming every missing component. It reports success only after a non-empty APK exists.

## Command-line validation/build

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2c1\Editor\Unity.exe' `
  -batchmode -quit -projectPath . `
  -executeMethod DecisionDisc.Editor.DecisionDiscBuild.ValidateProject `
  -logFile validate.log

& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2c1\Editor\Unity.exe' `
  -batchmode -quit -projectPath . `
  -executeMethod DecisionDisc.Editor.DecisionDiscBuild.BuildApk `
  -logFile build-android.log
```

## JSON format

Exports use an envelope with `format`, integer `version`, `exportedAtUtc`, and `records`. Import currently accepts version `1`; unsupported versions are rejected before any data changes.

