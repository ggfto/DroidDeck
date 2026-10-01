# Monitores

Em vez de executar uma ação, um botão pode mostrar estatísticas do PC ao vivo. No painel de
propriedades, escolha um **Dynamic Feature**:

| Monitor | Mostra |
|---|---|
| **CPU Monitor** | Uso total do processador, em um medidor. |
| **Memory Monitor** | Uso de RAM, com "usado/total GB". |
| **GPU Monitor** | Uso do motor 3D da GPU, em um medidor. |
| **Network Monitor** | Velocidade de download ↓ e upload ↑ somando todos os adaptadores de rede ativos (K = KB/s, M = MB/s). |

Os medidores mudam de cor conforme a carga: **verde** até 60 %, **laranja** acima de 60 %,
**vermelho** acima de 85 %.

- Os valores são atualizados **a cada segundo** enquanto houver um celular conectado. O PC para de
  coletá-los quando nenhum celular está conectado.
- Um botão de monitor só exibe informação: escolher um monitor remove a ação do botão, e tocar nele
  não faz nada.
- O uso da GPU vem dos contadores de desempenho do Windows e exige um driver de vídeo razoavelmente
  recente. Quando não está disponível, aparece 0 %.
