# Botões e ações

Cada botão executa uma **ação**. Escolha em **Ação** no editor do botão; os campos abaixo mudam de
acordo com o tipo. Todas as ações rodam no PC, menos **Abrir perfil** e **Voltar**, que rodam no
celular.

Além do toque normal, um botão pode ter uma ação de [toque longo e de toque duplo](#gestures), ser um
[botão de dois estados](#two-states), [mostrar uma informação](monitors.md) (relógio, clima, o que
está tocando, um controle deslizante, o uso de IA...), ocupar [mais de uma casa](#size) e ser apertado por
uma [automação](automations.md).

!!! info "Atalho de teclado, Abrir programa, Script e Requisição HTTP são criados no PC"
    Por segurança, botões que enviam teclas, abrem programas, rodam scripts ou fazem requisições HTTP
    (inclusive multiações com esses passos) só podem ser criados ou alterados no
    [configurador](../getting-started/configurator.md), no PC. O celular executa esses botões normalmente.

## Tipos de ação { #action-types }

### No PC { #on-the-pc }

Envie teclas, abra coisas e automatize o próprio Windows.

| Ação | O que faz |
|---|---|
| [Atalho de teclado](#hotkey) | Envia uma combinação de teclas para a janela em foco. |
| [Abrir programa](#launch-app) | Abre um programa, arquivo, pasta ou URL. |
| [Multiação](#multi-action) | Executa vários passos em sequência, com esperas e condições. |
| [Script](#script) | Roda um script do PowerShell, do Python ou um `.bat`, com argumentos. |
| [Requisição HTTP](#http) | Chama um webhook ou qualquer API. |
| [Windows](#windows) | Troca o áudio padrão, trava, suspende ou desliga a tela. |
| [Jogo da Steam](#steam) | Abre um jogo instalado, com a capa no botão. |

### Mídia e áudio { #media-and-audio }

Controle o que está tocando e o volume.

| Ação | O que faz |
|---|---|
| [Mídia](#media) | Tocar/pausar, próxima, anterior, parar. |
| [Mixer](#mixer) | Silencia apps; silencia ou ajusta o volume de dispositivos de áudio. |
| [Spotify](../integrations/spotify.md) | Player, curtir, aleatório, playlists e a capa no botão. |
| [Voicemeeter](../integrations/voicemeeter.md) | Mudo e ganho de entradas e saídas, Macro Buttons, parâmetros. |
| [Soundboard](../integrations/soundboard.md) | Toca um som no seu microfone. |

### Streaming { #streaming }

Chat de voz, a sua live e as ferramentas em volta dela. Cada uma precisa da integração configurada uma vez.

| Ação | O que faz |
|---|---|
| [Discord](../integrations/discord.md) | Controles de voz e soundboard. |
| [OBS](../integrations/obs.md) | Cenas, gravação, transmissão e mais. |
| [Twitch](../integrations/twitch.md) | Marcador, clipe, anúncio, chat, enquete e título. |
| [YouTube](../integrations/youtube.md) | Chat da live e espectadores. |
| [Streamer.bot](../integrations/streamerbot.md) | Dispara uma ação do Streamer.bot, com argumentos. |
| [VTube Studio](../integrations/vtubestudio.md) | Dispara atalhos do modelo ou carrega um modelo. |

### Estúdio: rádio e TV { #studio }

| Tipo | O que faz |
|---|---|
| [Tally e ON AIR](../integrations/tally.md) | Tela cheia de tally ou ON AIR, ou o estado no botão. |
| [vMix](../integrations/vmix.md) | Corte, entradas, overlays, áudio, gravação e transmissão. |
| [Companion](../integrations/companion.md) | Abre a superfície do Bitfocus Companion. |
| [mAirList e RadioDJ](../integrations/radio.md) | No ar, automação, players e carts. |
| Rede (OSC, TCP, UDP) | Manda uma mensagem para mesas de som, QLab e outros aparelhos da rede. Só nasce no PC. |

### Luzes e casa inteligente { #lights-and-smart-home }

Aparelhos na sua rede ou na sua central de casa inteligente.

| Ação | O que faz |
|---|---|
| [Tuya](../integrations/tuya.md) | Lâmpadas, tomadas e interruptores do Smart Life. |
| [Home Assistant](../integrations/homeassistant.md) | Luzes, tomadas, cenas, scripts e sensores. |
| [Philips Hue](../integrations/hue.md) | Luzes, salas e cenas pelo Hue Bridge. |
| [Elgato Key Light](../integrations/elgato.md) | Liga/desliga, brilho e temperatura das luzes Elgato. |
| [Govee](../integrations/govee.md) | Luzes Govee pela API LAN. |
| [MQTT](../integrations/mqtt.md) | Publica uma mensagem no seu broker. |

### Timers e navegação { #timers-and-navigation }

| Ação | O que faz |
|---|---|
| [Timer, pomodoro e contador](#timer) | Relógio ou contagem ao vivo no botão. |
| [Abrir perfil](#open-profile-and-back) | Abre outro perfil como uma pasta (no celular). |
| [Voltar](#open-profile-and-back) | Sai de uma pasta (no celular). |

## Atalho de teclado { #hotkey }

Envia teclas para **a janela que está em foco** no PC, com a sintaxe *SendKeys* do Windows mais `#`
para a tecla Windows: `^c` é Ctrl+C, `^+s` é Ctrl+Shift+S, `%{F4}` é Alt+F4, `#d` mostra a área de
trabalho. A sintaxe completa, as teclas especiais e as limitações estão em
[Atalhos de teclado](hotkeys.md).

## Abrir programa { #launch-app }

O **Caminho** aceita qualquer coisa que você abriria pelo *Executar* (++win+r++): um `.exe`
(`C:\Windows\System32\notepad.exe`), um documento, uma pasta ou uma URL (`https://…`, `steam://…`).

## Multiação { #multi-action }

Executa vários passos, um depois do outro, cada um com uma espera e, se você quiser, só quando uma
[condição](multi-action.md#conditions) vale (a live está no ar, um app está aberto, é dia de semana...).
Dá também para [gravar os passos pelo teclado](multi-action.md#record). Veja
[Multiação](multi-action.md).

## Mídia { #media }

Controla a sessão de mídia que o Windows mostra no overlay de mídia: Spotify, navegadores, players.

**Comando de mídia:** **Play / Pause** (padrão), **Próxima faixa**, **Faixa anterior**, **Parar**.
Um botão de tocar/pausar troca o ícone ao vivo entre ▶ e ⏸.

## Mixer { #mixer }

| Operação | Alvo: app | Alvo: dispositivo |
|---|---|---|
| Alternar mudo / Mutar / Desmutar | ✓ | ✓ |
| Definir volume (0–100) | — | ✓ |
| Volume + / Volume − (passo) | — | ✓ |

- **Aplicativo (processo)**: o nome do processo **sem** `.exe`, por exemplo `Spotify` ou `chrome`.
  Afeta todos os fluxos de áudio desse app. O volume por app fica na aba **Áudio** do celular.
- **Dispositivo do Windows**: **Padrão do Windows** segue o dispositivo padrão atual do Windows
  (escolha **saída** ou **entrada**, ou seja, alto-falante ou microfone), ou escolha um dispositivo
  específico. Aumentar o volume também tira o dispositivo do mudo.

Defina uma **cor ativa** para o botão acender enquanto o alvo estiver silenciado.

## Abrir perfil e voltar { #open-profile-and-back }

**Abrir perfil** mostra outro perfil por cima do atual, como uma pasta. Dá para aninhar pastas.
**Voltar** fecha a pasta atual; a seta da barra de título faz o mesmo. As duas ações rodam só no celular.

## Timer, pomodoro e contador { #timer }

O botão mostra o tempo ao vivo. O relógio fica no PC, então todos os celulares veem o mesmo tempo e
o fim da contagem é detectado mesmo com a tela do celular apagada.

| Tipo | Toque | No fim |
|---|---|---|
| **Timer** | Inicia / pausa. Depois do fim, o toque zera. | Aviso sonoro no PC, vibração no celular e o botão acende na cor ativa. |
| **Cronômetro** | Inicia / pausa. | — |
| **Pomodoro** | Inicia / pausa. Foco e pausa se alternam sozinhos e o botão conta os ciclos. | Aviso a cada troca de fase. |
| **Contador** | Soma o **passo**. | — |

**Ao tocar** muda o que o toque faz: **Zerar**, **Pular para a próxima fase** (pomodoro), **Somar**
ou **Subtrair** (contador). Com **Timer controlado** um botão age sobre o timer de outro: por
exemplo, um botão "Zerar" ao lado do relógio. Os contadores continuam valendo depois de reiniciar o PC.

## Windows { #windows }

| Ação | O que faz |
|---|---|
| **Trocar o dispositivo de áudio padrão** | Define a saída (ou entrada) padrão do Windows. Com **Alternar com** preenchido, cada toque alterna entre os dois, por exemplo fone ↔ caixa de som. O botão acende enquanto o dispositivo escolhido é o padrão. |
| **Travar o computador** | Mesmo que ++win+l++. |
| **Suspender** | Põe o PC para dormir. |
| **Desligar a tela** | Desliga os monitores; mexer o mouse liga de novo. |

## Requisição HTTP { #http }

Chama uma URL a partir do PC: webhooks do n8n, do Home Assistant ou do IFTTT, ou qualquer API.
Escolha o **método**, a **URL**, os **cabeçalhos** (um por linha, `Nome: valor`) e, em POST/PUT/PATCH,
o **corpo** e o formato dele (JSON, formulário ou texto). A requisição tem 10 segundos para
responder, e o resultado fica no log do DroidDeck.

## Script { #script }

Roda um script no PC, sem janela. **Script** escolhe de onde ele vem:

- **Escrito aqui**: escolha a **Linguagem** (PowerShell, Python ou Prompt de comando `.bat`) e digite
  o **Código** no botão. O DroidDeck o salva em `%LocalAppData%\DroidDeck\scripts\` e roda de lá.
- **Um arquivo do PC**: o caminho (**Arquivo**) de um `.ps1`, `.py`, `.bat` ou `.cmd`.

Os **Argumentos (opcional)** são separados por espaço; use aspas para um argumento com espaços
(`"meu jogo" 2`). Eles chegam em `$args` (PowerShell), `sys.argv` (Python) ou `%1`, `%2`… (`.bat`), então
um mesmo arquivo de script serve para vários botões, cada um com os seus argumentos.

??? note "Como os scripts rodam"
    - O PowerShell roda com `-NoProfile -ExecutionPolicy Bypass`. O Python usa o `py` do instalador
      oficial, ou o `python` do `PATH`. A pasta de trabalho é a pasta do script.
    - Um script que passa de **5 minutos** é encerrado.
    - A saída não aparece no botão. Erros (código de saída diferente de zero) vão para o log do
      DroidDeck. Para mostrar um valor num botão, use a informação
      [Saída de um comando](monitors.md#informacao).

!!! warning "Só é criado no PC"
    Um script pode fazer tudo o que a sua conta do Windows faz, então, como os atalhos de teclado,
    botões de script só podem ser criados ou alterados no configurador do PC. O celular e o deck no
    navegador executam esses botões normalmente.

## Jogo da Steam { #steam }

Lista os jogos instalados na Steam do PC (todas as bibliotecas). Ao escolher um, o botão ganha o nome
e a capa do jogo; tocar abre o jogo pela Steam. A capa vem do cache da Steam: se um jogo aparecer sem
imagem, abra a biblioteca da Steam uma vez para ela baixar as capas.

## Tamanho { #size }

Em **Tamanho**, no editor, **Largura (casas)** e **Altura (casas)** fazem um botão ocupar até 4 × 4
casas: um botão de mudo grande, um [controle deslizante](monitors.md#volume-slider) alto, um
relógio largo. A posição do botão é a casa do canto superior esquerdo.

Um botão grande nunca cobre outros botões: ele para onde houver outro botão e é cortado na borda da
grade. Para abrir espaço, mova os vizinhos antes.

## Toque longo e toque duplo { #gestures }

Além do toque normal, um botão pode ter uma ação para **segurar** e outra para **dois toques**
(seção *Toque longo e toque duplo* do editor). Os tipos de ação são os mesmos do toque normal.

- Exemplo: um timer que inicia no toque e zera no toque longo; uma cena do OBS no toque e a
  gravação no toque duplo.
- Só os botões com toque duplo esperam um instante (~0,3 s) antes de executar o toque simples, para
  diferenciar os dois. Os demais respondem na hora.
- Num botão com toque longo, segurar executa a ação; para editar o botão no celular, use o
  [modo de edição](profiles.md#modo-de-edicao).

## Dois estados { #two-states }

Na seção **Dois estados** do editor:

- **Alternar a cada toque** deixa o botão ligado num toque e desligado no seguinte, para ações sem
  estado próprio (um atalho que liga e desliga algo num jogo, por exemplo). O estado fica no PC: todos
  os celulares mostram o mesmo e ele continua depois de reiniciar.
- **Ação quando ligado** (opcional) roda no toque que desliga o botão. Exemplo: o toque liga a gravação
  por um atalho e o seguinte manda o atalho de parar. Sem ela, o mesmo toque roda sempre.
- **Rótulo quando ativo** e **Ícone quando ativo** trocam o visual quando o botão está ativo, pelo
  alternar ou pelo estado da própria ação (mudo do Discord, gravação do OBS, lâmpada acesa...).
  A **cor quando ativo** fica junto das cores do botão.

## Estado do botão

Alguns botões refletem o estado ao vivo:

| Ação | Estado exibido |
|---|---|
| Mídia tocar/pausar | Ícone ▶ / ⏸ |
| Mixer (mudo de app) | Ícone de volume e cor ativa enquanto silenciado |
| Discord | Cor ativa enquanto você está silenciado/ensurdecido |
| OBS | Aceso na cena atual, ou enquanto gravação/transmissão/câmera virtual/replay buffer estiver ligado |
| Tuya | Liga/desliga, nível de brilho e a cor real da lâmpada |
| Timer | Tempo restante ou decorrido, fase do pomodoro e cor ativa no fim |
| Windows (áudio) | Cor ativa enquanto o dispositivo é o padrão |
| Home Assistant | Aceso com a entidade ligada (com o brilho e a cor da luz), ou o valor do sensor |
| Spotify | Play/pause, curtida, aleatório e repetir; a capa no "mostrar o que está tocando" |
| Twitch / YouTube | Ao vivo e espectadores, no "só mostrar" |
| Dois estados | Cor, rótulo e ícone do ativo enquanto ligado |
