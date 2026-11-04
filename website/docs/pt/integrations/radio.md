# Rádio: mAirList e RadioDJ

O DroidDeck controla dois programas de automação de rádio. Para a luz de **no ar**, veja
[Tally e ON AIR](tally.md); para o corte de tosse do microfone, veja [Voicemeeter](voicemeeter.md).

## mAirList

Usa a interface REST do [mAirList](https://www.mairlist.com).

1. No mAirList, abra **Configuração → Remote Control** e adicione uma interface **REST** (a porta padrão é
   `9300`).
2. Crie um usuário e ligue os plugins **ExecuteCommand** (comandos) e **GeneralRead** (estado no ar).
3. Abra **Configuração → Integrações → mAirList** no DroidDeck e preencha o endereço, o usuário e a senha (a
   senha fica cifrada).

Segundo o fórum do mAirList, a interface REST pede a edição **Advanced** ou superior.

Botões do tipo **mAirList**:

- **No ar:** alternar, entrar e sair. O botão fica vermelho no ar.
- **Automação:** alternar, ligar e desligar, por playlist.
- **Playlist e players:** próximo, tocar, parar e fade out.
- **Cartwall:** tocar um cart e parar todos.
- **Outro comando:** qualquer
  [comando remoto do mAirList](https://wiki.mairlist.com/reference:remote_control_commands).

## RadioDJ

Usa o plugin **REST Server** do [RadioDJ](https://www.radiodj.ro).

1. No RadioDJ 2, copie o `Plugin_RestServer` da pasta **Disabled Plugins** para a pasta **Plugins** e abra o
   RadioDJ.
2. Nas opções do plugin, defina o endereço, a porta e a senha, e inicie o servidor.
3. Abra **Configuração → Integrações → RadioDJ** no DroidDeck e preencha o mesmo endereço e a mesma senha.

Botões do tipo **RadioDJ**:

- **Player:** tocar uma faixa da playlist, parar, pausar, retomar, reiniciar e tocar da introdução.
- **Modos:** AutoDJ, assistido e entrada ao vivo, com o estado no botão.
- **Eventos e carts:** ligar e desligar eventos, tocar um cart.
- **Carregar:** uma playlist ou uma faixa, pelo ID.
- **Outro comando:** um comando da lista nas opções do plugin.

!!! warning "Comandos por ID na versão 2.0.4"
    No fórum do RadioDJ há relatos de erro em **Carregar uma playlist** e **Pôr uma faixa no topo** na versão
    2.0.4, enquanto os demais comandos funcionam. O desenvolvedor não reproduziu o problema.

O RadioDJ não informa se o player está pausado, então os botões de pausa não mostram esse estado.

!!! warning "Ainda não testado com o programa de verdade"
    Estas integrações foram feitas a partir da documentação e do fórum de cada programa e testadas com um simulador
    do protocolo, não com o mAirList e o RadioDJ reais. Se algo não funcionar, [abra uma issue](https://github.com/ggfto/DroidDeck/issues).
