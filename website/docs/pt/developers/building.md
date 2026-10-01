# Compilando a partir do código-fonte

## Requisitos

- Windows 10 1809+ com o **.NET 8 SDK**.
- **Flutter** (canal stable) para o app; Android SDK e JDK 17 para o APK.

## Backend

```powershell
# run (tray app + server at http://localhost:4787)
cd RaspDeck
dotnet run

# build the solution
dotnet build DroidDeck.sln -c Debug
```

!!! warning "Atenção"
    Não use `dotnet build -o <dir>` no projeto do backend: isso corrompe o `obj/` e gera um assembly
    sem os recursos, e o app fecha logo depois de iniciar.

Defina `ASPNETCORE_ENVIRONMENT=Development` para ativar o Swagger em `/swagger`.

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

As releases são feitas pelo workflow **Release** (`.github/workflows/release.yml`), disparado
manualmente em **Actions → Release → Run workflow**. Ele usa o **semantic-release**, então a versão
vem dos commits ([Conventional Commits](https://www.conventionalcommits.org)):

| Commit | Incremento de versão |
|---|---|
| `feat:` | minor |
| `fix:`, `perf:` | patch |
| `feat!:` ou `BREAKING CHANGE` | major |

Ele atualiza o `CHANGELOG.md`, cria a tag e a GitHub Release, e anexa o `DroidDeck-win-x64.zip`
(backend autocontido com o configurador web embutido) e o `DroidDeck.apk`. Se não houver nenhum
commit que gere release desde a última versão, nada é publicado.

## Este site

A landing page (`website/landing/`) e esta documentação (`website/docs/`, MkDocs Material, em inglês
e português) são publicadas no GitHub Pages pelo `.github/workflows/pages.yml` a cada push na `main`
que mexa em `website/`.

```powershell
pip install -r website/requirements.txt
cd website
mkdocs serve        # http://127.0.0.1:8000/DroidDeck/docs/
```

As páginas ficam em `website/docs/en/` e `website/docs/pt/`, com os mesmos nomes de arquivo. Ao
adicionar uma página, inclua-a no `nav` do `website/mkdocs.yml` e crie-a nos dois idiomas.
