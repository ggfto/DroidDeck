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

O estado vem direto dos eventos do OBS, então mudanças feitas no próprio OBS também aparecem no deck.

## Configuração

1. No OBS: **Ferramentas → Configurações do servidor WebSocket**. Marque **Ativar servidor WebSocket**.
   Anote a porta (padrão `4455`) e a senha (ou desmarque a autenticação).
2. No DroidDeck, abra as configurações do OBS (celular: **configurações → OBS**; configurador: o botão
   do OBS na barra lateral).
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
