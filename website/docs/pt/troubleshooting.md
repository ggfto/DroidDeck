# Solução de problemas

## O celular não encontra o PC ou não conecta

1. **Mesma rede**: celular e PC precisam estar na mesma rede local. Redes Wi-Fi de convidados e o
   "isolamento de AP" impedem que os dispositivos se enxerguem.
2. **Firewall**: libere TCP 4787 e UDP 7573 em redes privadas (veja [Instalação](getting-started/installation.md#firewall))
   e confira se o Windows marca sua rede como **Privada**.
3. **O DroidDeck está rodando**: procure o ícone na bandeja. No PC, `http://localhost:4787/api/ping`
   deve retornar `{"ok":true,...}`.
4. **IP errado**: em PCs com vários adaptadores de rede, VPNs ou adaptadores virtuais (Hyper-V, WSL,
   VirtualBox), o QR pode trazer o endereço errado. Descubra o certo com `ipconfig` e digite-o
   manualmente em **IP do Servidor**.
5. **"Servidor não encontrado"** na pesquisa: a descoberta usa broadcast, que alguns roteadores
   bloqueiam. Digite o IP manualmente.

## "Falha de autenticação… Refaça o pareamento"

A chave do celular não confere com a do PC (por exemplo, depois que o arquivo `apikey` foi apagado).
Leia o QR code de novo pelo menu da bandeja.

## O configurador mostra erros 401

O configurador só entra automaticamente no próprio PC. Abra-o em `http://localhost:4787/` na máquina
onde o DroidDeck está rodando.

## Um atalho de teclado não faz nada

- As teclas vão para a **janela em foco** — confira se o app certo está na frente.
- Apps rodando **como administrador** ignoram entradas de um DroidDeck sem privilégios de administrador.
- Jogos com anti-cheat podem ignorar teclas simuladas.
- Confira a sintaxe em [Atalho de teclado](guide/actions.md#hotkey).

## Os botões de mídia não fazem nada

Verifique se o app aparece no overlay de mídia do Windows (aperte uma tecla de mídia no teclado). Se
o log mencionar o media broker travado, espere um minuto ou reinicie o app que está tocando.

## Integrações

- **Discord**: veja [Discord → Solução de problemas](integrations/discord.md#troubleshooting).
- **OBS**: veja [OBS → Solução de problemas](integrations/obs.md#troubleshooting).
- **Tuya**: *"QR expirado; gere outro."* — o QR vale por 2 minutos. Apps de marca recusando o QR:
  veja [Tuya](integrations/tuya.md#linking-your-account).

## Logs

O DroidDeck grava um log em **`%LocalAppData%\DroidDeck.log`** (ao lado da pasta `DroidDeck`, não
dentro dela). Anexe-o ao [abrir uma issue](https://github.com/ggfto/DroidDeck/issues) — ele nunca
contém sua chave de API.
