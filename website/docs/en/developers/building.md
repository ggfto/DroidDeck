# Building from source

## Requirements

- Windows 10 1809+ with the **.NET 8 SDK**.
- **Flutter** (stable channel) for the app; Android SDK and JDK 17 for the APK.

## Backend

```powershell
# run (tray app + server at http://localhost:4787)
cd RaspDeck
dotnet run

# build the solution
dotnet build DroidDeck.sln -c Debug
```

!!! warning
    Don't use `dotnet build -o <dir>` on the backend project: it corrupts `obj/` and produces an
    assembly without its resources, and the app closes right after starting.

Set `ASPNETCORE_ENVIRONMENT=Development` to enable Swagger at `/swagger`.

## App

```powershell
cd app

# web configurator -> builds and copies to RaspDeck/wwwroot
..\scripts\deploy-web.ps1

# Android APK
flutter build apk --debug --target-platform android-arm64
adb install -r build\app\outputs\flutter-apk\app-debug.apk
```

## Releases

Releases are made by the **Release** workflow (`.github/workflows/release.yml`), started manually
from **Actions → Release → Run workflow**. It uses **semantic-release**, so the version comes from
the commits ([Conventional Commits](https://www.conventionalcommits.org)):

| Commit | Version bump |
|---|---|
| `feat:` | minor |
| `fix:`, `perf:` | patch |
| `feat!:` or `BREAKING CHANGE` | major |

It updates `CHANGELOG.md`, creates the tag and the GitHub Release, and attaches
`DroidDeck-win-x64.zip` (self-contained backend with the web configurator embedded) and `DroidDeck.apk`.
If there is no releasable commit since the last version, nothing is published.

## This website

The landing page (`website/landing/`) and this documentation (`website/docs/`, MkDocs Material, English
and Portuguese) are published to GitHub Pages by `.github/workflows/pages.yml` on every push to `main`
that touches `website/`.

```powershell
pip install -r website/requirements.txt
cd website
mkdocs serve        # http://127.0.0.1:8000/DroidDeck/docs/
```

Pages live in `website/docs/en/` and `website/docs/pt/` with the same file names. When you add a
page, add it to `nav` in `website/mkdocs.yml` and create it in both languages.
