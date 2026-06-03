# DroidDeck

Transforma um celular Android num **Stream Deck** para o PC Windows: atalhos, macros,
monitores ao vivo (CPU/GPU/RAM/Rede), **controle de volume por app**, controle de **mídia**
e um **plugin de Discord** (mute/deafen, entrar em canal de voz, volume, modo de voz, por-usuário).

Monorepo com as duas metades do projeto, que evoluem juntas.

## Estrutura

```
DroidDeck/
  RaspDeck/      Backend C# (.NET 8 / WinForms tray + ASP.NET Core + SignalR). Serve a API e o web.
  app/           App Flutter (companion): runtime no celular + configurador web (mesmo código).
  tests/         Testes do backend.
  scripts/       Utilitários (deploy do web para o wwwroot).
```

O app e o backend compartilham um contrato: **REST** (`/api/...`), **SignalR** (`/deckHub`) e
**discovery UDP** (porta 7573). Autenticação por API key (`X-API-KEY` no REST, `access_token`
na query do SignalR), com a chave em `%LocalAppData%\DroidDeck\apikey`. Pareamento por QR.

## Backend (RaspDeck)

```powershell
# rodar (app de bandeja + servidor web em http://localhost:5000)
Set-Location 'G:\ggfto\DroidDeck\RaspDeck'; dotnet run

# build
dotnet build 'G:\ggfto\DroidDeck\DroidDeck.sln' -c Debug
```

Modos: padrão (bandeja), `--headless` (só servidor), `--print-pairing` (imprime a URI/QR e sai).

## App (Flutter, em `app/`)

```powershell
Set-Location 'G:\ggfto\DroidDeck\app'

# Web (configurador) -> deploya no wwwroot que o backend serve:
..\scripts\deploy-web.ps1            # flutter build web + copia para RaspDeck/wwwroot

# APK (celular):
flutter build apk --debug --target-platform android-arm64
adb install -r build\app\outputs\flutter-apk\app-debug.apk
```

- No **navegador** (servido pelo PC) o app abre direto no **configurador** (edição de perfis,
  drag-and-drop, propriedades). No **celular** ele é o **runtime** (a grade de botões que cabe
  na tela; o celular reporta as dimensões da grade ao PC).
- O configurador autentica sem QR via `/api/pairing/local-key` (só em loopback).

## Discord

Requer um app no Discord Developer Portal (Client ID + Secret) com o redirect
`http://localhost:5000/discord` salvo em OAuth2 → Redirects. Configurado via
`POST /api/discord/config`; o token fica em `%LocalAppData%\DroidDeck\discord.json` e reconecta
sozinho no startup. Câmera/compartilhar tela não são possíveis (RPC privado da Discord).

## Releases (download pronto pro usuário)

CI em `.github/workflows/release.yml`, disparo **manual** em **Actions → Release → Run workflow**.
Usa **semantic-release**: a versão sai dos commits (Conventional Commits) — `feat:` → minor,
`fix:`/`perf:` → patch, `BREAKING CHANGE`/`feat!:` → major. Ele gera o `CHANGELOG.md`, cria a
tag + a Release e anexa:
- **`DroidDeck-win-x64.zip`** — backend self-contained (não precisa instalar .NET). Descompacte e rode `DroidDeck.exe`.
- **`DroidDeck.apk`** — app Android assinado (instale no celular; "fontes desconhecidas").

Se não houver commit releasable (só `chore:`/`ci:`/`docs:`…) desde a última versão, ele não publica nada.
A assinatura do APK usa os secrets `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEY_ALIAS`,
`ANDROID_KEY_PASSWORD`, `ANDROID_STORE_PASSWORD` (já configurados); sem eles, cai pra debug.

## Histórico

`app/` foi incorporado a partir do antigo repositório `companion` via `git subtree`
(histórico preservado). O repo `companion` foi arquivado.
