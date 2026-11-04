# vMix

O DroidDeck controla o [vMix](https://www.vmix.com) pela **TCP API** dele e mostra o estado no botão: a entrada
no programa fica vermelha, a do preview fica verde, e gravação e transmissão acendem.

## Configuração

1. Abra o vMix. A TCP API (porta `8099`) já fica ligada, sem configuração nem senha.
2. Abra **Configuração → Integrações → vMix** no DroidDeck e toque em **Conectar**.

vMix em outro PC: digite o IP dele e libere a porta `8099` no firewall de lá.

## O que dá para fazer

Crie um botão do tipo **vMix** e escolha:

- **Corte e transição:** cortar (preview para o programa) e Fade, com a duração.
- **Entradas:** mandar para o preview, para o programa, Quick Play. A lista traz as entradas do vMix.
- **Overlays:** alternar uma entrada num overlay (1 a 8) e tirar todos.
- **Áudio:** mudo e volume de uma entrada, mudo e volume do master.
- **Saídas:** gravar, transmitir, External, MultiCorder, Fade to Black e captura de tela.
- **Outra função:** qualquer função da
  [referência de atalhos do vMix](https://www.vmix.com/help29/ShortcutFunctionReference.html), com os parâmetros
  como na Web API (`Input=1&SelectedName=Titulo&Value=Oi`).

Para usar o celular como tally de uma câmera, veja [Tally e ON AIR](tally.md).

!!! warning "Ainda não testado com o programa de verdade"
    Esta integração foi feita a partir da documentação oficial e testada com um simulador do protocolo, não com o
    vMix real. Se algo não funcionar, [abra uma issue](https://github.com/ggfto/DroidDeck/issues).
