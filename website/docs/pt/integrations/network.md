# Rede: OSC, TCP e UDP

Um botão do tipo **Rede (OSC, TCP, UDP)** manda uma mensagem do PC para outro aparelho ou programa: mesas de
som como X32 e M32, QLab, placas de iluminação e qualquer coisa que aceite comandos pela rede.

!!! note "Só nasce no PC"
    A mensagem sai do PC, com o acesso de rede dele. Por isso, como a requisição HTTP, este botão só pode ser
    criado ou alterado no configurador do PC. O celular só aperta.

## Campos

| Campo | Significado |
|---|---|
| **Protocolo** | OSC, UDP ou TCP. O OSC vai por UDP. |
| **Endereço** / **Porta** | O IP ou nome do aparelho e a porta em que ele escuta. Veja no manual dele. |
| **Caminho OSC** | Só OSC. Começa com `/` e não tem espaços, como `/ch/01/mix/on`. |
| **Argumentos (opcional)** | Só OSC. Um por linha, veja abaixo. |
| **Mensagem** | UDP e TCP. O texto ou os bytes a enviar. |
| **Formato** | UDP e TCP. **Texto**, ou **Hexadecimal** (`DE AD 01`). |
| **Fim da mensagem** | Só texto. Nada, LF, CR + LF ou CR. Muitos aparelhos só aceitam o comando com um desses no fim. |

## Argumentos do OSC

| Linha | Tipo |
|---|---|
| `i:1` | Inteiro |
| `f:0.5` | Decimal |
| `s:texto` | Texto |
| `T` ou `F` | Verdadeiro ou falso |

Sem prefixo, o tipo é deduzido: `7` vira inteiro, `0.5` vira decimal, e o resto vira texto. Use o prefixo quando o
aparelho exigir um tipo: `f:1` manda o decimal `1.0`, e `s:12` manda o texto `12`.

!!! example "Exemplo: mutar o canal 1 de uma X32"
    Protocolo **OSC**, porta `10023`, caminho `/ch/01/mix/on`, argumento `i:0`. Confira o caminho no manual da
    sua mesa.

!!! warning "Sem resposta do aparelho"
    O DroidDeck só envia. O UDP e o OSC não confirmam a entrega, então o botão não sabe se o aparelho recebeu e
    não mostra estado. Esta integração foi testada com um receptor de teste, não com uma mesa de verdade. Se algo
    não funcionar, [abra uma issue](https://github.com/ggfto/DroidDeck/issues).
