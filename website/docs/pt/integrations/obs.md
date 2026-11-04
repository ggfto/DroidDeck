# OBS Studio

O DroidDeck se conecta ao **OBS Studio 28 ou mais recente** pelo servidor WebSocket embutido (obs-websocket v5).

## O que dá para fazer

| Operação | Parâmetros | O botão acende quando |
|---|---|---|
| Trocar de cena | Cena (escolhida em uma lista) | Essa cena está no ar |
| Alternar gravação | — | Gravando |
| Alternar transmissão | — | Ao vivo |
| Alternar câmera virtual | — | Câmera virtual ligada |
| Alternar replay buffer | — | Replay buffer ligado |
| Salvar replay | — | — |
| Alternar mudo de fonte de áudio | Fonte de áudio (escolhida em uma lista) | — |
| Mostrar / ocultar fonte | Cena e fonte (escolhidas em listas) | — |
| Ligar / desligar filtro | Fonte ou cena, e filtro (escolhidos em listas) | — |
| Pausar gravação (alternar) | — | Gravação pausada |
| Marcador de capítulo na gravação | Nome do capítulo (opcional). Exige OBS 30.2+ gravando em MP4 híbrido | — |
| Captura de tela | — (PNG da cena do programa, salvo na pasta de gravação do OBS) | — |

O estado vem direto dos eventos do OBS, então mudanças feitas no próprio OBS também aparecem no deck.

O botão de **Trocar de cena** mostra onde a cena está: vermelho no programa (ou a cor escolhida para o botão) e, com o
**Modo de estúdio** ligado, verde no preview. Se a mesma cena estiver nos dois, vale o programa. Por padrão, o OBS troca
as duas cenas a cada transição, então o botão alterna entre vermelho e verde. Tocar no botão manda a cena direto para o
programa.

Para usar o celular como luz de "no ar" ou tally de câmera, veja [Tally e ON AIR](tally.md).

## Configuração

1. No OBS: **Ferramentas → Configurações do servidor WebSocket**. Marque **Ativar servidor WebSocket**.
   Anote a porta (padrão `4455`) e a senha (ou desmarque a autenticação).
2. No DroidDeck, abra as configurações do OBS (celular: **Configuração → Integrações → OBS**;
   configurador: **Integrações** na barra lateral).
3. Preencha **Host** (`localhost`), **Porta** e **Senha**, clique em **Salvar** e depois em **Conectar**.

No celular você também pode tocar em **Ler QR do OBS** e ler o QR de **Mostrar informações de
conexão** no OBS. O host é sempre definido como `localhost`, porque o OBS roda no mesmo PC que o DroidDeck.

Depois de configurado, o DroidDeck se reconecta sozinho a cada 10 segundos quando o OBS é fechado e
aberto de novo.

## Solução de problemas { #troubleshooting }

| Mensagem | Causa / solução |
|---|---|
| *OBS não respondeu (handshake). O obs-websocket está ativo?* | Ative o servidor WebSocket no OBS e confira a porta. |
| *OBS recusou o pedido.* | Senha errada, ou a cena/fonte não existe mais. |
