# Multiação

Um botão de **Multiação** executa vários passos, um depois do outro. Cada passo tem uma **espera** (ms)
que acontece **antes** de ele rodar (**Esperar antes (ms)**). O editor oferece três tipos de passo:

- **Hotkey**: teclas, com a sintaxe dos [atalhos de teclado](hotkeys.md);
- **Abrir app**: um caminho, como na ação [Abrir programa](actions.md#launch-app);
- **Mutar app**: alterna o mudo de um processo.

Exemplo, "Iniciar reunião": abrir o app de reunião, esperar 3000 ms, enviar `^+m`.

Passos de outros tipos (Discord, OBS, scripts..., vindos de uma
[importação do Stream Deck](import-share.md#stream-deck), por exemplo) são mantidos e executados, mesmo
o editor só criando esses três tipos.

## Condições { #conditions }

Cada passo tem um campo **Só se**. Com uma condição, o passo só roda quando ela vale; senão ele é
pulado e a multiação segue. **Não** inverte: o passo roda quando a condição *não* vale. O padrão,
**Sempre**, roda o passo toda vez.

| Só se… | Valor |
|---|---|
| **A live do OBS estiver no ar** | Nenhum |
| **O OBS estiver gravando** | Nenhum |
| **O microfone do Discord estiver mudo** | Nenhum |
| **O áudio do Discord estiver desativado** | Nenhum |
| **Um botão de dois estados estiver ligado** | O botão de [dois estados](actions.md#two-states), escolhido numa lista. |
| **Um app estiver aberto** | Nome do processo sem `.exe`, ex.: `Spotify`. |
| **Um app estiver em foco** | Nome do processo da janela em foco. |
| **For entre dois horários** | `08:00-18:00`. Um intervalo que passa da meia-noite funciona: `22:00-06:00`. |
| **For um destes dias da semana** | `0` = domingo … `6` = sábado, com intervalos e vírgulas: `1-5`, `0,6`. |
| **Um alvo do tally estiver no ar** / **em preview** | Uma cena, entrada, microfone ou o sinal manual, da lista do [tally](../integrations/tally.md). |

- A condição é conferida na vez do passo, depois da espera dele. A espera acontece mesmo que o passo
  acabe pulado.
- Exemplo, um botão "Entrar ao vivo": iniciar a transmissão; depois, *só se* o `obs64` **não**
  estiver em foco, trocar para a cena da live; e *só se* for entre `18:00-23:59`, acender a luz da mesa.

## Gravar do teclado { #record }

No configurador do PC, **Gravar do teclado** grava o que você digitar no teclado do PC e transforma
em passos de atalho: vá até o programa, digite a sequência e clique em **Parar**.

- Teclas digitadas em sequência viram um passo só (`oi{ENTER}`); uma pausa de mais de meio segundo
  começa um passo novo, com a espera arredondada para 100 ms.
- A gravação para sozinha depois de 60 segundos.

!!! warning "Revise antes de salvar"
    Por ler o teclado inteiro, a gravação só funciona no configurador aberto no próprio PC; o celular
    não tem esse botão. Tudo o que foi digitado entra, inclusive senhas.

!!! info "Só é criado no PC"
    Uma multiação com passo de atalho, programa, script ou HTTP só pode ser criada ou alterada no
    [configurador](../getting-started/configurator.md), no PC. O celular a executa normalmente.
