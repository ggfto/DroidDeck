# Discord

O DroidDeck controla o **app desktop do Discord** rodando no mesmo PC, pelo RPC local do Discord.

## O que dá para fazer

| Operação | Parâmetros |
|---|---|
| Alternar mudo do microfone | — |
| Alternar ensurdecer | — |
| Entrar em canal de voz | Servidor e canal (escolhidos em uma lista) |
| Desconectar da voz | — |
| Volume do microfone + / − | Passo (1–50) |
| Volume de saída + / − | Passo (1–50) |
| Alternar modo de voz | Alterna entre detecção de voz e push-to-talk |
| Silenciar um usuário (localmente) | Usuário, escolhido na chamada atual |
| Definir o volume de um usuário | Usuário e volume (0–200 %) |

Há também uma fonte **soundboard do Discord** nativa na ação [Soundboard](soundboard.md).

Câmera e compartilhamento de tela **não** podem ser controlados: o Discord não os expõe pelo RPC.

## Configuração (uma vez, uns 2 minutos)

O Discord só deixa o **dono de uma aplicação** usar o RPC dela sem passar por uma revisão do
Discord, então cada usuário cria a sua própria aplicação (gratuita):

1. Acesse [discord.com/developers/applications](https://discord.com/developers/applications) e clique em **New Application**.
2. Em **OAuth2**, copie o **Client ID** e o **Client Secret**.
3. Em **OAuth2 → Redirects**, adicione `http://localhost:4787/discord` e clique em **Save Changes**.
4. No DroidDeck, abra as configurações do Discord — no celular: **configurações → Discord**; no
   configurador: **Configurar Discord** na barra lateral.
5. Cole o Client ID e o Secret, clique em **Salvar credenciais** e depois em **Conectar**.
6. O Discord mostra um popup de autorização no PC. Aprove em até 60 segundos.

O token é renovado automaticamente, e o DroidDeck se reconecta sozinho toda vez que inicia, sem
mostrar o popup de novo. Se o Discord estiver fechado, o DroidDeck tenta de novo a cada 10 segundos.

!!! tip "Dica"
    No editor de botões, as ações do Discord mostram um aviso com um link **Configurar** enquanto o
    Discord não estiver configurado ou conectado. A lista de participantes das ações por usuário só
    é preenchida enquanto você está em uma chamada de voz.

## Solução de problemas { #troubleshooting }

| Mensagem | Causa / solução |
|---|---|
| *Pipe do Discord não encontrado — o Discord está aberto?* | O app desktop do Discord não está rodando (a versão do navegador não funciona). |
| *Configure o Client ID e o Secret do Discord primeiro.* | Conclua a configuração acima. |
| *Discord não respondeu ao handshake (READY).* | O Discord ainda está iniciando; o DroidDeck tenta de novo sozinho. |
| A autorização falha | Confira se o redirect `http://localhost:4787/discord` foi salvo no Developer Portal. |
