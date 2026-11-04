# Voicemeeter

O DroidDeck fala com o [Voicemeeter](https://vb-audio.com/Voicemeeter/) (Standard, Banana ou Potato) aberto
no mesmo PC pela **Voicemeeter Remote API**. Não tem nada para configurar: crie um botão do tipo
**Voicemeeter** e escolha o que ele faz.

O DroidDeck carrega a `VoicemeeterRemote64.dll` da pasta onde o Voicemeeter foi instalado (achada pelo
registro do Windows, ou `C:\Program Files (x86)\VB\Voicemeeter`). Se o Voicemeeter não estiver instalado,
ou não estiver aberto, o botão não faz nada e o log diz o motivo.

## O que dá para fazer

| Operação | Parâmetros |
|---|---|
| Mudo (alternar) / Mutar / Desmutar | **Onde**: entrada (Strip) ou saída (Bus); **Número** contando da esquerda (1 = a primeira) |
| Definir o ganho | Onde, número e ganho em dB (-60 a 12) |
| Ganho (+/-) | Onde, número e passo em dB (o resultado fica entre -60 e 12) |
| Apertar botão do Macro Buttons | O id do botão no Macro Buttons (o primeiro é 0). Aperta e solta, como um clique |
| Definir um parâmetro | Qualquer parâmetro numérico da Remote API, ex.: `Strip[0].A1`, `Bus[1].Gain`, `Fx.Reverb.On`, e um valor |
| Alternar um parâmetro (0/1) | Idem, trocando entre 0 e 1 (ex.: ligar e desligar o envio de uma entrada para A1) |

!!! tip "Dica"
    Os nomes dos parâmetros contam a partir de 0 (`Strip[0]` é a primeira entrada), diferente do campo
    **Número**. A lista completa está no PDF *Voicemeeter Remote API* que vem com o Voicemeeter.

Os botões não mostram o estado do Voicemeeter (ex.: mutado). Para um botão que muda de cara quando ligado,
use um [botão de dois estados](../guide/actions.md#two-states) com **Mutar** e **Desmutar**.

## Corte de tosse

A operação **Cortar enquanto segura (tosse)** deixa a entrada muda só enquanto o dedo estiver no botão. Ao soltar, ela volta ao que era: se já estava muda, continua muda. Se o celular perder a conexão com o dedo em cima, o PC devolve o microfone sozinho, para ele não ficar mudo no ar.
