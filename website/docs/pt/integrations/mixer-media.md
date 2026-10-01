# Mixer e mídia

Além dos botões de [Mixer](../guide/actions.md#mixer) e [Mídia](../guide/actions.md#media), o app do
celular tem uma aba **Áudio** — um mixer completo do PC.

## A aba Áudio

- **Dispositivos de saída e entrada**: mudo e slider de volume para o alto-falante e o microfone atuais.
- **Apps**: um slider e um botão de mudo por app que está tocando áudio, com o ícone e o título da janela.
- **Mídia**: tocar/pausar, próxima e anterior para apps que expõem uma sessão de mídia.

Mudanças feitas no PC (mixer de volume do Windows, teclas do teclado) são enviadas ao celular.

## Como funciona o controle de mídia

O DroidDeck usa o sistema de **sessões de mídia** do Windows — o mesmo por trás do overlay de mídia
que aparece quando você aperta uma tecla de mídia. Qualquer app que aparece ali pode ser controlado:
Spotify, navegadores (YouTube etc.), Windows Media Player, VLC e outros.

Se nada estiver tocando, o comando vai para a sessão mais recente. Se o serviço de mídia do Windows
parar de responder, o DroidDeck pausa as verificações em segundo plano por um minuto (aparece um
aviso no log), mas os toques nos botões continuam funcionando.

## Dicas

- Use um botão **Mixer → Alternar mudo** com alvo **Padrão do Windows → entrada** como um mudo de
  microfone universal, que funciona em qualquer app.
- Para um controle de volume prático em jogos, crie dois botões: **Volume +** e **Volume −** no
  dispositivo **Padrão do Windows → saída**.
