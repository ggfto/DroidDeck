# Tally e ON AIR

Um celular em tela cheia vira luz de **no ar** para o estúdio de rádio ou tally de uma câmera: vermelho no
ar, verde em preview, escuro fora do ar. A tela não apaga enquanto estiver aberta.

## Como usar

1. Abra **Configuração → Integrações → Tally e ON AIR**, ou crie um botão do tipo **Tally e ON AIR** com
   **Abrir a tela de tally**.
2. Escolha **o que acompanhar**. Cada celular guarda a própria escolha: um pode ser a câmera 1, outro a
   câmera 2 e outro a luz do corredor.
3. Toque na tela para ver os botões de voltar e de trocar.

## O que dá para acompanhar

A lista traz só o que está conectado agora:

| Origem | O que aparece | Quando fica vermelho |
|---|---|---|
| [OBS](obs.md) | Cada cena, transmissão e gravação | Cena no programa (em verde, no preview do Modo Estúdio), ou transmitindo/gravando |
| [vMix](vmix.md) | Cada entrada, transmissão e gravação | Entrada no programa (em verde, no preview), ou transmitindo/gravando |
| [mAirList](radio.md) | No ar | Quando o mAirList está no ar |
| [RadioDJ](radio.md) | Entrada ao vivo | Quando a entrada ao vivo está ligada |
| [Voicemeeter](voicemeeter.md) | Microfone aberto, por entrada | Quando a entrada não está muda |
| Sinal manual | ON AIR | Quando um botão do deck ou a tela de tally liga o sinal |

Para uma rádio sem automação, o **sinal manual** basta: um botão **Sinal manual de ON AIR (alternar)** no
deck do locutor e um celular no corredor acompanhando o sinal manual. Também dá para ligar e desligar na
própria tela de tally: com o sinal manual escolhido, toque na tela e use o botão que aparece.

## SEM SINAL

Se o celular perde a conexão com o PC, ou o programa acompanhado fecha, a tela fica cinza e mostra **SEM
SINAL**. Ela nunca mostra "fora do ar" sem saber: quem está diante da câmera precisa poder confiar nisso.

!!! warning "Não é um tally de transmissão profissional"
    O estado passa pelo Wi-Fi e chega com uma pequena demora. Serve como aviso para apresentador e equipe,
    mas não tem a precisão de quadro de um sistema de tally com cabo.

## No botão

Com **Só mostrar o estado no botão**, o botão fica vermelho ou verde sem abrir a tela cheia. Se o alvo
for o sinal manual, o toque no botão também liga e desliga.

O botão de cena do [OBS](obs.md) segue as mesmas cores: vermelho no programa (ou a cor escolhida para o botão) e
verde no preview.

## Em automações e multiações

O tally também serve de sinal para o resto do deck:

- [Automações](../guide/automations.md): **Quando um alvo do tally entrar no ar** ou **sair do ar**. Por exemplo,
  acender uma luz da Hue quando o microfone abrir.
- [Multiações](../guide/multi-action.md#conditions): as condições **Um alvo do tally estiver no ar** e **em
  preview**. Alvo indisponível (programa fechado) não vale como nenhum dos dois; com **Não**, a condição passa.
