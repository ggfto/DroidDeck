# Pareamento

O pareamento passa ao celular o endereço do PC e uma **chave de API** secreta. Você só faz isso uma vez.

## Parear com QR code (recomendado)

1. No PC, clique com o botão direito no ícone da bandeja → **Parear dispositivo (QR)…**. Uma janela
   mostra um QR code e o texto `IP: <endereço>:4787  Chave: <chave>`.
2. No celular, abra o DroidDeck. Na primeira vez, a tela de **configurações** abre sozinha; nas
   outras, toque na engrenagem da aba **Áudio**.
3. Toque em **Parear (QR)** e leia o código.

O celular salva o endereço, a porta e a chave, e se conecta. O indicador de status fica verde (*Conectado*).

??? info "O que tem dentro do QR code"
    `droiddeck://pair?ip=<LAN IP>&port=4787&key=<api key>` — o endereço do PC na rede local, a porta
    e a chave de API. Trate-o como uma senha: quem ler esse código consegue controlar seu PC pelo DroidDeck.

## Encontrar o servidor automaticamente

**Pesquisar Servidor** faz um broadcast na rede local (UDP 7573) e preenche o IP do PC. A descoberta
**não** envia a chave de API, então você ainda precisa parear com o QR code antes que o celular
consiga se conectar.

## Digitar o endereço manualmente

Digite o IP do PC em **IP do Servidor** (`192.168.0.10` ou `192.168.0.10:4787`) e toque em **Salvar**.
A chave de API de um pareamento anterior é mantida.

## Parear de novo e trocar a chave

- Se o celular mostrar *"Falha de autenticação (chave inválida ou expirada). Refaça o pareamento."*,
  leia o QR code de novo.
- A chave fica guardada no PC em `%LocalAppData%\DroidDeck\apikey`. Para revogar o acesso de todos os
  celulares pareados, feche o DroidDeck, apague esse arquivo e abra-o de novo. Uma nova chave é
  gerada, e todos os celulares precisam parear outra vez.

## Comportamento da conexão

O celular se reconecta sozinho se o PC reiniciar ou o Wi-Fi cair. No começo ele tenta de novo
rapidamente (0, 2, 5, 10, 20 s) e depois a cada 30 s, indefinidamente. Toque no indicador de status
para ver a URL de conexão (com a chave mascarada) e os últimos dados recebidos.
