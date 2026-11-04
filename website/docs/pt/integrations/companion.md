# Companion

Com o [Bitfocus Companion](https://bitfocus.io/companion), o celular vira uma **superfície**, como um Stream
Deck: as teclas e páginas que você montou lá aparecem no celular, e o toque aciona o que estiver configurado
nelas. Assim o DroidDeck alcança os centenas de equipamentos que o Companion controla (mesas de corte, mesas de
som, câmeras PTZ...).

## Configuração

1. Abra o Companion. A porta da Satellite API (`16622`) já fica ligada por padrão.
2. Abra **Configuração → Integrações → Companion** no DroidDeck, escolha as **colunas** e **linhas** da grade
   (o padrão é 8 × 4) e toque em **Conectar**.
3. O DroidDeck aparece em **Surfaces** no Companion.

Companion em outro PC: digite o IP dele (`192.168.0.20`, ou `192.168.0.20:16622`).

## Como usar

Toque em **Abrir a superfície**, ou crie um botão do tipo **Companion** no deck. A tela fica acesa enquanto
estiver aberta. Segurar uma tecla conta como segurar: o Companion recebe o aperto e a soltura separados.

- Todos os celulares mostram a mesma superfície. Para o Companion, é um aparelho só.
- Se o celular cair com o dedo numa tecla, o PC solta a tecla sozinho.

!!! warning "Ainda não testado com o programa de verdade"
    Esta integração foi feita a partir da documentação oficial e testada com um simulador do protocolo, não com o
    Companion real. Se algo não funcionar, [abra uma issue](https://github.com/ggfto/DroidDeck/issues).
