# Botões e ações

Cada botão executa uma **ação**. Escolha o tipo de ação no painel de propriedades; os campos abaixo
dele mudam de acordo com o tipo.

| Tipo de ação | Roda no | Resumo |
|---|---|---|
| [Atalho de teclado](#hotkey) | PC | Envia uma combinação de teclas para a janela em foco. |
| [Abrir app](#launch-app) | PC | Abre um programa, arquivo, pasta ou URL. |
| [Mídia](#media) | PC | Tocar/pausar, próxima, anterior, parar. |
| [Mixer](#mixer) | PC | Silencia apps; silencia ou ajusta o volume de dispositivos de áudio. |
| [Abrir perfil](#open-profile-and-back) | Celular | Abre outro perfil como uma pasta. |
| [Voltar](#open-profile-and-back) | Celular | Sai de uma pasta. |
| [Multiação](#multi-action) | PC | Executa vários passos em sequência, com esperas. |
| [Discord](../integrations/discord.md) | PC | Controles de voz e soundboard. |
| [OBS](../integrations/obs.md) | PC | Cenas, gravação, transmissão e mais. |
| [Soundboard](../integrations/soundboard.md) | PC | Toca um som. |
| [Tuya](../integrations/tuya.md) | PC | Controla dispositivos de casa inteligente. |

## Atalho de teclado { #hotkey }

Envia teclas para **a janela que está em foco** no PC.

O campo **Keys** usa a sintaxe *SendKeys* do Windows:

| Símbolo | Tecla | Exemplo |
|---|---|---|
| `^` | Ctrl | `^c` → Ctrl+C |
| `+` | Shift | `^+s` → Ctrl+Shift+S |
| `%` | Alt | `%{F4}` → Alt+F4 |
| `{NOME}` | Tecla especial | `{ENTER}`, `{TAB}`, `{ESC}`, `{F5}`, `{DEL}`, `{HOME}`, `{PGDN}` |

!!! warning "Limitações"
    - A **tecla Windows** não pode ser enviada.
    - Alguns jogos que leem a entrada diretamente (DirectInput/Raw Input) ignoram essas teclas.
    - O Windows bloqueia entradas para apps rodando **como administrador**, a menos que o DroidDeck
      também rode como administrador.

## Abrir app { #launch-app }

O caminho aceita qualquer coisa que você abriria pelo *Executar* (++win+r++): um `.exe`
(`C:\Windows\System32\notepad.exe`), um documento, uma pasta ou uma URL (`https://…`, `steam://…`).

## Mídia { #media }

Controla a sessão de mídia que o Windows mostra no overlay de mídia — Spotify, navegadores, players.

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

## Multiação { #multi-action }

Executa vários passos, um depois do outro. Cada passo tem uma **espera** (ms) que acontece **antes**
de ele rodar (**Esperar antes (ms)**). O editor oferece três tipos de passo:

- **Hotkey** — teclas, como acima;
- **Abrir app** — caminho, como acima;
- **Mutar app** — alterna o mudo de um processo.

Exemplo — "Iniciar reunião": abrir o app de reunião, esperar 3000 ms, enviar `^+m`.

## Estado do botão

Alguns botões refletem o estado ao vivo:

| Ação | Estado exibido |
|---|---|
| Mídia tocar/pausar | Ícone ▶ / ⏸ |
| Mixer (mudo de app) | Ícone de volume e cor ativa enquanto silenciado |
| Discord | Cor ativa enquanto você está silenciado/ensurdecido |
| OBS | Aceso na cena atual, ou enquanto gravação/transmissão/câmera virtual/replay buffer estiver ligado |
| Tuya | Liga/desliga, nível de brilho e a cor real da lâmpada |
