# Automações

Uma **automação** aperta um botão do deck sozinha quando algo acontece no PC — num horário, quando um
app abre ou fecha, quando o PC volta da suspensão, quando algo entra no ar. Você não encosta no celular.

Automações não têm ação própria: elas apertam um botão que já existe. Crie antes o botão com a ação
desejada (pode ficar num perfil que você não usa no dia a dia) e depois aponte uma automação para ele.

## Criar uma automação

Abra **Automações**: no app do celular em **Configuração → Automações**, ou na barra lateral do
configurador do PC. Toque em **Nova automação** e preencha:

| Campo | Significado |
|---|---|
| **Nome (opcional)** | Aparece na lista. Sem nome, a lista mostra o acontecimento. |
| **Quando** | O acontecimento — veja abaixo. |
| **Horário** / **Dias (opcional)** | Para *Num horário*: `HH:mm` em 24 horas, ex.: `09:00`. Os dias usam `0` = domingo … `6` = sábado, com intervalos e vírgulas: `1-5` (dias úteis), `0,6` (fim de semana). Vazio = todo dia. |
| **Processo do aplicativo** | Para os acontecimentos de app: o nome do processo sem `.exe`, ex.: `obs64`, `Spotify`, `chrome`. |
| **O que acompanhar** | Para os acontecimentos de tally: uma cena, entrada, microfone ou o sinal manual. A lista traz só o que está conectado agora. |
| **Apertar o botão** | Qualquer botão com ação, de qualquer perfil (`Perfil · rótulo`). |

| Acontecimento | Quando dispara |
|---|---|
| **Num horário** | Uma vez naquele minuto, nos dias escolhidos. |
| **Quando um app abrir** | Quando o processo começa. |
| **Quando um app fechar** | Quando o processo termina. |
| **Quando o PC voltar da suspensão** | Depois de suspender ou hibernar. |
| **Quando o DroidDeck iniciar** | Alguns segundos depois que o DroidDeck abre — logo depois de entrar no Windows, se ele inicia com o Windows. |
| **Quando um alvo do tally entrar no ar** | Quando o [alvo](../integrations/tally.md) fica vermelho. |
| **Quando um alvo do tally sair do ar** | Quando ele deixa de estar vermelho. Perder a conexão não conta, porque não se sabe se saiu: se o OBS fechar, nada dispara; quando ele voltar, a automação compara com o último estado conhecido. |

A chave ao lado de cada automação liga ou desliga. **Testar agora** (no menu do item) aperta o botão na
hora, sem esperar o acontecimento; **Excluir** apaga.

## Como funciona

- As automações rodam no PC: funcionam com o celular desligado ou desconectado.
- O PC confere a cada dois segundos, mais ou menos, então um acontecimento de app pode levar uns 2 s
  para disparar. Os acontecimentos de tally disparam na hora.
- O botão roda como se você o tivesse tocado, inclusive um botão de [dois estados](actions.md#two-states),
  que troca de estado. Um botão de [multiação](multi-action.md) mantém as
  [condições dos passos](multi-action.md#conditions).
- Se o botão for apagado, a automação fica na lista como *(botão apagado)* e não faz nada.
- Como a automação só aperta botões salvos, ela não consegue fazer nada que um botão já não pudesse.

!!! example "Ideias"
    - Às `18:00` nos dias `1-5`, apertar um botão que dispara a sua multiação de "fim do expediente".
    - Quando o `obs64` abrir, apertar um botão que liga a Key Light; quando fechar, outro que desliga.
    - Quando o PC voltar da suspensão, voltar o áudio padrão para o fone.
    - Quando o microfone do locutor abrir no Voicemeeter, acender uma luz vermelha da Hue no corredor; quando
      fechar, apagar.

As automações ficam em `%AppData%\DroidDeck\triggers.json` (veja [Arquivos e configurações](../reference/files.md)).
