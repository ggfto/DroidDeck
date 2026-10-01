# Casa inteligente (Tuya / Smart Life)

Controle lâmpadas, tomadas e interruptores pelo deck. Funciona com **qualquer marca baseada em
Tuya** — Nova Digital, Positivo Casa Inteligente, RSmart, Elgin, Geonav, Aubess e muitas outras são
versões rebatizadas da mesma plataforma.

**Não é preciso conta de desenvolvedor.** Você vincula sua conta lendo um QR code.

## Vinculando sua conta { #linking-your-account }

A vinculação é feita no **app do celular**:

1. No app **Smart Life** (ou **Tuya Smart**): **Eu → ⚙️ → Conta e segurança → Código de usuário**. Copie o código.
2. No DroidDeck do celular: **configurações → Casa inteligente (Tuya)**. Cole o código e toque em **Gerar QR**.
3. Leia o QR com o app Smart Life (aba **Início** → ícone de leitura) em até 2 minutos. O app pede
   para você confirmar um login do **"Home Assistant"** — veja o motivo abaixo.

A sessão fica salva no PC e se reconecta automaticamente ao iniciar.

!!! warning "Apps de marca recusam o QR"
    Se seus dispositivos estão em um **app de marca** (Nova Digital, Positivo, RSmart…), a leitura
    falha com *"please use the designated app to scan the code to login"*. Só o **Smart Life** e o
    **Tuya Smart** aceitam, e compartilhar os dispositivos não resolve.

    **Solução:** remova o dispositivo do app da marca e pareie de novo no **Smart Life**. É o mesmo
    hardware e funciona do mesmo jeito; você só perde as automações criadas no app da marca.

??? info "Por que aparece Home Assistant?"
    O DroidDeck usa o registro público de app do Home Assistant para o login por QR, porque a Tuya
    não oferece esse registro por autoatendimento. O `clientId` e o `schema` ficam no `tuya.json`,
    então trocar para um registro próprio no futuro é só uma mudança de configuração.

## Botões

Escolha o tipo de ação **Tuya**, depois o **dispositivo** (os offline aparecem marcados) e a
**função**. Os campos se adaptam ao dispositivo:

| Tipo de função | Controle no editor |
|---|---|
| Liga/desliga | Alternar, ou um valor fixo ligado/desligado |
| Número (brilho, temperatura…) | Slider com a faixa e a unidade do próprio dispositivo |
| Modo | Lista das opções permitidas |
| Outro | Valor em texto puro |

- **Alternar (liga/desliga)** liga se estiver desligado e vice-versa — o comportamento normal de um deck.
- **Sempre ligar / sempre desligar** sempre envia o mesmo valor.
- **Testar agora** envia o comando na hora.

Defina uma **cor ativa** para o botão acender enquanto o dispositivo estiver ligado. Dimmers mostram
o nível, e lâmpadas coloridas mostram a cor real. O estado chega por **push**, então mudanças feitas
no interruptor da parede ou no app Smart Life também aparecem no deck.

## Cota da API

O plano gratuito da Tuya permite cerca de 26.000 chamadas de API por mês. Por isso o DroidDeck
**nunca faz polling**: o estado chega por push, e a lista de dispositivos só é recarregada quando
você aperta atualizar (umas 4 chamadas por dispositivo). Apertar botões não é problema — cada toque
é uma chamada.

## Limitações

- **Só nuvem.** Os comandos passam pela nuvem da Tuya, então os botões não funcionam sem internet.
- Um **dispositivo offline** falha com o erro 2001 da Tuya.
- Se o push estiver desconectado, o primeiro **alternar** pode ir para o lado errado, porque usa o
  último estado conhecido.
