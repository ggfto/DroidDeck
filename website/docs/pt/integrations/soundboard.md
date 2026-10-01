# Soundboard

Toque efeitos sonoros pelo deck — nos seus alto-falantes e no seu microfone, para que o pessoal no
Discord ou na sua live também ouça.

## Fontes de som

**MyInstants** (padrão)
:   Busque na biblioteca do [MyInstants](https://www.myinstants.com) direto no editor de botões, ouça
    uma prévia no PC e salve o som no botão. O rótulo é preenchido com o título do som. Os sons são
    baixados uma vez e ficam em cache no PC.

**Discord**
:   Toca um som do **soundboard nativo do Discord** do seu servidor no canal de voz em que você está.
    Exige a [integração com o Discord](discord.md) e que você esteja em um canal de voz. Só os sons
    do servidor aparecem na lista; usar sons de outro servidor exige Nitro. Esses sons não são
    ouvidos na sua live/OBS.

A operação **Parar tudo** interrompe o que estiver tocando. Um som novo interrompe o anterior.

## Levando o som para o microfone

O DroidDeck toca os sons do MyInstants em um dispositivo de áudio à sua escolha. Para que eles
cheguem ao Discord, a jogos ou ao OBS, use um cabo de áudio virtual:

1. Instale o [VB-Cable](https://vb-audio.com/Cable/) (gratuito).
2. Nas configurações de **Soundboard** do DroidDeck, defina **Saída cabo** como **CABLE Input**.
3. Ative **Tocar também no monitor** e escolha seu fone, para você também ouvir os sons.
4. No Discord (ou no OBS), use **CABLE Output** como microfone / captura de entrada de áudio.

!!! tip "Falar e tocar sons ao mesmo tempo"
    Só com o VB-Cable, o Discord ouve apenas o cabo, não o seu microfone de verdade. Para misturar os
    dois, use o [Voicemeeter](https://vb-audio.com/Voicemeeter/), ou adicione seu microfone real ao
    OBS como uma fonte separada.

Outras configurações: **volume** (0–100) e **Parar tudo**. Se a saída ficar vazia, é usado o
dispositivo padrão do Windows.
