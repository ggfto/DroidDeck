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
# rodar (app de bandeja + servidor web em http://localhost:4787)
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

**Cada usuário usa o próprio app do Discord** — o RPC só libera o dono do app (sem precisar de
aprovação da Discord), e o Client Secret não pode ser compartilhado. Setup (1 vez, ~2 min):

1. discord.com/developers/applications → **New Application**
2. Em **OAuth2**, copie o **Client ID** e o **Client Secret**
3. Em **OAuth2 → Redirects**, adicione `http://localhost:4787/discord` e **Salve**

No app/configurador: **Configurações → Plugin do Discord** (ou o botão "Configurar Discord" na
sidebar do configurador web). Cole Client ID + Secret → **Salvar** → **Conectar** (aprove o
popup que abre no Discord do PC). O token fica em `%LocalAppData%\DroidDeck\discord.json` e
reconecta sozinho no startup. No editor, ações de Discord mostram um **aviso** se ainda não
estiver configurado/conectado. Câmera/compartilhar tela não são possíveis (RPC privado da Discord).

## Casa inteligente (Tuya / Smart Life)

Controla lâmpadas, tomadas e interruptores pelo deck. Vale para **qualquer marca que use
Tuya por baixo** — Nova Digital, Positivo Casa Inteligente, RSmart, Elgin, Geonav, Aubess…
são todas rebrand da mesma plataforma.

**Não precisa de conta de desenvolvedor.** O pareamento é por QR:

1. No app **Smart Life** (ou Tuya Smart): **Eu → ⚙️ → Conta e segurança → Código de usuário**
2. No configurador: **Configurações → Casa inteligente (Tuya)**, cole o código, **Gerar QR**
3. Escaneie o QR com o app (aba Home → ícone de scan). Ele pede para confirmar login
   **"Home Assistant"** — ver a ressalva abaixo.

A sessão fica em `%LocalAppData%\DroidDeck\tuya.json` e reconecta sozinha no startup.

### ⚠️ Se você usa o app da marca (Nova Digital, Positivo, RSmart…)

**O scan vai falhar** com _"please use the designated app to scan the code to login"_. O QR
carrega o registro de app do Home Assistant, e só **Smart Life** e **Tuya Smart** aceitam —
apps de marca recusam. Compartilhar o dispositivo não resolve (vários OEMs nem oferecem
a opção).

Solução: **remova o aparelho do app da marca e pareie de novo pelo Smart Life**. É o mesmo
hardware e funciona igual; você perde só as automações configuradas no app da marca.

> Usamos o registro público do Home Assistant porque a Tuya não abre esse cadastro no
> autoatendimento — depende de _business review_. Por isso o app mostra o nome dele na
> autorização. O `clientId`/`schema` ficam em `tuya.json`, então trocar por um registro
> próprio é mudança de configuração, não de código.

### Botões

No editor, tipo de ação **`tuya`**: escolha o dispositivo, depois o que controlar. Os campos
se adaptam ao aparelho (o `specifications` da Tuya diz o tipo de cada função): liga/desliga
vira switch, brilho vira slider já na faixa certa, modo vira lista de opções.

- **Alternar** — liga se estiver desligado e vice-versa. É o uso comum de um deck.
- **Sempre ligar / valor fixo** — manda um valor determinístico.

Configure a **Cor ativa** para o botão acender quando o aparelho estiver ligado. O estado
chega por push (MQTT) e reflete inclusive mudanças feitas no interruptor de parede ou no app.

### Cota da API

O plano gratuito da Tuya permite ~26 mil chamadas/mês (≈0,6 por minuto), com cota separada
para as mensagens de push. Por isso o estado vem **por push, nunca por polling**, e a
reenumeração de dispositivos (botão "atualizar") só roda quando você pede. Apertar botões
não é problema — cada clique é uma chamada.

### Limitações conhecidas

- **Sem controle local.** Tudo passa pela nuvem; sem internet, os botões não funcionam.
  O protocolo local dos aparelhos recentes (3.4/3.5) exige um handshake que nenhuma
  biblioteca .NET implementa hoje. Também renderia pouco em latência: medimos ~240 ms no
  canal local contra ~310 ms pela nuvem.
- **Aparelho offline** falha com erro 2001 da Tuya.

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
