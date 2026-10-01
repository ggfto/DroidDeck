# Configurador

O configurador é o editor dos seus decks. Ele roda no navegador do PC e é servido pelo próprio
DroidDeck — abra-o pela bandeja (**Abrir no navegador**) ou acesse **`http://localhost:4787/`**.

!!! note "Use no próprio PC"
    O configurador só entra automaticamente a partir da mesma máquina (`localhost`). Aberto de outro
    computador da rede, as requisições são recusadas com *401 Unauthorized*.

## Layout

A tela tem três painéis:

1. **Perfis** — sua lista de decks. **New** cria um; o ícone de lixeira apaga, após confirmação.
2. **Grade** — os botões do perfil selecionado. Clique em uma célula para selecioná-la.
3. **Propriedades** — rótulo, ícone, cores e a ação do botão selecionado. Clique em **Save Changes**.

- **Arraste e solte** um botão para movê-lo. Soltar em uma célula ocupada troca os dois de lugar.
- O ícone de lápis renomeia o perfil.
- Atalhos na barra lateral abrem as configurações de **Discord**, **OBS** e **Soundboard**.

Cada alteração salva é enviada ao celular na hora.

## Tamanho da grade

Quem decide o tamanho da grade é o celular: ele calcula quantos botões (cerca de 96 px cada) cabem na
tela e informa ao PC. Girar o celular atualiza a grade. O configurador mostra a mesma grade, então o
que você edita bate com o que aparece no celular.

## Perfis, páginas e pastas

- No celular, **cada perfil é uma página** — deslize para o lado para trocar.
- Um botão com a ação **Abrir perfil** abre outro perfil como uma **pasta**. Use um botão **Voltar**
  ou a seta de voltar na barra de título para retornar.
- Novos perfis são criados no configurador. Se você não tiver nenhum, um perfil *Default* é criado
  automaticamente.

## Editando no celular

Você também pode editar no próprio celular:

- **Toque** em um botão para executá-lo; **toque e segure** para editá-lo.
- **Toque em uma célula vazia** para criar um botão ali.
- O ícone de engrenagem na barra de título renomeia o perfil.

!!! note "Atalho, abrir app e multi-ação são editados no PC"
    Botões que enviam teclas ou abrem programas conseguem rodar qualquer coisa no seu PC, por isso só
    podem ser criados ou alterados no configurador, no próprio PC. No celular você continua usando
    esses botões e pode mudar nome, ícone e cor; o editor mostra um cadeado com o que o botão faz.

## Propriedades do botão

| Propriedade | Descrição |
|---|---|
| **Label** | Texto exibido no botão. Botões de mídia e de soundboard o preenchem sozinhos quando vazio. |
| **Icon** | Um dos 16 ícones embutidos, ou uma imagem personalizada da sua galeria (até 512×512, guardada no perfil). |
| **Background Color** | Cor do botão. |
| **Cor ativa** | Cor usada quando o estado do botão está "ligado" (microfone silenciado, lâmpada acesa…). Disponível para ações de Discord, mixer e Tuya. |
| **Action Type** | O que o botão faz — veja [Botões e ações](../guide/actions.md). |
| **Dynamic Feature** | Transforma o botão em um monitor ao vivo — veja [Monitores](../guide/monitors.md). |

## Backup

Os perfis são arquivos JSON em `%AppData%\DroidDeck\Profiles\`. Para fazer backup dos seus decks, ou
levá-los para outro PC, copie essa pasta. Veja [Arquivos e configurações](../reference/files.md).
