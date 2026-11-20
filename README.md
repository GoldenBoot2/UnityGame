# Block Dodge

A small Unity game for Android: drag left and right to dodge the falling blocks. Each block that passes scores a point, and the best score is kept.

- Unity 6.3 LTS (6000.3.29f1). The scene, sprites, icon and splash are all created in code (`Assets/Scripts`, `Assets/Editor/BuildScript.cs`).
- Built by GitHub Actions with [GameCI](https://game.ci) (`.github/workflows/android.yml`). The APK is uploaded as a workflow artifact.
- Package: `com.goldenboot.blockdodge`, Android 8.0+ (API 26), arm64 + armv7.

## Building

The workflow needs three repository secrets: `UNITY_LICENSE` (the contents of the `.ulf` license file), `UNITY_EMAIL` and `UNITY_PASSWORD`. Locally, open the project in Unity 6000.3.29f1 and use **Block Dodge > Build Android APK**.

Developer: GoldenBoot
